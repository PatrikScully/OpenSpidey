using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerDistanceAttackPreparationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerDistanceAttackPreparationTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-distance-attack-preparation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var combat = new PlayerCombatState(100, 100)
            {
                ArenaConstraintActive = (byte)input[12], Field5B0 = (int)input[18], Field5AC = (int)input[19],
                Field54C = (byte)input[20], IsCrawling = input[21] != 0, Field8E8 = (byte)input[22], Field8E9 = (byte)input[23],
                HeadingTurnTicksRemaining = (int)input[25], FloorCameraState = (int)input[26],
                CrawlSurfaceNormal = Vector(28), Velocity = Vector(31), ForwardDirection = Vector(34), UpDirection = Vector(37),
                LookaroundActive = input[10] != 0
            };
            var host = new Host(combat, input);
            var additional = new PlayerAdditionalEffectState
            {
                SenseTicks = (int)input[13], SenseSoundHandle = (uint)input[1],
                SenseBody = host.Handle(2, (int)input[2], (uint)input[48])
            };
            additional.BonusFists[0] = host.Handle(6, (int)input[6], (uint)input[49]);
            additional.BonusFists[1] = host.Handle(7, (int)input[7], (uint)input[50]);
            var attack = new PlayerWebAttackState { DomeHandle = host.Handle(8, (int)input[8], (uint)input[51]) };
            var state = new PlayerDistanceAttackPreparationState(combat, attack, additional, new() { Value = (int)input[14] });
            host.State = state;
            PlayerDistanceAttackPreparation.Prepare(state, host, Vector(40));
            long[] actual = [combat.ArenaConstraintActive, host.AutoAimReticlePresent ? 1 : 0, additional.SenseTicks, state.Water.Value,
                additional.SenseSoundHandle, Host.Identity(additional.SenseBody.Target), additional.SenseBody.Validation,
                Host.Identity(additional.BonusFists[0].Target), additional.BonusFists[0].Validation,
                Host.Identity(additional.BonusFists[1].Target), additional.BonusFists[1].Validation,
                Host.Identity(attack.DomeHandle.Target), attack.DomeHandle.Validation,
                host.HeldObjectPresent ? 3 : 0, host.SwingPresent ? 4 : 0, host.CurrentWebPresent ? 5 : 0,
                host.Jump.Velocity, host.Jump.Phase, host.Jump.Hold, combat.Field5B0, combat.Field5AC,
                host.TrailPresent[0] ? 9 : 0, host.TrailFades[0], host.TrailPresent[1] ? 10 : 0, host.TrailFades[1],
                host.TrailPresent[2] ? 11 : 0, host.TrailFades[2], host.TrailPresent[3] ? 12 : 0, host.TrailFades[3],
                combat.Field54C, combat.IsCrawling ? 1 : 0, combat.Field8E8, combat.Field8E9,
                combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z,
                combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z, host.HeadingDelta, combat.HeadingTurnTicksRemaining,
                input[10], host.CameraFocusHook, combat.FloorCameraState];
            JsonElement expected = row.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Whole original distance preparation{index}: arena/sense/jump/bonus fields, pointer-only versus full empty handles, four surviving fades and camera mode gate.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Whole original distance preparation{index++}: reticle/additional/swing/web/dome ownership order, wrapped held drop, pre-reset orientation and camera parameters.");
        }
    }

    private sealed record Body(int Identity, bool Alive);
    private sealed class Host(PlayerCombatState combat, long[] input) : IPlayerDistanceAttackPreparationHost
    {
        public PlayerDistanceAttackPreparationState State { get; set; } = null!;
        public List<long> Events { get; } = [];
        public bool AutoAimReticlePresent { get; private set; } = input[0] != 0;
        public bool HeldObjectPresent { get; private set; } = input[3] != 0;
        public bool SwingPresent { get; private set; } = input[4] != 0;
        public bool CurrentWebPresent { get; private set; } = input[5] != 0;
        public PlayerJumpImpulse Jump { get; private set; } = new((int)input[15], (int)input[16], (int)input[17]);
        public int HeadingDelta { get; private set; } = (int)input[24];
        public int CameraFocusHook { get; private set; } = (int)input[27];
        public bool[] TrailPresent { get; } = Enumerable.Range(0, 4).Select(slot => (input[9] & (1 << slot)) != 0).ToArray();
        public int[] TrailFades { get; } = [81, 82, 83, 84];
        public PlayerKickHandle Handle(int identity, int kind, uint validator) => new(kind == 0 ? null : new Body(identity, kind == 1), validator);
        public static int Identity(object? body) => body is Body value ? value.Identity : 0;
        private void Destroy(int identity, int list) => Events.AddRange([1, identity, list, 2, identity, 1]);
        public void DeleteAutoAimReticle() { Destroy(1, 1); AutoAimReticlePresent = false; }
        public void StopSound(uint handle) => Events.AddRange([3, handle, combat.ArenaConstraintActive, State.AdditionalEffects.SenseTicks, State.Water.Value]);
        public object? RecoverAdditionalBody(ref PlayerKickHandle handle) => Recover(ref handle);
        public object? RecoverDome(ref PlayerKickHandle handle) => Recover(ref handle);
        private object? Recover(ref PlayerKickHandle handle)
        {
            Events.AddRange([4, Identity(handle.Target), handle.Validation]);
            if (handle.Target is Body { Alive: false }) { handle = handle with { Target = null }; return null; }
            return handle.Target;
        }
        public void DeleteAdditionalBody(object body) { int id = Identity(body); Destroy(id, 2); if (id == 2) Events.Add(9); }
        public void ClearJumpDurations() => Jump = Jump with { Phase = 0, Hold = 0 };
        public void DropHeldObject(FixedVector3 velocity)
        { Events.AddRange([5, 3, velocity.X, velocity.Y, velocity.Z, Jump.Phase, Jump.Hold]); HeldObjectPresent = false; }
        public void SwingBackAndDelete() { Events.AddRange([6, 4, 2, 4, 1]); SwingPresent = false; }
        public void DeleteCurrentWeb() { Events.AddRange([2, 5, 1]); CurrentWebPresent = false; }
        public void BurstDome(object dome) => Events.AddRange([7, Identity(dome), combat.Field5B0, combat.Field5AC, 9]);
        public bool SmokeTrailPresent(int slot) => TrailPresent[slot];
        public void FadeSmokeTrail(int slot) { TrailFades[slot] = 1; TrailPresent[slot] = false; }
        public void OrientToNormal(FixedVector3 direction) => Events.AddRange([8, 1, direction.X, direction.Y, direction.Z,
            combat.Field54C, combat.IsCrawling ? 1 : 0, combat.Field8E8, combat.Field8E9,
            combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z, HeadingDelta, combat.HeadingTurnTicksRemaining,
            combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z]);
        public void ClearHeadingTurn() { HeadingDelta = 0; combat.HeadingTurnTicksRemaining = 0; }
        public void ExitLookaround() => Events.AddRange([10, combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z, HeadingDelta, combat.HeadingTurnTicksRemaining]);
        public void SetCameraFocusHook(int hook) => CameraFocusHook = hook;
        public void SetFloorCamera(int duration)
        {
            if (input[11] != 3) return;
            for (int slot = 0; slot < 5; slot++) Events.AddRange([11, slot, slot == 3 ? (ushort)input[43 + slot] : (short)input[43 + slot], duration]);
            combat.FloorCameraState = 0;
        }
        public void PutCameraBehind(int duration) => Events.AddRange([12, duration, combat.FloorCameraState]);
    }
}
