using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class ActorSteeringTests
{
    public static void Run(Action<bool, string> assert)
    {
        CheckYaw(assert);
        using Stream stream = typeof(ActorSteeringTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-steering.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var body = new ActorBodyMotion
            {
                Position = Vector(12), Angles = new(11, (short)input[4], 13),
                AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13),
                MovementFlags = (uint)input[11]
            };
            var actor = new ActorProcedureState(body)
            {
                BehaviorFlags = (uint)input[8], CompletionFlags = (uint)input[9]
            };
            var target = new ActorBodyMotion { Position = Vector(15) };
            uint mask = (uint)input[10];
            int rate = (int)input[2], tolerance = (int)input[3];
            ActorLookAtProcedure procedure = input[0] switch
            {
                257 => ActorLookAtProcedure.ForBody(actor, () => input[7] != 0 ? target : null, mask, rate, tolerance),
                258 => ActorLookAtProcedure.ForPoint(actor, target.Position, mask, rate, tolerance),
                _ => ActorLookAtProcedure.ForYaw(actor, (int)input[1], mask, rate, tolerance)
            };
            procedure.Flags = (int)input[5]; procedure.WaitTicks = (int)input[6];
            int nextCalls = 0;
            procedure.ExecuteNext = () => nextCalls++;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                procedure.Execute();
                long[] actual = [body.Angles.Y, body.AngularVelocity.Y, body.AngularAcceleration.Y,
                    body.MovementFlags, actor.CompletionFlags, actor.BehaviorFlags,
                    procedure.Flags, procedure.WaitTicks, procedure.CompletionMask, procedure.Tolerance, procedure.Rate, nextCalls];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    $"LookAt mode {input[0]}, rate {rate}, yaw {input[4]} matches compiled recovered steering and continuation.");
                assert(body.Angles.X == 11 && body.Angles.Z == 13 && body.AngularVelocity.X == 11 &&
                    body.AngularVelocity.Z == 13 && body.AngularAcceleration.X == 11 && body.AngularAcceleration.Z == 13,
                    "Yaw steering preserves pitch and roll motion.");
            }
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }

        var trackingActor = new ActorProcedureState(new ActorBodyMotion());
        var trackingTarget = new ActorBodyMotion { Position = new(409600, 0, 0) };
        ActorLookAtProcedure tracking = ActorLookAtProcedure.ForBody(trackingActor, () => trackingTarget, 1, 0, 16);
        tracking.Execute();
        assert(trackingActor.Body.Angles.Y == 3072, "Body LookAt recovers its current target position.");
        trackingTarget.Position = new(-409600, 0, 0);
        tracking.Execute();
        assert(trackingActor.Body.Angles.Y == 1024, "Body LookAt follows a moving target on subsequent updates.");
        trackingTarget = null!;
        tracking.Execute();
        assert((tracking.Flags & 1) != 0, "Invalid recovered body finishes the LookAt procedure.");
    }

    private static void CheckYaw(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorSteeringTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-yaw.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var body = new ActorBodyMotion
            {
                Angles = new(11, (short)input[0], 13),
                AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13)
            };
            int residual = ActorYawSteering.YawTowards(body, input[1], input[2]);
            int[] actual = [residual, body.Angles.Y, body.AngularVelocity.Y, body.AngularAcceleration.Y];
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())),
                "Direct YawTowards preserves source wrap, signed-short crossing, snap and residual behavior.");
        }
    }
}
