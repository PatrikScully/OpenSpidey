using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSenseTests
{
    private sealed record Identity(int Id);
    private sealed class Target(JsonElement source) : IPlayerMeleeTarget
    {
        public object Identity { get; } = new Identity(source[0].GetInt32());
        public FixedVector3 Position => new(source[10].GetInt32(), source[11].GetInt32(), source[12].GetInt32());
        public ushort Type => 304;
        public ushort ItemFlags => source[3].GetUInt16();
        public IPlayerMeleeTarget? Next => null;
    }
    private sealed class Host : IPlayerSenseHost
    {
        private readonly JsonElement[] bodies;
        private readonly PlayerTargetCandidate[] candidates;
        public Host(JsonElement input)
        {
            bodies = input.GetProperty("bodies").EnumerateArray().ToArray();
            candidates = bodies.Select(body => new PlayerTargetCandidate(new Target(body), body[1].GetUInt16(), body[2].GetUInt16(), body[4].GetUInt32(), body[5].GetUInt32(), body[6].GetByte())).ToArray();
            JsonElement point = input.GetProperty("reference"); ReferencePositionWholeUnits = new(point[0].GetInt32(), point[1].GetInt32(), point[2].GetInt32());
            short[] m = input.GetProperty("matrix").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            ReferenceRotation = new(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], 31, 37, 41);
        }
        public uint Clock { get; set; }
        public FixedVector3 ReferencePositionWholeUnits { get; }
        public FixedJointTransform ReferenceRotation { get; }
        public IEnumerable<PlayerTargetCandidate> SourceBaddyOrder => candidates;
        public List<int> Events { get; } = [];
        public PlayerTargetCandidate Get(int index) => candidates[index];
        public uint Validation(int index) => bodies[index][7].GetUInt32();
        public int Count => candidates.Length;
        public PlayerKickHandle MakeBaddyHandle(PlayerTargetCandidate target)
        {
            int index = Array.IndexOf(candidates, target);
            int id = bodies[index][0].GetInt32(); uint validation = Validation(index);
            Events.AddRange([1, id, unchecked((int)validation)]);
            return new(target.Target.Identity, validation);
        }
        public PlayerTargetCandidate? RecoverBaddy(ref PlayerKickHandle handle)
        {
            object? identity = handle.Target;
            int index = Array.FindIndex(candidates, candidate => ReferenceEquals(candidate.Target.Identity, identity));
            JsonElement body = bodies[index]; int id = body[0].GetInt32(), alive = body[8].GetInt32(), clear = body[9].GetInt32();
            Events.AddRange([2, id, unchecked((int)handle.Validation), alive, clear]);
            if (alive != 0) return candidates[index];
            if (clear != 0) handle = handle with { Target = null };
            return null;
        }
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSenseTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-sense.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        uint[] elapsed = [0, 2, 3, 4, 20, 21, 23, 24];
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input");
            var combat = new PlayerCombatState(100, 100) { Field528 = input.GetProperty("count").GetInt32() };
            var quips = new PlayerKillQuips(new PlayerKillQuipHistory()) { EncounterActive = input.GetProperty("encounter").GetByte(), StartingHealth = 73, EncounterClock = 79 };
            var history = new PlayerSenseHistory { RebuildClock = input.GetProperty("previous").GetUInt32(), RefreshClock = input.GetProperty("refresh").GetUInt32() };
            var state = new PlayerSenseState(combat, quips, history) { MaximumTargetDistance = input.GetProperty("maximum").GetUInt32(), MinimumTargetDistance = input.GetProperty("minimum").GetUInt32(), HasCombatTarget = input.GetProperty("has").GetInt32() };
            var host = new Host(input);
            for (int slot = 0; slot < 6; slot++)
            {
                bool occupied = input.GetProperty("preload").GetInt32() != 0 && host.Count != 0;
                state.Indicators[slot].Direction = new(slot + 1, slot + 2, slot + 3);
                state.Indicators[slot].Age = 100 + slot;
                state.Indicators[slot].Handle = new(occupied ? host.Get(slot % host.Count).Target.Identity : null,
                    occupied ? host.Validation(slot % host.Count) : (uint)(70 + slot));
            }
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                host.Clock = unchecked(input.GetProperty("start").GetUInt32() + elapsed[frame]); host.Events.Clear();
                PlayerSenseTargets.Refresh(state, host); PlayerSenseTargets.Rebuild(state, host);
                long[] actual = [history.RebuildClock, history.RefreshClock, combat.Field528, state.MaximumTargetDistance, state.MinimumTargetDistance, state.HasCombatTarget, quips.EncounterActive];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(7).Select(value => value.GetInt64())) &&
                    host.Events.SequenceEqual(expected[8].EnumerateArray().Select(value => value.GetInt32())),
                    $"Sense{index}:{frame} matches original PC unsigned clock throttle, live source enemy count/min/max, encounter retirement, handle generation duplicate check, scan capacity and recovery callback order.");
                for (int slot = 0; slot < 6; slot++)
                {
                    PlayerSenseIndicator indicator = state.Indicators[slot];
                    long[] entry = [indicator.Direction.X, indicator.Direction.Y, indicator.Direction.Z,
                        indicator.Handle.Target is Identity identity ? identity.Id : 0, indicator.Handle.Validation, indicator.Age];
                    assert(entry.SequenceEqual(expected[7][slot].EnumerateArray().Select(value => value.GetInt64())),
                        "Original six slots preserve pointer-only clears, failed recovery semantics, age reset and source whole-unit camera transform with flattened depth.");
                }
                assert(quips.StartingHealth == 73 && quips.EncounterClock == 79, "Target-count retirement preserves the original encounter health and clock snapshot.");
                frame++;
            }
            index++;
        }
    }
}
