using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class ProjectileBlastDamageTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ProjectileBlastDamageTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.projectile-blast.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var events = new List<long>();
            var nodes = new Node[4];
            for (int i = 0; i < nodes.Length; i++) nodes[i] = new(i, Vector(10 + i * 3), events);
            nodes[0].Next = nodes[1]; nodes[1].Next = nodes[2];
            nodes[0].AfterHit = () =>
            {
                switch (input[9])
                {
                    case 1: nodes[0].Next = nodes[0].Next?.Next; break;
                    case 2: nodes[3].Next = nodes[0].Next; nodes[0].Next = nodes[3]; break;
                    case 3:
                        if (nodes[0].Next is { } next) next.Position = Vector(1) + new FixedVector3(8192, 0, 0);
                        break;
                }
            };
            if (input[0] == 0)
                ProjectileBlastDamage.GiveScaledDamageToObjects(Vector(1), (ushort)input[4], (int)input[5], (int)input[6], (int)input[7], (int)input[8], nodes[0]);
            else ProjectileBlastDamage.GiveScaledDamageToEnviro(Vector(1), (ushort)input[4], (int)input[5], nodes[0]);
            long[] expected = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(events.SequenceEqual(expected), $"Projectile blast source case {caseIndex++}: input [{string.Join(',', input)}], events [{string.Join(',', events)}], expected [{string.Join(',', expected)}]");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }

        // Modern crash fix, deliberately separate from source-valid gold cases:
        // the original divides by zero for coincident or truncated subunit range.
        foreach (FixedVector3 position in new[] { default(FixedVector3), new FixedVector3(4095, 0, 0) })
        {
            var events = new List<long>();
            var center = new Node(0, position, events);
            var skipped = new Node(1, new(8192, 0, 0), events);
            var survivor = new Node(2, new(12288, 0, 0), events);
            center.Next = skipped; skipped.Next = survivor;
            center.AfterHit = () => center.Next = survivor;
            ProjectileBlastDamage.GiveScaledDamageToObjects(default, 40, 100, 100, 10, 29, center);
            assert(center.Health == 60 && center.Hits.Count == 1, "Zero-range blast delivers full health damage");
            ActorHit hit = center.Hits.Single();
            assert(hit.Flags == 6 && hit.Direction == default && hit.SlideDistance == 0 && hit.SlideFrames == 1 && !center.ImpulseRequested,
                "Zero-range blast has no direction or knockback flag");
            assert(skipped.Hits.Count == 0 && survivor.Hits.Count == 1, "Zero-range damage callback preserves source next-link mutation");
        }
        var untouched = new Node(0, default, []);
        bool rejected = false;
        try { ProjectileBlastDamage.GiveScaledDamageToObjects(default, 40, 0, 100, 10, 29, untouched); }
        catch (ArgumentOutOfRangeException exception) { rejected = exception.ParamName == "radius"; }
        assert(rejected && untouched.Health == 100 && untouched.Hits.Count == 0, "Zero blast radius rejects before any target hit");
    }

    private sealed class Node(int id, FixedVector3 position, List<long> events) : IProjectileBlastBody, IProjectileBlastEnvironment
    {
        public FixedVector3 Position { get; set; } = position;
        public Node? Next { get; set; }
        public IProjectileBlastBody? NextBody => Next;
        public IProjectileBlastEnvironment? NextEnvironment => Next;
        public Action? AfterHit { get; set; }
        public int Health { get; private set; } = 100;
        public bool ImpulseRequested { get; private set; }
        public List<ActorHit> Hits { get; } = [];
        public void Hit(ActorHit hit)
        {
            events.AddRange([1, id, hit.Flags, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]);
            Hits.Add(hit);
            Health -= hit.Damage;
            ImpulseRequested |= (hit.Flags & 8) != 0;
            AfterHit?.Invoke();
        }
        public void HitEnvironment(ushort damage)
        { events.AddRange([2, id, damage, 0]); AfterHit?.Invoke(); }
    }
}
