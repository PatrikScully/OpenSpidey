using Godot;
using OpenSpidey.Core.World;

namespace OpenSpidey.Game.World;

public partial class ImportedTriggerSurface : Area3D
{
    public string? SourceAsset { get; internal set; }
    public uint? ModelChecksum { get; internal set; }
    public int InstanceIndex { get; internal set; }
    public int ModelIndex { get; internal set; }
    public IReadOnlyList<CollisionTriangle> SourceTriangles { get; internal set; } = [];
}
