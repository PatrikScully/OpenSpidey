using Godot;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Effects;

/// <summary>CSmokeTrail owners for CChopperMissile::CommonInitialisation (6 segments, 80/80/96 colour).</summary>
public partial class GodotChopperMissileSmoke : Node3D
{
    private GodotSmokeTrails? trails;
    private bool disposed;

    public static GodotChopperMissileSmoke Create(byte[] originalBits)
    {
        var owner = new GodotChopperMissileSmoke { Name = "ChopperMissileSmoke" };
        owner.trails = GodotSmokeTrails.Create(originalBits);
        owner.AddChild(owner.trails);
        return owner;
    }

    public SmokeTrail Emit(FixedVector3 position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SmokeTrail trail = trails!.Emit(position, 6, 80, 80, 96);
        trail.Segments[0].Scale = 768;
        return trail;
    }

    public void Rebuild() => trails?.Rebuild();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            if (trails is not null && GodotObject.IsInstanceValid(trails)) trails.Free();
        }
        base.Dispose(disposing);
    }
}
