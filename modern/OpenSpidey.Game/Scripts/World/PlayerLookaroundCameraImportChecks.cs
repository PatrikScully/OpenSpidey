using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L1A1 posed entry, source mode7 transition/world-clearance placement, native target triangles and complete exit.</summary>
internal static class PlayerLookaroundCameraImportChecks
{
    public static async Task VerifyAsync(Node3D root, GodotPlayer player, PlayerFollowCamera camera,
        GodotPlayerCombatRuntime combat, GodotPlayerWebRuntime webs, byte[] originalBits)
    {
        GodotPlayerMelee melee = player.Melee ?? throw new InvalidOperationException("Native lookaround proof requires the genuine player melee owner.");
        GodotPlayerWebAttack attack = webs.Attack ?? throw new InvalidOperationException("Native lookaround proof requires the shared source web attack owner.");
        var target = new GodotScreenTarget { Name = "ActualScreenTargetProof", Camera = camera };
        root.AddChild(target);
        GodotPlayerLookaroundAim aim = webs.Lookaround ?? throw new InvalidOperationException("The player has no shared lookaround angle owner.");
        var lookaround = new GodotPlayerLookaroundCamera(player, aim, camera, combat.World, target.SetTargetVisible);
        Action<int>? oldCameraBehind = player.Combat.CameraBehind;
        bool oldActive = player.Combat.State.LookaroundActive;
        byte oldSurface = melee.Kick.SurfaceTransition;
        var neck = player.Model.GetSourceJoint(1); var head = player.Model.GetSourceJoint(3);
        player.Combat.CameraBehind = camera.PutBehind;
        try
        {
            short heading = player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
            FixedQuaternion body = FixedQuaternion.FromMatrix(player.Controller.Orientation.Basis);
            FixedQuaternion snapshot = FixedQuaternion.FromMatrix(PlayerLookaroundTransition.FlipCameraColumns(camera.SourceRotation.ToMatrix()));
            FixedVector3 up = player.Combat.State.UpDirection;
            FixedVector3 hook = melee.GetPlayerHookPosition(8);
            int rayCount = combat.World.RayQueries;
            lookaround.EnterLookaroundMode();
            Assert(aim.State.Active && aim.State.EntranceTicks == 24 && camera.SourceControlMode == 7 &&
                lookaround.State.Path is { Length: 24 } path && path[0] == snapshot && path[23] == body &&
                lookaround.State.Offset == up * 128 && lookaround.State.Anchor == hook + up * 128,
                "Actual native entry must use genuine posed hook8/up128, exact camera-to-body24 quaternion path, shared8EA and source front mode7.");
            PlayerLookaroundCameraSetupResult result = default;
            for (int frame = 0; frame < 12; frame++)
            {
                result = lookaround.UpdateCameraPlacement(2);
                camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
                Assert(camera.SourceRotation == camera.LookaroundMode.DisplayRotation && camera.SourceControlMode == 7,
                    "Native front camera must display the exact source desired quaternion after its own source update, without orbit interpolation.");
                Assert(camera.LookaroundMode.Angles == FixedAim.Calculate(camera.LookaroundMode.Position, player.Controller.Position),
                    "Source camera7 mAngles must follow actual Utils_CalcAim(camera.mPos, default player focus).");
                var display = camera.SourceRotation.ToMatrix();
                Assert(camera.SourceHeading == (short)((-1024 - GameAngle.Atan2(display.M22, display.M02)) & 4095),
                    "Source camera7 field23A must follow actual LoadIntoMikeCamera from the exact final display matrix, separately from focus aim angles and orbit yaw.");
            }
            Assert(result.SelectTarget && aim.State.EntranceTicks == 0 && combat.World.RayQueries > rayCount &&
                lookaround.State.LastCameraPosition == camera.LookaroundMode.Position && !target.State.Visible,
                "The real twelve source updates must complete entry, query imported geometry through the actual clearance grid and expose genuine steady target selection.");
            Assert(player.Model.GetSourceJoint(1).RotationX == 85 && player.Model.GetSourceJoint(3).RotationX == 85,
                "Actual native lookaround placement must retain the original half-pitch neck/head corrections.");
            var targets = new GodotPlayerLookaroundTargets(player, webs.Actions, lookaround, combat.Directory, combat.Session,
                combat.World, target, new()
                {
                    Clock = () => webs.Actions.Clock,
                    BeginZip = _ => throw new InvalidOperationException("This bounded inactive-target proof excludes the separate source zip action-entry driver."),
                    BeginSwing = _ => throw new InvalidOperationException("This bounded inactive-target proof excludes the separate source100 swing action driver.")
                });
            targets.Update(result);
            Assert(!combat.World.LineOfSightCheck && webs.Actions.ExplicitAimPosition == targets.State.TargetPosition &&
                webs.Actions.ExplicitAimSurfaceNormal == targets.State.SurfaceNormal &&
                ReferenceEquals(melee.State.AimTarget, targets.State.TorsoTarget) && ReferenceEquals(melee.AutoAim.Target, targets.State.TorsoTarget),
                "Native steady lookaround must use the genuine baddy/switch/world selection, restore the source LOS flag and share full-vector normal/position and DCC identity with FireWeb/auto-aim.");
            await PlayerLookaroundReticleImportChecks.VerifyAsync(root, originalBits, camera, targets.State);
            aim.State.Yaw = 1025; attack.AimTorso(melee.Kick);
            Assert(player.Controller.HeadingTurn.Target == (ushort)((heading + 1025) & 4095),
                "The shared source lookaround raw yaw owner must still bind the real short/bool web torso target.");
            target.SetTarget(player.Controller.Position, 24, 544); target.SetTargetVisible(true);
            await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
            await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(target.Draws.Count == 4 && target.Draws.All(triangle => triangle.Argb == 0xFFA0A000),
                "Actual Screen_DrawTarget must produce allfour original flat native triangles and the original packedARGB.");
            lookaround.ExitLookaroundMode();
            Assert(!aim.State.Active && aim.State.EntranceTicks == 0 && aim.State.ExitTicks == 0 && lookaround.State.Path is null &&
                !camera.LookaroundModeEnabled && camera.SourceControlMode == 3 && !target.State.Visible &&
                player.Model.GetSourceJoint(1).RotationX == 0 && player.Model.GetSourceJoint(1).RotationY == 0 &&
                player.Model.GetSourceJoint(3).RotationX == 0 && player.Model.GetSourceJoint(3).RotationY == 0,
                "Actual native exit must retire its24-sample path, pop original mode, call genuine PutCameraBehind0, hide target and clear both original neck/head joints.");
            GD.Print("Verified native lookaround camera: original posed hook8, exact camera-to-body24 path, twelve source mode7 updates, real world clearance grid/target selection, separate display heading/focus aim, shared web torso aim, original4 target triangles and complete exit.");
        }
        finally
        {
            if (aim.State.Active) lookaround.ExitLookaroundMode();
            player.Combat.CameraBehind = oldCameraBehind;
            player.Combat.State.LookaroundActive = oldActive; melee.Kick.SurfaceTransition = oldSurface;
            player.Model.SetSourceJoint(1, neck); player.Model.SetSourceJoint(3, head); melee.LockTargetTorsoAngle();
            target.Free();
        }
    }
    private static void Assert(bool condition, string evidence)
    {
        if (!condition) throw new InvalidDataException(evidence);
    }
}
