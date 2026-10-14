using System.Text.Json;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;

internal static class JonahScorpionContactTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(JonahScorpionContactTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.jonah-contact.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] v = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var brain = new JonahBrainState(new(0), new(new()), new(), new())
            { Position = new(v[0],v[1],v[2]), PreviousDisplacementX = v[6], PreviousDisplacementZ = v[7] };
            var state = new JonahScorpionContactState(brain)
            { DesiredX = v[8], SmoothedX = v[9], DesiredZ = v[10], SmoothedZ = v[11] };
            var scorpionPosition = new FixedVector3(v[3],v[4],v[5]);
            int step = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                JonahScorpionContact.Resolve(state, scorpionPosition);
                int[] actual = [brain.Position.X,brain.Position.Y,brain.Position.Z,brain.PreviousDisplacementX,brain.PreviousDisplacementZ,
                    state.DesiredX,state.SmoothedX,state.DesiredZ,state.SmoothedZ];
                int[] source = expected.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                assert(actual.SequenceEqual(source), $"Whole original PC contact case {index} visit{step++} preserves near/far thresholds, actual Q12 normalization, source adjustment rounding, deadzone, full32-bit wrapping and all9 canonical fields; actual [{string.Join(',',actual)}], expected [{string.Join(',',source)}].");
                brain.Position = brain.Position with
                { X = unchecked(brain.Position.X-brain.PreviousDisplacementX), Z = unchecked(brain.Position.Z-brain.PreviousDisplacementZ) };
            }
            assert(step == v[12], "Repeated contact visits consume the genuine preceding AI displacement rather than accumulating contact offsets.");
            index++;
        }
    }
}
