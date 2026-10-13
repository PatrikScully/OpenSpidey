using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerAirborneMovementTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerAirborneMovementTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-airborne-movement.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            string kind = row.GetProperty("kind").GetString()!;
            var combat = new PlayerCombatState((short)input[0], 100)
            {
                ActionState = kind == "jumping" ? 2u : 4u, AnimationId = (ushort)input[1], AnimationFrame = (short)input[2], FieldEA6 = 77,
                Velocity = new((int)input[7], (int)input[8], (int)input[9]), LastHitAction = (uint)input[14], LastHitClock = (uint)input[15],
                FlailDropChain = (byte)input[17], PendingWebReleaseAnimation = (byte)input[18], FieldAE5 = (byte)input[19], FieldAE6 = (byte)input[20],
                Position = new(12345, (int)input[21], 34567), AirborneTicks = (ushort)input[24], CollisionFlags = (ushort)input[26], FieldE8C = (byte)input[34]
            };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Pressed[16] = (byte)input[10]; melee.Input.Triggered[16] = 122; melee.Input.Mirrors[16] = 123;
            melee.Input.Sideways = (sbyte)input[32]; melee.Input.Forward = (sbyte)input[33];
            var state = new PlayerAirborneMovementState(melee)
            {
                Jump = new((int)input[4], (int)input[5], (int)input[6]), FallReferenceHeight = (int)input[22], FallReferenceTick = (int)input[23],
                FallDamageStartsAt = (int)input[29], FallDamageFullHealthAt = (int)input[30]
            };
            var host = new Host(state, input);
            if (kind == "jumping") PlayerAirborneMovement.UpdateJumping(state, host);
            else if (kind == "falling") PlayerAirborneMovement.UpdateFalling(state, (int)input[25], host);
            else assert(PlayerAirborneLanding.Check(state, host) == (row.GetProperty("result").GetInt32() != 0),
                $"Actual PC landing{index}: exact collision/damage/health result.");
            long[] actual = [combat.ActionState, combat.AnimationId, combat.AnimationFrame, host.AnimationFinished ? 1 : 0, combat.FieldEA6,
                state.Jump.Velocity, state.Jump.Phase, state.Jump.Hold, combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z,
                combat.FlailDropChain, combat.PendingWebReleaseAnimation, combat.FieldAE5, combat.FieldAE6, state.FallReferenceHeight,
                state.FallReferenceTick, host.LevelStatus, combat.Health, combat.FieldE8C, melee.Input.Mirrors[16], melee.Input.Triggered[16]];
            JsonElement expected = row.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Original full {kind}{index}: shared impulse/action/reference/damage and exact E8C/AE5/AE6 fields; actual[{string.Join(',',actual)}], expected[{string.Join(',',wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Original full airborne{index++}: ordered genuine guards, difficulty705 exception, source animation/Hit/haptic callbacks; actual[{string.Join(',',host.Events)}], expected[{string.Join(',',wantedEvents)}].");
        }
    }

    private sealed class Host(PlayerAirborneMovementState state, long[] input) : IPlayerAirborneMovementHost, IPlayerAirborneLandingHost
    {
        private PlayerCombatState Combat => state.Melee.Combat;
        public List<long> Events { get; } = [];
        public int LevelStatus { get; private set; } = 99;
        public uint Clock => (uint)input[16];
        public int Difficulty => (int)input[11];
        public int LevelId { get { Events.Add(11); return (int)input[12]; } }
        public bool AnimationFinished { get; private set; } = input[3] != 0;
        public bool UseLevelSoundAliases => input[27] != 0;
        public bool VibrationEnabled => input[31] != 0;
        private bool Guard(int index) { Events.Add(index); return (input[13] & (1L << index)) != 0; }
        public bool CheckStickToCeiling() => Guard(1);
        public bool CheckJumpingSmashKick() => Guard(2);
        public bool CheckJumpingSwingWeb() => Guard(3);
        public bool CheckJumpingR1ZipWeb() => Guard(4);
        public bool CheckJumpingR2ZipWeb() => Guard(5);
        public bool CheckLanded() => Guard(6);
        public bool CheckStickToWall() => Guard(7);
        public bool ShouldPlayerDropFlail() => Guard(8);
        public void SwitchToDeathMode() => Events.AddRange([9,0]);
        public void SetLevelStatus(int status) => LevelStatus = status;
        public void PlayAnimation(ushort animation)
        { Events.AddRange([10,animation,0,-1]); Combat.AnimationId = animation; Combat.AnimationFrame = 0; AnimationFinished = false; }
        public int Random(int bound) { Events.AddRange([12,bound]); return (int)input[28]; }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([13,sound,position.X,position.Y,position.Z,pitch]);
        public void ActuatorOn(int controller, ushort time, int argument, int strength) => Events.AddRange([14,controller,time,argument,strength]);
        public void HitPlayer(ActorHit hit)
        {
            Events.AddRange([15,hit.Flags,hit.Damage,hit.Direction.X,hit.Direction.Y,hit.Direction.Z]);
            Combat.Health = unchecked((short)(input[0]-1));
        }
    }
}
