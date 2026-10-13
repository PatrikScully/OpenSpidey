using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugTorsoAimTests
{
    private sealed class Host(long[] input) : IThugTorsoAimHost
    {
        public ThugPlayerState Player => new(new((int)input[10], (int)input[11], (int)input[12]), 100, 0, 0, false);
        public int AttackClock => (int)input[4];
        public bool HasJoints => input[2] != 0;
        public short JointOneRoll { get; set; } = (short)input[3];
        public ushort ItemFlags { get; set; } = (ushort)input[1];
        public List<int> Events { get; } = [];
        public void ApplyPoseForType(int type) => Events.AddRange([type == 304 ? 304 : 312, ItemFlags, HasJoints ? 1 : 0, HasJoints ? JointOneRoll : 0]);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugTorsoAimTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-torso.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(9, 0x1101) { DistanceSampleClock = (int)input[5], CachedPlayerDistance = (int)input[6] };
            state.SetThugType((int)input[0]);
            var actor = new ActorProcedureState(new ActorBodyMotion { Position = new((int)input[7], (int)input[8], (int)input[9]) });
            var host = new Host(input);
            FixedVector3 origin = new((int)input[13], (int)input[14], (int)input[15]);
            int update = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                host.Events.Clear();
                ThugTorsoAim.Step(state, actor, origin, host);
                assert(host.ItemFlags == expected[0].GetInt32() && host.JointOneRoll == expected[1].GetInt32() &&
                    state.DistanceSampleClock == expected[2].GetInt32() && state.CachedPlayerDistance == expected[3].GetInt32(),
                    $"Torso case {caseIndex} update {update} matches original unsigned ratio, cached distance, asymmetric roll smoothing, short narrowing and item flags.");
                assert(host.Events.SequenceEqual(expected[4].EnumerateArray().Select(value => value.GetInt32())),
                    $"Torso case {caseIndex} update {update} preserves missing-joint pose initialization and source type-specific pose application ordering.");
                update++;
            }
            caseIndex++;
        }
        int value = -4096;
        foreach (JsonElement expected in document.RootElement.GetProperty("arcCos").EnumerateArray())
        {
            assert(ThugTorsoAim.ArcCos(value) == expected.GetInt32(), $"ArcCos {value} matches original PC helper0x4E6700 floating stores and final truncation.");
            value++;
        }
        assert(ThugTorsoAim.ArcCos(4097) == 0 && ThugTorsoAim.ArcCos(-4097) == 0 && ThugTorsoAim.ArcCos(int.MaxValue) == 0,
            "Invalid acos domains retain the zero low word of the original MSVC _ftol invalid-conversion result.");
    }
}
