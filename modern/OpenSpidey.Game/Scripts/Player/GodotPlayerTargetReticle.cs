using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

/// <summary>Source miscellaneous-render-list CBody503, using its original items model and native source yaw.</summary>
public partial class GodotPlayerTargetReticle : Node3D
{
    public const uint ModelChecksum=0xB08EC1FB;
    public const ushort ActorType=503;
    public const ushort MinorRadius=100;
    private MeshInstance3D mesh=null!;
    private ArrayMesh geometry=null!;
    private PsxMaterialLibrary materials=null!;
    private bool disposed;
    internal bool ResourcesDisposed => disposed;
    internal ArrayMesh RenderMesh => geometry;
    public ushort ModelIndex {get;private set;}
    public FixedVector3 SourcePosition {get;set;}
    public short SourceYaw {get;set;}
    public static GodotPlayerTargetReticle Create(PsxAssetLibrary items)
    {
        if(!items.AssetPath.Equals("data/items.psx",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Player target reticle requires the original items region.");
        int index=items.Scene.Models.Select((model,index)=>(model,index)).Single(value=>value.model.Checksum==ModelChecksum).index;
        var actor=new GodotPlayerTargetReticle {Name="OriginalTargetReticle503",ModelIndex=checked((ushort)index)};
        try
        {
            actor.materials=new(items.Textures);
            actor.geometry=PsxGeometryBuilder.BuildModel(items.Scene.Models[index],items.Scene.ColorTable,actor.materials)
                ?? throw new InvalidDataException("Original player reticle model has no source faces.");
            actor.mesh=new MeshInstance3D {Name="OriginalItemsReticleModel",Mesh=actor.geometry};
            actor.AddChild(actor.mesh);actor.Synchronize();return actor;
        }
        catch{actor.Free();throw;}
    }
    public void Synchronize()
    {
        Transform3D transform=PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(0,SourceYaw,0,0,0,0)));
        transform.Origin=PsxCoordinates.FixedWorldPosition((SourcePosition>>12)<<12);
        Transform=transform;
    }
    public override void _Notification(int what)
    {
        if(what!=NotificationPredelete || disposed)return;
        disposed=true;
        if(mesh is not null && GodotObject.IsInstanceValid(mesh))mesh.Mesh=null;
        geometry?.Dispose();materials?.Dispose();
    }
}
