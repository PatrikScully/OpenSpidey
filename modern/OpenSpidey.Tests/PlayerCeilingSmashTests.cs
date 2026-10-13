using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerCeilingSmashTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerCeilingSmashTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-ceiling-smash.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { Position = new(12345, -23456, 34567), ActionState = (uint)input[10],
                AnimationId = (ushort)input[11], LookaroundActive = input[4] != 0, Field8E8 = (byte)input[5], Field8E9 = (byte)input[6],
                IsCrawling = input[7] != 0, CrawlSurfaceNormal = new(123, -456, 789), FieldAE4 = 173, Field8DC = 99 };
            var melee = new PlayerMeleeState(combat);
            melee.AimTarget = input[3] == 0 ? null : new Target(new((int)input[0], (int)input[1], (int)input[2]));
            melee.Input.Triggered[18] = (byte)input[8]; melee.Input.Triggered[19] = (byte)input[9];
            var state = new PlayerSmashAttackState(melee) { EntryClock = 1000, PreviousEntryClock = 900, TargetPosition = new(31, -32, 33), Field8D8 = 77 };
            var host = new Host(combat, unchecked((uint)input[12]));
            bool accepted = PlayerAirborneAttackSelection.CheckCeilingSmash(state, host);
            long[] actual = [accepted ? 1 : 0, combat.ActionState, combat.AnimationId, combat.FieldAE4,
                combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z, combat.IsCrawling ? 1 : 0,
                combat.Field8DC, state.EntryClock, state.PreviousEntryClock, state.TargetPosition.X, state.TargetPosition.Y,
                state.TargetPosition.Z, state.Field8D8, melee.Input.Triggered[18], melee.Input.Triggered[19], host.SoundEntryPresent ? 1 : 0];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC CeilingSmash[{string.Join(',', input)}] exact source1000000 fields and untouched18/19 edges; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC CeilingSmash[{string.Join(',', input)}] original orientation/torso/animation sequence, deferred trails and no swing release; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
        }
    }

    private sealed class Target(FixedVector3 position) : IPlayerMeleeTarget
    {
        public object Identity => this;
        public FixedVector3 Position => position;
        public ushort Type => 304;
        public ushort ItemFlags => 0;
        public IPlayerMeleeTarget? Next => null;
    }
    private sealed class Host(PlayerCombatState combat, uint clock) : IPlayerCeilingSmashSelectionHost
    {
        public uint Clock => clock;
        public List<long> Events { get; } = [];
        public bool SoundEntryPresent { get; private set; } = true;
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Events.AddRange([1, useTarget ? 1 : 0, target.X, target.Y, target.Z]);
        public void SetTargetTorsoAngle(FixedVector3 target) => Events.AddRange([2, target.X, target.Y, target.Z]);
        public void SelectAndRunAnimation(ushort animation)
        { SoundEntryPresent = false; combat.AnimationId = animation; Events.AddRange([3, animation, 0, -1]); }
    }
}
