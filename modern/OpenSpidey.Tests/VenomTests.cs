using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Mathematics;

internal static class VenomTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VenomTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.venom.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, count = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            if (mode == "C") Construct(assert, index, input, expected);
            else Wait(assert, index, input, expected);
            index++; count++;
        }
        assert(count == 29, "Venom fixture keeps 24 recovered constructor and 5 original wait cases.");
        assert(VenomState.ModuleName == "venom" && VenomState.Type == 313, "Source module name stays tied to type313.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        VenomState state = new(input[1], input[0], input[2], 0);
        Host host = new();
        VenomBrain.Construct(state, host, () => { });
        string model = VenomState.ModelNameFor(input[0]);
        int[] actual = [VenomState.Type, state.MinorRadius, state.LevelId, state.AiState, state.Substate, state.Health,
            state.Field39C, unchecked((int)state.Field218), model == "venom2" ? 1 : 0, state.SourceNode];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CVenom constructor case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Wait(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        VenomState state = new(1, 0x501, 2, 0) { AiState = input[1], Substate = input[2], Field3B0 = 11, Field3B4 = 22 };
        if (input[0] != 0) VenomWaitState.Enter(state);
        else VenomWaitState.Exit(state, unchecked((ushort)input[3]), unchecked((ushort)input[4]));
        int[] actual = [state.AiState, state.Substate, state.Field3B0, state.Field3B4];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CVenom wait case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class Host : IVenomHost
    {
        public ActorBodyMotion Body { get; set; } = new();
        public int LevelId => 0x501;
        public int ElapsedTicks { get; set; } = 1;
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public FixedVector3 PlayerPosition { get; set; } = new(4096, 0, 4096);
        public FixedVector3 PrimaryAimTarget { get; set; } = new(8192, 0, 8192);
        public FixedVector3 SecondaryAimTarget { get; set; } = new(8192, 0, 8192);
        public FixedVector3? MechListPosition => null;
        public void RunAnimation(int animation) => AnimationId = animation;
    }
}
