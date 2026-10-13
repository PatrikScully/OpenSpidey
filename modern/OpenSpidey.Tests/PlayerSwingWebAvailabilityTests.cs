using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerSwingWebAvailabilityTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSwingWebAvailabilityTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-swing-availability.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var state = new PlayerSwingWebAvailabilityState { TargetPosition = new(11, -12, 13), PlaneNormal = new(21, -22, 23),
                NearCentre = new(31, -32, 33), FarCentre = new(41, -42, 43), FarFromWall = 77 };
            bool result = PlayerSwingWebAvailability.Check(state, new(new(input[5], input[6], input[7]), new(input[8], input[9], input[10]),
                (short)input[0], input[1], (uint)input[2], (uint)input[3], input[4] != 0));
            int[] actual = [result ? 1 : 0, state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z,
                state.PlaneNormal.X, state.PlaneNormal.Y, state.PlaneNormal.Z, state.NearCentre.X, state.NearCentre.Y, state.NearCentre.Z,
                state.FarCentre.X, state.FarCentre.Y, state.FarCentre.Z, state.FarFromWall];
            int[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            assert(actual.SequenceEqual(expected), $"Actual PC swing-availability[{string.Join(',', input)}]: strict normal/distance/face/action gates, partial target, genuine ground580/forward320 adjustment, vertical2896 and near/far geometry; actual[{string.Join(',', actual)}], expected[{string.Join(',', expected)}].");
        }
        var degenerate = new PlayerSwingWebAvailabilityState();
        assert(!PlayerSwingWebAvailability.Check(degenerate, new(default, default, -32768, 513, 0, 2, false)),
            "Coincident modern swing target is safely rejected while preserving its real partial target write instead of the original zero-length division crash.");
    }
}
