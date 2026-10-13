using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

/// <summary>Uses a genuine imported thug and original posed collision boxes; no replacement hook or actor geometry.</summary>
internal static class TrapWebImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PlayerInteractionFixture fixture, PkrArchive archive,
        GodotThugActor thug, GodotThugVisualEffects visuals)
    {
        var fragments = new GodotWebFragments { Name = "TrapWebCheckFragments" };
        fixture.Root.AddChild(fragments);
        int firstContacts = 0;
        var contacts = GodotActorWebContacts.Create(archive.Read("data/bits.psx"), fixture.Directory,
            actor => visuals.Resources.CollisionBoxes(actor.State.ModelName!), fragments,
            new GameRandom(0x4F8860).Next, position => GodotBouncingRocks.GetGroundY(fixture.WorldQuery, position),
            (_, _) => throw new InvalidOperationException("Ordinary native trap proof must not substitute an unported burning relocation."),
            () => firstContacts++);
        fixture.Root.AddChild(contacts);
        uint behavior = thug.Actor.BehaviorFlags, tint = thug.State.TintColor;
        ushort flags = thug.ItemFlags; short health = thug.State.Health;
        int clip = thug.Model.ClipId, frame = thug.Model.Playback.Frame;
        Transform3D cameraTransform = fixture.Camera.GlobalTransform;
        try
        {
            PlayerCameraFacingImportChecks.Verify(fixture, thug);
            // The authored NPC is away from the player-facing fixture camera; frame its real source body for this render proof.
            fixture.Camera.GlobalPosition = thug.GlobalPosition + new Vector3(0, 1, 4);
            fixture.Camera.LookAt(thug.GlobalPosition);
            fixture.Camera.MakeCurrent();
            thug.State.Health = 50; thug.Actor.BehaviorFlags &= ~0x10000u;
            Assert(contacts.TrapWeb(thug) && thug.Messages.Head is { Code: 5, SenderHandle: null },
                "Real304 TrapWeb must enqueue genuine null-sender message5 before adding original contact geometry.");
            thug.Messages.Head!.Flags |= 1; thug.Messages.Cleanup();
            for (int index = 0; index < 4; index++) contacts.AddTrap(thug, 0);
            TrapWebEffect trap = contacts.Contact(thug, 0) ?? throw new InvalidDataException("Real thug failed to retain source field104 trap contact.");
            Assert(firstContacts == 1 && trap.HookCount > 0 && trap.StrandCount == 0,
                "Four actual Web_Trap batches reuse one effect and retain the source cached-count delay until Move.");
            int hooks = trap.HookCount, height = trap.ProjectorHeight;
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(trap.HookCount == hooks && trap.ProjectorHeight == height && trap.StrandCount == 0,
                "Render frames must not advance the original trap projector or bit simulation.");
            contacts.AdvanceBits();
            GD.Print($"Native trap geometry: hooks={hooks}, cached={trap.StrandCount}, endpoints={contacts.ActiveTrapEndpoints(thug)?.Count}, lines={contacts.LineMesh.GetSurfaceCount()}, triangles={contacts.TriangleMesh.GetSurfaceCount()}/{trap.Triangles.Count}.");
            Assert(trap.StrandCount == hooks && contacts.ActiveTrapEndpoints(thug)?.Count == hooks &&
                contacts.LineMesh.GetSurfaceCount() == hooks && contacts.TriangleMesh.GetSurfaceCount() == trap.Triangles.Count,
                "Actual source Move must evaluate every animated hook, expose its cached endpoints and build genuine lines/texture triangles.");
            for (int index = 0; index <= hooks; index++)
                Assert(trap.Points[index] == thug.GetDynamicHookPosition(trap.Hooks[index]),
                    $"Original trap hook{index} must follow actual imported posed part{trap.Hooks[index].PartIndex}.");
            FixedVector3[] first = trap.Points.Take(hooks + 1).ToArray();
            thug.Model.RunClip(1); thug.Model.SetSourceAnimationFrame(Math.Min(3, thug.Model.FrameCount - 1));
            thug.BuildTransform(); contacts.AdvanceBits();
            Assert(!first.SequenceEqual(trap.Points.Take(hooks + 1)),
                "Trap web must follow changed original thug animation, not retain static model bounds or generated attachment points.");
            thug.Model.VerifyCurrentPose();
            foreach (TrapWebTriangle strand in trap.Triangles)
                Assert(strand.A == trap.Points[strand.Newest] && strand.B == trap.Points[strand.Previous] &&
                    strand.C == trap.Points[strand.Widest] && strand.D == strand.C,
                    "Native source triangle corners must follow their exact accepted hook indices.");

            contacts.ReplaceYank(thug, 16);
            TrapWebEffect yank = contacts.Contact(thug, 1) ?? throw new InvalidDataException("Real thug has no source field10C yank effect.");
            contacts.AdvanceBits();
            Node originalParent = thug.GetParent();
            var reparented = new Node3D { Name = "ActualWebContactReparentProof" };
            fixture.Root.AddChild(reparented);
            PlayerKickHandle lifetime = fixture.Directory.MakeHandle(thug);
            try
            {
                thug.Reparent(reparented, true);
                Assert(ReferenceEquals(fixture.Directory.Recover(ref lifetime), thug) &&
                    ReferenceEquals(contacts.Contact(thug, 0), trap) && ReferenceEquals(contacts.Contact(thug, 1), yank) &&
                    trap.Alive && yank.Alive && !trap.IsDisposed && !yank.IsDisposed,
                    "A real actor reparent must retain its source generation and both genuine attached contacts; TreeExiting is not a CSuper destructor.");
                contacts.AdvanceBits();
                Assert(trap.Points.Take(hooks + 1).SequenceEqual(trap.Hooks.Take(hooks + 1).Select(thug.GetDynamicHookPosition)),
                    "The same original protected trap must continue following its real posed actor after reparenting.");
            }
            finally
            {
                if (GodotObject.IsInstanceValid(thug) && GodotObject.IsInstanceValid(originalParent) && thug.GetParent() != originalParent)
                    thug.Reparent(originalParent, true);
                reparented.Free();
            }
            Assert(ReferenceEquals(fixture.Directory.Recover(ref lifetime), thug) &&
                ReferenceEquals(contacts.Contact(thug, 0), trap) && ReferenceEquals(contacts.Contact(thug, 1), yank),
                "Returning the real actor to its source scene retains both contact identities and the same allocation validation.");
            int yankHooks = yank.HookCount;
            thug.Actor.BehaviorFlags |= 0x200;
            Assert(!contacts.TugWeb(thug) && !yank.Alive && contacts.Contact(thug, 1) is null &&
                fragments.Count == yankHooks / 2,
                "Rejected native yank must Burst its actual attached contact into original paired WebFragments and clear the handle immediately.");
            trap.Burst();
            Assert(contacts.Contact(thug, 0) is null && contacts.Count == 2,
                "Burst clears source contact handles while dead nonrendered owners remain allocated until Bit_RemoveDeadBits.");
            contacts.AdvanceBits();
            Assert(contacts.Count == 0 && contacts.LineMesh.GetSurfaceCount() == 0 && contacts.TriangleMesh.GetSurfaceCount() == 0,
                "Actual bit retirement must delete both owned polylines and every triangle without stale geometry.");
            GD.Print($"Verified genuine304 trap/yank contacts: posed boxes, {hooks} animated hooks, original triangle texture, cached endpoints, live actor reparent generation/contact preservation, null-sender message5, rejected-yank Burst fragments and child retirement.");
        }
        finally
        {
            thug.Actor.BehaviorFlags = behavior; thug.State.TintColor = tint; thug.ItemFlags = flags; thug.State.Health = health;
            thug.Model.RunClip(clip); thug.Model.SetSourceAnimationFrame(frame); thug.BuildTransform();
            fixture.Camera.GlobalTransform = cameraTransform;
            contacts.Free(); fragments.Free();
        }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native trap web: {evidence}"); }
}
