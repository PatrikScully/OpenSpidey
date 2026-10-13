using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Source hide/range gates and current posed character bounds against the modern camera's actual viewport frustum.</summary>
public sealed class GodotActorVisibility(Func<Camera3D> activeCamera)
{
    public bool IsCulled(GodotScriptActor actor, ImportedAnimatedModel model, ushort flags)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(activeCamera);
        if ((flags & 0x1001) != 0) return true;
        Camera3D camera = activeCamera();
        if (!GodotObject.IsInstanceValid(camera) || !camera.IsInsideTree() || !model.IsInsideTree())
            throw new InvalidOperationException("Character culling requires the actual posed model and active camera in their native world.");
        FixedVector3 cameraPosition = PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12;
        FixedVector3 position = actor.SourcePosition >> 12;
        FixedVector3 delta = position - cameraPosition;
        if (GameWord.Absolute(delta.X) > 0x7FFF || GameWord.Absolute(delta.Y) > 0x7FFF ||
            ((flags & 0x2000) == 0 && GameWord.Absolute(delta.Z) > 0x7FFF)) return true;

        // AI can change live mPos after its cached display transform. Cull at the current source position.
        Transform3D transform = model.GlobalTransform;
        transform.Origin += PsxCoordinates.FixedWorldPosition(position << 12) - actor.GlobalPosition;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int corner = 0; corner < corners.Length; corner++) corners[corner] = transform * model.PoseBounds.GetEndpoint(corner);
        Godot.Collections.Array<Plane> planes = camera.GetFrustum();
        using Godot.Collections.Array ownedPlanes = (Godot.Collections.Array)planes;
        if (planes.Count != 6) throw new InvalidDataException("The active native camera did not provide its six viewport frustum planes.");
        for (int index = 0; index < planes.Count; index++)
        {
            // Source item2000 bypasses the depth planes while retaining side-plane visibility.
            if ((flags & 0x2000) != 0 && index < 2) continue;
            bool outside = true;
            foreach (Vector3 corner in corners)
                if (!planes[index].IsPointOver(corner)) { outside = false; break; }
            if (outside) return true;
        }
        return false;
    }
}
