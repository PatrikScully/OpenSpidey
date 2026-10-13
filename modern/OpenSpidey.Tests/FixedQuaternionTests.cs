using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

internal static class FixedQuaternionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(FixedQuaternionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.quaternions.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            FixedQuaternion start = Quaternion(test.GetProperty("start"));
            FixedQuaternion target = Quaternion(test.GetProperty("target"));
            JsonElement expected = test.GetProperty("expected");
            FixedJointTransform matrix = start.ToMatrix();
            assert(matrix == new FixedJointTransform(Short(8), Short(9), Short(10), Short(11), Short(12), Short(13),
                Short(14), Short(15), Short(16), 0, 0, 0), "Q12 quaternion matrix terms match the compiled recovered conversion.");
            assert(FixedQuaternion.FromMatrix(matrix) == Quaternion(expected, 17),
                "Quaternion reconstruction matches all recovered matrix trace/axis branches.");
            FixedQuaternion result = FixedQuaternion.Slerp(ref start, target, test.GetProperty("fraction").GetInt32());
            assert(start == Quaternion(expected) && result == Quaternion(expected, 4),
                "Quaternion interpolation preserves source sign mutation, sine-table weights and integer rounding.");
            short Short(int index) => (short)expected[index].GetInt32();
        }
    }

    private static FixedQuaternion Quaternion(JsonElement value, int offset = 0) => new(value[offset].GetInt32(),
        value[offset + 1].GetInt32(), value[offset + 2].GetInt32(), value[offset + 3].GetInt32());
}
