using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class ActorHeightControllerTests
{
    private sealed class HeightHost : IActorHeightHost
    {
        public int Height { get; set; }
        public ActorBodyMotion? Support { get; set; }
        public int Calls { get; private set; }
        public FixedVector3 LastPosition { get; private set; }
        public int LastAbove { get; private set; }
        public int LastBelow { get; private set; }

        public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support)
        {
            Calls++;
            LastPosition = position;
            LastAbove = above;
            LastBelow = below;
            support = Support;
            return Height;
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorHeightControllerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-height.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var actor = new ActorProcedureState(new ActorBodyMotion { Position = new((int)input[0], (int)input[1], (int)input[2]) });
            actor.BehaviorFlags = (uint)input[8];
            var support1 = new ActorBodyMotion();
            var support2 = new ActorBodyMotion();
            var host = new HeightHost();
            var controller = new ActorHeightController(actor, host)
            {
                GroundHeight = (int)input[3], Clearance = (short)input[4], QueryInterval = (byte)input[5],
                QueryElapsed = (byte)input[6], RouteFlags = (uint)input[7], Support = Recover(input[9])
            };
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                int offset = 10 + frame * 10;
                host.Height = (int)input[offset + 4]; host.Support = Recover(input[offset + 5]);
                support1.Velocity = new(0, (int)input[offset + 6], 0);
                support2.Velocity = new(0, (int)input[offset + 7], 0);
                actor.BehaviorFlags = (uint)input[offset + 8];
                controller.RouteFlags = (uint)input[offset + 9];
                int result = controller.SetHeight(input[offset] != 0, (int)input[offset + 1], (int)input[offset + 2], (int)input[offset + 3]);
                long[] actual = [result, actor.Body.Position.Y, controller.GroundHeight,
                    SupportId(controller.Support),
                    controller.QueryInterval, controller.QueryElapsed, host.Calls,
                    host.LastPosition.X, host.LastPosition.Y, host.LastPosition.Z, host.LastAbove, host.LastBelow];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    "SetHeight matches recovered query gates, byte timing, support outputs, wrapping, smoothing and snap thresholds.");
                assert(actor.Body.Position.X == input[0] && actor.Body.Position.Z == input[2] &&
                    actor.BehaviorFlags == (uint)input[offset + 8] && controller.RouteFlags == (uint)input[offset + 9],
                    "Ground height adjustment preserves horizontal position and actor behavior flags.");
                frame++;
            }
            ActorBodyMotion? Recover(long id) => id switch { 1 => support1, 2 => support2, _ => null };
            int SupportId(ActorBodyMotion? support)
            {
                if (ReferenceEquals(support, support1)) return 1;
                if (ReferenceEquals(support, support2)) return 2;
                return 0;
            }
        }

        var failedActor = new ActorProcedureState(new ActorBodyMotion { Position = new(17, 123, 23) });
        var movingSupport = new ActorBodyMotion { Velocity = new(0, 4096, 0) };
        var failedHost = new HeightHost { Height = -1, Support = movingSupport };
        var failed = new ActorHeightController(failedActor, failedHost)
        {
            GroundHeight = 999, QueryElapsed = 29, QueryInterval = 30
        };
        assert(failed.SetHeight(true, 300, 600, 1) == 0 && ReferenceEquals(failed.Support, movingSupport) &&
            failed.GroundHeight == 999 && failed.QueryElapsed == 29 && failedActor.Body.Position.Y == 123,
            "A failed ground query retains its support out parameter and leaves cached height, time and position untouched.");
    }
}
