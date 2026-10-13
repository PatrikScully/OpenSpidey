using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugFightTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugFightTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-fight.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                Health = (short)input[2], State = (int)input[12], Substate = (int)input[13], AlertTicks = (int)input[14],
                FightSuppressionTicks = (int)input[15], RangedCooldownTicks = (int)input[16], FightPollInterval = (int)input[17],
                ClosePlayerTicks = (int)input[18], DistanceSampleClock = (int)input[19], CachedPlayerDistance = (int)input[20],
                BodyFlags = (uint)input[23], LastSeenPlayerPosition = Vector(36)
            };
            state.SetThugType((int)input[1]);
            state.SetParamByIndex(1, (int)input[24]);
            state.SetParamByIndex(2, (int)input[25]);
            state.SetParamByIndex(3, (int)input[26]);
            state.SetParamByIndex(4, (int)input[27]);
            state.SetParamByIndex(6, (int)input[28]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(29), Angles = new(0, (short)input[32], 0)
            }) { BehaviorFlags = (uint)input[22] };
            var host = new Host(state, actor)
            {
                Player = new(Vector(33), (short)input[3], (uint)input[4], (int)input[5], input[6] != 0),
                HasFightOwner = input[7] != 0, GlobalOwner = input[8] != 0, AttackMask = (uint)input[9],
                AttackFlags = (byte)input[10], OwnedMask = (byte)input[11], AttackClock = (int)input[21],
                LineClear = input[39] != 0, TargetPathResult = (int)input[40], LastPathResult = (int)input[41],
                FirstAddResult = input[42] != 0, SecondAddResult = input[43] != 0
            };
            int result = (int)input[0] switch
            {
                0 => ThugFightBehavior.Determine(state, actor, host) ? 1 : 0,
                1 => ThugPerception.PlayerIsVisible(state, actor, host) ? 1 : 0,
                2 => ThugPerception.CheckSightCone(state, actor.Body, host.Player, host.AttackClock) ? 1 : 0,
                _ => ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, (int)input[0] - 1)
            };
            long[] actual = [result, state.State, state.Substate, state.AlertTicks, state.FightPollInterval,
                state.ClosePlayerTicks, state.DistanceSampleClock, state.CachedPlayerDistance, actor.BehaviorFlags,
                state.BodyFlags, state.LastSeenPlayerPosition.X, state.LastSeenPlayerPosition.Y, state.LastSeenPlayerPosition.Z,
                state.PathPointIndex, host.HasFightOwner ? 1 : 0, host.GlobalOwner ? 1 : 0, host.AttackMask,
                host.AttackFlags, host.OwnedMask];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(19).Select(value => value.GetInt64())),
                "Thug fight transitions, visibility memory, eligibility, strict range bounds and signed cache clock match recovered C++.");
            assert(host.Events.SequenceEqual(expected[19].EnumerateArray().Select(value => value.GetInt32())),
                "Fight visibility/path/add-point queries, neutralization and signal callbacks preserve source order and short circuits.");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private sealed class Host(ThugState state, ActorProcedureState actor) : IThugFightHost
    {
        private int adds;
        public ThugPlayerState Player { get; init; }
        public int AttackClock { get; init; }
        public bool HasFightOwner { get; set; }
        public bool GlobalOwner { get; set; }
        public uint AttackMask { get; set; }
        public byte AttackFlags { get; set; }
        public byte OwnedMask { get; set; }
        public bool LineClear { get; init; }
        public int TargetPathResult { get; init; }
        public int LastPathResult { get; init; }
        public bool FirstAddResult { get; init; }
        public bool SecondAddResult { get; init; }
        public List<int> Events { get; } = [];
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags)
        {
            Events.Add(1);
            return LineClear;
        }
        public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance)
        {
            Events.AddRange([2, to.X, to.Y, to.Z, clearance]);
            return to == state.LastSeenPlayerPosition ? LastPathResult : TargetPathResult;
        }
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            bool result = adds++ == 0 ? FirstAddResult : SecondAddResult;
            Events.AddRange([3, point.X, point.Y, point.Z, argument, result ? 1 : 0]);
            if (result) state.PathPointIndex++;
            return result;
        }
        public void SetFightOwner() => HasFightOwner = true;
        public void ClearAttackOwnership()
        {
            if (GlobalOwner) GlobalOwner = false;
            else if ((AttackFlags & 2) != 0) AttackMask &= ~(uint)OwnedMask;
            AttackFlags = 0;
            OwnedMask = 0;
        }
        public void Neutralize()
        {
            Events.Add(4);
            actor.Neutralize(new ActorProcedureList());
        }
        public void SendSignal() => Events.Add(5);
    }
}
