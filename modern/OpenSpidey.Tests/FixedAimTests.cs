using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Mathematics;

internal static class FixedAimTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenSpidey.Tests.Fixtures.aim-vectors.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            int[] values = fixture.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var from = new FixedVector3(values[0], values[1], values[2]);
            var to = new FixedVector3(values[3], values[4], values[5]);
            AimAngles aim = FixedAim.Calculate(from, to);
            FixedVector3 velocity = FixedAim.Velocity(values[6], aim);
            int[] actual = [aim.Pitch, aim.Yaw, velocity.X, velocity.Y, velocity.Z, FixedAim.Distance(from, to)];
            int[] expected = fixture.GetProperty("result").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            assert(actual.SequenceEqual(expected), $"Recovered aiming, velocity and distance match compiled reference case {index++}.");
        }
    }
}
