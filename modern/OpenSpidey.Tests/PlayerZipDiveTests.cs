using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerZipDiveTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(PlayerZipDiveTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-zip-dive.json")!);
        assert(document.RootElement.GetProperty("compiledSourceActionsVerified").GetBoolean() && document.RootElement.GetProperty("originalPcThugGrabVerified").GetBoolean(),
            "ZipDive gold needs compiled source slices and independent original-PC thug Grab proof.");
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            long[] actual;
            if (input[0] == 4)
            {
                var state = new ThugState(0, 0) { State = (int)input[1], Substate = (int)input[2] };
                var actor = new ActorProcedureState(new()) { BehaviorFlags = (uint)input[3] };
                var flags = new ThugStateFlags(); var events = new List<long>();
                bool grabbed = ThugGrabEntry.TryGrab(state, actor, flags, new(1, 2, 3), (position, argument) => { events.AddRange([11, position.X, position.Y, position.Z, argument]); return input[4] != 0; });
                actual = [grabbed ? 1 : 0, state.State, state.Substate, actor.BehaviorFlags, flags.CachedIndex, ..events];
            }
            else if (input[0] == 3)
            {
                PlayerZipDiveForwardResult result = PlayerZipDiveForwardTurn.Evaluate(new((byte)input[1], (sbyte)input[2], (sbyte)input[3], 123,
                    (ushort)input[4], (short)input[5], (short)input[6], input[7] != 0, (ushort)input[8], (int)input[9], (byte)input[10]));
                actual = result.Retarget ? [result.RetargetFlag, 12, result.Wanted] : [result.RetargetFlag];
            }
            else
            {
                FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
                var combat = new PlayerCombatState(100, 100) { ActionState = input[0] == 0 ? 0x02000000u : input[0] == 1 ? 0x04000000u : 0x08000000u,
                    AnimationId = (ushort)input[1], AnimationFrame = (int)input[2], FieldEA6 = 77, Position = Vector(12), ForwardDirection = Vector(15), UpDirection = Vector(18),
                    Field528 = (int)input[30], Velocity = Vector(31) };
                var state = new PlayerMeleeState(combat) { ZipDiveHandle = new(new object(), 55), ZipDiveClock = (int)input[5], ZipDiveAirControlSuppression = 3 };
                state.Input.Triggered[18] = (byte)input[21]; state.Input.Triggered[19] = (byte)input[22]; state.Input.Triggered[16] = (byte)input[23]; state.Input.Mirrors[16] = 1;
                state.Input.Forward = (sbyte)input[24]; state.Input.Sideways = (sbyte)input[25];
                var host = new Host { Clock = (uint)input[4], Target = new(new object(), Vector(9), (ushort)input[7], (short)input[6]), Valid = input[8] != 0,
                    GrabResult = input[26] != 0, JumpResult = input[27] != 0, Suit = (int)input[28], Difficulty = (int)input[29] };
                assert(PlayerZipDive.Update(state, (ushort)input[3], host), "All source ZipDive action states have a real dispatcher.");
                actual = [combat.ActionState, combat.FieldEA6, state.ZipDiveHandle.Target is null ? 0 : 1, state.ZipDiveHandle.Validation, state.ZipDiveClock,
                    state.ZipDiveAirControlSuppression, combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z, combat.Field534, combat.Field52C,
                    state.Input.Triggered[18], state.Input.Triggered[16], state.Input.Mirrors[16], host.TargetFlags, ..host.Events];
            }
            long[] expected = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(expected), $"ZipDive source{index}, input[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
            index++;
        }
        // Deliberate modern crash fixes are kept separate from original-valid gold.
        foreach (bool kicking in new[] { false, true })
        {
            var combat = new PlayerCombatState(100, 100) { ActionState = kicking ? 0x08000000u : 0x02000000u, AnimationId = kicking ? (ushort)128 : (ushort)123,
                ForwardDirection = new(4096, 0, 0), Velocity = new(11, 22, 33) };
            var state = new PlayerMeleeState(combat) { ZipDiveHandle = new(new object(), 55), ZipDiveAirControlSuppression = 3 };
            state.Input.Triggered[18] = 1;
            var host = new Host { Valid = false };
            PlayerZipDive.Update(state, ushort.MaxValue, host);
            assert(state.ZipDiveHandle.Target is null && state.ZipDiveHandle.Validation == 55 && host.Events.SequenceEqual(new long[] { 1, 4, kicking ? 127 : 121 }),
                "Expired ZipDive handles retain validation and use only the original ungrab/release animation, with no target dereference, damage, effects or stand fallback.");
            assert(kicking ? combat.ActionState == 4 && combat.Velocity == new FixedVector3(98315, -262122, 33) && state.ZipDiveAirControlSuppression == 1 && state.Input.Triggered[18] == 0 :
                combat.ActionState == 0x02000000 && combat.Velocity == new FixedVector3(11, 22, 33), "Expired kick follows actual falling impulse; expired holding exits through ungrab121.");
        }
    }
    private sealed class Host : IPlayerZipDiveHost
    {
        public uint Clock { get; set; } public int Difficulty { get; set; } public int Suit; public bool Valid, GrabResult, JumpResult;
        public PlayerZipDiveTarget Target = null!; public uint TargetFlags = 0x51;
        public List<long> Events { get; } = [];
        public PlayerZipDiveTarget? RecoverTarget(ref PlayerKickHandle handle) { Events.Add(1); if (Valid) return Target; handle = handle with { Target = null }; return null; }
        public bool GrabTarget(PlayerZipDiveTarget target, FixedVector3 destination) { Events.AddRange([2, destination.X, destination.Y, destination.Z]); return GrabResult; }
        public void ClearGrabFlag(PlayerZipDiveTarget target) => TargetFlags &= ~0x40u;
        public bool CheckJump() { Events.Add(5); return JumpResult; }
        public void CheckForwards() => Events.Add(6);
        public void LockTargetTorsoAngle() => Events.Add(7);
        public void SwitchToStandMode() => Events.Add(3);
        public void PlayAnimation(ushort animation) => Events.AddRange([4, animation]);
        public int GetDamage(int damage) { if (Suit is 2 or 3 or 4) damage = unchecked(damage * 2); return Difficulty switch { 2 => damage, 0 => unchecked(damage << 13) >> 12, 1 => unchecked((damage * 3) << 11) >> 12, _ => unchecked((damage * 3) << 10) >> 12 }; }
        public void HitTarget(PlayerZipDiveTarget target, ActorHit hit) => Events.AddRange([8, hit.Flags, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]);
        public void CreateCombatImpact(FixedVector3 position, int kind) => Events.AddRange([10, position.X, position.Y, position.Z, kind]);
        public void PlayPositionalSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([9, sound, position.X, position.Y, position.Z, pitch]);
    }
}
