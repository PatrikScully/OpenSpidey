using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Effects;

namespace OpenSpidey.Game.Actors;

/// <summary>Owns MakeVertexWibbler (0x4A2790) CVertexWobble bits for the live symbiote model bank.</summary>
public partial class GodotSimbyVertexWibbler : Node
{
    private readonly PsxLibraryRepository repository;
    private readonly List<VertexWobbleState> wobblers = [];
    private VertexWobbleModel? model;
    private bool created;

    public GodotSimbyVertexWibbler(PsxLibraryRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Name = "SimbyVertexWibbler";
    }

    public int Count => wobblers.Count;
    public bool Created => created;
    internal VertexWobbleModel? Model => model;
    internal IReadOnlyList<VertexWobbleState> Wobblers => wobblers;

    /// <summary>First live CSimby construction creates the two original bits once.</summary>
    public void Ensure(int levelId, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (created) return;
        string name = levelId == SimbyState.DarkModelLevel ? SimbyState.DarkModelName : SimbyState.BrightModelName;
        PsxAssetLibrary library = repository.Load(PsxLibraryRepository.AssetPath(name));
        if (library.Scene.Models.Count <= VertexWobbleState.SimbyModelIndex)
            throw new InvalidDataException($"Original {name} requires model index {VertexWobbleState.SimbyModelIndex} for CVertexWobble.");
        PsxModel source = library.Scene.Models[VertexWobbleState.SimbyModelIndex];
        if (source.Vertices.Count <= VertexWobbleState.SimbyIndicesB.Max())
            throw new InvalidDataException($"Original {name} model1 needs the MakeVertexWibbler vertex indices.");
        var positions = new List<(short X, short Y, short Z)>(source.Vertices.Count);
        foreach (PsxVertex vertex in source.Vertices)
            positions.Add(((short)vertex.Position.X, (short)vertex.Position.Y, (short)vertex.Position.Z));
        model = new VertexWobbleModel(positions);
        wobblers.AddRange(MakeVertexWibbler.Create(model, random));
        created = true;
    }

    public void Advance()
    {
        if (!created) return;
        foreach (VertexWobbleState wobbler in wobblers) wobbler.Move();
    }
}
