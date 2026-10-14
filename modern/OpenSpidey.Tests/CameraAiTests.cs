using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Mathematics;

/// <summary>Extra frame commands and per-frame outputs layered on the shared camera AI sequence format.</summary>
internal interface ICameraAiSequenceExtension
{
    bool Execute(int kind, CameraAi ai, CameraFocusTarget boss, Action<CameraFocusTarget?> setBoss);
    IEnumerable<int> Outputs(CameraAi ai, CameraFocusTarget? boss);
}

internal static class CameraAiTests
{
    private const int None = 0, SetMode = 1, FixedPos = 2, FixedPosAngles = 3, FixedFocus = 4, CamAngle = 5, Shake = 6,
        TripodMotion = 7, XzDistance = 8, YDistance = 9;

    private static readonly CameraParameter[] TransitionOrder = [CameraParameter.HorizontalDistance, CameraParameter.VerticalDistance,
        CameraParameter.OffsetX, CameraParameter.OffsetY, CameraParameter.OffsetZ];

    private sealed class Host(int[][] hits, int[] input, CameraAi ai) : ICameraAiHost
    {
        private int cursor;
        public List<int> Events { get; } = [];
        public int Flags = input[85], HeadingCalls;
        public CameraFocusTarget? Tripod { get; set; }
        public CameraFocusTarget? Boss { get; set; }
        public bool TripodDead => false;
        public uint TotalTime { get; set; }
        public FixedVector3 TripodHookPosition(int hook) => throw new InvalidOperationException("The fixture tripod has no hook.");
        public CameraLineHit? CameraLineToItem(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            int[] hit = cursor < hits.Length ? hits[cursor] : new int[8];
            cursor++;
            return hit[0] == 0 ? null : new(hit[1], new(hit[2], hit[3], hit[4]),
                unchecked((short)hit[5]), unchecked((short)hit[6]), unchecked((short)hit[7]));
        }
        public bool PlayerActionLocked => input[86] != 0;
        public bool PlayerMoving => (input[87] | input[88]) != 0;
        public bool PlayerHiddenWhenFar => input[89] != 0;
        public short PlayerEffectiveHeading() { HeadingCalls++; return unchecked((short)input[90]); }
        public void HidePlayer() => Flags |= 0x800;
        public void ShowPlayer() => Flags &= ~0x800;
        public void SetCameraAngle(short heading) => ai.Heading.Set(heading, 0, ai.Mode.Mode);
    }

    public static void Run(Action<bool, string> assert)
    {
        (int index, int frames) = RunSequences("camera-ai.json", "Original PC camera AI417CB0", assert, null);
        assert(index == 700 && frames > 4000, "The whole camera AI fixture covers every captured sequence.");

        var unknown = new CameraAi(new ScriptedCameraRig(), new CameraTripodState(), new CameraNormalPlacementState(), new CameraShakeState());
        unknown.Mode.Mode = 13;
        bool rejected = false;
        try { unknown.Update(2, new Host([], new int[91], unknown)); }
        catch (NotSupportedException) { rejected = true; }
        assert(rejected, "Camera mode13 has no CCamera AI routine and is rejected rather than approximated.");
    }

    internal static (int Cases, int Frames) RunSequences(string fixture, string label, Action<bool, string> assert,
        Func<ICameraAiSequenceExtension>? createExtension)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CameraAiTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures." + fixture)!);
        int index = 0, frames = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ICameraAiSequenceExtension? extension = createExtension?.Invoke();
            int[] input = Values(test.GetProperty("input"));
            int[][] hits = test.GetProperty("hits").EnumerateArray().Select(Values).ToArray();
            JsonElement expected = test.GetProperty("expected");
            object tripodIdentity = new(), targetIdentity = new(), bossIdentity = new();
            FixedVector3 tripodPosition = default, targetPosition = default, bossPosition = default;
            var tripodBody = new CameraFocusTarget(tripodIdentity, () => tripodPosition);
            var targetBody = new CameraFocusTarget(targetIdentity, () => targetPosition);
            var bossBody = new CameraFocusTarget(bossIdentity, () => bossPosition);

