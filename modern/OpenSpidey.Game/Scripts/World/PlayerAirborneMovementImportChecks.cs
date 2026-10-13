using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Real Jump16 input, original rig/roof physics and production2/4/8/64 owners; no authored replacement surface.</summary>
internal static class PlayerAirborneMovementImportChecks
{
    public static void Verify(GodotPlayer player, GodotPlayerWebRuntime webs, Action<PlayerInputSample, PlayerButtonInput> step)
    {
        GodotPlayerAirborneMovement airborne = webs.AirborneMovement ?? throw new InvalidDataException("Production runtime omitted full source airborne movement.");
        GodotPlayerCameraFacing torso = webs.Swing?.CameraFacing ?? throw new InvalidDataException("Production runtime omitted the real camera/torso finalizer.");
        PlayerTerrainController controller = player.Controller;
        PlayerCombatState combat = player.Combat.State;
        Assert(ReferenceEquals(controller.AirborneMovement, airborne) && ReferenceEquals(controller.CameraFacing, torso) &&
            ReferenceEquals(airborne.State.Melee, player.Melee!.State) &&
            controller.IsGrounded && controller.Action == PlayerAction.Idle,
            "Manual air sequence requires the production shared owner settled on the original roof.");
        FixedVector3 start = controller.Position;
        int jumpingBefore = airborne.JumpingUpdates, fallingBefore = airborne.FallingUpdates;
        int expectedTorsoUpdates = torso.TorsoUpdates;
        int startWebbing = player.Inventory!.State.Webbing;
        uint firstClock = player.Combat.Timer;
        int highestY = start.Y;
        var clips = new HashSet<ushort>();
        bool jumping = false, falling = false, landed = false;
        int? apexReference = null;
        uint? apexClock = null;
        int preparedClip = -1;
        FixedJointTransform[] preparedRaw = [], preparedSource = [], preparedRender = [];
        controller.BeforePlayerInput += CapturePreparedPose;
        try
        {
            VerifiedStep(default, new(PlayerButton.Jump, PlayerButton.Jump));
            Assert(controller.Action == PlayerAction.JumpWindup && player.Model.ClipId == 210 &&
                torso.TorsoUpdates == ++expectedTorsoUpdates,
                "Actual Jump16 edge on an unheld original roof must enter64 with source standing210 windup.");
            for (int frame = 0; frame < 160; frame++)
            {
                VerifiedStep(default, default);
                Assert(torso.TorsoUpdates == ++expectedTorsoUpdates && torso.LastTorsoClock == player.Combat.Timer &&
                    torso.LastTorsoAction == (uint)controller.Action && controller.HeadingTurn.RemainingTicks == 0,
                    "Each released Jump16 frame must run exactly one genuine camera/torso slice after action dispatch and retain its source airborne/idle lock through turn integration.");
                clips.Add((ushort)player.Model.ClipId);
                highestY = Math.Min(highestY, controller.Position.Y);
                jumping |= controller.Action == PlayerAction.Jumping;
                falling |= controller.Action == PlayerAction.Falling;
                landed |= controller.Action == PlayerAction.WallAttached;
                if (controller.Action == PlayerAction.Falling && player.Model.ClipId == 212)
                {
                    apexReference = combat.FallReferenceHeight;
                    apexClock = unchecked((uint)combat.FallReferenceTick);
                }
                if (jumping && falling && landed && controller.IsGrounded && controller.Action == PlayerAction.Idle) break;
            }
            Assert(jumping && falling && landed && controller.IsGrounded && controller.Action == PlayerAction.Idle &&
                airborne.JumpingUpdates > jumpingBefore && airborne.FallingUpdates > fallingBefore,
                $"Real manual jump must execute source64→2→4→8→idle and both concrete airborne callbacks. Jumping={jumping}, falling={falling}, landed={landed}, action={controller.Action}, grounded={controller.IsGrounded}, clips={string.Join(',',clips)}.");
            Assert(clips.Contains(211) && clips.Contains(212) && clips.Contains(213) && highestY < start.Y - (96 << 12) &&
                controller.Position == start && controller.SourceJumpImpulse.Phase == 0 && controller.SourceJumpImpulse.Hold == 0 &&
                player.Inventory.State.Webbing == startWebbing && webs.Actions.State.CurrentStrand is null,
                $"Source released-jump duration, original211/212/213 clips and actual roof landing must preserve position and web supplies. Start={start}, end={controller.Position}, highestY={highestY}, impulse={controller.SourceJumpImpulse}, clips={string.Join(',',clips)}, webbing={startWebbing}/{player.Inventory.State.Webbing}.");
            Assert(combat.FallReferenceTick == airborne.State.FallReferenceTick && combat.FallReferenceHeight == airborne.State.FallReferenceHeight &&
                apexReference is { } reference && reference < start.Y && apexClock is { } referenceClock &&
                unchecked(referenceClock - firstClock) > 0 && unchecked(player.Combat.Timer - referenceClock) < 320 &&
                combat.PendingWebReleaseAnimation == 0 && combat.FieldAE5 == 0,
                $"Actual falling must update the one sharedE38/E3C reference at the apex and landing must clear shared54D/AE5. Apex={apexReference}/{apexClock}, currentClock={player.Combat.Timer}, 54D={combat.PendingWebReleaseAnimation}, AE5={combat.FieldAE5}.");
            GD.Print("Verified manual native Jump16: source64→2→4→8→idle, genuine210/211/212/213 assets, actual roof/apex physics, once-per-frame camera/torso phase and airborne lock, shared impulse/fall references and untouched web supplies.");
        }
        finally { controller.BeforePlayerInput -= CapturePreparedPose; }

        void CapturePreparedPose(uint _)
        {
            // Ob_AI's animation preparation precedes all player AI/RunAnim selections.
            player.Model.VerifyCurrentPose();
            preparedClip = player.Model.ClipId;
            preparedRaw = Enumerable.Range(0, player.Model.SourcePartCount).Select(player.Model.GetSourceAnimationPartPose).ToArray();
            preparedSource = Enumerable.Range(0, player.Model.SourcePartCount).Select(player.Model.GetSourcePartPose).ToArray();
            preparedRender = Enumerable.Range(0, player.Model.SourcePartCount).Select(player.Model.GetPartPose).ToArray();
        }
        void VerifiedStep(PlayerInputSample input, PlayerButtonInput buttons)
        {
            step(input, buttons);
            if (player.Model.ClipId != preparedClip && (player.ActorState.ItemFlags & 4) == 0)
                Assert(Enumerable.Range(0, player.Model.SourcePartCount).All(part =>
                    player.Model.GetSourceAnimationPartPose(part) == preparedRaw[part] &&
                    player.Model.GetSourcePartPose(part) == preparedSource[part] && player.Model.GetPartPose(part) == preparedRender[part]),
                    "Same-frame source RunAnim with flag4 clear must preserve the independently verified Ob_AI raw/hook/render poses through the transform-only finalizer.");
            else player.Model.VerifyCurrentPose();
        }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native airborne movement: {evidence}"); }
}
