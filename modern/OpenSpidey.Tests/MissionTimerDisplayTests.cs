using System.Text.Json;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.UI;

internal static class MissionTimerDisplayTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionTimerDisplayTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-timer-display.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var countdown = new MissionCountdown { DisplayEnabled = (byte)input[4], RemainingTicks = (uint)input[5] };
            var state = new MissionTimerDisplayState { FirstScanActive = (byte)input[6], SecondScanActive = (byte)input[7],
                FirstScanPosition = (int)input[8], SecondScanPosition = (int)input[9], LastSeconds = (int)input[10] };
            var host = new Host(input);
            MissionTimerDisplay.Display(countdown, state, (int)input[11], host);
            long[] actual = [state.FirstScanActive, state.FirstScanPosition, state.SecondScanActive, state.SecondScanPosition, state.LastSeconds];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC timer[{string.Join(',', input)}] persistent scan animation and unmodified sixty-tick countdown; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC timer[{string.Join(',', input)}] complete random/module/audio/font order, unsigned minute/second bytes, real stretched mirrored border and every scan draw; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
            assert(countdown.RemainingTicks == (uint)input[5], "Panel timer display must leave shared countdown decay to its owning Panel_Display phase.");
        }
    }

    private sealed class Host(long[] input) : IMissionTimerDisplayHost
    {
        public List<long> Events { get; } = [];
        public int LevelId => (int)input[0];
        public bool PlayerPresent => input[1] != 0;
        public int PlayerScriptGate => (int)input[2];
        public bool PlayerSynthesizedInput => input[3] != 0;
        public bool BorderTexturePresent => input[12] != 0;
        public int Random(int range) { int value = (int)input[range == 20 ? 13 : 14]; Events.AddRange([1, range, value]); return value; }
        public void CallModule(string module, uint function) => Events.AddRange([2, module switch { "l2a1lsc" => 0, "venom" => 1, "l5a5lsc" => 2, "superock" => 3, _ => throw new InvalidDataException(module) }, function]);
        public void PlaySound(ushort sound, short volume, int pitch) => Events.AddRange([3, sound, volume, pitch]);
        public void DrawBorder(int x, int y, int width, int height, bool mirrored)
        {
            int scaled = unchecked(width * 6554); if ((scaled & 4095) >= 2048) scaled = unchecked(scaled + 4096); scaled >>= 12;
            short left = unchecked((short)x), top = unchecked((short)y), right = unchecked((short)(x + scaled)), bottom = unchecked((short)(y + height));
            Events.AddRange([4, 7, 1, 5, left, top, right, top, left, bottom, right, bottom, 0xFF808080,
                mirrored ? 1 : 0, 0, mirrored ? 0 : 1, 0, mirrored ? 1 : 0, 1, mirrored ? 0 : 1, 1, 6000]);
        }
        public void SetTextScale(int scale) => Events.AddRange([6, scale]);
        public void SetTextJustification(byte justification) => Events.AddRange([7, justification]);
        public void SetTextColour(byte red, byte green, byte blue, int argument) => Events.AddRange([8, red, green, blue, argument]);
        public string GetCurrentFontName() { Events.Add(9); return "sp_fnt00.fnt"; }
        public void SetCurrentFont(string name) => Events.AddRange([10, name == "sp_fnt01.fnt" ? 1 : 0]);
        public void DrawText(int x, int y, ReadOnlyMemory<byte> text, int argument, uint depth)
        { Events.AddRange([11, x, y]); foreach (byte character in text.Span) Events.Add(character); Events.AddRange([argument, depth]); }
        public void DrawFlat(float depth, int x, int y, int width, int height, byte red, byte green, byte blue, int argument, int blend) =>
            Events.AddRange([12, (int)Math.Round(depth * 10000), x, y, width, height, red, green, blue, argument, blend]);
    }
}
