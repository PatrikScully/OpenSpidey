using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Mathematics;

internal static class SwitchTests
{
    private sealed class Host(JsonElement input) : ISwitchHost
    {
        public SwitchEnvironmentItem Off { get; } = new(new object(), 0x12345678, new(100, 200, 300))
        { ItemFlags = input.GetProperty("offFlags").GetUInt16(), Color = input.GetProperty("offRgb").GetUInt32() };
        public SwitchEnvironmentItem On { get; } = new(new object(), 0x89ABCDEF, new(400, 500, 600))
        { ItemFlags = input.GetProperty("onFlags").GetUInt16(), Color = input.GetProperty("onRgb").GetUInt32() };
        public bool HasPlayer => input.GetProperty("player").GetInt32() != 0;
        public int PlayerCounter { get; set; } = input.GetProperty("counter").GetInt32();
        public List<int> Events { get; } = [];
        public IEnumerable<SwitchPulseNode> SourceTriggerOrder => input.GetProperty("nodes").EnumerateArray().Select((node, index) =>
            new SwitchPulseNode((ushort)index, node[0].GetUInt16(), new(node[1].GetInt32(), node[2].GetInt32(), node[3].GetInt32())));
        public void AttachToControlList(SwitchState state) => Events.Add(7);
        public SwitchEnvironmentItem? FindEnvironmentItem(uint checksum)
        {
            Events.AddRange([5, unchecked((int)checksum)]);
            return checksum == Off.ModelChecksum ? Off : checksum == On.ModelChecksum ? On : null;
        }
        public void MarkModelSwitchFaces(SwitchEnvironmentItem item) => Events.AddRange([6, ReferenceEquals(item, Off) ? 1 : 2]);
        public void SendPulseToNode(ushort node) => Events.AddRange([2, node]);
        public void SendAttachedPulse(ushort sourceNode) => Events.AddRange([1, sourceNode]);
        public void ReportInvalidState(int mode) => Events.Add(4);
        public void ReportMissingPulseNode(int selector) => Events.Add(4);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SwitchTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.switch.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input");
            var host = new Host(input);
            var state = new SwitchState(23) { Mode = (SourceSwitchMode)input.GetProperty("mode").GetInt32(),
                ElapsedOnTicks = input.GetProperty("timer").GetUInt32(), Timeout = input.GetProperty("timeout").GetUInt16(),
                InputFlags = input.GetProperty("signal").GetUInt16(), BodyFlags = input.GetProperty("flags").GetUInt16(),
                SubtractPlayerCounter = input.GetProperty("subtract").GetByte(), OffItem = host.Off, OnItem = host.On,
                OffAimPosition = host.Off.Position, OnAimPosition = host.On.Position };
            if (input.GetProperty("kind").GetInt32() == 1)
            {
                state = new SwitchState(23);
                byte[] parameters = new byte[14];
                BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(2), host.Off.ModelChecksum);
                BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(6), host.On.ModelChecksum);
                BinaryPrimitives.WriteInt16LittleEndian(parameters.AsSpan(10), (short)input.GetProperty("initial").GetInt32());
                BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(12), input.GetProperty("timeout").GetUInt16());
                var definition = new TriggerActorDefinition(23, 1, 407, [2, 3], [1, 2], new(11, -13, 17), new(19, 23, -29), 0, parameters);
                SwitchBehavior.Construct(state, definition, 0, host);
                Check(test.GetProperty("expected"), 0);
                assert(state.Position == new FixedVector3(11, -13, 17) << 12 && state.Angles.X == 19 && state.Angles.Y == 23 && state.Angles.Z == -29 &&
                    ReferenceEquals(state.OffItem, host.Off) && ReferenceEquals(state.OnItem, host.On),
                    "Switch constructor consumes aligned checksum operands after actual source position/angles, attaches Control first and owns original ENV references.");
            }
            else
            {
                int frame = 0;
                foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
                {
                    JsonElement step = input.GetProperty("steps")[frame]; host.Events.Clear();
                    state.InputFlags |= step[2].GetUInt16();
                    if (step[0].GetInt32() == 0) SwitchBehavior.Advance(state, step[1].GetInt32(), host);
                    else SwitchBehavior.Flick(state, host);
                    host.Off.Position += new FixedVector3(17, -31, 43);
                    host.On.Position += new FixedVector3(-47, -59, 61);
                    Check(expected, frame); frame++;
                }
            }
            assert(SwitchBehavior.Hit() && ReferenceEquals(SwitchBehavior.FindOwner(host.Off.Identity, [state]), state) &&
                ReferenceEquals(SwitchBehavior.FindOwner(host.On.Identity, [state]), state) && SwitchBehavior.FindOwner(new object(), [state]) is null,
                "Source CBody Hit accepts without switch transition; both exact ENV identities recover the owning switch.");
            index++;
            void Check(JsonElement expected, int frame)
            {
                object? aim = state.GetAutoAimPoint()?.Identity;
                int point = ReferenceEquals(aim, state.OffAimIdentity) ? 1 : ReferenceEquals(aim, state.OnAimIdentity) ? 2 : 0;
                long[] actual = [(int)state.Mode, state.ElapsedOnTicks, state.Timeout, state.InputFlags, state.BodyFlags, 0,
                    state.SubtractPlayerCounter, host.PlayerCounter, host.Off.ItemFlags, host.Off.Color, host.On.ItemFlags, host.On.Color, point];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(13).Select(value => value.GetInt64())) &&
                    host.Events.SequenceEqual(expected[13].EnumerateArray().Select(value => value.GetInt32())),
                    $"Switch{index}:{frame} matches source/PC input/flick priority, strict/wrapped timers, actual ENV RGB ramp/fade, own zombie flag, repeated fading flick counters, LFA1 coordinate-coded first node pulses, owner aims and visibility flags.");
                FixedVector3 aimPosition = state.GetAutoAimPoint()?.Position ?? default;
                assert(aimPosition == Vector(expected[14]) && host.Off.Position == Vector(expected[15]) && host.On.Position == Vector(expected[16]),
                    $"Switch{index}:{frame} matches original PC4D19E0: moving the actual ENV CItem records preserves the switch's copied constructor aim points.");
            }
        }
    }
    private static FixedVector3 Vector(JsonElement vector) => new(vector[0].GetInt32(), vector[1].GetInt32(), vector[2].GetInt32());
}
