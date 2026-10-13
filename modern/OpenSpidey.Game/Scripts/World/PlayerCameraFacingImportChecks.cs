using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>The real E70 generation, original player/304 positions and concrete camera transitions.</summary>
internal static class PlayerCameraFacingImportChecks
{
    public static void Verify(PlayerInteractionFixture fixture, GodotThugActor actor)
    {
        GodotPlayer player = fixture.Player;
        PlayerCombatState combat = player.Combat.State;
        PlayerMeleeInput input = fixture.Melee.State.Input;
        var shared = (combat.CameraAngleLock, combat.Field54C, combat.Field8E8, combat.FieldEA6,
            combat.Field551, combat.SwingCameraResetLatch);
        var edges = (input.Pressed[5], input.Triggered[4], input.Triggered[17], input.Mirrors[17]);
        var camera = fixture.Camera;
        var heading = (camera.HeadingTransition.Heading, camera.HeadingTransition.PublishedHeading,
            camera.HeadingTransition.Target, camera.HeadingTransition.Rate, camera.HeadingTransition.RemainingTicks);
        var angles = new PlayerLookaroundState(combat) { HoldTicks = 57 };
        var native = new GodotPlayerCameraFacing(player, camera, fixture.Directory, new(fixture.Melee.State), angles);
        try
        {
            combat.CameraAngleLock = combat.Field54C = combat.Field8E8 = 0; combat.FieldEA6 = 0;
            input.Pressed[5] = 1;
            native.SetFocusLockTarget(actor);
            PlayerKickHandle original = native.State.CameraTarget;
            short expected = FixedAim.Calculate(combat.Position, actor.SourcePosition).Yaw;
            short actual = native.UpdateCameraFacing();
            Assert(actual == expected && camera.HeadingTransition.Heading == expected && camera.HeadingTransition.RemainingTicks == 0 &&
                combat.Field551 == 1 && native.State.CameraTarget == original,
                "Genuine E70 recovery must aim at the same live source304 body with original Utils_CalcAim and immediate native SetCamAngle.");
            input.Pressed[5] = 0; input.Triggered[4] = 1;
            combat.SwingCameraResetLatch = 1;
            native.UpdateCameraFacing();
            Assert(input.Triggered[4] == 0 && combat.SwingCameraResetLatch == 0 && angles.HoldTicks == 0 && combat.Field551 == 0,
                "The actual pre-heading finalizer must consume its synthetic target4 edge and shared201/56C through genuine PutCameraBehind0.");
            GD.Print("Verified native camera-facing finalizer: genuine304 E70 validation/Utils_CalcAim, real source SetCamAngle and shared201/56C/4 reset consumption.");
        }
        finally
        {
            (combat.CameraAngleLock, combat.Field54C, combat.Field8E8, combat.FieldEA6,
                combat.Field551, combat.SwingCameraResetLatch) = shared;
            (input.Pressed[5], input.Triggered[4], input.Triggered[17], input.Mirrors[17]) = edges;
            camera.SetCameraAngle(heading.Heading, 0);
            (camera.HeadingTransition.Heading, camera.HeadingTransition.PublishedHeading,
                camera.HeadingTransition.Target, camera.HeadingTransition.Rate, camera.HeadingTransition.RemainingTicks) = heading;
        }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native camera-facing: {evidence}"); }
}
