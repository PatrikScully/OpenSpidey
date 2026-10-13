using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class ActorLaserCollisionTests
{
    private sealed class Host(ActorLaserLine line, ActorLaserBody[] bodies, long[] input) : IActorLaserHost
    {
        private int queryIndex;
        private readonly object literalPointer = new();
        public object ActorIdentity => bodies[0].Identity;
        public object PlayerIdentity => bodies[1].Identity;
        public FixedVector3 ActorPosition => bodies[0].Position;
        public FixedVector3 PlayerPosition => bodies[1].Position;
        public IReadOnlyList<ActorLaserBody> PlayerBodies { get; } = bodies.Skip(1).ToArray();
        public bool IgnoreSphereRadius => input[3] != 0;
        public bool LineOfSightCheck { get; set; } = input[4] != 0;
        public List<int> Events { get; } = [];
        public ActorLaserContact? TraceZoned(FixedVector3 start, FixedVector3 end, bool includeEnvironment)
        {
            Events.AddRange([2, start.X, start.Y, start.Z, end.X, end.Y, end.Z, includeEnvironment ? 1 : 0, LineOfSightCheck ? 1 : 0]);
            return Contact((int)input[5 + queryIndex++]);
        }
        public ActorLaserContact? TraceEnvironmental(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([3, start.X, start.Y, start.Z, end.X, end.Y, end.Z, LineOfSightCheck ? 1 : 0]);
            return Contact((int)input[7]);
        }
        private ActorLaserContact? Contact(int code) => code switch
        {
            0 => null,
            <= 3 => new(bodies[code - 1].Identity, new(12345, -67890, 24680)),
            4 => new(line, new(12345, -67890, 24680)),
            _ => new(literalPointer, new(12345, -67890, 24680), 12)
        };
        public int Random(int range) { Events.AddRange([4, range]); return (int)input[8] % range; }
        public void PlayPlayerHitSound(ushort sound) => Events.AddRange([5, sound, PlayerPosition.X, PlayerPosition.Y, PlayerPosition.Z, 0]);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorLaserCollisionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-laser.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var line = new ActorLaserLine { Start = Vector(11), End = Vector(14) };
            FixedVector3 direction = Vector(17), output = new(7, 11, 13);
            var bodies = new ActorLaserBody[4];
            for (int i = 0; i < bodies.Length; i++)
            {
                int offset = 20 + i * 5;
                bodies[i] = new(new object(), Vector(offset), (ushort)input[offset + 3], (uint)input[offset + 4]);
            }
            var host = new Host(line, bodies, input);
            int result;
            ActorSphereContact? sphere = null;
            switch (input[0])
            {
                case 0: result = ActorLaserCollision.Check((int)input[1], line, ref direction, input[2] != 0, host, host.Random); break;
                case 1:
                    sphere = ActorLaserCollision.CheckBodies(line.Start, line.End, host.PlayerBodies,
                        input[9] >= 0 ? bodies[(int)input[9]].Identity : null, (int)input[10], host.IgnoreSphereRadius);
                    result = sphere is null ? -1 : Array.FindIndex(bodies, body => ReferenceEquals(body.Identity, sphere.Body.Identity));
                    if (sphere is not null) output = sphere.Position;
                    break;
                default: result = ActorLaserCollision.CheckObjectCollision(line.Start, line.End, host) ? 1 : 0; break;
            }
            JsonElement expected = test.GetProperty("expected");
            long[] actual = [result, line.Start.X, line.Start.Y, line.Start.Z, line.End.X, line.End.Y, line.End.Z,
                direction.X, direction.Y, direction.Z, host.LineOfSightCheck ? 1 : 0];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(11).Select(value => value.GetInt64())),
                $"Laser case {caseIndex} matches original safety obstruction, player/environment test, pointer exclusions and exact mutated line/direction/LOS flag.");
            if (input[0] == 1)
                assert(output == new FixedVector3(expected[11].GetInt32(), expected[12].GetInt32(), expected[13].GetInt32()) &&
                    (sphere?.Parameter ?? int.MaxValue) == expected[15].GetInt32(),
                    $"Sphere case {caseIndex} preserves strict radius/endpoints, source nearest signed parameter and original double-shift hit position.");
            assert(host.Events.SequenceEqual(expected[16].EnumerateArray().Select(value => value.GetInt32())),
                $"Laser case {caseIndex} preserves zoned safety-before-object-before-beam queries, environmental-only fallback and player sound order.");
            caseIndex++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
        CheckDegenerateDirection(assert);
    }

    private static void CheckDegenerateDirection(Action<bool, string> assert)
    {
        long[] input = new long[40]; input[7] = 3;
        var line = new ActorLaserLine { Start = default, End = new(409600, 0, 0) };
        ActorLaserBody[] bodies = Enumerable.Range(0, 4).Select(_ => new ActorLaserBody(new object(), new(0, 0, 40960000), 0, 0x40)).ToArray();
        var host = new Host(line, bodies, input);
        FixedVector3 direction = default;
        bool threw = false;
        try { ActorLaserCollision.Check(304, line, ref direction, true, host, host.Random); }
        catch (DivideByZeroException) { threw = true; }
        assert(threw && host.Events[0] == 2 && host.Events[9] == 3 && !host.LineOfSightCheck,
            "A zero-length laser direction reports the original division failure after object collision, retaining the environmental flag reset.");
    }
}
