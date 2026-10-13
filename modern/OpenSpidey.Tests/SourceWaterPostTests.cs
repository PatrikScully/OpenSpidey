using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;

internal static class SourceWaterPostTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SourceWaterPostTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.water-post.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = row.GetProperty("expected");
            var water = new PlayerWaterEffectState { Value = (int)input[0] };
            if (row.GetProperty("kind").GetString() == "prepare")
            {
                var host = new SkyHost((uint)input[1], (uint)input[2]);
                SourceWaterPostEffects.PreparePlayer(water, host);
                assert(new long[] { water.Value, host.Current, host.Target }.SequenceEqual(expected.EnumerateArray().Take(3).Select(value => value.GetInt64())) &&
                    host.Events.SequenceEqual(expected[3].EnumerateArray().Select(value => value.GetInt64())),
                    $"Original player water preparation{index++}: only value1 restores actual sky target before clearing the process flag.");
                continue;
            }
            var state = new SourceWaterPostState(water) { PauseValue = (int)input[1], Timer = (int)input[3], WordCountdown = (ushort)input[4] };
            SourceWaterPostEffects.AdvanceRender(state, (uint)input[2], (int)input[5], (int)input[6], (uint)input[7], (uint)input[8]);
            long[] actual = [water.Value, state.PauseValue, state.Timer, state.WordCountdown];
            var events = new List<long>();
            if (state.Quad is { } quad) events.AddRange([1, 1, 1, 2, 0, 0, Bits(quad.Width), Bits(quad.Height),
                0, 0, Bits(1), Bits(1), quad.Argb, Bits(SourceWaterQuad.SourceSlot), 0]);
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Whole original water post{index}: pause and water gates, exact48*clock wrap, retained timer and per-render ushort countdown.");
            long[] expectedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(events.SequenceEqual(expectedEvents),
                $"Whole original water post{index++}: genuine source initialized trig, unsigned resolution float conversions, direct ARGB, full quad/UV and source-slot6. Input=[{string.Join(',', input)}], actual=[{string.Join(',', events)}], expected=[{string.Join(',', expectedEvents)}].");
        }
        var sense = new PlayerAdditionalEffectState { SenseTicks = 123, SenseKind = 456, SenseSoundHandle = 77, SenseBody = new(new object(), 99) };
        var context = new MissionCommandContext(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0);
        var handler = new PlayerSenseBuzzCommandHandler(() => sense);
        foreach (ushort kind in new ushort[] { 0, 1, 32768, 65535 })
            foreach (ushort duration in new ushort[] { 0, 1, 2, 65535 })
            {
                byte[] arguments = new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(arguments, kind);
                BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(2), duration);
                handler.Execute(context, new(196, 0, arguments));
                assert(sense.SenseKind == kind && sense.SenseTicks == duration && sense.SenseSoundHandle == 77 && sense.SenseBody.Validation == 99,
                    "Source196 writes only zero-extended kind/duration words and leaves real sound/body ownership for the preparation phase.");
            }
        byte[] list = [4, 0, 196, 0, 1, 0, 255, 255, 196, 0, 0, 128, 2, 0, 255, 255];
        var mission = new MissionRuntime(new([new(0, 4, 0, list)]), new MissionRuntimeTests.ActorFactory(), [handler]);
        mission.QueueAutoexec();
        assert(mission.ExecutePending() == 1 && sense.SenseKind == 32768 && sense.SenseTicks == 2,
            "The genuine source command reader consumes196's two words, even FFFF duration, without misreading the following196/end opcodes.");
        byte[] brightness = Enumerable.Range(0, 256).Select(value => (byte)Math.Min(255, value * 2)).ToArray();
        assert(SourceWaterPostEffects.ApplyBrightness(0x30804032, brightness) == 0x30FF8064,
            "Direct2D PC water brightness preserves alpha48 and doubles/saturates RGB without3D half/bias or BGR swapping.");
    }
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private sealed class SkyHost(uint current, uint target) : IPlayerWaterEffectFrameHost
    {
        public uint Current { get; private set; } = current;
        public uint Target => target;
        public List<long> Events { get; } = [];
        public void RestoreSkyColourTarget() { Current = Target; Events.AddRange([3, Current]); }
    }
}
