using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ActorShadowTests
{
    public static void Run(Action<bool,string> assert)
    {
        using Stream stream = typeof(ActorShadowTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-shadows.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            long[] gold = test.GetProperty("output").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ActorShadowState();
            state.Update((uint)input[0],new(input[1],input[2],input[3]),(ushort)input[4],(short)input[5],(short)input[6]);
            assert(state.Exists == (gold[0] != 0),$"Source shadow case{index} preserves body flag8 lifetime.");
            if (state.Exists)
            {
                for (int corner = 0; corner < 4; corner++)
                    assert(state.Corners[corner] == new FixedVector3((int)gold[1+corner*3],(int)gold[2+corner*3],(int)gold[3+corner*3]),
                        $"Source shadow case{index} corner{corner} preserves exact fixed normal/perpendicular orientation and ushort scale.");
                assert(state.Tint == gold[13] && state.CodeBgr == gold[14] && state.FrigDeltaZ == gold[15] && state.Protected == (gold[16] != 0),
                    $"Source shadow case{index} preserves signed threshold division, byte narrowing, quad code/protection/depthbias.");
                assert(state.PcBlendMode == 1 && state.PcAlpha == 255,
                    "Original PC409F69..409F8C tests selector40/80 only; shadow’s subtraction bits do not select blend3.");
            }
            state.Kill();
            assert(!state.Exists,"KillShadow deletes the protected quad.");
            index++;
        }
        var invalid = new ActorShadowState();
        bool rejected = false;
        try { invalid.Update(8,default,50,0,0); } catch(DivideByZeroException) { rejected = true; }
        assert(rejected && !invalid.Exists,"Zero shadow threshold rejects the original invalid division before mutation.");
    }
}
