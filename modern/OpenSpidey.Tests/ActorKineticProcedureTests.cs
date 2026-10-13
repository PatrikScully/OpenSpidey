using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class ActorKineticProcedureTests
{
    private sealed class LandingHost : IActorFallHost
    {
        public int LandingY { get; set; }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorKineticProcedureTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-kinetic.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var body = new ActorBodyMotion
            {
                Position = new(17, 0, 23), LocalVelocity = new(17, 19, (int)input[3]),
                Acceleration = new(17, 19, (int)input[4])
            };
            var actor = new ActorProcedureState(body)
            {
                CompletionFlags = (uint)input[7], BehaviorFlags = (uint)input[8]
            };
            if (input[0] == 0) CheckFall(test, input, actor, assert);
            else CheckAcceleration(test, input, actor, assert);
        }
    }

    private static void CheckFall(JsonElement test, long[] input, ActorProcedureState actor, Action<bool, string> assert)
    {
        var host = new LandingHost();
        var procedure = new ActorFallProcedure(actor, host, (uint)input[9]) { Flags = (int)input[5] };
        int nextCalls = 0, frame = 0;
        procedure.ExecuteNext = () => nextCalls++;
        foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
        {
            actor.Body.Position = actor.Body.Position with { Y = (int)input[10 + frame * 3] };
            host.LandingY = (int)input[11 + frame * 3];
            actor.BehaviorFlags = (uint)input[12 + frame * 3];
            procedure.Execute(frame - 3);
            long[] actual = [actor.Body.Position.Y, actor.CompletionFlags, actor.BehaviorFlags,
                procedure.Flags, procedure.CompletionMask, nextCalls];
            assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                "Fall matches recovered landing threshold, inactive retirement, completion retention and continuation.");
            assert(actor.Body.Position.X == 17 && actor.Body.Position.Z == 23,
                "Fall preserves horizontal position.");
            frame++;
        }
    }

    private static void CheckAcceleration(JsonElement test, long[] input, ActorProcedureState actor, Action<bool, string> assert)
    {
        var procedure = new ActorAccelerationProcedure(actor, (int)input[1], (int)input[2], (uint)input[9])
        {
            Flags = (int)input[5], WaitTicks = (int)input[6]
        };
        int nextCalls = 0;
        procedure.ExecuteNext = () => nextCalls++;
        JsonElement.ArrayEnumerator states = test.GetProperty("expected").EnumerateArray();
        states.MoveNext();
        CheckState(states.Current);
        for (int frame = 0; frame < 7; frame++)
        {
            procedure.Flags |= (int)input[10 + frame];
            procedure.Execute(frame - 3);
            states.MoveNext();
            CheckState(states.Current);
            assert(actor.Body.LocalVelocity.X == 17 && actor.Body.LocalVelocity.Y == 19 &&
                actor.Body.Acceleration.X == 17 && actor.Body.Acceleration.Y == 19,
                "Forward acceleration preserves local horizontal and vertical components.");
            actor.Body.LocalVelocity += new FixedVector3(0, 0, actor.Body.Acceleration.Z);
        }

        void CheckState(JsonElement expected)
        {
            long[] actual = [actor.Body.LocalVelocity.Z, actor.Body.Acceleration.Z, actor.CompletionFlags,
                actor.BehaviorFlags, procedure.Flags, procedure.WaitTicks, procedure.CompletionMask,
                procedure.Acceleration, procedure.TargetSpeed, nextCalls];
            assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                "AccZ matches recovered conversions, signed clamp, wrapped addition, waits, stop flags and continuation.");
        }
    }
}
