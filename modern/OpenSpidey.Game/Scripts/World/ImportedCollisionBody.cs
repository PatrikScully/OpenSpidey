using Godot;
using OpenSpidey.Core.World;
using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Game.World;

public partial class ImportedCollisionBody : StaticBody3D
{
    public string? SourceAsset { get; internal set; }
    public uint? ModelChecksum { get; internal set; }
    public int InstanceIndex { get; internal set; }
    public int ModelIndex { get; internal set; }
    public ushort SourceFlags { get; internal set; }
    public ulong? SupportId { get; internal set; }
    public FixedJointTransform SourceRotation { get; internal set; }
    public IReadOnlyList<CollisionTriangle> SourceTriangles { get; internal set; } = [];
}
