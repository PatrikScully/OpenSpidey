using System.Text.Json;
using OpenSpidey.Core.Gameplay;

internal static class ScorpionApproachProgressTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ScorpionApproachProgressTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.scorpion-progress.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = Read(row.GetProperty("input"));
            var countdown = new MissionCountdown { RemainingTicks = (uint)input[1] };
            var state = new ScorpionApproachState();
            for (int warning = 0; warning < 5; warning++)
            { state.PrimaryWarnings[warning] = (byte)input[8 + warning]; state.SecondaryWarnings[warning] = (byte)input[13 + warning]; }
            var host = new Host(countdown, state, input);
            ScorpionApproachProgress.Display(countdown, state, host);
            JsonElement expected = row.GetProperty("expected");
            long[] actual = [countdown.RemainingTicks, host.AlternateStory ? 1 : 0, host.SpeechAllowed ? 1 : 0,
                .. state.PrimaryWarnings.Select(value => (long)value), .. state.SecondaryWarnings.Select(value => (long)value), host.TextureMask, host.Allocations];
            long[] wanted = [expected[0].GetInt64(), expected[1].GetInt64(), expected[2].GetInt64(),
                .. Read(expected[3]), expected[4].GetInt64(), expected[5].GetInt64()];
            assert(actual.SequenceEqual(wanted), $"Whole original Scorpion progress{index} flags/texture cache/panel allocation: actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] expectedEvents = Read(expected[6]);
            assert(host.Events.SequenceEqual(expectedEvents), $"Whole original Scorpion progress{index} exact speech-before-draw events and all original quads: actual[{string.Join(',', host.Events)}], expected[{string.Join(',', expectedEvents)}].");
            index++;
        }
        var missingPortrait = new Host(new() { RemainingTicks = 0 }, new(), [0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 0, 0]);
        bool rejected = false;
        try { ScorpionApproachProgress.Display(missingPortrait.Countdown, missingPortrait.State, missingPortrait); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected && missingPortrait.Allocations == 0,
            "An absent source Scorpion poly cannot be replaced by a fabricated fixed Jonah rectangle during the final overlapping portrait path.");
    }

    private static long[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class Host : IScorpionApproachProgressHost
    {
        private readonly long[] input;
        private int speechCalls;
        public MissionCountdown Countdown { get; }
        public ScorpionApproachState State { get; }
        public Host(MissionCountdown countdown, ScorpionApproachState state, long[] input)
        { Countdown = countdown; State = state; this.input = (long[])input.Clone(); TextureMask = (uint)input[19]; }
        public List<long> Events { get; } = [];
        public int Allocations { get; private set; }
        public uint TextureMask { get; private set; }
        public int Difficulty => (int)input[0];
        public int ScreenOffsetY => (int)input[2];
        public bool AlternateStory => input[3] != 0;
        public bool SpeechAllowed => input[4] != 0;
        public bool SpeechPlaying => input[5] != 0;
        public int SpeechBank => (int)input[6];
        public int SpeechSlot => (int)input[7];
        public bool TexturePresent(int index) => (TextureMask & (1u << index)) != 0;
        public void FindTexture(int index, string name)
        {
            if (name != ScorpionApproachProgress.TextureNames[index]) throw new InvalidOperationException("Wrong source texture lookup name.");
            Events.AddRange([1, index]); TextureMask |= (uint)input[18] & (1u << index);
        }
        public void PlaySpeech(int bank, int slot, int priority)
        {
            Events.AddRange([2, bank, slot, priority, .. State.PrimaryWarnings.Select(value => (long)value), .. State.SecondaryWarnings.Select(value => (long)value)]);
            if (speechCalls++ != 0) return;
            if (input[20] == 1) Countdown.RemainingTicks = 0;
            else if (input[20] == 2) { Countdown.RemainingTicks = uint.MaxValue; input[4] = 0; }
            else if (input[20] == 3) input[3] = input[4] = 1;
        }
        public ScorpionProgressPiece CreatePiece(int texture, int x, int y, int width, int height)
        { Allocations++; return ScorpionApproachProgress.CreatePanelPiece(x, y, width, height); }
        public void DrawPiece(int texture, ScorpionProgressPiece piece, float depth)
        {
            var quad = piece.Quad;
            Events.AddRange([3, texture, 1, 4, texture, quad.Left, quad.Top, quad.Right, quad.Bottom, piece.Colour, (int)depth]);
        }
    }
}
