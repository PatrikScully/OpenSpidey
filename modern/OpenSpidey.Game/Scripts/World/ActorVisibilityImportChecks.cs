using Godot;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Actual original Black Cat pose bounds and native viewport planes, including Hor+ widescreen edges.</summary>
internal static class ActorVisibilityImportChecks
{
    public static async Task VerifyAsync(Node3D owner, GodotBlackCatActor actor)
    {
        Node originalParent = actor.GetParent();
        var viewport = new SubViewport { Name = "OriginalActorVisibilityProof", OwnWorld3D = true,
            Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        var root = new Node3D(); viewport.AddChild(root);
        var camera = new Camera3D { Current = true, Fov = 60, Near = .05f, Far = 1000, KeepAspect = Camera3D.KeepAspectEnum.Height };
        root.AddChild(camera);
        var visibility = new GodotActorVisibility(() => viewport.GetCamera3D() ?? throw new InvalidDataException("Culling proof lost its real active camera."));
        originalParent.RemoveChild(actor); root.AddChild(actor);
        ushort flags = actor.ItemFlags;
        try
        {
            Vector3 centre = actor.Model.GlobalTransform * actor.Model.PoseBounds.GetCenter();
            camera.GlobalTransform = new(Basis.Identity, centre + new Vector3(0, 0, 10));
            await Frame();
            Assert(!visibility.IsCulled(actor, actor.Model, flags) && camera.IsPositionInFrustum(centre),
                "The actual posed original model is visible when its native geometry lies in the active640x480 frustum.");
            Assert(visibility.IsCulled(actor, actor.Model, (ushort)(flags | 1)) &&
                visibility.IsCulled(actor, actor.Model, (ushort)(flags | 0x1000)),
                "Source hide1 and model-disabled1000 gates force cull independently of the current view.");
            camera.GlobalPosition = centre + new Vector3(-90, 0, 100);
            await Frame();
            Assert(visibility.IsCulled(actor, actor.Model, flags),
                "The actual posed mesh lies fully beyond a4:3 viewport side plane, rather than using an origin-only visibility shortcut.");
            viewport.Size = new(1280, 720);
            await Frame();
            Assert(!visibility.IsCulled(actor, actor.Model, flags),
                "The same posed model enters the real16:9 Hor+ camera frustum without changing its animation, scale or source position.");
            camera.GlobalPosition = centre + new Vector3(0, 0, -10);
            await Frame();
            Assert(visibility.IsCulled(actor, actor.Model, flags), "A complete actual posed model behind the active camera is culled.");
            camera.Far = 10; camera.GlobalPosition = centre + new Vector3(0, 0, 100);
            await Frame();
            Assert(visibility.IsCulled(actor, actor.Model, flags) &&
                !visibility.IsCulled(actor, actor.Model, (ushort)(flags | 0x2000)),
                "Source depth-bypass2000 retains side-plane culling while bypassing the native camera's near/far planes.");
            camera.GlobalPosition = centre + new Vector3(400, 0, 10);
            await Frame();
            Assert(visibility.IsCulled(actor, actor.Model, (ushort)(flags | 0x2000)),
                "Original signed32767 X/Y range gates remain active even when source depth bypass is set.");
            actor.Model.VerifyCurrentPose();
            GD.Print("Verified native actor culling: actual Black Cat posed bounds, current camera planes, source1001/32767/2000 gates, behind-camera rejection and4:3-to16:9 Hor+ visibility.");
        }
        finally
        {
            root.RemoveChild(actor); originalParent.AddChild(actor); viewport.Free();
        }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native actor culling: {evidence}"); }
}