            var rig = new ScriptedCameraRig();
            var ai = new CameraAi(rig, new CameraTripodState(), new CameraNormalPlacementState(), new CameraShakeState());
            CameraModeState mode = ai.Mode;
            mode.Mode = (uint)input[0];
            mode.Position = Vector(input, 1);
            mode.CurrentRotation = Quaternion(input, 4);
            mode.DesiredRotation = Quaternion(input, 8);
            mode.DisplayRotation = default;
            ai.Tripod.TrackingEnabled = input[13] != 0;
            ai.Tripod.Position = Vector(input, 14);
            ai.Tripod.RemainingTicks = input[17];
            ai.Tripod.Target = Vector(input, 18);
            ai.Tripod.Rate = Vector(input, 21);
            (ai.Tripod.EaseX, ai.Tripod.EaseY, ai.Tripod.EaseZ) = (input[24], input[25], input[26]);
            ai.Focus.Target = input[27] switch { 1 => tripodBody, 2 => targetBody, _ => null };
            ai.Focus.TrackingEnabled = input[28] != 0;
            ai.Focus.Position = Vector(input, 29);
            ai.Focus.RemainingTicks = input[32];
            ai.Focus.MotionTarget = Vector(input, 33);
            ai.Focus.Rate = Vector(input, 36);
            rig.Zoom.Restore(new(input[39], input[42], input[40], input[41]));
            mode.SmoothedDistance = input[43];
            mode.DesiredDistance = input[44];
            ai.Normal.Pitch = (short)input[45];
            rig.Heading = (short)input[46];
            ai.Normal.Roll = (short)input[47];
            ai.Boss.SpinRate = input[48];
            ai.Boss.StationaryRadius = input[49];
            ai.Shake.Amplitude = new((short)input[50], (short)input[51], (short)input[52]);
            ai.Shake.Decay = new((byte)input[53], (byte)input[54], (byte)input[55]);
            ai.Shake.Speed = new((short)input[56], (short)input[57], (short)input[58]);
            for (int t = 0; t < 5; t++)
                rig.Parameter(TransitionOrder[t]).Restore(new(input[59 + 4 * t], input[60 + 4 * t], input[62 + 4 * t], input[61 + 4 * t]));
            rig.HeadingTransition.PublishedHeading = input[79];
            rig.HeadingTransition.Target = input[80];
            rig.HeadingTransition.Rate = input[81];
            rig.HeadingTransition.RemainingTicks = input[82];
            ai.Projection = new() { LookAngle = input[83], Distance = input[84] };
            var host = new Host(hits, input, ai) { Tripod = input[12] != 0 ? tripodBody : null };
            int diagnostics = 0;
            ai.Diagnostic = message => { if (message == "No valid tripod") diagnostics++; else throw new InvalidOperationException(message); };

