using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class ImpactWebTests
{
    private sealed class Host(int[] input, uint initialClock) : IImpactWebHost
    {
        public List<long> Events { get; } = [];
        private int randomIndex = input[25];
        private int memoryReads;
        public int UpdateIndex { get; set; }
        private readonly object environment = new(), body = new();
        public uint Clock { get; set; } = initialClock;
        public int Random(int bound) { int value = (randomIndex++ * 7 + 1) % bound; Events.AddRange([1, bound, value]); return value; }
        public int ScaleDamage(int damage) { Events.AddRange([2, damage]); return damage + 77; }
        private void Trace(int mode, int los, FixedVector3 a, FixedVector3 b) =>
            Events.AddRange([3, mode, los, a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
        public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 a, FixedVector3 b)
        {
            Trace(0, 1, a, b);
            return input[10] == 0 ? null : new(environment, (ushort)input[12], (uint)input[13],
                new(input[14], input[15], input[16]), new((short)input[17], (short)input[18], (short)input[19]), input[11]);
        }
        public object? CheckObjectCollision(FixedVector3 a, FixedVector3 b)
        {
            Events.AddRange([4, a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
            return UpdateIndex == input[20] ? body : null;
        }
        public void SetPlayerFirstContact() => Events.AddRange([6, 360, (5 + 11) << 10]);
        public void HitBody(object identity, ActorHit hit)
        {
            if (!ReferenceEquals(identity, body)) throw new InvalidDataException("Impact web lost genuine body identity.");
            Events.AddRange([7, hit.AttackKind, hit.Flags, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]);
        }
        public void PlayPositionSound(ushort sound, FixedVector3 p, int pitch) => Events.AddRange([5, sound, p.X, p.Y, p.Z, pitch]);
        public void CreateSplat(FixedVector3 p, ActorAngles n) => Events.AddRange([10, p.X, p.Y, p.Z, n.X, n.Y, n.Z]);
        public void FlickEligibleSwitch(object identity)
        {
            if (!ReferenceEquals(identity, environment)) throw new InvalidDataException("Impact web lost genuine environmental identity.");
            Events.Add(8); if (input[23] != 0) Events.Add(9);
        }
        public void CreateImpactRing(ImpactWebRingRequest r) => Events.AddRange([11, r.Position.X, r.Position.Y, r.Position.Z, r.Red, r.Green, r.Blue, r.Size, r.Growth]);
        public int TraceGroundY(FixedVector3 a, FixedVector3 b) { Trace(1, 0, a, b); return input[21] != 0 ? -123456 : b.Y; }
        public bool LowMemory => memoryReads++ >= input[22];
        public void ResetUpdate() { Events.Clear(); memoryReads = 0; }
        public void CreateWebFragment(ImpactWebFragmentRequest r, Func<int, int> random) =>
            Events.AddRange([12, r.GroundY, r.Previous.X, r.Previous.Y, r.Previous.Z,
                r.FirstHook.X, r.FirstHook.Y, r.FirstHook.Z, r.SecondHook.X, r.SecondHook.Y, r.SecondHook.Z,
                r.OwnerPosition.X, r.OwnerPosition.Y, r.OwnerPosition.Z, r.Speed, r.Jitter]);
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ImpactWebTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.impact-web.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] values = test.GetProperty("input").EnumerateArray().Select(v => v.GetInt64()).ToArray();
            int[] input = values.Select(v => unchecked((int)v)).ToArray();
            uint clock = unchecked((uint)input[9]);
            var host = new Host(input, clock);
            var projectile = new ImpactWebProjectile(new(input[0], input[1], input[2]),
                new((short)input[3], (short)input[4], (short)input[5]), input[6], input[7], input[8], clock, host, host.Random);
            int update = -1;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (update >= 0)
                {
                    clock = unchecked(clock + (uint)input[24]); host.Clock = clock; host.UpdateIndex = update;
                    host.ResetUpdate(); projectile.Advance(clock, host, host.Random);
                }
                long[] actual = [projectile.Position.X, projectile.Position.Y, projectile.Position.Z,
                    projectile.Velocity.X, projectile.Velocity.Y, projectile.Velocity.Z, projectile.Damage,
                    projectile.LastClock, projectile.Lifetime, projectile.Age, projectile.Scale, projectile.Angle,
                    projectile.AngularVelocity, projectile.Alive ? 0 : 1];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(14).Select(v => v.GetInt64())),
                    $"Impact web case {index} update {update}: exact source constructor/motion, wrapped age/angle/clock, scale and terminal state.");
                assert(host.Events.SequenceEqual(expected[14].EnumerateArray().Select(v => v.GetInt64())),
                    $"Impact web case {index} update {update}: source collision priority, hit/switch callbacks, previous-position effects, ground ray, fragment cap/RNG order.");
                update++;
            }
            assert(projectile.AnimationIndex == 9 && projectile.Frame == 0 && projectile.PostScale == 0x0A001000,
                $"Impact web case {index}: original animation and postscale contract.");
            index++;
        }
    }
}
