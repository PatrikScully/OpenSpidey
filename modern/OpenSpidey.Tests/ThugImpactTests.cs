using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ThugImpactTests
{
    private sealed class Host(uint seed) : IThugBulletImpactHost
    {
        private uint randomState = seed;
        public List<int> Events { get; } = [];
        public List<ThugLaserPingState> LaserPings { get; } = [];
        public List<ThugAttachedPingState> AttachedPings { get; } = [];
        public FixedVector3 HookPosition;
        public int Update, ValidUntil = 99;
        public bool Collision;
        public readonly object Actor = new(), Handle = new();
        public int Random(int range)
        {
            Events.AddRange([1, range]);
            randomState = unchecked(randomState * 1664525 + 1013904223);
            return range == 0 ? 0 : (int)(randomState % (uint)range);
        }
        public object StoreImpactHandle(object actor) { Events.Add(3); return Handle; }
        public object? RecoverImpactHandle(object handle) { Events.Add(4); return Update < ValidUntil ? Actor : null; }
        public FixedVector3 GetDynamicHookPosition(object actor, ActorHook hook)
        {
            Events.AddRange([5, hook.X, hook.Y, hook.Z, hook.PartIndex]); return HookPosition;
        }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) => Events.AddRange([2, sound, position.X, position.Y, position.Z, volume]);
        public bool CollideWithActor(object actor, FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook)
        {
            Events.AddRange([6, scale]); hook = new(11, -13, 17, 3); return Collision;
        }
        public void EmitLaserPing(ThugLaserPingState ping) => LaserPings.Add(ping);
        public void EmitAttachedPing(ThugAttachedPingState ping) => AttachedPings.Add(ping);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugImpactTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-impact.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var host = new Host((uint)input[1]);
            if (input[0] == 2)
            {
                ThugPingPixel[]? actual = ThugPingProjection.Project(new(-12, -12, 24, 24, 0x7D3FEC27), (ushort)input[2], (short)input[1], (int)input[3], (int)input[4], (int)input[5]);
                assert(actual is not null && actual.SelectMany(pixel => new[] { pixel.X, pixel.Y }).SequenceEqual(expected.EnumerateArray().SelectMany(pixel => pixel.EnumerateArray().Select(value => value.GetInt32()))),
                    "Attached ping rotation retains source Q12 heading table, unsigned packed offsets, scale perspective and12-bit corner wrapping.");
            }
            else if (input[0] == 0)
            {
                ThugBulletImpact.CreateRicochet(new(Vector(5), Vector(8), Vector(11), Vector(14)), host, host.Random, (byte)input[2], (byte)input[3], (byte)input[4]);
                assert(host.Events.SequenceEqual(expected[0].EnumerateArray().Select(value => value.GetInt32())) && host.LaserPings.Count == 2,
                    $"Ricochet{index} preserves sound random order and two original reflected impact planes.");
                for (int pingIndex = 0; pingIndex < 2; pingIndex++)
                {
                    var ping = host.LaserPings[pingIndex];
                    JsonElement reference = expected[1][pingIndex];
                    assert(ping.Origin == ReadVector(reference[0]) && ping.Direction == ReadVector(reference[1]) && ping.Perpendicular == ReadVector(reference[2]),
                        "Ricochet preserves original normalize/reflection, smallest-axis ties, wrapping GTE cross and plane order.");
                    int update = 0;
                    foreach (JsonElement frame in reference[3].EnumerateArray())
                    {
                        if (update++ != 0) ping.Advance();
                        long[] actual = [ping.Spread, ping.Alive ? 0 : 1, ping.Tint, ping.TextureAnimationIndex, ping.TextureFrame, ping.SourceType];
                        assert(actual.SequenceEqual(frame.EnumerateArray().Take(6).Select(value => value.GetInt64())) &&
                            ping.Position == ReadVector(frame[6]) && ping.PositionB == ReadVector(frame[7]) && ping.PositionC == ReadVector(frame[8]) && ping.PositionD == ReadVector(frame[9]),
                            "Laser ping preserves machine-confirmed world quad motion, strict fifth-update death and retained last corners.");
                    }
                }
            }
            else
            {
                host.ValidUntil = (int)input[5]; host.HookPosition = Vector(6);
                var ping = new ThugAttachedPingState(host.Actor, new((short)input[9], (short)input[10], (short)input[11], (ushort)input[12]), host, host.Random);
                int update = 0;
                foreach (JsonElement frame in expected.EnumerateArray())
                {
                    if (update != 0)
                    {
                        host.Update = update; host.HookPosition += new FixedVector3(11, -13, 17); host.Events.Clear(); ping.Advance();
                    }
                    long[] actual = [ping.Position.X, ping.Position.Y, ping.Position.Z, ping.CodeRgb, ping.Angle, ping.Scale, ping.Frame, ping.Alive ? 0 : 1];
                    assert(actual.SequenceEqual(frame.EnumerateArray().Take(8).Select(value => value.GetInt64())) && host.Events.SequenceEqual(frame[8].EnumerateArray().Select(value => value.GetInt32())),
                        $"Attached ping{index} update{update} matches original handle/hook query order, signed angle, fixed frame/scale, RGB fade and invalid-handle death.");
                    update++;
                }
            }
            index++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
        var combined = new Host(0) { Collision = true };
        ThugBulletImpact.Create(default, new(4096000, 0, 0), combined.Actor, new(default, new(4096000, 0, 0), default, new(4096, 0, 0)), combined, combined.Random, 255, 128, 0);
        int collision = combined.Events.IndexOf(6);
        assert(combined.LaserPings.Count == 2 && combined.AttachedPings.Count == 1 && collision > combined.Events.IndexOf(2) && combined.Events[collision + 1] == 4096 && combined.Events[collision + 2] == 3,
            "Tracer impact construction performs line ricochet before posed-box4096 query, then stores attached hook handle before ribbon construction.");
        combined.Collision = false; combined.AttachedPings.Clear(); combined.Events.Clear();
        ThugBulletImpact.Create(default, default, combined.Actor, null, combined, combined.Random, 100, 100, 255);
        assert(combined.AttachedPings.Count == 0 && combined.Events.SequenceEqual([6, 4096]), "Body attachment misses emit no fake impact or random draws.");
    }
    private static FixedVector3 ReadVector(JsonElement vector) => new(vector[0].GetInt32(), vector[1].GetInt32(), vector[2].GetInt32());
}
