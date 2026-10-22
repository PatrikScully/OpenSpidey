using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;

/// <summary>CSimbyBase::AI (0x4ACF00) and CBaddy constructor defaults captured by tools/reference_simby_base_vectors.py.</summary>
internal static class SimbyBaseTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(SimbyBaseTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.simby-base.json")!);
        int[] constructor = document.RootElement.GetProperty("constructor").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        var fresh = new SimbyBaseState(default, new ActorAngles(1, 2, 3));
        assert(constructor.SequenceEqual([fresh.ScaleX, fresh.ScaleY, fresh.ScaleZ, fresh.ItemFlags, fresh.BodyFlags]),
            "CSimbyBase starts from the original CBaddy constructor scale and flag defaults.");
        int index = 0, frames = 0, mismatches = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] input = test.GetProperty("state").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int[][] script = test.GetProperty("frames").EnumerateArray().Select(frame => frame.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray();
            int[][] expected = test.GetProperty("expected").EnumerateArray().Select(frame => frame.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray();
            var state = new SimbyBaseState(default, default)
            {
                ScaleX = unchecked((short)input[0]), ScaleY = unchecked((short)input[1]), ScaleZ = unchecked((short)input[2]),
                ItemFlags = unchecked((ushort)input[3]), BodyFlags = unchecked((ushort)input[4]), Following = input[5], TargetScale = input[6]
            };
            for (int frame = 0; frame < script.Length; frame++)
            {
                if (script[frame][1] != -1) state.Following = script[frame][1];
                if (script[frame][2] != -99999) state.TargetScale = script[frame][2];
                int recoverCalls = 0;
                bool alive = script[frame][0] != 0;
                SimbyBaseBrain.Advance(state, () => { recoverCalls++; return alive; });
                int[] actual = [state.ScaleX, state.ScaleY, state.ScaleZ, state.ItemFlags, state.BodyFlags, state.Following, state.TargetScale, recoverCalls, 1];
                if (!actual.SequenceEqual(expected[frame]) && mismatches++ < 5)
                    assert(false, $"CSimbyBase case {index} frame {frame}: expected [{string.Join(',', expected[frame])}], got [{string.Join(',', actual)}].");
                frames++;
            }
            index++;
        }
        assert(mismatches == 0 && index > 0, $"CSimbyBase::AI matches the original over {index} cases and {frames} frames ({mismatches} mismatches).");
    }
}
