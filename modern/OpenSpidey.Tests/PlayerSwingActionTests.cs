using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSwingActionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSwingActionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-swing-actions.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            {
                ActionState = input[0] == 0 ? 0x100u : input[0] == 1 ? 0x200u : 0x400u,
                AnimationId = 0x111, AnimationFrame = (int)input[1], FieldEA6 = 77, Field54C = 23, FlailDropChain = 24,
                Field8E8 = 26, FieldAE5 = 27, ActionProgress = (int)input[3], LastHitAction = (uint)input[4], LastHitClock = (uint)input[6],
                Velocity = new(12345, (int)input[7], 67890), Position = new(12345, -54321, 67890),
                CrawlSurfaceNormal = new(111, -222, 333), ForwardDirection = new(4096, 0, 0),
                CollisionFlags = (ushort)input[17], SynthesizedInputActive = input[22] != 0
            };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Sideways = 9; melee.Input.Forward = 10;
            for (int button = 0; button < 4; button++) melee.Input.Triggered[16 + button] = (byte)((input[23] >> button) & 1);
            var availability = new PlayerSwingWebAvailabilityState
            {
                PlaneNormal = new(0, -4096, 0), NearCentre = new(100000, -200000, 300000), FarCentre = new(150000, -250000, 350000),
                FarFromWall = (byte)input[16]
            };
            var targets = new PlayerLookaroundTargetState(new(new(combat)), melee)
            { SwingFacingTarget = input[2] != 0 ? new(-123456, 0, 345678) : default };
            var state = new PlayerSwingActionState(melee, availability, targets) { ContactNormal = new(3500, (int)input[19], -900) };
            if (input[18] != 0) state.Contact = new(new(default, state.ContactNormal, 0, (uint)input[20], false), 0, 0, 0);
            if (input[14] != 0)
            {
                state.Current = new(default, 80, default, default, 0, default, _ => 0);
                if (input[15] != 0) state.Current.Advance(40, default);
            }
            var host = new Host(state, input);
            if (input[0] == 0) PlayerSwingActions.UpdateWindup(state, host);
            else if (input[0] == 1) PlayerSwingActions.UpdateRelease(state, host);
            else PlayerSwingActions.UpdateSwinging(state, host);
            long[] actual = [combat.FieldEA6, combat.Field54C, host.Jump.Velocity, host.Jump.Phase, host.Jump.Hold,
                combat.ActionState, combat.ActionProgress, combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z,
                combat.Position.X, combat.Position.Y, combat.Position.Z, combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y,
                combat.CrawlSurfaceNormal.Z, combat.Field8E8, host.PendingRelease, combat.FlailDropChain, host.LandingLatch,
                combat.FieldAE5, combat.IsCrawling ? 1 : 0, host.Fence, host.AirborneLatch, melee.Input.Sideways, melee.Input.Forward,
                host.Ramp, combat.AnimationFrame, combat.AnimationId, host.AnimationSpeed, availability.FarFromWall, host.CameraHook,
                state.Current is null ? 0 : 1, state.Length, host.OrientationReference.X, host.OrientationReference.Y,
                host.OrientationReference.Z, state.ContactNormal.X, state.ContactNormal.Y, state.ContactNormal.Z];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            int mismatch = Enumerable.Range(0, actual.Length).FirstOrDefault(i => actual[i] != wanted[i], -1);
            assert(actual.SequenceEqual(wanted), $"Source swing action {index}: exact100/200/400 partial fields; first difference {mismatch}.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Source swing action {index++}: ordered guards, allocation/release, complete collision/fence branches, sound and animation callbacks.");
            state.Current?.Dispose();
        }
    }

    private sealed class Host(PlayerSwingActionState state, long[] input) : IPlayerSwingActionHost
    {
        private PlayerCombatState Combat => state.Melee.Combat;
        private int probes;
        public List<long> Events { get; } = [];
        private void Emit(params long[] values) => Events.AddRange(values);
        public PlayerJumpImpulse Jump { get; private set; } = new(111, 222, 333);
        public int PendingRelease { get; private set; } = 3;
        public int LandingLatch { get; private set; } = 25;
        public int Fence { get; private set; }
        public int AirborneLatch { get; private set; } = 28;
        public int Ramp { get; private set; } = 99;
        public int AnimationSpeed { get; private set; } = 42;
        public int CameraHook { get; private set; } = 44;
        public FixedVector3 OrientationReference { get; private set; }
        public uint Clock => (uint)input[5];
        public byte FireVariant => (byte)input[25];
        public int PracticeDifficulty => (int)input[21];
        public int AnimationFrameCount => 25;
        public int? SupportVerticalVelocity => input[8] != 0 ? (int)input[9] : null;
        public bool CheckStickToCeiling() { Emit(5); return input[11] != 0; }
        public bool CheckJumpingSmashKick() { Emit(16); return input[12] != 0; }
        public bool CheckJumpingZipWeb() { Emit(17); return input[13] != 0; }
        public bool ShouldDropFlail() { Emit(4); return input[10] != 0; }
        public void SetJumpImpulse(PlayerJumpImpulse impulse) => Jump = impulse;
        public void SetForcedAirControl(sbyte upDown, sbyte leftRight, int ramp)
        { state.Melee.Input.Sideways = upDown; state.Melee.Input.Forward = leftRight; Ramp = ramp; }
        public void SetPendingWebReleaseAnimation(byte value) => PendingRelease = value;
        public void SetLandingAnimationLatch(byte value) => LandingLatch = value;
        public void ClearAirborneReleaseLatch() => AirborneLatch = 0;
        public void SetOrientationReference(FixedVector3 direction) => OrientationReference = direction;
        public void SetFenceSurfaceDetected() => Fence = 1;
        public bool CheckFenceSurfaceTransition() { Emit(20); return true; }
        public void TidyLandingPosition(int distance) => Emit(19, distance);
        public void SetCameraFocusHook(int hook) => CameraHook = hook;
        public void PutCameraBehind(int duration) => Emit(3, duration);
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Emit(14, useTarget ? 1 : 0, target.X, target.Y, target.Z);
        public void LockTargetTorsoAngle() => Emit(18);
        public void PlayAnimation(ushort animation) { Emit(1, animation, 0, -1); Combat.AnimationId = animation; Combat.AnimationFrame = 0; }
        public void SetAnimationFrame(short frame) { Combat.AnimationFrame = frame; Emit(21, frame); }
        public void SetAnimationSpeed(int speed) => AnimationSpeed = speed;
        public bool DecreaseWebbing(int amount) { Emit(6, amount); return false; }
        public WebSwingAnchor CreateSwing(FixedVector3 anchor, int length, SourceMatrix basis, FixedVector3 surfaceNormal)
        {
            Emit(11, anchor.X, anchor.Y, anchor.Z); Emit(7, 0x190); Emit(8, anchor.X, anchor.Y, anchor.Z, length,
                surfaceNormal.X, surfaceNormal.Y, surfaceNormal.Z);
            return new(anchor, length, basis, surfaceNormal, Clock, default, _ => 0);
        }
        public void ReleaseSwing(WebSwingAnchor swing) => Emit(9);
        public void DeleteSwing(WebSwingAnchor swing) { Emit(10); swing.Dispose(); }
        public PlayerWorldHit? TraceWorld(FixedVector3 start, FixedVector3 end)
        {
            Emit(15, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1);
            if (++probes != input[24]) return null;
            return new(new(new(123456, -234567, 345678), new(3500, (int)input[19], -900), 0, (uint)input[20], false), 0, 0, 0);
        }
        public FixedVector3 GetPlayerHookPosition(int hook) { Emit(22, hook); return new(123456, -234567, 345678); }
        public int Random(int bound) { Emit(12, bound); return 2 % bound; }
        public void PlaySound(ushort sound, int volume, int pitch) => Emit(13, sound, volume, pitch);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Emit(2, sound, position.X, position.Y, position.Z, pitch);
    }
}
