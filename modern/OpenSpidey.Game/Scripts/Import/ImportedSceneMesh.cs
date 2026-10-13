using Godot;

namespace OpenSpidey.Game.Import;

/// <summary>Static source instance identity retained for transfer to a live object actor.</summary>
public partial class ImportedSceneMesh : MeshInstance3D
{
    public int SourceInstanceIndex { get; internal set; }
    public int SourceModelIndex { get; internal set; }
}
