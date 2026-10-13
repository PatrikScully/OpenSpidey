using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine posed hook2 tracking and timed PutCameraBehind16 over the private original L1A1 player.</summary>
internal static class CameraTrackingImportChecks
{
    public static void Verify(GodotPlayer player, PlayerFollowCamera camera)
    {
        GodotPlayerMelee melee = player.Melee ?? throw new InvalidOperationException("Camera hook proof requires the original player rig.");
        if (camera.SourceControlMode != 3 || player.Combat.State.Field8E8 != 0 || camera.ScriptRig.AngleLocked)
            throw new InvalidDataException("The bounded native swing camera proof requires the genuine unheld floor player and source camera3.");
        camera.InitializeSourceTracking();
        CameraHeadingTransition heading = camera.HeadingTransition;
        CameraTripodState tripod = camera.Tripod;
        (short Heading, int Published, int Target, int Ticks, int Rate) savedHeading =
            (heading.Heading, heading.PublishedHeading, heading.Target, heading.RemainingTicks, heading.Rate);
        (FixedVector3 Position, FixedVector3 Target, FixedVector3 Rate, int Ticks, int Hook, bool Tracking, int X, int Y, int Z) savedTripod =
            (tripod.Position, tripod.Target, tripod.Rate, tripod.RemainingTicks, tripod.HookIndex, tripod.TrackingEnabled, tripod.EaseX, tripod.EaseY, tripod.EaseZ);
        int savedHook = camera.FocusHookIndex;
        int clip = player.Model.ClipId, frame = player.Model.Playback.Frame;
        byte finished = player.Model.Playback.Finished;
        try
        {
            camera.SetSourceFocusHook(2);
            Assert(camera.FocusHookIndex == 2 && tripod.HookIndex == 2,
                "Source swing camera field12C must select the genuine posed hook2 without changing player geometry.");
            player.Model.RunClip(275);
            FixedVector3 firstHook = melee.GetPlayerHookPosition(2);
            bool hookMoved = false;
            var expectedTripod = new CameraTripodState { Position = tripod.Position, Target = tripod.Target, Rate = tripod.Rate,
                RemainingTicks = tripod.RemainingTicks, HookIndex = 2, TrackingEnabled = tripod.TrackingEnabled,
                EaseX = tripod.EaseX, EaseY = tripod.EaseY, EaseZ = tripod.EaseZ };
            short target = player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
            short start = unchecked((short)((target + 1709) & 4095));
            camera.SetCameraAngle(start, 0);
            FixedVector3 cameraPosition = camera.SourcePosition;
            camera.SourcePutBehind(16);
            var expectedHeading = new CameraHeadingTransition { Heading = start, PublishedHeading = start };
            expectedHeading.Set(target, 16, 3);
            Assert(heading.RemainingTicks == 16 && heading.Target == target && heading.Rate == expectedHeading.Rate &&
                camera.SourcePosition == cameraPosition,
                "Actual native PutCameraBehind16 must use the player's genuine effective heading and source timer, retaining the camera position until its scheduled update.");
            for (int update = 0; update < 8; update++)
            {
                player.Model.SetSourceAnimationFrame(Math.Min(update, player.Model.FrameCount - 1));
                FixedVector3 posed = melee.GetPlayerHookPosition(2);
                hookMoved |= posed != firstHook;
                expectedTripod.Advance(player.Controller.Position, player.ActorState.IsDead,
                    index => index == 2 ? posed : throw new InvalidDataException("Unexpected native focus hook."), 2);
                expectedHeading.Advance(2, 3);
                camera.AdvanceSourceTracking(2);
                Assert(camera.SourceTripodPosition == expectedTripod.Position && heading.Heading == expectedHeading.Heading &&
                    heading.PublishedHeading == expectedHeading.PublishedHeading && heading.RemainingTicks == expectedHeading.RemainingTicks &&
                    camera.ScriptRig.Heading == expectedHeading.Heading,
                    "Each source camera tick must follow the actual animated hook2 with original4/8/4 easing and the exact timed heading owner shared with the native/script pose.");
            }
            Assert(hookMoved && heading.RemainingTicks == 0 && heading.Heading == target,
                "The genuine swing clip must move the posed focus, and sixteen source ticks must land exactly on the effective heading.");
            camera.SetSourceFocusHook(-1);
            expectedTripod.HookIndex = -1;
            expectedTripod.Advance(player.Controller.Position, player.ActorState.IsDead,
                _ => throw new InvalidDataException("Default camera tripod must read the body position."), 2);
            camera.AdvanceSourceTracking(2);
            Assert(camera.FocusHookIndex == -1 && camera.SourceTripodPosition == expectedTripod.Position,
                "Clearing source field12C must return to the real player position in the next camera update.");
            GD.Print("Verified native source camera tracking: genuine animated hook2, original4/8/4 tripod easing, exact PutCameraBehind16 heading and timer completion, and source-1 body fallback.");
        }
        finally
        {
            camera.SetCameraAngle(savedHeading.Heading, 0);
            heading.Heading = savedHeading.Heading; heading.PublishedHeading = savedHeading.Published;
            heading.Target = savedHeading.Target; heading.RemainingTicks = savedHeading.Ticks; heading.Rate = savedHeading.Rate;
            camera.SetSourceFocusHook(savedHook);
            tripod.Position = savedTripod.Position; tripod.Target = savedTripod.Target; tripod.Rate = savedTripod.Rate;
            tripod.RemainingTicks = savedTripod.Ticks; tripod.HookIndex = savedTripod.Hook; tripod.TrackingEnabled = savedTripod.Tracking;
            tripod.EaseX = savedTripod.X; tripod.EaseY = savedTripod.Y; tripod.EaseZ = savedTripod.Z;
            player.Model.RunClip(clip); player.Model.SetSourceAnimationFrame(frame); player.Model.SetAnimationFinished(finished);
        }
    }

    private static void Assert(bool condition, string evidence)
    {
        if (!condition) throw new InvalidDataException(evidence);
    }
}
