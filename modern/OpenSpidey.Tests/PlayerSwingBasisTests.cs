using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSwingBasisTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSwingBasisTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-swing-basis.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            FixedVector3 Vector(int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
            SourceMatrix previous = default(SourceMatrix) with { Reserved = 0xBEEF, Translation = new(123456789, -987654321, 213456789) };
            PlayerSwingBasisResult result = PlayerSwingBasis.Calculate(Vector(0), Vector(3), Vector(6), previous);
            SourceMatrix m = result.Matrix;
            long[] actual = [m.M00, m.M01, m.M02, m.M10, m.M11, m.M12, m.M20, m.M21, m.M22, m.Reserved,
                m.Translation.X, m.Translation.Y, m.Translation.Z, result.VerticalAlignment];
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64())),
                $"Actual PC swing basis {index++}: full signed surface normal, two normalized Q12 cross products, column ownership and untouched MATRIX tail.");
        }
    }
}
