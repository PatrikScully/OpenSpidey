using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

internal static class CameraCommandModeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CameraCommandModeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-command-mode.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            ushort[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            var rig = new ScriptedCameraRig();
            CameraModeState mode = rig.ModeState;
            mode.Position = new(1000, 1111, 1222); mode.CurrentRotation = new(10, 21, 32, 43);
            mode.AngleInterpolationTicks = 77;
            rig.FixedPosition.Set(mode.Position, new(-4000, -3778, -3556), input[2]);
            rig.FixedOrientation.Set(mode.CurrentRotation, new(-20, -33, -46, -59), input[2]);
            rig.SetMode(input[0]); mode.PushMode(); mode.SetMode(7);
            mode.Position = new(-1000, -1111, -1222); mode.CurrentRotation = new(-10, -21, -32, -43);
            var handler = new CameraCommandHandler(160, rig, () => default);
            byte[] arguments = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(arguments, input[1]);
            var context = new MissionCommandContext(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0);
            JsonElement expected = row.GetProperty("expected");
            assert(mode.Mode == expected[0].GetUInt32() && rig.Mode == 7,
                "The script rig reads actual source7 from the same current camera mode owner before160 dispatch.");
            handler.Execute(context, new(160, 0, arguments));
            assert(Snapshot().SequenceEqual(expected[1].EnumerateArray().Select(value => value.GetInt64())) && rig.Mode == mode.Mode,
                $"Original mode ownership{index}:160 publishes the one current mode while preserving source saved pose, fixed timers/targets and angle clock.");
            mode.PopMode();
            assert(Snapshot().SequenceEqual(expected[2].EnumerateArray().Select(value => value.GetInt64())) && rig.Mode == mode.Mode,
                $"Original mode ownership{index++}:later Pop restores the actual previously saved mode/pose without copying a stale script-mode flag.");

            List<long> Snapshot()
            {
                List<long> values = [mode.Mode, mode.SavedMode, mode.AngleInterpolationTicks];
                void Vec(FixedVector3 vector) => values.AddRange([vector.X, vector.Y, vector.Z]);
                void Quat(FixedQuaternion quaternion) => values.AddRange([quaternion.X, quaternion.Y, quaternion.Z, quaternion.W]);
                Vec(mode.Position); Quat(mode.CurrentRotation); Vec(mode.SavedPosition); Quat(mode.SavedRotation);
                values.AddRange([rig.FixedPosition.RemainingTicks, rig.FixedOrientation.RemainingTicks]);
                Vec(rig.FixedPosition.Target); Quat(rig.FixedOrientation.Target);
                return values;
            }
        }
        foreach ((bool cameraPresent, bool playerPresent) in new[] { (false, false), (false, true), (true, false) })
        {
            byte[] commands = [4, 0, 185, 0, 9, 0, 186, 0, 17, 0, 160, 0, 4, 0, 167, 0, 232, 3, 3, 0,
                180, 0, 0, 0, 0, 0, 0, 128, 2, 0, 1, 0, 187, 0, 0, 0, 195, 0, 12, 0, 255, 255];
            byte[] commandPoint = new byte[commands.Length + 6];
            BinaryPrimitives.WriteUInt16LittleEndian(commandPoint, 6);
            // Genuine kind6 header: zero links, four-byte-aligned checksum, then the source command list.
            commands.AsSpan(2).CopyTo(commandPoint.AsSpan(8));
            var file = new TriggerFile([new(0, 6, 0, commandPoint)]);
            var rig = new ScriptedCameraRig(); byte angleLock = 0;
            rig.BindAngleLock(() => angleLock != 0, value => angleLock = value ? (byte)1 : (byte)0);
            var runtime = new MissionRuntime(file, new MissionRuntimeTests.ActorFactory(),
                CameraCommandHandler.CreateHandlers(rig, () => throw new InvalidOperationException("A missing required owner must skip the player pose callback."),
                    () => cameraPresent, () => playerPresent).Concat(FixedCameraCommandHandler.CreateHandlers(file, rig,
                    () => throw new InvalidOperationException("Null camera/missing authored link must skip native position."),
                    () => throw new InvalidOperationException("Null camera/missing authored link must skip native quaternion."), () => cameraPresent)));
            runtime.Queue(0);
            assert(runtime.ExecutePending() == 1 && rig.WideScreenScriptValue == 12 &&
                rig.Mode == (cameraPresent ? 4 : 3) && rig.Zoom.RemainingTicks == (cameraPresent ? 3 : 0) &&
                rig.Profiles.Get(CameraSurface.Floor, CameraParameter.OffsetX) == 0 && angleLock == (playerPresent ? 1 : 0),
                "Original null-owner guards preserve command consumption:185/186/160/167 require camera,180 requires player+camera,187 requires only player,195 remains process-global.");
        }
        var unsupported = new ScriptedCameraRig();
        var unsupportedContext = new MissionCommandContext(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0);
        var unsupportedInstruction = new TriggerInstruction(160, 0, new byte[] { 42, 0 });
        new CameraCommandHandler(160, unsupported, () => default, () => false).Execute(unsupportedContext, unsupportedInstruction);
        bool rejected = false;
        try { new CameraCommandHandler(160, unsupported, () => default).Execute(unsupportedContext, unsupportedInstruction); }
        catch (NotSupportedException) { rejected = true; }
        assert(rejected && unsupported.Mode == 3 && !unsupported.Enabled,
            "An absent camera skips160 without native dereference; a live camera still explicitly rejects an unported mode instead of accepting a fake renderer path.");
    }
}
