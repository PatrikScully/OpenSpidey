using Godot;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Real scene dependencies for source324/412; the session binds each hook to its canonical owners.</summary>
public sealed class GodotSimbyConfiguration
{
    public required Func<SimbyState, ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<PunchObState, ImportedAnimatedModel> PunchObModelFactory { get; init; }
    public required Func<int, ActorPoseLinkPacket> PosePacket { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<int, int> Random { get; init; }
    public required SimbyWorldState World { get; init; }
    public required GodotBossGlobalOwner BossGlobal { get; init; }
    public required Func<GodotSimbyActor, bool> IsSourceCulled { get; init; }
    public required Func<GodotPunchObActor, bool> IsPunchObCulled { get; init; }
    /// <summary>MakeVertexWibbler (0x4A2790) creates the owned CVertexWobble pair once.</summary>
    public required Action EnsureVertexWibbler { get; init; }
    public required GodotSimbyDroplets Droplets { get; init; }
    /// <summary>InitItem("sym_base") model 0 for CSimbyBase.</summary>
    public required Func<GodotSimbyBaseModel> SymBaseMesh { get; init; }
    /// <summary>CSimbyShot/CSimbyDrop/splat Quad bits; absent in isolated import checks that never shoot.</summary>
    public GodotSimbyBits? Bits { get; init; }
    public required Func<FixedVector3, int> GroundY { get; init; }
    /// <summary>dword_5FBEF0 read by the Utils_CheckObjectCollision sphere pass.</summary>
    public required Func<bool> IgnoreSphereRadius { get; init; }
    /// <summary>Scene sound, kill-quip and dome services shared with the thugs; absent in isolated import checks.</summary>
    public IGodotThugNativeEffects? Effects { get; init; }
    /// <summary>Redbook_XAPlayPos, bound by the story session like CRhino::PlayXAPlease.</summary>
    public Func<int, int, FixedVector3, int, bool> TryPlayPositionSpeech { get; set; } =
        (_, _, _, _) => throw new InvalidOperationException("CSimby XA quips need the session speech host.");
    public Func<uint> SourceClock { get; set; } = () => 0;
}

/// <summary>The sym_base model 0 mesh and the bank it was built from (visibility boxes test that bank).</summary>
public sealed record GodotSimbyBaseModel(MeshInstance3D Mesh, PsxAssetLibrary Library);

/// <summary>Original symbi_02 / sym_dark / sym_gen models and the private 0x5554A0 pose packet.</summary>
public sealed class GodotSimbyResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateSimbyModel(SimbyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(state.ModelName));
        if (library.Animations is null || library.Animations.ClipCount < 1)
            throw new InvalidDataException($"Original {state.ModelName} assets require the authored animation bank.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public ImportedAnimatedModel CreatePunchObModel(PunchObState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(PunchObState.ModelName));
        if (library.Animations is null || library.Animations.ClipCount < 1)
            throw new InvalidDataException("Original sym_gen assets require the authored punch-ob animation bank.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    /// <summary>Spool_FindPSX("sym_base") with CItem model index 0 (InitItem 0x460020 clears 0x1A).</summary>
    public GodotSimbyBaseModel CreateSymBaseMesh()
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(SimbyBaseState.ModelName));
        if (library.Scene.Models.Count == 0) throw new InvalidDataException("Original sym_base assets require model 0.");
        ArrayMesh model = PsxGeometryBuilder.BuildModel(library.Scene.Models[0], library.Scene.ColorTable,
            new PsxMaterialLibrary(library.Textures)) ?? throw new InvalidDataException("Original sym_base model has no visible faces.");
        return new(new MeshInstance3D { Name = "SymBaseModel", Mesh = model, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }, library);
    }

    public ActorPoseLinkPacket ReadPosePacket(int partCount)
    {
        OriginalExecutableData data = new(originalImage);
        ReadOnlyMemory<byte> header = data.Slice(SimbyState.PosePacketAddress, 4);
        int count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header.Span[2..]);
        return ActorPoseLinkPacket.Read(data.Slice(SimbyState.PosePacketAddress, checked(4 + count * 12)).Span, partCount);
    }
}
