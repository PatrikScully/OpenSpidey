using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Mathematics;

internal static class SimbyConstructionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SimbyConstructionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.simby-construction.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            if (mode == "C") Construct(assert, index, input, expected);
            else if (mode == "F") Flash(assert, index, input, expected);
            else Update(assert, index, input, expected);
            index++; counts++;
        }
        assert(counts == 585, "Actual PC CSimby fixture keeps all constructor and Flash cases.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var state = new SimbyState(input[1], input[0], 0) { BodyFlags = 0, CombatFlags = 0 };
        var world = new SimbyWorldState { Count = input[3], CountResetFlag = input[4] };
        var host = new Host { Body = { Position = new FixedVector3(input[5], input[6], input[7]) << 12,
            Angles = new((short)input[8], (short)input[9], (short)input[10]) } };
        host.Events.Add(1); // CBaddy base, matching the patched original leaf.
        host.Events.AddRange([4, 5]); // SquirtPos / SquirtAngles order from the original call chain.
        SimbyConstruction.Construct(state, world, host, input[2]);
        int[] actual =
        [
            host.ItemFlags, (int)state.BodyFlags, SimbyState.Type, state.Health, state.MinorRadius, state.SurfaceClearance,
            state.PushValue, state.GeneralParameter, (int)state.CombatFlags, state.WaitUpdates, state.Stage,
            state.SetupLow, state.SetupHigh, state.AttackRelatedSnapshot, state.AlertNoiseTimer, state.Field3B8,
            state.Field3BC, state.Field3C0, state.Field30C, (int)state.Field3A0, (int)state.BehaviorFlags, state.LevelId,
            state.SourceNode, world.Count, world.CountResetFlag, host.Body.Position.X, host.Body.Position.Y, host.Body.Position.Z,
            host.Body.Angles.X, host.Body.Angles.Y, host.Body.Angles.Z
        ];
        int[] reference = expected.Take(31).ToArray();
        assert(actual.SequenceEqual(reference) && host.Events.SequenceEqual(expected.Skip(31)),
            $"Actual PC CSimby constructor case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', reference)}]; " +
            $"events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', expected.Skip(31))}].");
    }

    private static void Flash(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var state = new SimbyState(0, 0, 0)
        {
            Rgb = input[5], FlashRestore = input[6], FlashFrames = (short)input[7],
            FlashDeltaR = (ushort)input[8], FlashDeltaG = (ushort)input[9], FlashDeltaB = (short)input[10]
        };
        ushort item = (ushort)input[4];
        SimbyFlash.Flash(state, ref item, input[0], (byte)input[1], (byte)input[2], (byte)input[3]);
        int[] actual = [item, state.Rgb, state.FlashFrames, state.FlashDeltaR, state.FlashDeltaG, state.FlashDeltaB, state.FlashRestore];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CSimby::Flash case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Update(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var state = new SimbyState(0, 0, 0)
        {
            Rgb = input[1], FlashRestore = input[2], FlashFrames = (short)input[3],
            FlashDeltaR = (ushort)input[4], FlashDeltaG = (ushort)input[5], FlashDeltaB = (short)input[6]
        };
        ushort item = (ushort)input[0];
        SimbyFlash.Update(state, ref item);
        int[] actual = [item, state.Rgb, state.FlashFrames, state.FlashDeltaR, state.FlashDeltaG, state.FlashDeltaB, state.FlashRestore];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CSimby::FlashUpdate case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static int[] Words(JsonElement element) => element.ValueKind == JsonValueKind.Array
        ? element.EnumerateArray().SelectMany(value => value.ValueKind == JsonValueKind.Array ? Words(value) : [value.GetInt32()]).ToArray()
        : throw new InvalidDataException("Fixture words require a JSON array.");

    private sealed class Host() : ISimbyConstructionHost
    {
        public ActorBodyMotion Body { get; } = new();
        public ushort ItemFlags { get; set; }
        public List<int> Events { get; } = [];
        public void InitializeModel(string name) => Events.AddRange([2, name == SimbyState.DarkModelName ? 1 : name == SimbyState.BrightModelName ? 0 : -1]);
        public void AttachToSourceBaddyList() => Events.AddRange([3, 0x56E990]);
        public void ReadPosePacket() => Events.AddRange([7, (int)SimbyState.PosePacketAddress]);
        public void ParseAuthoredScript() => Events.Add(8);
        public int NextRandom(int bound) { Events.AddRange([6, bound]); return bound == 0 ? 0 : bound / 3; }
        public void EnsureVertexWibbler() => Events.Add(9);
    }
}
