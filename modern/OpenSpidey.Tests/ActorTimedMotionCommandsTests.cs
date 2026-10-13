using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

internal static class ActorTimedMotionCommandsTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorTimedMotionCommandsTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-timed-motion.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            int[] initial = row.GetProperty("initial").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            short actorType = row.GetProperty("actorType").GetInt16();
            int level = row.GetProperty("level").GetInt32(), mode = row.GetProperty("mode").GetInt32();
            var state = new ActorScriptState { ActorType = actorType, SkipDuringDeath = row.GetProperty("skip").GetBoolean(), WaitTicks = -5 };
            var body = new ActorBodyMotion { Position = Vector(0), Velocity = Vector(3), Acceleration = Vector(6), LocalVelocity = Vector(9),
                Angles = Angles(12), AngularVelocity = Angles(15), AngularAcceleration = Angles(18),
                LinearDamping = new(1, 4, 31), AngularDamping = new(1, 5, 31),
                UseLocalVelocity = row.GetProperty("local").GetBoolean(), ScriptWaitTicks = -5 };
            body.Recorded.ElapsedTicks = row.GetProperty("old").GetBoolean() ? 1 : 0;
            body.Recorded.TotalTicks = row.GetProperty("old").GetBoolean() ? 3 : 0;
            body.Recorded.StartPosition = body.Position + new FixedVector3(10000, 10000, 10000);
            body.Recorded.EndPosition = body.Position + new FixedVector3(20000, 20000, 20000);
            body.Recorded.VelocityPerTick = new(100, 200, 300);
            body.Recorded.StartAngles = body.Angles + new ActorAngles(100, 100, 100);
            body.Recorded.EndAngles = body.Angles + new ActorAngles(200, 200, 200);
            body.Recorded.AngularVelocityPerTick = new(2, 3, 4);
            ushort operand = row.GetProperty("operand").GetUInt16(), resolved = row.GetProperty("resolved").GetUInt16();
            byte[] bytes = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, operand);
            var cursor = new ActorScriptCursor(bytes);
            int variableReads = 0;
            int calls = 0;
            bool result = ActorTimedMotionCommands.Execute(state, body, cursor, (_, _) => { variableReads++; return unchecked((short)resolved); },
                new(FastForwardDeath: row.GetProperty("fast").GetBoolean(), LevelId: level), ticks =>
                {
                    calls++; assert(ticks == 2, "Original4280 forces only the virtual physics interval2 without changing the shared source clock.");
                    if (actorType != 402) body.Integrate(ticks);
                    else
                    {
                        var platform = new PlatformMotion(body);
                        platform.Integrate(ticks, (short)mode, level);
                    }
                });
            RecordedActorMotion recorded = body.Recorded;
            long[] actual = [(result ? 1 : 0), cursor.Position, calls, row.GetProperty("elapsed").GetInt32(), state.WaitTicks,
                recorded.ElapsedTicks, recorded.TotalTicks, ..Components(body.Position), ..Components(body.Velocity), ..Components(body.Acceleration),
                ..Components(body.LocalVelocity), ..Components(body.Angles), ..Components(body.AngularVelocity), ..Components(body.AngularAcceleration),
                ..Components(recorded.StartPosition), ..Components(recorded.EndPosition), ..Components(recorded.VelocityPerTick),
                ..Components(recorded.StartAngles), ..Components(recorded.EndAngles), ..Components(recorded.AngularVelocityPerTick)];
            long[] expected = row.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(expected) && body.ScriptWaitTicks == state.WaitTicks,
                $"Original4280 recording case{caseIndex++}, type{actorType}, level{level:X}, raw{operand:X}: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
            assert(variableReads == ((operand & 0x2000) != 0 ? 1 : 0),
                "TimedMotion retains its actual variable-expression flag, independent of raw4511/4512 operand semantics.");

            FixedVector3 Vector(int offset) => new(initial[offset], initial[offset + 1], initial[offset + 2]);
            ActorAngles Angles(int offset) => new(unchecked((short)initial[offset]), unchecked((short)initial[offset + 1]), unchecked((short)initial[offset + 2]));
        }
    }
    private static long[] Components(FixedVector3 vector) => [vector.X, vector.Y, vector.Z];
    private static long[] Components(ActorAngles angles) => [angles.X, angles.Y, angles.Z];
}
