using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerAirborneEntryTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerAirborneEntryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-airborne-entry.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            {
                LookaroundActive = input[2] != 0, HeldObjectPresent = input[3] != 0, IsCrawling = input[4] != 0,
                Field8E8 = (byte)input[5], Field8E9 = (byte)input[6], ActionState = (uint)input[7],
                CollisionFlags = (ushort)input[8], AnimationId = (ushort)input[9],
                CrawlSurfaceNormal = new((int)input[19], (int)input[20], (int)input[21]),
                Position = new((int)input[22], (int)input[23], (int)input[24]),
                UpDirection = new((int)input[25], (int)input[26], (int)input[27]),
                ForwardDirection = new((int)input[28], (int)input[29], (int)input[30]),
                FieldE8C = (byte)input[32], FieldAE5 = (byte)input[33], FieldAE6 = (byte)input[34],
                FallReferenceHeight = (int)input[36], FallReferenceTick = (int)input[37]
            };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Triggered[16] = (byte)input[0]; melee.Input.Mirrors[16] = (byte)input[1];
            melee.Input.Sideways = (sbyte)input[10]; melee.Input.Forward = (sbyte)input[11];
            var lookaround = new PlayerLookaroundState(combat) { JumpReleased = (byte)input[35] };
            var host = new Host(combat, input);
            string kind = row.GetProperty("kind").GetString()!;
            bool result = kind == "jump" ? PlayerAirborneEntrySelection.CheckJump(melee, lookaround, host) :
                PlayerAirborneEntrySelection.CheckGroundGone(melee, host);
            long[] actual = [result ? 1 : 0, melee.Input.Triggered[16], melee.Input.Mirrors[16], combat.HeldObjectPresent ? 1 : 0,
                combat.IsCrawling ? 1 : 0, combat.Field8E8, combat.Field8E9, combat.ActionState, combat.CollisionFlags, combat.AnimationId,
                host.Jump.Velocity, host.Jump.Phase, host.Jump.Hold, host.SpeedRamp,
                combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z,
                combat.Position.X, combat.Position.Y, combat.Position.Z, host.GroundContactGrace,
                combat.FieldE8C, combat.FieldAE5, combat.FieldAE6, lookaround.JumpReleased, combat.FallReferenceHeight, combat.FallReferenceTick];
            JsonElement expected = row.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Original whole air entry {kind}{index}: consumed Jump16/held/lookaround gates, source wall/ceiling flags, signed support impulse, byte grace and shared fall/control writes.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Original whole air entry {kind}{index++}: ordered source animation frames, genuine up/forward orientation, held drop and positional sound.");
        }
    }

    private sealed class Host(PlayerCombatState combat, long[] input) : IPlayerAirborneEntryHost
    {
        public PlayerJumpImpulse Jump { get; set; } = new((int)input[15], (int)input[16], (int)input[17]);
        public int SpeedRamp { get; set; } = (int)input[18];
        public byte GroundContactGrace { get; set; } = (byte)input[31];
        public int? SupportVerticalVelocity => input[13] == 0 ? null : (int)input[14];
        public short InputHeading => (short)input[12];
        public List<long> Events { get; } = [];
        public void PlaySingleAnimation(ushort animation, int firstFrame)
        { Events.AddRange([1, animation, firstFrame, -1]); combat.AnimationId = animation; }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) =>
            Events.AddRange([3, sound, position.X, position.Y, position.Z, pitch]);
        public void OrientToNormal(bool useTarget, FixedVector3 target) =>
            Events.AddRange([2, useTarget ? 1 : 0, target.X, target.Y, target.Z]);
        public void DropHeldObject(FixedVector3 velocity) => Events.AddRange([4, velocity.X, velocity.Y, velocity.Z]);
    }
}
