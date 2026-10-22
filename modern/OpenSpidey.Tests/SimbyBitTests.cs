using System.Text.Json;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

/// <summary>Replays tools/reference_simby_bit_vectors.py captures of the original Simby slime bits and SplattyDeath blobs.</summary>
internal static class SimbyBitTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SimbyBitTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.simby-bits.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, hits = 0, bursts = 0, splats = 0;
        int[] perKind = new int[7];
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int kind = test.GetProperty("kind").GetInt32();
            int[] header = Ints(test.GetProperty("header"));
            int[] queues = Ints(test.GetProperty("queues"));
            long[][] states = test.GetProperty("states").EnumerateArray().Select(Longs).ToArray();
            long[][] events = test.GetProperty("events").EnumerateArray().Select(Longs).ToArray();
            (List<long[]> actualStates, List<long[]> actualEvents, long texture) = Replay(kind, header, queues);
            long[] textures = events.Where(e => e[0] == 9).Select(e => e[1]).ToArray();
            events = events.Where(e => e[0] != 9).ToArray();
            assert(textures.Length == (texture < 0 ? 0 : 1) && (texture < 0 || textures[0] == texture),
                $"Actual PC Simby bit case {index} kind{kind}: CQuadBit::SetTexture slot/frame {texture} expected [{string.Join(',', textures)}].");
            bool statesMatch = actualStates.Count == states.Length && actualStates.Zip(states).All(pair => pair.First.SequenceEqual(pair.Second));
            bool eventsMatch = actualEvents.Count == events.Length && actualEvents.Zip(events).All(pair => pair.First.SequenceEqual(pair.Second));
            assert(statesMatch && eventsMatch,
                $"Actual PC Simby bit case {index} kind{kind}: states {(statesMatch ? "match" : Describe(actualStates, states))}; " +
                $"events {(eventsMatch ? "match" : Describe(actualEvents, events))}.");
            perKind[kind]++;
            if (events.Any(e => e[0] == 5)) hits++;
            if (kind == 0 && events.Any(e => e[0] == 10 && e[1] == 1)) bursts++;
            if (kind == 1 && events.Any(e => e[0] == 10 && e[1] == 2)) splats++;
            index++;
        }
        assert(perKind.All(count => count >= 100) && hits >= 20 && bursts >= 20 && splats >= 50,
            "Simby bit fixture covers shots hitting the player and bursting on walls, drops splatting, both splats, MakeDroplets, SplattyDeath and blobs.");
    }

    private static (List<long[]> States, List<long[]> Events, long Texture) Replay(int kind, int[] h, int[] q)
    {
        var host = new Host(h, q);
        var states = new List<long[]>();
        FixedVector3 a = new(h[8], h[9], h[10]), b = new(h[11], h[12], h[13]);
        SimbyQuadBit? bit = kind switch
        {
            0 => new SimbyShot(a, host),
            1 => new SimbyDrop(a, b, h[14], h[15], host),
            2 => new SimbyDropSplat(a, h[14], host),
            3 => new SimbyShotSplat(a, b, host),
            _ => null
        };
        if (kind == 4) SimbyDrop.MakeDroplets(a, h[14], host);
        else if (kind == 5)
        {
            SimbySplattyDeath.Run(a, part =>
            {
                host.Events.Add([12, part, 0, 0, 0]);
                return a + new FixedVector3(part * 4096 * 37, -part * 4096 * 11, part * 4096 * 5);
            }, new HookRecorder(host));
            // Simby_SplattyDeath's caller-visible mFlags |= 1 is applied by GodotSimbyActor before Run.
            states.Add([1]);
        }
        else if (kind == 6)
        {
            // InitItem("symblob") and AttachTo(BulletList) precede the constructor's random draws.
            host.Events.Add([13, 1]);
            host.Events.Add([14, 1]);
            var blob = new SimbyBlob(a, b, h[14], host.Random);
            states.Add(BlobSnapshot(blob));
            for (int step = 0; step < h[0] && !blob.Dead; step++)
            {
                blob.Move(host.Random);
                if (blob.Dead) host.Events.Add([11]);
                states.Add(BlobSnapshot(blob));
            }
        }
        else if (bit is not null)
        {
            states.Add(Snapshot(kind, bit));
            for (int step = 0; step < h[0] && !bit.Dead; step++)
            {
                bit.Move(host);
                states.Add(Snapshot(kind, bit));
            }
        }
        foreach (GlowFlashState glow in host.Glows) host.Events.Add([8, unchecked((short)glow.Angle)]);
        // A shot built without a player never reaches SetTexture.
        long texture = bit is null || bit is SimbyShot { Dead: true, Length: 0 } && !host.PlayerPresent ? -1 : (int)bit.Texture << 8 | bit.TextureFrame;
        return (states, host.Events, texture);
    }

    private static long[] BlobSnapshot(SimbyBlob blob) =>
    [
        blob.Position.X, blob.Position.Y, blob.Position.Z, blob.Velocity.X, blob.Velocity.Y, blob.Velocity.Z,
        blob.AngleX, blob.AngleY, blob.AngleZ, blob.ScaleX, blob.ScaleY, blob.ScaleZ, blob.SpinX, blob.SpinY, blob.SpinZ,
        blob.GroundY, blob.Age, blob.Landed ? 1 : 0, 0x200, blob.Dead ? 1 : 0
    ];

    /// <summary>Records each blob's unk_5555C8 part (-1 for the body blobs) as the patched constructor saw it.</summary>
    private sealed class HookRecorder(Host host) : ISimbyBitHost
    {
        private int blobs;
        public bool PlayerPresent => host.PlayerPresent;
        public FixedVector3 PlayerPosition => host.PlayerPosition;
        public FixedVector3 CameraPosition => host.CameraPosition;
        public int Random(int bound) => host.Random(bound);
        public SimbyLineHit? LineToItem(FixedVector3 start, FixedVector3 end) => host.LineToItem(start, end);
        public SimbyGround GroundY(FixedVector3 position) => host.GroundY(position);
        public FixedVector3? LineToPlayer(FixedVector3 from, FixedVector3 to) => host.LineToPlayer(from, to);
        public void HitPlayer(ActorHit hit) => host.HitPlayer(hit);
        public void PlaySound(int sound, FixedVector3 position) => host.PlaySound(sound, position);
        public GlowFlashState CreateGlowFlash(GlowFlashParameters parameters) => host.CreateGlowFlash(parameters);
        public void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int itemVelocityY) =>
            host.SpawnDrop(position, velocity, groundY, itemVelocityY);
        public void SpawnShotSplat(FixedVector3 position, FixedVector3 normal) => host.SpawnShotSplat(position, normal);
        public void SpawnDropSplat(FixedVector3 position, int itemVelocityY) => host.SpawnDropSplat(position, itemVelocityY);
        public void SpawnBlob(FixedVector3 position, FixedVector3 velocity, int groundY)
        {
            int part = blobs < SimbySplattyDeath.Parts.Length ? SimbySplattyDeath.Parts[blobs] : -1;
            blobs++;
            host.Events.Add([10, 3, part, position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z, groundY]);
        }
    }

    private static long[] Snapshot(int kind, SimbyQuadBit bit)
    {
        var s = new List<long>();
        foreach (FixedVector3 corner in bit.Corners) s.AddRange([corner.X, corner.Y, corner.Z]);
        s.AddRange([bit.RenderFlags, bit.ColourCode, bit.Age, bit.Dead ? 1 : 0]);
        switch (bit)
        {
            case SimbyShot shot:
                s.AddRange([shot.HitEnvironment ? 1 : 0, shot.Normal.X, shot.Normal.Y, shot.Normal.Z, shot.Origin.X, shot.Origin.Y,
                    shot.Origin.Z, shot.Direction.X, shot.Direction.Y, shot.Direction.Z, shot.Back, shot.Front, shot.Length]);
                break;
            case SimbyDrop drop:
                s.AddRange([drop.Velocity.X, drop.Velocity.Y, drop.Velocity.Z, drop.NeedsGround ? 1 : 0, drop.GroundY, drop.ItemVelocityY]);
                break;
            case SimbySurfaceSplat splat:
                s.AddRange([splat is SimbyDropSplat dropSplat ? dropSplat.VelocityY : 0, splat.TargetScale, splat.Scale,
                    splat.Center.X, splat.Center.Y, splat.Center.Z, splat.AxisU.X, splat.AxisU.Y, splat.AxisU.Z,
                    splat.AxisV.X, splat.AxisV.Y, splat.AxisV.Z]);
                break;
        }
        _ = kind;
        return s.ToArray();
    }

    private sealed class Host : ISimbyBitHost
    {
        private readonly int[] random, ground, item, sphere;
        private int randomIndex, groundIndex, itemIndex, sphereIndex;
        public List<long[]> Events { get; } = [];
        public List<GlowFlashState> Glows { get; } = [];
        public bool PlayerPresent { get; }
        public FixedVector3 PlayerPosition { get; }
        public FixedVector3 CameraPosition { get; }

        public Host(int[] h, int[] q)
        {
            CameraPosition = new(h[1], h[2], h[3]);
            PlayerPresent = h[4] != 0;
            PlayerPosition = new(h[5], h[6], h[7]);
            int cursor = 0;
            random = Take(q, ref cursor, 1);
            ground = Take(q, ref cursor, 3);
            item = Take(q, ref cursor, 7);
            sphere = Take(q, ref cursor, 4);
        }

        private static int[] Take(int[] q, ref int cursor, int width)
        {
            int count = q[cursor++] * width;
            int[] values = q[cursor..(cursor + count)];
            cursor += count;
            return values;
        }

        public int Random(int bound)
        {
            int value = random[randomIndex++ % random.Length];
            Events.Add([1, bound]);
            return bound != 0 ? (int)((uint)value % (uint)bound) : 0;
        }

        public SimbyGround GroundY(FixedVector3 position)
        {
            int k = groundIndex++ % (ground.Length / 3) * 3;
            Events.Add([2, position.X, position.Y, position.Z]);
            return new(ground[k], ground[k + 1] == 1 ? ground[k + 2] : null);
        }

        public SimbyLineHit? LineToItem(FixedVector3 start, FixedVector3 end)
        {
            int k = itemIndex++ % (item.Length / 7) * 7;
            Events.Add([3, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 0, 1]);
            if (item[k] == 0) return null;
            return new(new(item[k + 1], item[k + 2], item[k + 3]),
                unchecked((short)item[k + 4]), unchecked((short)item[k + 5]), unchecked((short)item[k + 6]));
        }

        public FixedVector3? LineToPlayer(FixedVector3 from, FixedVector3 to)
        {
            int k = sphereIndex++ % (sphere.Length / 4) * 4;
            Events.Add([4, from.X, from.Y, from.Z, to.X, to.Y, to.Z, 1, 0, 4096]);
            return sphere[k] == 0 ? null : new FixedVector3(sphere[k + 1], sphere[k + 2], sphere[k + 3]);
        }

        public void HitPlayer(ActorHit hit) =>
            Events.Add([5, 1, hit.Flags, hit.AttackKind, unchecked((short)hit.Damage), hit.Direction.X, hit.Direction.Y, hit.Direction.Z]);
        public void PlaySound(int sound, FixedVector3 position) => Events.Add([6, sound, position.X, position.Y, position.Z, 0]);

        public GlowFlashState CreateGlowFlash(GlowFlashParameters p)
        {
            Events.Add([7, p.Position.X, p.Position.Y, p.Position.Z, p.Points, p.CentreRed, p.CentreGreen, p.CentreBlue, p.CentreFadeRate,
                p.SectionRed, p.SectionGreen, p.SectionBlue, p.SectionFadeRate, p.FringeWidth, p.WidthDelta, p.ReselectRadii,
                p.OddRadius, p.OddRange, p.EvenRadius, p.EvenRange, p.RadiusDecay, p.Lifetime]);
            // The capture replaces CGlowFlash's constructor, so its radius draws stay off the scripted Rand queue.
            var glow = new GlowFlashState(p, _ => 0);
            Glows.Add(glow);
            return glow;
        }

        public void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int itemVelocityY) =>
            Events.Add([10, 0, position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z, groundY, itemVelocityY]);
        public void SpawnShotSplat(FixedVector3 position, FixedVector3 normal) =>
            Events.Add([10, 1, position.X, position.Y, position.Z, unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z)]);
        public void SpawnDropSplat(FixedVector3 position, int itemVelocityY) =>
            Events.Add([10, 2, position.X, position.Y, position.Z, itemVelocityY]);
        public void SpawnBlob(FixedVector3 position, FixedVector3 velocity, int groundY) =>
            throw new InvalidOperationException("Only Simby_SplattyDeath spawns blobs.");
    }

    private static string Describe(List<long[]> actual, long[][] expected)
    {
        for (int i = 0; i < Math.Min(actual.Count, expected.Length); i++)
            if (!actual[i].SequenceEqual(expected[i]))
                return $"first difference at {i}: [{string.Join(',', actual[i])}] expected [{string.Join(',', expected[i])}]";
        return $"count {actual.Count} expected {expected.Length}";
    }

    private static int[] Ints(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static long[] Longs(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt64()).ToArray();
}
