using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Real scene dependencies for source307; the session binds each hook to its canonical owners.</summary>
public sealed class GodotRhinoConfiguration
{
    public required Func<ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<SourceModelLight> Light { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<GodotRhinoActor, bool> IsSourceCulled { get; init; }
    public required Action<RhinoState> CreateHealthBar { get; init; }
    /// <summary>Bound after BindCamera to gBossRelated0x56E998 via GodotScorpionEncounterRuntime.</summary>
    public Action<CameraFocusTarget?> SetGlobalBoss { get; set; } = _ => { };
    public Action SetBossCameraMode { get; set; } = () =>
        throw new NotSupportedException("CRhino::AI boss camera (CAMERAMODE_USER/16) requires the session BindCamera hook.");
    public Func<int> PlayerInputSuppression { get; set; } = () => 0;
    /// <summary>gNumDomes at 0x6B55A8; charge sphere radius branches on non-zero.</summary>
    public Func<int> WebDomeCount { get; set; } = () => 0;
    public ReadOnlyMemory<RhinoSpeechEntry> SpeechTable { get; init; }
    public RhinoWallChunkLists WallChunks { get; init; } = new();
    public Func<uint> SourceClock { get; set; } = () => 0;
    public Func<int, int, FixedVector3, int, bool> TryPlayPositionSpeech { get; set; } =
        (_, _, _, _) => throw new InvalidOperationException("CRhino::PlayXAPlease needs the session speech host.");
    /// <summary>CBody::StopMyXA (0x460400) against the session's current XA.</summary>
    public Action<ActorSpeechAttachment> StopSpeech { get; set; } =
        _ => throw new InvalidOperationException("CRhino::DieRhino StopMyXA needs the session speech host.");
    /// <summary>CPlayer, CCamera, web contacts and controller feedback, available once the player is bound.</summary>
    public Func<GodotEnemyPlayerBinding> PlayerBinding { get; init; } =
        () => throw new InvalidOperationException("CRhino::AI needs the bound CPlayer/CCamera.");
    /// <summary>Trig_SendSignal (0x4DFFE0) over this Rhino's authored links.</summary>
    public Action<GodotRhinoActor> SendSignal { get; init; } =
        _ => throw new InvalidOperationException("CRhino::DetermineFightState signals need the scene actor directory.");
    /// <summary>CBaddy::CreateCombatImpactEffect rings for CAIProc_MonitorAttack contacts.</summary>
    public Action<FixedVector3> CreateCombatImpact { get; init; } =
        _ => throw new InvalidOperationException("CRhino MonitorAttack impacts need the scene combat-ring owner.");
}

/// <summary>Original rhino.psx model and M3d_RhinoLight at 0x552080.</summary>
public sealed class GodotRhinoResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel()
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(RhinoState.ModelName));
        if (library.Animations is null || library.Animations.ClipCount < 1)
            throw new InvalidDataException("Original rhino assets require the source animation bank.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public SourceModelLight ReadLight() =>
        SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(RhinoState.LightAddress, 52).Span);

    public ReadOnlyMemory<RhinoSpeechEntry> ReadSpeechTable() => RhinoSpeechTable.Read(originalImage);

    public RhinoWallChunkLists ReadWallChunks() => RhinoWallChunkLists.Read(originalImage);
}
