using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class GlowStateTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(GlowStateTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.glow.json")
            ?? throw new InvalidDataException("Original CGlow fixtures are missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("constructor").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            GlowState state = input[0] == 0 ? new((int)input[1], (int)input[2]) :
                new(new FixedVector3((int)input[3], (int)input[4], (int)input[5]), (int)input[6], (int)input[7], (uint)input[8], (uint)input[9]);
            JsonElement expected = test.GetProperty("expected");
            int step = 0;
            Verify();
            foreach (JsonElement action in test.GetProperty("actions").EnumerateArray())
            {
                long[] operands = action.EnumerateArray().Select(value => value.GetInt64()).ToArray();
                switch (operands[0])
                {
                    case 0: state.SetRadius((int)operands[1]); break;
                    case 1: state.SetCentreRgb((byte)operands[1], (byte)operands[2], (byte)operands[3]); break;
                    case 2: state.SetRgb((byte)operands[1], (byte)operands[2], (byte)operands[3]); break;
                    case 3: state.SetFringeWidth((int)operands[1], (uint)operands[2]); break;
                    case 4: state.SetFringeRgb((int)operands[1], (byte)operands[2], (byte)operands[3], (byte)operands[4]); break;
                    default: throw new InvalidDataException("Unknown source CGlow setter fixture.");
                }
                Verify();
            }
            caseIndex++;

            void Verify()
            {
                long[] actual = Snapshot(state);
                long[] gold = expected[step++].EnumerateArray().Select(value => value.GetInt64()).ToArray();
                assert(actual.SequenceEqual(gold), $"CGlow case{caseIndex}/step{step}: source construction and radial/fringe setters match PC gold.");
            }
        }
        var grenade = new GlowState(4, 2);
        grenade.SetFringeRgb(0, 100, 50, 0);
        grenade.SetFringeRgb(1, 0, 0, 0);
        assert(grenade.Fringes.Take(8).All(fringe => fringe.CodeBgr == 0x3A003264) &&
            grenade.Fringes.Skip(8).All(fringe => fringe.CodeBgr == 0x3A000000),
            "Original PC keeps the grenade's orange inner fringe independent from its black outer fringe.");
        foreach (int invalid in new[] { -1, 2 })
        {
            long[] before = Snapshot(grenade);
            bool rejected = false;
            try { grenade.SetFringeRgb(invalid, 255, 255, 255); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            assert(rejected && Snapshot(grenade).SequenceEqual(before), "Invalid fringe handles fail before mutating live source geometry.");
        }
    }

    private static long[] Snapshot(GlowState state)
    {
        var values = new List<long> { state.Position.X, state.Position.Y, state.Position.Z, state.Sections.Count, state.FringeCount,
            state.CentreCodeBgr, state.StepAngle, state.SkipTriangles ? 1 : 0, state.Angle, state.Mask };
        foreach (GlowSection section in state.Sections) { values.Add(section.Radius); values.Add(section.PadBgr); }
        foreach (GlowFringe fringe in state.Fringes) { values.Add(fringe.Width); values.Add(fringe.CodeBgr); }
        return values.ToArray();
    }
}
