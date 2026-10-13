using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class GrenadeProjectileTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(GrenadeProjectileTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.grenade.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] v = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var host = new Host
            {
                Timer = (int)v[1], TotalTime = (int)v[2], LevelId = (int)v[3],
                RandomValues = v.Skip(50).Select(value => (int)value).ToArray(), Ground = (int)v[44],
                Collision = new(v[32] != 0, (int)v[33], Vector(34), Vector(37)),
                Shadow = new(v[40] != 0, 0, Vector(41), default)
            };
            var grenade = new GrenadeProjectile(Vector(12), Vector(15), (int)v[4], (int)v[5], (int)v[6],
                v[7] != 0 ? GrenadeTargetList.Player : GrenadeTargetList.Baddies, (uint)v[8], (byte)v[9], (byte)v[10], host, host.Random);
            if (v[0] != 0)
            {
                host.Events.Clear();
                host.RandomCursor = 0;
                grenade.Position = Vector(18);
                grenade.Velocity = Vector(21);
                grenade.Angles = new((short)v[24], (short)v[25], (short)v[26]);
                grenade.Settled = (byte)v[27];
                grenade.StartTime = (int)v[28];
                grenade.Red = (byte)v[29]; grenade.Green = (byte)v[30]; grenade.Blue = (byte)v[31];
                grenade.ShadowPosition = new(1, 2, 3);
                grenade.ShadowScale = (ushort)v[45];
                grenade.ShadowDistance = (short)v[46];
                grenade.BodyFlags = (ushort)v[47];
                for (int update = 0; update < v[48]; update++) grenade.Update();
            }
            long[] actual = [grenade.Position.X, grenade.Position.Y, grenade.Position.Z, grenade.Velocity.X, grenade.Velocity.Y, grenade.Velocity.Z,
                grenade.Angles.X, grenade.Angles.Y, grenade.Angles.Z, grenade.Settled, grenade.StartTime, grenade.BodyFlags,
                grenade.ShadowPosition.X, grenade.ShadowPosition.Y, grenade.ShadowPosition.Z, grenade.ShadowScale, grenade.ShadowDistance,
                grenade.Damage, grenade.Radius, grenade.Type, grenade.Region];
            if (v[49] != 0) grenade.Dispose();
            long[] expected = test.GetProperty("state").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            long[] events = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            string label = $"Grenade source case {index++}: input [{string.Join(',', v)}]";
            assert(actual.SequenceEqual(expected), $"{label}: actual [{string.Join(',', actual)}], expected [{string.Join(',', expected)}]");
            assert(host.Events.SequenceEqual(events), $"{label}: events [{string.Join(',', host.Events)}], expected [{string.Join(',', events)}]");
            grenade.Dispose();
            FixedVector3 Vector(int offset) => new((int)v[offset], (int)v[offset + 1], (int)v[offset + 2]);
        }

        foreach ((int speed, int radius, string parameter) in new[] { (0, 512, "speed"), (int.MinValue, 512, "speed"), (35, 0, "radius"), (35, 65536, "radius") })
        {
            var host = new Host();
            bool rejected = false;
            try { using var grenade = new GrenadeProjectile(default, new(409600, 0, 0), speed, 10, radius, GrenadeTargetList.Baddies, 0, 0, 0, host, _ => 0); }
            catch (ArgumentOutOfRangeException exception) { rejected = exception.ParamName == parameter; }
            assert(rejected && host.Events.Count == 0, $"Grenade rejects unsafe source {parameter} division before world mutation");
        }
    }

    private sealed class Host : IGrenadeProjectileHost
    {
        public List<long> Events { get; } = [];
        public int Timer { get; set; }
        public int TotalTime { get; set; }
        public int LevelId { get; set; }
        public byte ObjectFileRegion => 16;
        public byte DefaultGrenadeRegion => 9;
        public int[] RandomValues { get; set; } = [0];
        public int RandomCursor { get; set; }
        public GrenadeSweep Collision { get; set; }
        public GrenadeSweep Shadow { get; set; }
        public int Ground { get; set; }
        public int Random(int bound) { Events.AddRange([2, bound]); return RandomValues[RandomCursor++ % RandomValues.Length] % bound; }
        public void AttachProjectile(GrenadeProjectile projectile) => Events.Add(0);
        public void LoadModel(uint checksum, byte region) => Events.AddRange([1, checksum, region]);
        public GrenadeSweep Sweep(FixedVector3 start, FixedVector3 end, bool lineOfSightCheck)
        { Events.AddRange([13, start.X, start.Y, start.Z, end.X, end.Y, end.Z, lineOfSightCheck ? 1 : 0]); return lineOfSightCheck ? Collision : Shadow; }
        public int GroundHeight(FixedVector3 position, int above, int below)
        { Events.AddRange([14, position.X, position.Y, position.Z, above, below]); return Ground; }
        public void SetShadow(bool enabled, FixedVector3 position, ushort scale, short distance) => Events.AddRange([15, enabled ? 1 : 0, position.X, position.Y, position.Z, scale, distance]);
        public void CreateGlow(int sectors, int bands) => Events.AddRange([3, sectors, bands]);
        public void SetGlowSkipTriangles(bool skip) => Events.AddRange([4, skip ? 1 : 0]);
        public void SetGlowFringeWidth(int index, int width) => Events.AddRange([5, index, width]);
        public void SetGlowCentreRgb(byte red, byte green, byte blue) => Events.AddRange([6, red, green, blue]);
        public void SetGlowRgb(byte red, byte green, byte blue) => Events.AddRange([7, red, green, blue]);
        public void SetGlowFringeRgb(int index, byte red, byte green, byte blue) => Events.AddRange([8, index, red, green, blue]);
        public void SetGlowAngle(int angle) => Events.AddRange([9, angle]);
        public void SetGlowRadius(int radius) => Events.AddRange([10, radius]);
        public void SetGlowPosition(FixedVector3 position) => Events.AddRange([11, position.X, position.Y, position.Z]);
        public void SetGlowProtected(bool protect) => Events.AddRange([12, protect ? 1 : 0]);
        public void PlaySound(uint sound, FixedVector3 position, int pitchOffset) => Events.AddRange([16, sound, position.X, position.Y, position.Z, pitchOffset]);
        public void GiveScaledDamageToObjects(GrenadeDamageRequest request) => Events.AddRange([17, (int)request.List, request.Position.X, request.Position.Y, request.Position.Z,
            request.Damage, request.Radius, request.SlideDistance, request.SlideFrames, request.AttackKind]);
        public void GiveScaledDamageToEnviro(FixedVector3 position, ushort damage, ushort radius) => Events.AddRange([18, position.X, position.Y, position.Z, damage, radius]);
        public void CreateGrenadeExplosion(FixedVector3 position) => Events.AddRange([19, position.X, position.Y, position.Z]);
        public void CreateGlowFlash(FixedVector3 position, IReadOnlyList<int> arguments)
        { Events.AddRange([20, position.X, position.Y, position.Z]); Events.AddRange(arguments.Select(value => (long)value)); }
        public void MarkDead() => Events.Add(21);
        public void DestroyGlow() => Events.Add(22);
        public void DetachProjectile() => Events.Add(23);
    }
}
