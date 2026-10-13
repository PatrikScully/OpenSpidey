using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class CameraParameterTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(CameraParameterTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-parameters.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int kind = test.GetProperty("parameter").GetInt32();
            int initial = test.GetProperty("initial").GetInt32(), target = test.GetProperty("target").GetInt32();
            if (kind == 3) { initial = unchecked((ushort)initial); target = unchecked((ushort)target); }
            var transition = new CameraParameterTransition(initial, kind < 3);
            transition.Set(target, (ushort)test.GetProperty("duration").GetInt32(), test.GetProperty("enabled").GetBoolean());
            foreach (JsonElement result in test.GetProperty("expected").EnumerateArray())
            {
                transition.Advance(test.GetProperty("ticks").GetInt32());
                assert(transition.Value == result[0].GetInt32() && transition.RemainingTicks == result[1].GetInt32(),
                    "Recovered camera scalar value/countdown matches the compiled setters and AI block.");
            }
        }
        var profiles = new CameraParameterProfiles();
        assert(!profiles.Set(CameraSurface.Wall, CameraParameter.HorizontalDistance, 512, true, default) &&
            profiles.Get(CameraSurface.Wall, CameraParameter.HorizontalDistance) == 512,
            "Inactive surface profiles still store their new setting without changing the current camera.");
        assert(profiles.Set(CameraSurface.Falling, CameraParameter.VerticalDistance, -200, true, new(false, false, false, 4)) &&
            !profiles.Set(CameraSurface.Falling, CameraParameter.VerticalDistance, -100, true, new(false, false, false, 2)),
            "Falling camera values use the source's exact falling action gate.");
        var rig = new ScriptedCameraRig();
        rig.SetMode(3); rig.SetAngleLock(0);
        rig.SetPlayerValue(CameraSurface.Floor, CameraParameter.VerticalDistance, 0, 0, true, default);
        FixedVector3 tripod = new(20 << 12, 30 << 12, 40 << 12);
        ScriptedCameraPose pose = rig.DesiredPose(tripod);
        assert(pose.Position == tripod + new FixedVector3(0, 0, 512 << 12) && pose.Focus == tripod && pose.Down == new FixedVector3(0, 4096, 0),
            "The zero-pitch recovered desired pose retains its original axes and source distance.");
        rig.SetWideScreenScriptValue(0);
        assert(rig.Enabled && rig.AngleLocked && rig.WideScreenScriptValue == 0,
            "Script camera state keeps the original lock polarity and records presentation requests separately from viewport aspect.");
        byte playerAngleLock = 0x80;
        var boundRig = new ScriptedCameraRig();
        boundRig.BindAngleLock(() => playerAngleLock != 0, value => playerAngleLock = value ? (byte)1 : (byte)0);
        assert(boundRig.AngleLocked && playerAngleLock == 0x80,
            "Binding the camera reads the existing player8EC byte without overwriting retained state.");
        var lockHandler = new CameraCommandHandler(187, boundRig, () => default);
        var lockContext = new MissionCommandContext(new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty), 0);
        lockHandler.Execute(lockContext, new(187, 0, new byte[] { 1, 0 }));
        assert(playerAngleLock == 0 && !boundRig.AngleLocked,
            "Source187 unlocks the same player8EC owner used by crawl facing and PutCameraBehind.");
        playerAngleLock = 0xFF;
        assert(boundRig.AngleLocked, "Direct player angle-lock changes are visible to the camera without a copied flag.");
        lockHandler.Execute(lockContext, new(187, 0, new byte[] { 0, 0 }));
        assert(playerAngleLock == 1 && boundRig.AngleLocked,
            "Source187 zero operand normalizes the canonical player angle lock to one.");
        double originalVertical = CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom);
        double slope = Math.Tan(originalVertical * Math.PI / 360);
        assert(Math.Abs(slope - 2365.0 * 3 / (4096 * 4)) < 1e-12,
            "The vertical projection reproduces the original square-pixel PC projection at four-to-three aspect.");
        foreach (double aspect in new[] { 4.0 / 3, 16.0 / 9, 21.0 / 9 })
        {
            double horizontal = 2 * Math.Atan(slope * aspect) * 180 / Math.PI;
            assert(horizontal >= 2 * Math.Atan(2365.0 / 4096) * 180 / Math.PI - 1e-10,
                "Wider views gain horizontal field while retaining the original vertical projection.");
        }
        byte[] commands = [4, 0, 167, 0, 232, 3, 3, 0, 187, 0, 1, 0, 255, 255];
        var zoomRig = new ScriptedCameraRig();
        var runtime = new MissionRuntime(new([new(0, 4, 0, commands)]), new MissionRuntimeTests.ActorFactory(),
            CameraCommandHandler.CreateHandlers(zoomRig, () => default));
        runtime.QueueAutoexec();
        assert(runtime.ExecutePending() == 1 && zoomRig.Zoom.Value == 2365 && zoomRig.Zoom.Target == 1000 &&
            zoomRig.Zoom.RemainingTicks == 3 && !zoomRig.AngleLocked,
            "The zoom command consumes both words and leaves the following camera command executable.");
        zoomRig.Advance(2);
        assert(zoomRig.Zoom.Value == 1455 && zoomRig.Zoom.RemainingTicks == 1,
            "Timed zoom truncates the signed per-tick rate before applying elapsed ticks.");
        zoomRig.Advance(2);
        assert(zoomRig.Zoom.Value == 1000 && zoomRig.Zoom.RemainingTicks == 0 && !zoomRig.Enabled,
            "Zoom completes exactly without switching the camera's current movement mode.");
        zoomRig.SetZoom(4096, 0);
        assert(zoomRig.Zoom.Value == 4096 && zoomRig.Zoom.RemainingTicks == 0,
            "Zero-duration zoom updates the projection immediately.");
    }
}
