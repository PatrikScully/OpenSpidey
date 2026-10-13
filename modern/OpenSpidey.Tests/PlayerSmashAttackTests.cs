using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSmashAttackTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSmashAttackTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-smash-action.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { Position = Vector(0), Velocity = new(12345, -23456, 34567),
                CrawlSurfaceNormal = new(111, -222, 333), Field8DC = (int)input[6], AnimationId = (ushort)input[7],
                LastHitAction = (uint)input[11], LastHitClock = (uint)input[12], CollisionFlags = (ushort)input[14],
                FieldAE5 = (byte)input[19], ActionState = (uint)input[22], FieldEA6 = 456 };
            var state = new PlayerSmashAttackState(new(combat)) { TargetPosition = Vector(3), Field8D8 = (byte)input[15],
                EntryClock = (int)input[16], PreviousEntryClock = (int)input[17], FieldAE4 = (byte)input[18], FieldE8C = (byte)input[20] };
            var host = new Host(combat, (uint)input[13], input[9] != 0, input[10] != 0);
            PlayerSmashAttackBehavior.Step(state, input[8] != 0, host);
            long[] actual = [combat.FieldEA6, combat.Field8DC, state.FieldAE4, combat.FieldAE5, state.FieldE8C, input[21],
                combat.ActionState, combat.AnimationId, host.FirstTrailPresent ? 1 : 0, host.SecondTrailPresent ? 1 : 0,
                combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z,
                combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC smash action[{string.Join(',', input)}] recovery, short normals, hit velocity, signed clock and byte fields; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC smash action[{string.Join(',', input)}] complete trail/pose/animation/stand boundary order; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
        VerifyContacts(assert);
    }

    private static void VerifyContacts(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSmashAttackTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-smash-contacts.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { Position = Vector(7), Velocity = new(12345, -23456, 34567),
                ForwardDirection = Vector(10), Field528 = (int)input[13], ActionState = (uint)input[15],
                Field534 = (int)input[16], Field52C = (int)input[17] };
            var state = new PlayerSmashAttackState(new(combat)) { Field8D8 = (byte)input[14] };
            var host = new ContactHost((int)input[0], (int)input[1], (ushort)input[2], (ushort)input[3]);
            PlayerSmashAttackContacts.Resolve(state, Vector(4), host);
            long[] actual = [state.Field8D8, combat.Field534, combat.Field52C, combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC smash contact[{string.Join(',', input)}] source type table, single-hit latch and wrapped knockback timer; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC smash contact[{string.Join(',', input)}] extended movement ray, ignored305/315 identity, exact SHitInfo and positional sound; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private sealed class ContactTarget(ushort type, int index) : IPlayerMeleeTarget
    {
        public int Index => index;
        public object Identity => this;
        public FixedVector3 Position => default;
        public ushort Type => type;
        public ushort ItemFlags => 0;
        public IPlayerMeleeTarget? Next => null;
    }
    private sealed class ContactHost(int contacts, int damage, ushort first, ushort second) : IPlayerSmashContactHost
    {
        private readonly ContactTarget[] targets = [new(first, 1), new(second, 2)];
        private int index;
        public List<long> Events { get; } = [];
        public PlayerSmashSphereContact? TraceBaddies(FixedVector3 start, FixedVector3 end, object? ignoredTarget)
        {
            Events.AddRange([1, start.X, start.Y, start.Z, end.X, end.Y, end.Z, ignoredTarget is ContactTarget target ? target.Index : 0, 4096]);
            return index >= contacts ? null : new(targets[index++], start + new FixedVector3(111, 222, 333));
        }
        public int GetDamage(int baseDamage) { Events.AddRange([2, baseDamage]); return damage; }
        public void HitTarget(IPlayerMeleeTarget target, ActorHit hit) => Events.AddRange([3, ((ContactTarget)target).Index, hit.Flags,
            hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]);
        public void PlayPositionSound(ushort sound, FixedVector3 position) => Events.AddRange([4, sound, position.X, position.Y, position.Z, 0]);
    }

    private sealed class Host(PlayerCombatState combat, uint clock, bool first, bool second) : IPlayerSmashAttackHost
    {
        public uint Clock => clock;
        public bool FirstTrailPresent { get; private set; } = first;
        public bool SecondTrailPresent { get; private set; } = second;
        public List<long> Events { get; } = [];
        public void CreateSmashAttackTrails() { Events.Add(1); FirstTrailPresent = SecondTrailPresent = true; }
        public void OrientToNormal(bool targetPresent, FixedVector3 target) => Events.AddRange([2, targetPresent ? 1 : 0, target.X, target.Y, target.Z]);
        public void PlaySingleAnimation(ushort animation) { Events.AddRange([3, animation, 0, -1]); combat.AnimationId = animation; }
        public void FadeSmashAttackTrails() { Events.Add(4); FirstTrailPresent = SecondTrailPresent = false; }
        public void SwitchToStandMode() => Events.Add(5);
    }
}
