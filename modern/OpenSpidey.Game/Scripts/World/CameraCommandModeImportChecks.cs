using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine native camera dispatch from the single source-mode owner, with saved lookaround pose and real timed camera data.</summary>
internal static class CameraCommandModeImportChecks
{
    public static void Verify(Node owner, PlayerFollowCamera actualCamera, TriggerNode commandContext)
    {
        var camera = new PlayerFollowCamera { Name = "SourceCameraModeCommandProof", Target = actualCamera.Target,
            SourceProjectionCache = actualCamera.SourceProjectionCache, SourceTotalTime = actualCamera.SourceTotalTime };
        bool wasCurrent = actualCamera.Current;
        int checks = 0;
        try
        {
            owner.AddChild(camera); camera.SetPhysicsProcess(false); camera.GlobalTransform = actualCamera.GlobalTransform;
            var position = actualCamera.SourcePosition;
            var rotation = actualCamera.SourceRotation;
            var target = actualCamera.Target.Controller.Position;
            camera.ScriptRig.SetFixedPositionAndRotation(position, target, rotation, rotation, 17);
            camera.ScriptRig.SetZoom(1733, 19);
            Assert(ReferenceEquals(camera.ScriptRig.ModeState, camera.LookaroundMode) && camera.SourceControlMode == 5,
                "Native fixed186 must publish the same actual camera mode owner read by source dispatch, without a second copied flag.");
            camera.PushLookaroundMode(); camera.SetFrontLookaroundMode();
            camera.SetLookaroundDesiredRotation(rotation);
            Assert(camera.SourceControlMode == 7 && camera.ScriptRig.Mode == 7 && camera.LookaroundModeEnabled &&
                camera.LookaroundMode.SavedMode == 5 && camera.LookaroundMode.SavedPosition == position &&
                camera.LookaroundMode.SavedRotation == rotation,
                "Actual Push/Set7 must retain the source fixed-mode pose snapshot while every owner reads the one current front mode.");
            camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
            Assert(camera.SourceControlMode == 7 && camera.SourceRotation == camera.LookaroundMode.DisplayRotation &&
                camera.ScriptRig.FixedPosition.RemainingTicks == 17 && camera.ScriptRig.FixedOrientation.RemainingTicks == 17 &&
                camera.ScriptRig.Zoom.RemainingTicks == 17,
                "Genuine mode7 dispatch must keep fixed movement/orientation timers dormant while the independent source zoom timer advances.");

            var handler = new CameraCommandHandler(160, camera.ScriptRig, () => default,
                () => GodotObject.IsInstanceValid(camera), () => GodotObject.IsInstanceValid(camera.Target));
            Mode(3);
            Assert(camera.SourceControlMode == 3 && !camera.LookaroundModeEnabled && camera.LookaroundMode.SavedMode == 5 &&
                camera.ScriptRig.FixedPosition.RemainingTicks == 17 && camera.ScriptRig.FixedOrientation.RemainingTicks == 17 &&
                camera.ScriptRig.FixedPosition.Target == target && camera.ScriptRig.FixedOrientation.Target == rotation,
                "Source160(3) must immediately leave native front dispatch without destroying the saved mode/pose or timed fixed targets.");
            Mode(4); camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
            Assert(camera.SourceControlMode == 4 && !camera.LookaroundModeEnabled &&
                camera.ScriptRig.FixedPosition.RemainingTicks == 15 && camera.ScriptRig.FixedOrientation.RemainingTicks == 17,
                "Source160(4) must resume genuine native fixed-position dispatch and its timer, retaining the separate dormant quaternion transition.");
            Mode(5); camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
            Assert(camera.SourceControlMode == 5 && !camera.LookaroundModeEnabled &&
                camera.ScriptRig.FixedPosition.RemainingTicks == 13 && camera.ScriptRig.FixedOrientation.RemainingTicks == 15 &&
                camera.SourceRotation == camera.ScriptRig.FixedOrientation.Value,
                "Source160(5) must route native fixed quaternion display through the shared mode and resume both original timed transitions.");
            camera.PopLookaroundMode();
            Assert(camera.SourceControlMode == 5 && camera.LookaroundMode.Position == position &&
                camera.LookaroundMode.CurrentRotation == rotation && camera.ScriptRig.FixedPosition.RemainingTicks == 13 &&
                camera.ScriptRig.FixedOrientation.RemainingTicks == 15,
                "Later genuine Pop restores the previously saved pose/mode while preserving all intervening fixed transition state.");
            Mode(4); camera.PushLookaroundMode();
            var savedFourPosition = camera.LookaroundMode.SavedPosition;
            var savedFourRotation = camera.LookaroundMode.SavedRotation;
            camera.SetFrontLookaroundMode();
            Assert(camera.SourceControlMode == 7 && camera.ScriptRig.Mode == 7 && camera.LookaroundMode.SavedMode == 4,
                "A second genuine Push from mode4 must save that mode independently and publish current7 through the same script/native owner.");
            Mode(3);
            Assert(camera.SourceControlMode == 3 && !camera.LookaroundModeEnabled && camera.LookaroundMode.SavedMode == 4 &&
                camera.ScriptRig.FixedPosition.RemainingTicks == 13 && camera.ScriptRig.FixedOrientation.RemainingTicks == 15,
                "Source160 while current7 must also preserve a saved4 snapshot and both independently retained fixed timers.");
            camera.PopLookaroundMode();
            Assert(camera.SourceControlMode == 4 && camera.LookaroundMode.Position == savedFourPosition &&
                camera.LookaroundMode.CurrentRotation == savedFourRotation,
                "Source Pop after160 must restore the genuine mode4 position/quaternion snapshot rather than the intervening script mode3.");
            camera.Free();
            new CameraCommandHandler(160, camera.ScriptRig, () => default, () => GodotObject.IsInstanceValid(camera))
                .Execute(new(commandContext, 0), new(160, 0, new byte[] { 42, 0 }));
            Assert(!GodotObject.IsInstanceValid(camera) && camera.ScriptRig.Mode == 4,
                "Actual native camera deletion must make source160's presence guard skip its body and renderer without inventing an unsupported mode.");
            GD.Print($"PASS: {checks} native camera mode ownership checks: actual shared160/186/7 dispatch, retained Push/Pop pose, separate fixed/zoom timers and real deleted-camera guard.");

            void Mode(ushort mode)
            {
                byte[] arguments = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(arguments, mode);
                handler.Execute(new(commandContext, 0), new(160, 0, arguments));
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(camera)) camera.Free();
            if (GodotObject.IsInstanceValid(actualCamera)) actualCamera.Current = wasCurrent;
        }
        void Assert(bool condition, string evidence) { checks++; if (!condition) throw new InvalidDataException(evidence); }
    }
}
