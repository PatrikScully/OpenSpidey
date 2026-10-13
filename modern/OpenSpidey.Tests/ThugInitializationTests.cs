using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugInitializationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugInitializationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-initialization.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var state = new ThugState(0, 1, input[1]) { Substate = input[0], PathPointIndex = input[3], AlertTicks = 600 };
            state.SetThugType(312);
            var host = new Host(input[4]);
            var graph = new TriggerFile([Node(0, input[5] != 0), Node(1, input[5] != 0)]);
            var route = new ActorWaypointRoute(graph, 0, _ => throw new Exception("Non-script waypoint must not invoke parsing."));
            int counter = input[2];
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (state.State == 0) ThugInitializationBehavior.Step(state, route, host, counter);
                int[] actual = [state.State, state.Substate, state.AlertTicks, host.Position.X, host.Position.Y, host.Position.Z,
                    host.BodyHeight, host.GroundHeight, host.GroundCalls, host.HeightCalls, host.CycleCalls, route.CurrentNode];
                int[] reference = expected.EnumerateArray().Select(value => value.GetInt32()).Where((_, index) => index != 11).ToArray();
                assert(actual.SequenceEqual(reference),
                    $"Thug grounding and route startup match original AI at attack counter {counter}, ground {input[4]}.");
                counter = unchecked(counter + 1);
            }
        }
    }

    private static TriggerNode Node(ushort id, bool linked)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)12);
        writer.Write((ushort)(linked ? 1 : 0));
        if (linked) writer.Write((ushort)(id ^ 1));
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write(100); writer.Write(200); writer.Write(300);
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)0);
        return new(id, 12, id * 64, stream.ToArray());
    }

    private sealed class Host(int ground) : IThugInitializationHost
    {
        public FixedVector3 Position { get; set; } = new(100, 789, 300);
        public int BodyHeight { get; private set; }
        public int GroundHeight { get; private set; }
        public int GroundCalls { get; private set; }
        public int HeightCalls { get; private set; }
        public int CycleCalls { get; private set; }
        public int FindGroundHeight(int above, int below)
        {
            if (above != 300 || below != 300) throw new Exception("Initial ground query changed its source range.");
            GroundCalls++;
            return ground;
        }
        public void StoreGroundPlacement(int bodyHeight, int groundHeight)
        {
            BodyHeight = bodyHeight;
            GroundHeight = groundHeight;
        }
        public int SetHeight(bool initial, int clearance, int range)
        {
            if (!initial || clearance != 100 || range != 600) throw new Exception("Initial height setup changed.");
            HeightCalls++;
            return 2;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            if (clip != 0 || direction != 1) throw new Exception("Initial idle animation changed.");
            CycleCalls++;
        }
    }
}
