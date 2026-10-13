using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSurfaceTransitionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSurfaceTransitionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-surface-transitions.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            {
                Position = new(12345, -54321, 67890), Velocity = new(12345, (int)input[1], 67890),
                ForwardDirection = new(4096, 0, 0), UpDirection = new(11, -4096, 13), CrawlSurfaceNormal = new(111, -222, 333),
                AnimationId = (ushort)input[7], AnimationFrame = 12, ActionState = (uint)input[8], HeadingTurnTicksRemaining = 99,
                CollisionFlags = (ushort)input[2], FieldEA6 = 77, Field54C = 23, FieldAE5 = 24,
                Field8E8 = (byte)input[10], Field8E9 = (byte)input[11], IsCrawling = input[12] != 0
            };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Pressed[16] = (byte)input[4];
            var state = new PlayerSurfaceTransitionState(melee)
            {
                LineNormal = new(3500, (int)input[5], -900), FenceDetected = (byte)input[9], ApplyHookPosition = 6,
                WallToCeilingMotion = 7, CeilingToWallMotion = 8, WallMotionRequested = 1, CeilingMotionRequested = 1,
                ForwardSlideRemaining = (int)input[14]
            };
            var host = new Host(state, input);
            int result = -1;
            if (input[0] == 0)
            {
                PlayerWorldHit? hit = input[3] != 0 ? new(new(new(123456, -234567, 345678), state.LineNormal, 0, (uint)input[6], false), 0, 0, 0) : null;
                result = PlayerSurfaceTransitions.CheckCeiling(melee, hit, 96, host) ? 1 : 0;
            }
            else if (input[0] == 1) result = PlayerSurfaceTransitions.CheckFence(state, host) ? 1 : 0;
            else PlayerSurfaceTransitions.Update(state, (ushort)input[15], 96, host);
            long[] actual = [result, combat.ActionState, combat.AnimationId, combat.AnimationFrame, combat.Position.X,
                combat.Position.Y, combat.Position.Z, combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z,
                host.OrientationReference.X, host.OrientationReference.Y, host.OrientationReference.Z, combat.IsCrawling ? 1 : 0,
                combat.Field54C, combat.FieldAE5, combat.Field8E8, combat.Field8E9, host.JumpPhase, host.JumpHold,
                host.CameraHook, combat.HeadingTurnTicksRemaining, state.LineNormal.X, state.LineNormal.Y, state.LineNormal.Z,
                state.ApplyHookPosition, state.WallToCeilingMotion, state.CeilingToWallMotion, state.ForwardSlideRemaining, combat.FieldEA6];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            int mismatch = Enumerable.Range(0, actual.Length).FirstOrDefault(i => actual[i] != wanted[i], -1);
            assert(actual.SequenceEqual(wanted), $"Source surface {index}: exact ceiling/fence/action2000 shared fields; difference {mismatch}.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Source surface {index++}: clip93 last-frame pose/camera/frame0 order, signed slide, all eighteen ground recovery probes and landing calls.");
        }
    }
    private sealed class Host(PlayerSurfaceTransitionState state, long[] input) : IPlayerCeilingAttachmentHost, IPlayerSurfaceTransitionHost
    {
        private PlayerCombatState Combat => state.Melee.Combat;
        private int groundCalls;
        public List<long> Events { get; } = [];
        private void Emit(params long[] values) => Events.AddRange(values);
        public FixedVector3 OrientationReference { get; private set; } = new(1, 2, 3);
        public int JumpPhase { get; private set; } = 222;
        public int JumpHold { get; private set; } = 333;
        public int CameraHook { get; private set; } = 44;
        public FixedVector3 RightDirection => new(0, 0, 4096);
        public void SetOrientationReference(FixedVector3 direction) => OrientationReference = direction;
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Emit(1, useTarget ? 1 : 0, target.X, target.Y, target.Z);
        public void ClearJumpDurations() => JumpPhase = JumpHold = 0;
        public void PlayAnimation(ushort animation) { Emit(2, animation, 0, -1); Combat.AnimationId = animation; Combat.AnimationFrame = 0; }
        public void SetCameraFocusHook(int hook) => CameraHook = hook;
        public void PlaySound(ushort sound, int volume, int pitch) => Emit(15, sound, volume, pitch);
        public void SelectAndResetAnimationSounds(ushort animation) => Emit(3, animation);
        public int AnimationFrameCount(ushort animation) { Emit(4, animation); return (int)input[13]; }
        public void RunAnimation(ushort animation) { Emit(7, animation, 0, -1); Combat.AnimationId = animation; Combat.AnimationFrame = 0; }
        public void SetAnimationFrame(short frame) { Combat.AnimationFrame = frame; Emit(5, frame); }
        public void ApplyPlayerPose() => Emit(6, 1, Combat.AnimationId, Combat.AnimationFrame);
        public FixedVector3 GetPlayerHookPosition(int hook) { Emit(14, hook); return new(123456, -234567, 345678); }
        public void SetTripodMotion(FixedVector3 position, uint duration) => Emit(8, position.X, position.Y, position.Z, duration);
        public void SetFloorCamera(int duration) => Emit(12, duration);
        public void LockTargetTorsoAngle() => Emit(13);
        public void SwitchToStandMode() { Emit(11); Combat.ActionState = 1; }
        public void TidyLandingPosition(int radius) => Emit(9, radius);
        public int GroundHeight(FixedVector3 position, int above, int below)
        { Emit(10, position.X, position.Y, position.Z, above, below, 0); return ++groundCalls == input[16] ? -400000 : -1; }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Emit(16, sound, position.X, position.Y, position.Z, pitch);
    }
}
