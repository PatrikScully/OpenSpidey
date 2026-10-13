using System.Text.Json;
using OpenSpidey.Core.UI;

internal static class ScreenTargetTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScreenTargetTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.screen-target.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var state = new ScreenTargetState { Visible = input[0] != 0 };
            state.SetTarget(new(123456, -234567, 345678), (ushort)input[3], (short)input[4]);
            long[] actual = [state.Visible ? 1 : 0, state.Position.X, state.Position.Y, state.Position.Z, state.Radius, state.Angle];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(6).Select(value => value.GetInt64())), "Actual PC Screen_TargetOn/SetTarget preserves independent visible flag, full fixed world position and unsigned-radius/signed-angle fields.");
            long[] geometry = state.Build((short)input[1], (short)input[2]).SelectMany(triangle => new long[]
                { triangle.First.X, triangle.First.Y, triangle.Second.X, triangle.Second.Y, triangle.Third.X, triangle.Third.Y, triangle.Argb }).ToArray();
            assert(geometry.SequenceEqual(expected[6].EnumerateArray().Select(value => value.GetInt64())),
                $"Actual PC screen-target[{string.Join(',', input)}]: original all4 triangle corners, short wrap, fixed sine/cosine offsets, signed320/512 ordinate division and packedARGB; actual[{string.Join(',', geometry)}], expected[{string.Join(',', expected[6].EnumerateArray().Select(value => value.GetInt64()))}].");
        }
    }
}
