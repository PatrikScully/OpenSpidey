using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class ActorMoveToTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorMoveToTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-move.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var body = new ActorBodyMotion
            {
                Position = Vector(0), Angles = new(0, (short)input[9], 0), MovementFlags = (uint)input[12]
            };
            var actor = new ActorProcedureState(body)
            {
                CompletionFlags = (uint)input[10], BehaviorFlags = (uint)input[11]
            };
            var procedure = new ActorMoveToProcedure(actor, Vector(3), (int)input[6], (int)input[7],
                (int)input[8], (uint)input[13]);
            int nextCalls = 0;
            procedure.ExecuteNext = () => nextCalls++;
            JsonElement.ArrayEnumerator states = test.GetProperty("expected").EnumerateArray();
            states.MoveNext();
            CheckState(states.Current, -1);
            for (int frame = 0; frame < 8; frame++)
            {
                if (input[14] == frame) actor.CompletionFlags |= 0x10000;
                if (input[15] != 0) procedure.Facing?.Execute();
                procedure.Execute((int)input[16 + frame]);
                states.MoveNext();
                CheckState(states.Current, frame);
                assert(body.Position.Y == input[1], "MoveTo preserves actor Y even when destination Y differs.");
            }

            void CheckState(JsonElement expected, int frame)
            {
                long[] actual = [body.Position.X, body.Position.Y, body.Position.Z,
                    procedure.Velocity.X, procedure.Velocity.Y, procedure.Velocity.Z,
                    actor.CompletionFlags, actor.BehaviorFlags, body.MovementFlags,
                    procedure.CompletionMask, procedure.Flags, procedure.Started ? 1 : 0, nextCalls,
                    body.Angles.Y, procedure.Facing?.CompletionMask ?? 0,
                    procedure.Facing?.Tolerance ?? 0, procedure.Facing?.Rate ?? 0];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    $"MoveTo constructor and frame {frame} match compiled recovered normalization, facing, arithmetic and signals.");
            }
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }

        var positiveActor = new ActorProcedureState(new ActorBodyMotion { Position = new(0, 789, 0) });
        var positive = new ActorMoveToProcedure(positiveActor, new(4096, 789, 0), 16, 0, 16, 1);
        positive.Execute(1);
        assert(positiveActor.Body.Position.X == 4096 && positive.Velocity.X != 0 && positive.Flags == 0,
            "Positive travel reaches an exact endpoint before clearing velocity on the next update.");
        positive.Execute(1);
        assert(positive.Velocity.X == 0 && positive.Flags == 1 && (positiveActor.CompletionFlags & 1) != 0,
            "Positive travel stops and signals when the next update crosses beyond the endpoint.");
        var negativeActor = new ActorProcedureState(new ActorBodyMotion { Position = new(4096, 789, 0) });
        var negative = new ActorMoveToProcedure(negativeActor, new(0, 789, 0), 16, 0, 16, 1);
        negative.Execute(1);
        assert(negativeActor.Body.Position.X == 0 && negative.Velocity.X == 0 && negative.Flags == 1,
            "Negative travel stops on the exact endpoint because the recovered test uses strict greater-than.");
    }
}
