using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;

internal static class JonahFootprintGeometryTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(JonahFootprintGeometryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.jonah-shadows.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var rotation = new FixedJointTransform((short)input[6],(short)input[7],(short)input[8],
                (short)input[9],(short)input[10],(short)input[11],(short)input[12],(short)input[13],(short)input[14],0,0,0);
            FixedVector3[] hooks = Enumerable.Range(0,4).Select(i => new FixedVector3(input[15+i*3],input[16+i*3],input[17+i*3])).ToArray();
            FixedVector3[] corners = JonahFootprintGeometry.FromWorldHooks(new(input[3],input[4],input[5]), rotation, (short)input[2], hooks);
            FixedVector3[] source = expected[8].EnumerateArray().Select(v => new FixedVector3(v[0].GetInt32(),v[1].GetInt32(),v[2].GetInt32())).ToArray();
            assert(corners.SequenceEqual(source), $"Whole original Jonah DoMGSShadow case {index++} retains four genuine hooks, inverse/forward GTE transforms, asymmetric minimum bounds, signed ground placement, source corner order and32-bit wrapping.");
            bool fresh = input[0] != 0;
            assert(expected[0].GetInt32() == 4*input[1] && expected[1].GetInt16() == (fresh ? 32 : 99) &&
                expected[2].GetUInt32() == 0x1C0341 && expected[3].GetUInt32() == 0x404040 &&
                expected[4].GetInt32() == (fresh ? 1 : 0) && expected[5].GetInt32() == (fresh ? 1 : 0) &&
                expected[6].GetInt32() == (fresh ? 1 : 0) && expected[7].GetInt32() == (fresh ? 1 : 0),
                "Repeated actual PC shadow visits allocate/attach one source CQuadBit, resolve the real Shadow2 override once, retain existing depth bias, and refresh exact transparency/subtractive/corner stores.");
        }
        var pose = new FixedJointTransform[16];
        Array.Fill(pose, new(4096,0,0,0,4096,0,0,0,4096,0,0,0));
        var position = new FixedVector3(12345,-67890,34567);
        FixedVector3[] baseline = JonahFootprintGeometry.FromPose(position,position>>12,pose[0],160,pose);
        foreach (int part in Enumerable.Range(0,16))
        {
            FixedJointTransform saved = pose[part];
            pose[part] = saved with { X = 32767, Z = -32768 };
            bool changes = !baseline.SequenceEqual(JonahFootprintGeometry.FromPose(position,position>>12,pose[0],160,pose));
            assert(changes == (part is 3 or 6 or 10 or 15), "Jonah's footprint uses precisely source part3/6/10/15; Black Cat's different hooks and unused model parts cannot affect its bounds.");
            pose[part] = saved;
        }
    }
}
