using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Whole jump/ground-loss guards against the existing genuine L1A3 node218 and source session ownership.</summary>
internal static class PlayerAirborneEntryImportChecks
{
    public static void Verify(PlayerInteractionFixture fixture, GodotManipulationActor originalItem)
    {
        GodotPlayer player = fixture.Player;
        GodotPlayerMelee melee = fixture.Melee;
        PlayerTerrainController controller = player.Controller;
        PlayerCombatState combat = player.Combat.State;
        GodotPlayerInteractions interactions = melee.Interactions ?? throw new InvalidDataException("Air entry requires actual held-object services.");
        Assert(fixture.Registry.Owns(originalItem) && originalItem.Definition.NodeId == 218 && originalItem.ScriptState.ActorType == 401 &&
            originalItem.State.ModelChecksum == 0x44EF15C1 && fixture.Session.HeldActor is null && melee.Kick.HeldObject is null,
            "Proof must reuse the actual unheld L1A3 node218, its original401 model and existing environmental/session ownership.");
        var angles = new PlayerLookaroundState(combat);
        var entry = new GodotPlayerAirborneEntry(player, angles);
        var saved = (combat.ActionState, combat.CollisionFlags, combat.AnimationId, combat.AnimationFrame, combat.LookaroundActive,
            combat.FieldE8C, combat.FieldAE5, combat.FieldAE6, combat.FallReferenceHeight);
        byte pressed = melee.State.Input.Triggered[16], mirrored = melee.State.Input.Mirrors[16];
        byte grace = controller.SourceGroundContactGrace, finished = player.Model.Playback.Finished;
        int playbackSpeed = player.Model.PlaybackSpeed;
        bool animationFinished = melee.State.AnimationFinished;
        PlayerJumpImpulse impulse = controller.SourceJumpImpulse;
        try
        {
            // The actual source held assignment precedes CManipOb.Pickup and persists through both guards.
            PlayerKickObject held = originalItem.KickObject;
            melee.Kick.HeldObject = held;
            interactions.Hold(held);
            interactions.Pickup(originalItem);
            Assert(ReferenceEquals(fixture.Session.HeldActor, originalItem) && combat.HeldObjectPresent &&
                (originalItem.State.ItemFlags & 0x820) == 0x820,
                "The original actor's own Pickup must establish actual held/visibility/collision flags.");
            melee.State.Input.Triggered[16] = melee.State.Input.Mirrors[16] = 1;
            combat.LookaroundActive = false;
            uint action = combat.ActionState;
            Assert(!entry.CheckJump() && melee.State.Input.Triggered[16] == 0 && melee.State.Input.Mirrors[16] == 0 &&
                ReferenceEquals(fixture.Session.HeldActor, originalItem) && combat.ActionState == action && controller.SourceJumpImpulse == impulse,
                "Actual held401 must reject Jump16 after consuming its real shared edge/mirror, retaining the same object and jump impulse.");

            combat.CollisionFlags &= unchecked((ushort)~2);
            controller.SourceGroundContactGrace = 2;
            Assert(!entry.CheckGroundGone() && controller.SourceGroundContactGrace == 1 &&
                ReferenceEquals(fixture.Session.HeldActor, originalItem),
                "The first actual lost-ground visit must decrement EA4 and preserve the genuine carried object.");
            FixedVector3 expectedDrop = combat.UpDirection * 4;
            int fallHeight = combat.Position.Y;
            Assert(entry.CheckGroundGone() && controller.SourceGroundContactGrace == 0 && fixture.Session.HeldActor is null &&
                melee.Kick.HeldObject is null && !combat.HeldObjectPresent && originalItem.Body.Velocity == expectedDrop &&
                originalItem.Body.Acceleration.Y == 4096 && (originalItem.State.Flags & 9) == 1 &&
                (originalItem.State.ItemFlags & 0x800) == 0 && combat.ActionState == 4 && combat.AnimationId == 212 &&
                combat.FallReferenceHeight == fallHeight && combat.FieldAE5 == 0 && combat.FieldAE6 == 0 && combat.FieldE8C == 0 &&
                fixture.Session.SourceEnvironmentalOrder.Any(item => ReferenceEquals(item.Identity, originalItem)),
                "Actual EA4 expiry must call original401 Drop(up*4), restore ballistic gravity, clear genuine session/Kick ownership and write the shared fall/action212 fields.");
            controller.ApplyCombatState();
            melee.ApplyPlayerPose(); player.Model.VerifyCurrentPose();
            GD.Print("Verified native CheckJump/CheckGroundGone: original L1A3 node218 held Jump16 consumption, shared EA4 grace, actual401 Drop/up*4/gravity, session/Kick release and real212 fall pose.");
        }
        finally
        {
            if (ReferenceEquals(fixture.Session.HeldActor, originalItem)) interactions.DropHeldObject(combat.UpDirection * 4);
            (combat.ActionState, combat.CollisionFlags, combat.AnimationId, combat.AnimationFrame, combat.LookaroundActive,
                combat.FieldE8C, combat.FieldAE5, combat.FieldAE6, combat.FallReferenceHeight) = saved;
            controller.SourceGroundContactGrace = grace;
            melee.State.Input.Triggered[16] = pressed; melee.State.Input.Mirrors[16] = mirrored;
            melee.PlaySingleAnimation(saved.AnimationId, 0, -1);
            combat.AnimationFrame = saved.AnimationFrame; melee.ApplyPlayerPose();
            player.Model.SetAnimationFinished(finished); player.Model.PlaybackSpeed = playbackSpeed;
            controller.ApplyCombatState(); melee.State.AnimationFinished = animationFinished;
        }
    }

    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native airborne entry: {evidence}"); }
}