            int frameIndex = 0;
            foreach (JsonElement frameElement in test.GetProperty("frames").EnumerateArray())
            {
                int[] frame = Values(frameElement);
                int elapsed = frame[0];
                tripodPosition = Vector(frame, 1);
                targetPosition = Vector(frame, 4);
                bossPosition = Vector(frame, 7);
                host.Boss = frame[10] != 0 ? bossBody : null;
                host.TotalTime = unchecked((uint)frame[11]);
                if (extension is null || !extension.Execute(frame[12], ai, bossBody, boss => host.Boss = boss))
                    Command(ai, frame[12], frame.AsSpan(13, 8).ToArray(), tripodPosition);
                ai.Update(elapsed, host);
                CameraFocusTarget? owner = ai.Focus.Target;
                int ownerCode = owner is null ? 0 : ReferenceEquals(owner, tripodBody) ? 1 : ReferenceEquals(owner, targetBody) ? 2 :
                    ReferenceEquals(owner, bossBody) ? 3 : -1;
                List<int> actual = [(int)mode.Mode, .. Ints(mode.Position), .. Ints(mode.Velocity), .. Ints(ai.Tripod.Position),
                    .. Ints(mode.FocusPosition), .. Ints(mode.CurrentRotation), .. Ints(mode.DesiredRotation), .. Ints(mode.DisplayRotation),
                    mode.Angles.Pitch, mode.Angles.Yaw, rig.Heading, mode.ViewHeading, rig.Zoom.Value, rig.Zoom.RemainingTicks,
                    ai.Projection.LookAngle, ai.Projection.Distance, .. Ints(ai.Offset), mode.SmoothedDistance, mode.DesiredDistance,
                    mode.CollisionFlag, .. Ints(mode.CollisionPosition), host.Flags & 0xFFFF, ownerCode, ai.Tripod.RemainingTicks,
                    ai.Focus.RemainingTicks, rig.FixedPosition.RemainingTicks, ai.Shake.Amplitude.X, ai.Shake.Amplitude.Y, ai.Shake.Amplitude.Z];
                foreach (CameraParameter parameter in TransitionOrder)
                    actual.AddRange([rig.Parameter(parameter).Value, rig.Parameter(parameter).RemainingTicks]);
                actual.AddRange([rig.HeadingTransition.PublishedHeading, rig.HeadingTransition.RemainingTicks, diagnostics, host.HeadingCalls]);
                if (extension is not null) actual.AddRange(extension.Outputs(ai, host.Boss));
                int[] wanted = Values(expected[frameIndex]);
                assert(actual.SequenceEqual(wanted),
                    $"{label} case{index} frame{frameIndex}: actual[{string.Join(',', actual)}] expected[{string.Join(',', wanted)}].");
                frameIndex++; frames++;
            }
            int[] rays = Values(expected[frameIndex]);
            assert(host.Events.SequenceEqual(rays),
                $"{label} case{index}: rays[{string.Join(',', host.Events)}] expected[{string.Join(',', rays)}].");
            index++;
        }
        return (index, frames);
    }

    /// <summary>Every authored160 mode and188 fixed focus must reach a ported CCamera AI routine.</summary>
    public static void VerifyOriginalScript(string path, TriggerFile file, TriggerNode node, TriggerInstruction instruction, Action<bool, string> assert)
    {
        var rig = new ScriptedCameraRig();
        ushort first = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
        if (instruction.Opcode == (ushort)MissionOpcode.SetCameraMode)
        {
            bool supported = true;
            try { new CameraCommandHandler(160, rig, () => default).Execute(new(node, 0), instruction); }
            catch (NotSupportedException) { supported = false; }
            assert(supported && rig.Mode == first, $"{path} node{node.Id}: authored camera mode{first} has a ported CCamera AI routine.");
            return;
        }
        new FixedCameraCommandHandler(file, rig, () => default, MissionOpcode.SetFixedCameraFocus).Execute(new(node, 0), instruction);
        ushort id = MissionRuntime.ReadLinks(node).FirstOrDefault(id => file.Nodes[id].TypeCode == 12, ushort.MaxValue);
        if (id == ushort.MaxValue) { assert(rig.Mode == 3, $"{path} node{node.Id}: unlinked188 leaves the camera unchanged."); return; }
        TriggerPosition source = TriggerPlacement.Read(file.Nodes[id]).Position;
        ushort duration = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span[2..]);
        assert(rig.Mode == 6 && rig.FixedFocus.Target == new FixedVector3(source.X, source.Y, source.Z) << 12 &&
            rig.FixedFocus.Distance == unchecked((short)(first / 3)) && rig.FixedPosition.RemainingTicks == duration,
            $"{path} node{node.Id}: original188 resolves its first linked camera node into CM_FixedFocus.");
    }

    private static void Command(CameraAi ai, int kind, int[] a, FixedVector3 tripodBody)
    {
        ScriptedCameraRig rig = ai.Rig;
        CameraModeState mode = ai.Mode;
        var vector = new FixedVector3(a[0], a[1], a[2]);
        ushort frames = unchecked((ushort)a[7]);
        switch (kind)
        {
            case None: break;
            case SetMode: rig.SetMode(frames); break;
            case FixedPos: rig.SetFixedPosition(mode.Position, vector, frames); break;
            case FixedPosAngles:
                rig.SetFixedPositionAndRotation(mode.Position, vector, mode.CurrentRotation, new(a[3], a[4], a[5], a[6]), frames);
                break;
            case FixedFocus: rig.SetFixedFocus(vector, unchecked((ushort)a[3]), frames); break;
            case CamAngle: ai.Heading.Set(unchecked((short)a[3]), frames, mode.Mode); break;
            case Shake: ai.Shake.Shake(vector, a[7]); break;
            case TripodMotion: ai.Tripod.SetMotion(vector, unchecked((uint)a[7]), tripodBody, false); break;
            case XzDistance: rig.SetHorizontalDistance(unchecked((ushort)a[3]), frames); break;
            case YDistance: rig.SetVerticalDistance(unchecked((short)a[3]), frames); break;
            default:
                if (mode.Mode is 4 or 5) break;
                rig.Parameter((CameraParameter)(kind - 10)).Set(a[3], frames);
                break;
        }
    }

    private static FixedVector3 Vector(int[] values, int start) => new(values[start], values[start + 1], values[start + 2]);
    private static FixedQuaternion Quaternion(int[] values, int start) => new(values[start], values[start + 1], values[start + 2], values[start + 3]);
    private static int[] Ints(FixedVector3 value) => [value.X, value.Y, value.Z];
    private static int[] Ints(FixedQuaternion value) => [value.X, value.Y, value.Z, value.W];
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
