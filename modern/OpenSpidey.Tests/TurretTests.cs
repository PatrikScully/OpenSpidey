using System.Text.Json;
using OpenSpidey.Core.Actors.Turret;

internal static class TurretTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(TurretTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.turret.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            TurretState state = new(input[0], input[1], 0);
            TurretBrain.Construct(state, () => { });
            int[] actual =
            [
                TurretState.Type, state.Health, state.MinorRadius, (int)state.CombatFlags, state.Field20C,
                (int)(state.BodyFlags & 0x214)
            ];
            assert(actual[0] == expected[0] && state.Health == 10 && state.MinorRadius == 0x80 &&
                (state.CombatFlags & 0x10200) != 0 && state.Field20C == 1,
                $"Actual PC CTurret constructor case {index}: type={actual[0]} health={state.Health} combat=0x{state.CombatFlags:X}.");
            index++; counts++;
        }
        assert(counts >= 4, "Turret fixture keeps L8A2/L5A4 constructor smoke cases.");
        assert(TurretState.ModuleName == "turret" && TurretState.Type == 325,
            "Source module name and type325 stay tied to the relocatable turret factory.");
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
}
