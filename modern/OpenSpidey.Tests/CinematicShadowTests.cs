using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Mathematics;

internal static class CinematicShadowTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CinematicShadowTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.cinematic-shadows.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            FixedVector3 position = new(input[1], input[2], input[3]);
            FixedJointTransform matrix = new((short)input[4], (short)input[5], (short)input[6],
                (short)input[7], (short)input[8], (short)input[9], (short)input[10], (short)input[11], (short)input[12], 0, 0, 0);
            FixedVector3[] hooks = Enumerable.Range(0, 4).Select(part => new FixedVector3(input[13 + part * 3], input[14 + part * 3], input[15 + part * 3])).ToArray();
            FixedVector3[] corners = CinematicActorPose.ShadowCorners(position, matrix, (short)input[0], hooks);
            FixedVector3[] reference = expected[4].EnumerateArray().Select(row => new FixedVector3(row[0].GetInt32(), row[1].GetInt32(), row[2].GetInt32())).ToArray();
            assert(corners.SequenceEqual(reference) && expected[0].GetInt32() == 4 && expected[1].GetInt32() == 99 &&
                expected[2].GetUInt32() == 0x340 && expected[3].GetUInt32() == 0x404040,
                $"Actual PC cinematic shadow case {index}: limb footprint minima, transposed/local/body transforms, signed groundY, four source corners and protected existing-quad transparency stores.");
            index++;
        }
    }
}
