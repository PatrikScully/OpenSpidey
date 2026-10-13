using System.Text.Json;
using OpenSpidey.Core.Mathematics;

internal static class VectorNormalTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VectorNormalTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.vector-normal.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input");
            FixedVector3 actual = FixedVectorMath.Normalize(new(input[0].GetInt32(), input[1].GetInt32(), input[2].GetInt32()));
            JsonElement expected = test.GetProperty("expected");
            assert(actual == new FixedVector3(expected[0].GetInt32(), expected[1].GetInt32(), expected[2].GetInt32()),
                $"VectorNormal matches original PC precision for {input}.");
        }
    }
}
