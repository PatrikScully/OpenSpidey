using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Original landing and windup actions over the production player, shared jump impulse and original animation assets.</summary>
internal static class PlayerLandingRecoveryImportChecks
{
    public static void Verify(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerWebRuntime webs)
    {
        GodotPlayerMelee melee = player.Melee ?? throw new InvalidDataException("Landing proof requires the original posed player.");
        GodotPlayerLandingRecovery recovery = webs.LandingRecovery ?? throw new InvalidDataException("Production swing omitted the actual landing owner.");
        PlayerTerrainController controller = player.Controller;
        PlayerCombatState combat = player.Combat.State;
        PlayerLookaroundState lookaround = webs.Lookaround!.State;
        if (!controller.IsGrounded || controller.Action != PlayerAction.Idle || player.Model.ClipId != 0 || controller.SupportBody is not null)
            throw new InvalidDataException("Bounded landing proof requires the genuine unheld player settled on the original roof.");
        Assert(ReferenceEquals(controller.LandingRecovery, recovery) && ReferenceEquals(recovery.State.Melee, melee.State) &&
            ReferenceEquals(recovery.State.Lookaround, lookaround), "Landing must reuse the production impulse, input and E8D owners.");

        uint action = combat.ActionState;
        ushort animation = combat.AnimationId;
        int animationFrame = combat.AnimationFrame, animationSpeed = player.Model.PlaybackSpeed;
        byte finished = player.Model.Playback.Finished;
        ushort itemFlags = player.ActorState.ItemFlags;
        bool sourcePoseEnabled = player.Model.SourcePoseEnabled;
        short suppression = combat.FieldEA6;
        byte ae4 = combat.FieldAE4, ae5 = combat.FieldAE5, e8c = combat.FieldE8C, e8d = lookaround.JumpReleased;
        byte ad7 = combat.FieldAD7, pressedJump = melee.State.Input.Pressed[16];
        PlayerJumpImpulse impulse = controller.SourceJumpImpulse, adapterImpulse = recovery.State.Jump;
        int ramp = controller.SpeedRamp, adapterRamp = recovery.State.SpeedRamp;
        PlayerAxes axes = controller.MovementInput.Axes;
        short sourceCameraHeading = camera.SourceHeading;
        short cameraAngle = camera.ScriptRig.Heading;
        short viewHeading = camera.LookaroundMode.ViewHeading;
        int cameraPublishedHeading = camera.HeadingTransition.PublishedHeading;
        var cameraTransition = (camera.HeadingTransition.Heading, camera.HeadingTransition.Target,
            camera.HeadingTransition.RemainingTicks, camera.HeadingTransition.Rate);
        bool animationFinished = melee.State.AnimationFinished;
        FixedVector3 position = controller.Position;
        try
        {
            SetAction(8, 178);
            combat.FieldE8C = 0; combat.FieldAE4 = 99; combat.FieldEA6 = 77;
            recovery.UpdateRecovery(ushort.MaxValue);
            Assert(combat.ActionState == 8 && combat.AnimationId == 178 && combat.FieldEA6 == 0 && combat.FieldAE4 == 0 &&
                controller.SourceJumpImpulse == impulse,
                "Protected source178 must skip every airborne guard, derive AE4 from E8C and retain its unfinished landing/impulse.");

            SetAction(8, 180);
            combat.FieldE8C = 37; combat.FieldAE4 = 0; melee.State.Input.Pressed[16] = 0;
            recovery.UpdateRecovery(180);
            Assert(combat.ActionState == 16 && combat.AnimationId == 180 && combat.FieldAE4 == 1,
                "Completed protected180 with released Jump16 must select source walking while retaining the current recovery clip.");

            SetAction(8, 180);
            var beforeJump = new PlayerJumpImpulse(12345, 0x12345, 77);
            controller.SetSwingJumpImpulse(beforeJump); controller.SetSourceSpeedRamp(0x18000);
            combat.FieldE8C = 37; lookaround.JumpReleased = 173; melee.State.Input.Pressed[16] = 1;
            recovery.UpdateRecovery(180);
            PlayerJumpImpulse repeated = new(-245760, beforeJump.Phase, (beforeJump.Phase >> 1) + beforeJump.Phase);
            Assert(combat.ActionState == 64 && player.Model.ClipId == 223 && combat.AnimationId == 223 &&
                lookaround.JumpReleased == 0 && controller.SourceJumpImpulse == repeated &&
                controller.SpeedRamp == ((controller.CollisionFlags & 1) != 0 ? 0 : 0x18000),
                "Held Jump16 after genuine landing must select imported223, clear the shared E8D byte and write actual E80/E84/E88 and collision-gated EBC.");

            controller.ApplyCombatState();
            player.Model.SetAnimationFinished(1); combat.FieldEA6 = -31;
            recovery.UpdateWindup();
            Assert(combat.ActionState == 2 && player.Model.ClipId is 224 or 231 && combat.FieldEA6 == 0 &&
                controller.SourceJumpImpulse == new PlayerJumpImpulse(-245760, 0x70000, 0xA8000),
                "Actual completed223 must consume the genuine RNG branch, select original224/231 and recompute the shared jumping impulse.");

            SetAction(64, 210); player.Model.SetAnimationFinished(1);
            recovery.UpdateWindup();
            Assert(combat.ActionState == 2 && player.Model.ClipId == 211 &&
                controller.SourceJumpImpulse == new PlayerJumpImpulse(-245760, 0x70000, 0xA8000),
                "Actual completed standing210 must select its original211 asset and the same source jumping durations.");

            // This is the same source E2D/E2E owner written by swinging's forced-control leaf.
            // No held object is needed: CheckForwards chooses these original clips from the current animation.
            controller.SetForcedAirControl(127, 0, controller.SpeedRamp);
            short effective = controller.Orientation.GetEffectiveHeading(controller.OnWall);
            short viewAngle = unchecked((short)((effective - controller.MovementInput.Heading) & 4095));
            // The player reads field23A, which the source rebuilds from SetCamAngle only on the next camera AI update.
            camera.SetCameraAngle(viewAngle, 0); camera.LookaroundMode.ViewHeading = viewAngle;
            foreach (var clips in new[] { (Idle: (ushort)200, Moving: (ushort)197), (Idle: (ushort)194, Moving: (ushort)191) })
            {
                SetAction(1, clips.Idle); combat.FieldAD7 = 173; combat.FieldEA6 = 91;
                Assert(recovery.CheckForwards(true) && combat.ActionState == 16 && combat.FieldAD7 == 0 && combat.FieldEA6 == 0 &&
                    combat.AnimationId == clips.Moving && player.Model.ClipId == clips.Moving,
                    $"Actual native forward selection must retain original held-idle{clips.Idle} to moving{clips.Moving} asset selection and shared fields.");
            }
            Assert(controller.Position == position, "Direct source recovery and forward selection must retain genuine roof geometry and body position.");
            melee.ApplyPlayerPose(); player.Model.VerifyCurrentPose();
            GD.Print("Verified native source8/64 landing: original178/180 guards, shared Jump16/E8D repeat impulse, genuine223 RNG and210/211 windup assets, and exact200/197 plus194/191 forward clip selection.");
        }
        finally
        {
            controller.SetSwingJumpImpulse(impulse); controller.SetForcedAirControl(axes.Forward, axes.Sideways, ramp);
            recovery.State.Jump = adapterImpulse; recovery.State.SpeedRamp = adapterRamp;
            melee.State.Input.Pressed[16] = pressedJump;
            combat.FieldEA6 = suppression; combat.FieldAE4 = ae4; combat.FieldAE5 = ae5; combat.FieldE8C = e8c;
            combat.FieldAD7 = ad7; lookaround.JumpReleased = e8d;
            melee.PlaySingleAnimation(animation, 0, -1); player.Model.SetSourceAnimationFrame(animationFrame);
            player.Model.SetAnimationFinished(finished); player.Model.PlaybackSpeed = animationSpeed;
            combat.AnimationId = animation; combat.AnimationFrame = animationFrame; combat.ActionState = action;
            controller.ApplyCombatState(); melee.ApplyPlayerPose();
            player.Model.ApplySourceJointPose(sourcePoseEnabled); player.ActorState.ItemFlags = itemFlags;
            melee.State.AnimationFinished = animationFinished;
            camera.SetCameraAngle(cameraAngle, 0);
            camera.LookaroundMode.ViewHeading = viewHeading;
            (camera.HeadingTransition.Heading, camera.HeadingTransition.Target,
                camera.HeadingTransition.RemainingTicks, camera.HeadingTransition.Rate) = cameraTransition;
            camera.HeadingTransition.PublishedHeading = cameraPublishedHeading;
            Assert(camera.SourceHeading == sourceCameraHeading, "Native landing proof must restore the actual camera heading owner.");
        }

        void SetAction(uint sourceAction, ushort clip)
        {
            melee.PlaySingleAnimation(clip, 0, -1);
            combat.ActionState = sourceAction;
            controller.ApplyCombatState();
        }
    }

    private static void Assert(bool condition, string evidence)
    {
        if (!condition) throw new InvalidDataException($"Native landing recovery: {evidence}");
    }
}
