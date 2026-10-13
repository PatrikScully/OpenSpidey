using System.Text.Json;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugOwnershipTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugOwnershipTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-ownership.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var events = new List<int>();
            var actor = new Participant(0, input[10], events)
            {
                Position = new(input[5], 0, input[6])
            };
            actor.State.GroundReferenceY = input[8];
            actor.State.AttackOwnershipFlags = (byte)input[2];
            actor.State.AttackSlotMask = (byte)input[3];
            actor.State.AttackSlotCursor = (byte)input[4];
            actor.State.State = 4;
            var other = new Participant(1, input[11], events);
            other.State.GroundReferenceY = input[9];
            other.State.AttackOwnershipFlags = 1;
            other.State.State = input[12];
            var coordinator = new ThugCombatCoordinator
            {
                PrimaryAttacker = input[0] < 0 ? null : input[0] == 0 ? actor : other,
                OccupiedSlots = (uint)input[1]
            };
            bool result = coordinator.AssignAttackPosition(actor, new(0, input[7], 0), input[13]);
            long[] actual = [result ? 1 : 0, (coordinator.PrimaryAttacker as Participant)?.Id ?? -1,
                coordinator.OccupiedSlots, actor.State.AttackOwnershipFlags, actor.State.AttackSlotMask, actor.State.AttackSlotCursor,
                other.State.AttackOwnershipFlags, other.State.AttackSlotMask];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(8).Select(value => value.GetInt64())),
                "Primary attacker replacement, height gates, quadrant slot selection and occupied masks match original source.");
            assert(events.SequenceEqual(expected[8].EnumerateArray().Select(value => value.GetInt32())),
                "Attack ownership preserves distance, fight transition and animation callback order.");
        }

        var first = new Participant(0, 100, []);
        var second = new Participant(1, 500, []);
        var shared = new ThugCombatCoordinator { PrimaryAttacker = first, FightOwner = second, OccupiedSlots = 4 };
        second.State.AttackOwnershipFlags = 2;
        second.State.AttackSlotMask = 4;
        shared.Remove(second);
        assert(shared.PrimaryAttacker == first && shared.FightOwner is null && shared.OccupiedSlots == 0,
            "Actor destruction releases its owned slot and ranged fight owner while retaining the other primary attacker.");
        shared.Remove(first);
        assert(shared.PrimaryAttacker is null, "Deleting the primary attacker clears the shared actor reference.");

        var reassignment = new ThugCombatCoordinator { PrimaryAttacker = second };
        second.State.State = 4;
        second.OnDetermine = () => reassignment.ClearAttackOwnership(second);
        assert(reassignment.AssignAttackPosition(first, default, 0) && reassignment.PrimaryAttacker == first,
            "A fight transition can release the previous primary actor without dereferencing the released reference.");
    }

    private sealed class Participant(int id, int distance, List<int> events) : IThugCombatParticipant
    {
        public int Id => id;
        public ThugState State { get; } = new(id, 0x101);
        public FixedVector3 Position { get; init; }
        public Action? OnDetermine { get; set; }
        public int DistanceToPlayer(int cacheTolerance) { events.AddRange([1, id, cacheTolerance]); return distance; }
        public void DetermineFightState()
        {
            events.AddRange([2, id]);
            OnDetermine?.Invoke();
        }
        public void SetAnimationMode(int mode, bool playAnimation) => events.AddRange([3, id, mode, playAnimation ? 1 : 0]);
    }
}
