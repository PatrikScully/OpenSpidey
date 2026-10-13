using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class FlamingWebTests
{
    private sealed class Trail : IFlamingWebTrail { public short Angle { get; set; } }
    private sealed class Host(int[] input) : IFlamingImpactWebHost
    {
        private int cursor = input[14];
        private readonly object environment = new(), body = new();
        public List<long> Events { get; } = [];
        public int UpdateIndex { get; set; }
        public int Random(int bound)
        {
            int value = (cursor++ * 7 + 1) % bound;
            Events.AddRange([1, bound, value]); return value;
        }
        public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([3, 0, 1, start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            return input[8] == 0 ? null : new(environment, 0, (uint)input[10], new(12345, -54321, 67890), new(0, -4096, 0), input[9]);
        }
        public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([4, start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            return UpdateIndex == input[11] ? body : null;
        }
        public void HitBody(object identity, ActorHit hit)
        {
            if (!ReferenceEquals(identity, body)) throw new InvalidDataException("Flaming web lost the source collision body identity.");
            if (hit.PartIndex != 0 || hit.SlideDistance != 0 || hit.SlideFrames != 0)
                throw new InvalidDataException("Flaming web fabricated source uninitialized hit fields.");
            Events.AddRange([7, hit.Flags, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z]);
        }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) =>
            Events.AddRange([5, sound, position.X, position.Y, position.Z, pitch]);
        public void FlickEligibleSwitch(object identity)
        {
            if (!ReferenceEquals(identity, environment)) throw new InvalidDataException("Flaming web lost the source cached environmental identity.");
            Events.Add(8); if (input[12] != 0) Events.Add(9);
        }
        public int GetGroundY(FixedVector3 position)
        { Events.AddRange([10, position.X, position.Y, position.Z]); return unchecked(position.Y + 0x1388000); }
        public IFlamingWebTrail CreateTrail(FlamingWebTrailRequest request)
        {
            Events.AddRange([11, request.Position.X, request.Position.Y, request.Position.Z,
                request.Velocity.X, request.Velocity.Y, request.Velocity.Z, request.AnimationIndex,
                request.Scale, request.ScaleDecay, request.ColourDecay]);
            return new Trail();
        }
        public void CreateSpark(FireSparkRequest request, Func<int, int> random)
        {
            Events.AddRange([12, request.Position.X, request.Position.Y, request.Position.Z,
                request.Velocity.X, request.Velocity.Y, request.Velocity.Z, request.GroundY]);
            _ = new FireSpark(request, random); // Its real constructor consumes the shared source RNG inside the callback.
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(FlamingWebTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.flaming-web.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => unchecked((int)value.GetInt64())).ToArray();
            var host = new Host(input);
            uint clock = unchecked((uint)input[7]);
            var state = new FlamingImpactWeb(new(input[0], input[1], input[2]),
                new((short)input[3], (short)input[4], (short)input[5]), input[6], clock, host, host.Random);
            int update = -1;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (update >= 0)
                {
                    clock = unchecked(clock + (uint)input[13]); host.UpdateIndex = update; host.Events.Clear();
                    state.Advance(clock, host, host.Random);
                }
                long[] actual = [state.Position.X, state.Position.Y, state.Position.Z,
                    state.Velocity.X, state.Velocity.Y, state.Velocity.Z, state.Damage, state.LastClock,
                    state.Lifetime, state.Age, state.Scale, state.Angle, state.AngularVelocity,
                    state.TrailCountdown, state.CodeRgb, state.Alive ? 0 : 1];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(16).Select(value => value.GetInt64())),
                    $"Flaming source/PC case {index} update {update}: exact unscaled damage, angle/age/clock, strict source lifetime and alternating blur countdown.");
                assert(host.Events.SequenceEqual(expected[16].EnumerateArray().Select(value => value.GetInt64())),
                    $"Flaming source/PC case {index} update {update}: actual blur-before-flight, full3DHit, switch/no-splat impact, overshot ground point and20spark shared-RNG constructor order.");
                update++;
            }
            index++;
        }
    }
}
