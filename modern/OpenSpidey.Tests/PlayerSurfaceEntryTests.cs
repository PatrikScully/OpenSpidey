using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSurfaceEntryTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSurfaceEntryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-surface-entry.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            int family = (int)input[0];
            var combat = new PlayerCombatState(100, 100) { Position = new(12345, -23456, 34567), Velocity = new(11111, -22222, 33333),
                ForwardDirection = new(4096, 0, 0), UpDirection = new(11, -4096, 13), ActionState = family == 3 ? 0x1000u : family == 4 ? 0x80000u : 16,
                AnimationId = 50, AnimationFrame = 12, IsCrawling = input[3] != 0, Field8E8 = (byte)input[4], Field8E9 = (byte)input[5],
                CrawlSurfaceNormal = Vector(12), CameraAngleLock = (byte)input[25], HeadingTurnTicksRemaining = 99, FieldAE5 = 77, FieldEA6 = 33, FloorCameraState = 88 };
            var melee = new PlayerMeleeState(combat); melee.Input.Sideways = (sbyte)input[18];
            var state = new PlayerSurfaceTransitionState(melee) { LineNormal = Vector(9), ExteriorCornerDetected = (byte)input[2],
                WallToCeilingMotion = (byte)input[19], CeilingToWallMotion = (byte)input[20], WallMotionRequested = (byte)input[21],
                CeilingToWallInputRequested = (byte)input[22], CeilingMotionRequested = (byte)input[23], ApplyHookPosition = (byte)input[24], InteriorSlideRemaining = (int)input[29] };
            var host = new Host(state, input);
            PlayerSurfaceEntryContact? contact = input[1] != 0 ? new(state.LineNormal, (int)input[7], (uint)input[8]) : null;
            int result = -1;
            switch (family)
            {
                case 0: result = PlayerSurfaceEntries.CheckInterior(state, contact, input[6] != 0, host) ? 1 : 0; break;
                case 1: result = PlayerSurfaceEntries.CheckExterior(state, contact, input[6] != 0, host) ? 1 : 0; break;
                case 2: PlayerSurfaceEntries.HandleControls(state, input[30] != 0); break;
                case 3: PlayerSurfaceEntries.UpdateInterior(state, (ushort)input[28], host); break;
                case 4: PlayerSurfaceEntries.UpdateWallCompletion(state, input[30] != 0, host); break;
                default: throw new InvalidDataException("Unknown original surface fixture family.");
            }
            long[] actual = [result, combat.ActionState, combat.AnimationId, combat.AnimationFrame,
                combat.Position.X, combat.Position.Y, combat.Position.Z, combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z,
                combat.CrawlSurfaceNormal.X, combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z,
                host.Reference.X, host.Reference.Y, host.Reference.Z, combat.IsCrawling ? 1 : 0, combat.Field8E8, combat.Field8E9,
                state.WallToCeilingMotion, state.CeilingToWallMotion, state.WallMotionRequested, state.CeilingToWallInputRequested,
                state.CeilingMotionRequested, state.ApplyHookPosition, state.InteriorSlideRemaining,
                combat.HeadingTurnTicksRemaining, combat.FieldAE5, combat.FieldEA6, combat.FloorCameraState,
                host.SoundSelected ? 1 : 0, host.SoundWords[0], host.SoundWords[1]];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC surfaceEntry[{string.Join(',', input)}] exact controls/1000/80000 source fields, frame narrowing and real sound-packet resets; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC surfaceEntry[{string.Join(',', input)}] full orientation/pose/hook/tripod and inline camera preset order; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private sealed class Host(PlayerSurfaceTransitionState state, long[] input) : IPlayerSurfaceEntryHost
    {
        private PlayerCombatState Combat => state.Melee.Combat;
        public List<long> Events { get; } = [];
        public FixedVector3 Reference { get; private set; } = new(1, 2, 3);
        public FixedVector3 RightDirection => new((int)input[15], (int)input[16], (int)input[17]);
        public bool SoundSelected { get; private set; }
        public int[] SoundWords { get; } = [65543, 131081];
        public void SetOrientationReference(FixedVector3 reference) => Reference = reference;
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Events.AddRange([1, useTarget ? 1 : 0, target.X, target.Y, target.Z]);
        public void PlayAnimation(ushort clip) { SelectAndResetAnimationSounds(clip); RunAnimation(clip); }
        public void RunAnimation(ushort clip) { Combat.AnimationId = clip; Combat.AnimationFrame = 0; Events.AddRange([2, clip, 0, -1]); }
        public void SelectAndResetAnimationSounds(ushort clip)
        { SoundSelected = true; for (int i = 0; i < SoundWords.Length; i++) SoundWords[i] &= 0xFFFF; }
        public int AnimationFrameCount(ushort clip) => (int)input[27];
        public void SetAnimationFrame(short frame) => Combat.AnimationFrame = frame;
        public void ApplyPlayerPose() => Events.AddRange([3, Combat.AnimationId, Combat.AnimationFrame]);
        public FixedVector3 GetPlayerHookPosition(int hook) { Events.AddRange([4, hook]); return new(123456, -234567, 345678); }
        public void SetTripodMotion(FixedVector3 position, uint duration) => Events.AddRange([5, position.X, position.Y, position.Z, duration]);
        public void SwitchToStandMode() { Events.Add(6); Combat.ActionState = 1; }
        public void LockTargetTorsoAngle() { Events.Add(7); Combat.HeadingTurnTicksRemaining = 0; }
        public void SetCameraAngle(short heading, ushort duration) => Events.AddRange([9, heading, duration]);
        public void SetFloorCamera(int duration) => CameraPreset(duration, 0, [-1234, 2345, -3456, 54321, -12345]);
        public void SetWallCamera(int duration) => CameraPreset(duration, 1, [1111, -2222, 3333, 62001, 4444]);
        public void SetCeilingCamera(int duration) => CameraPreset(duration, 2, [-31000, 32000, -32768, 65535, 32767]);
        private void CameraPreset(int duration, int floorState, int[] values)
        {
            if (input[26] != 3) return;
            for (int i = 0; i < values.Length; i++) Events.AddRange([8, i, values[i], (ushort)duration]);
            Combat.FloorCameraState = floorState;
        }
        public void TidyLandingPosition(int radius) => throw new InvalidOperationException("Original1000 and80000 have no tidy-landing call.");
        public int GroundHeight(FixedVector3 position, int above, int below) => throw new InvalidOperationException("Original1000 and80000 have no ground probe.");
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => throw new InvalidOperationException("Original surface entry/1000/80000 have no position sound.");
    }
}
