using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerMeleeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerMeleeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-melee.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        PlayerFightData data = PlayerFightData.Decode(Convert.FromHexString(root.GetProperty("distanceHex").GetString()!),
            Convert.FromHexString(root.GetProperty("movesHex").GetString()!));
        PlayerFightData[] variants = [data, ..root.GetProperty("variants").EnumerateArray().Select(variant =>
            PlayerFightData.Decode(Convert.FromHexString(root.GetProperty("distanceHex").GetString()!),
                Convert.FromHexString(variant.GetProperty("movesHex").GetString()!)))];
        assert(data.Moves.Count == 12 && data.Moves[0].Animation == 100 && data.Moves[1].Animation == 107 &&
            data.Moves[10].HitFlags == 0 && data.Moves[16].HitFlags == 1 && data.Moves[10].CollisionHooks.Count == 7,
            "Original PC fight records retain all twelve moves, source low-byte hit flags andFF-terminated collision hooks.");
        int caseIndex = 0;
        foreach (JsonElement test in root.GetProperty("cases").EnumerateArray())
        {
            int[] input = Integers(test.GetProperty("input"));
            var combat = new PlayerCombatState(100, 100)
            {
                ActionState = 0x800, Position = new(123456, -234567, 345678), ForwardDirection = new(0, 0, 4096),
                UpDirection = new(0, -4096, 0), FieldEA6 = 77, Field528 = 3, Field5AC = input[4], IsCrawling = input[12] != 0
            };
            var state = new PlayerMeleeState(combat)
            { HeadingDelta = 17, EffectiveHeading = 777, RightDirection = new(4096, 0, 0), AnimationFlags = 255, AttackEntryClock = 1000 };
            for (int hook = 0; hook < 4; hook++) state.SlideHookOrigins[hook] = new(100 + 300 * hook, 200 + 300 * hook, 300 + 300 * hook);
            var host = new Host(state, input);
            if (input[15] != 0) state.AimTarget = host.Targets[0];
            var controller = new PlayerMeleeController(state, variants[test.GetProperty("variant").GetInt32()], host,
                bound => { host.Events.AddRange([10, bound]); return bound > 0 ? bound / 2 : 0; });
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            controller.Initiate((ushort)input[0], input[1]);
            Verify(expected[0], -1, 0);
            int update = 1;
            foreach (JsonElement stepValue in test.GetProperty("steps").EnumerateArray())
            {
                int[] step = Integers(stepValue);
                state.AccumulatedTicks = step[0];
                host.TraceCursor = 0;
                host.Events.Clear();
                state.Input.Triggered[17] = (byte)(step[1] & 1);
                state.Input.Triggered[19] = (byte)((step[1] >> 1) & 1);
                state.Input.Triggered[18] = (byte)((step[1] >> 2) & 1);
                state.Input.Triggered[16] = (byte)((step[1] >> 3) & 1);
                state.Input.Pressed[17] = (byte)step[2];
                state.Input.Pressed[18] = (byte)step[3];
                state.Input.Pressed[19] = (byte)step[4];
                state.Input.Sideways = (sbyte)step[5];
                state.Input.Forward = (sbyte)step[6];
                state.AnimationFinished = step[7] != 0;
                host.GroundGone = step[8] != 0;
                host.Jump = step[9] != 0;
                int result = -2;
                if (input[14] != 0) controller.UpdatePunchCombo();
                else result = controller.Track();
                Verify(expected[update], result, update++);
            }
            caseIndex++;

            void Verify(JsonElement reference, int result, int updateIndex)
            {
                long[] actual = [result, combat.Position.X, combat.Position.Y, combat.Position.Z,
                    combat.AnimationId, combat.AnimationFrame, state.AnimationFinished ? 1 : 0,
                    state.Move, state.ComboStartTicks, state.LastButtonClock, state.TransitionAnimation,
                    state.DistanceCursor, state.PartsEnabled ? 1 : 0, state.TrackingActive,
                    state.FirstContactFrame ? 1 : 0, state.CollisionStreamActive ? 1 : 0,
                    state.HitTargetCount, state.HitSessionStarted, state.HitSessionHealth, state.HitSessionClock,
                    combat.Field5AC, combat.Field52C, combat.Field534, state.HeadingDelta, combat.ActionState,
                    combat.FieldEA6, state.WebAttackMode, state.WebAttackSubstate, state.WebAttackStarted,
                    state.WebFiringSide, state.AnimationFlags, state.ZipDiveClock, state.ZipDiveHandle.Target is null ? 0 : 1,
                    (int?)state.FollowOn?.Move ?? -1];
                long[] wanted = reference.GetProperty("state").EnumerateArray().Select(value => value.GetInt64()).ToArray();
                string context = $"Player combo case {caseIndex}, update {updateIndex}, input[{string.Join(',', input)}]";
                assert(actual.SequenceEqual(wanted), $"{context}: state actual[{string.Join(',', actual)}] expected[{string.Join(',', wanted)}]");
                int partIndex = 0;
                foreach (JsonElement expectedPart in reference.GetProperty("parts").EnumerateArray())
                {
                    PlayerComboPart part = state.Parts[partIndex];
                    int[] values = [part.Active ? 1 : 0, part.WaitingFirst ? 1 : 0, part.UsesLateWindow ? 1 : 0,
                        part.Started ? 1 : 0, part.Flags, part.LastPressTicks, part.InputCursor];
                    assert(values.SequenceEqual(Integers(expectedPart)), $"{context}, part {partIndex}: actual[{string.Join(',', values)}] expected{expectedPart}");
                    partIndex++;
                }
                int button = 0;
                foreach (JsonElement expectedButton in reference.GetProperty("buttons").EnumerateArray())
                {
                    int[] wantedButton = Integers(expectedButton);
                    assert(state.Input.Triggered[button] == wantedButton[0] && state.Input.Mirrors[button] == wantedButton[1],
                        $"{context}: source latch {button}, actual {state.Input.Triggered[button]}/{state.Input.Mirrors[button]}, expected {expectedButton}");
                    button++;
                }
                int[] wantedEvents = Integers(reference.GetProperty("events"));
                assert(host.Events.SequenceEqual(wantedEvents), $"{context}: events actual[{string.Join(',', host.Events)}] expected[{string.Join(',', wantedEvents)}]");
            }
        }
    }
    private static int[] Integers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class Target(int id, FixedVector3 position, ushort flags, ushort type) : IPlayerMeleeTarget
    {
        public object Identity => this;
        public int Id { get; } = id;
        public FixedVector3 Position { get; } = position;
        public ushort ItemFlags { get; } = flags;
        public ushort Type { get; } = type;
        public IPlayerMeleeTarget? Next { get; set; }
    }
    private sealed class Host : IPlayerMeleeHost
    {
        private readonly PlayerMeleeState state;
        private readonly int[] settings;
        private int soundEntry = 1;
        public List<int> Events { get; } = [];
        public Target[] Targets { get; }
        public int TraceCursor { get; set; }
        public bool GroundGone { get; set; }
        public bool Jump { get; set; }
        public Host(PlayerMeleeState state, int[] settings)
        {
            this.state = state; this.settings = settings;
            FixedVector3 position = state.Combat.Position + new FixedVector3(settings[10] << 12, 0, 0);
            Targets = Enumerable.Range(0, settings[16]).Select(index => new Target(index, position, (ushort)settings[8], (ushort)settings[9])).ToArray();
            for (int index = 0; index + 1 < Targets.Length; index++) Targets[index].Next = Targets[index + 1];
        }
        public uint Timer => unchecked((uint)(1000 + state.AccumulatedTicks));
        public int Suit => settings[2];
        public int Difficulty => settings[3];
        public bool CameraFlag180 => settings[13] != 0;
        public IPlayerMeleeTarget? BaddyHead => Targets.FirstOrDefault();
        public IPlayerMeleeTarget? SelectTargetBaddy(int range, int minimum, int maximum, int flags)
        { Events.AddRange([1, range, minimum, maximum, flags]); return settings[11] != 0 ? Targets[0] : null; }
        public PlayerKickHandle StoreTargetHandle(IPlayerMeleeTarget? target)
        { Events.AddRange([23, target is Target native ? native.Id : -1]); return new(target?.Identity, target is null ? 0u : 1u); }
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Events.AddRange([2, useTarget ? 1 : 0, target.X, target.Y, target.Z, state.HeadingDelta]);
        public void SelectAndResetAnimationSounds(ushort animation) => soundEntry = 1;
        public void RunAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([8, animation, firstFrame, lastFrame, soundEntry]);
        public void PlaySingleAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([24, animation, firstFrame, lastFrame]);
        public void PlaySound(ushort sound, int volume, int pitch) => Events.AddRange([9, sound, volume, pitch]);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([19, sound, position.X, position.Y, position.Z, pitch]);
        public void ApplyPlayerPose() => Events.Add(3);
        public FixedVector3 GetPlayerHookPosition(int hook)
        { Events.AddRange([13, hook]); return new(1000 + state.AccumulatedTicks * 300 + hook * 100, -2000 + state.AccumulatedTicks * 101, 3000 - state.AccumulatedTicks * 200); }
        public bool LineToBaddySphere(FixedVector3 start, FixedVector3 end, FixedVector3 direction, int argument, int scale)
        { Events.AddRange([11, start.X, start.Y, start.Z, end.X, end.Y, end.Z, direction.X, direction.Y, direction.Z, argument, scale]); return settings[7] == 1; }
        public bool TraceWorld(FixedVector3 start, FixedVector3 end, int flags)
        { Events.AddRange([12, start.X, start.Y, start.Z, end.X, end.Y, end.Z, flags]); return settings[7] == ++TraceCursor + 1; }
        public FixedVector3 GetPlayerDynamicHookPosition(ActorHook hook)
        {
            Events.AddRange([14, hook.X, hook.Y, hook.Z, hook.PartIndex]);
            return new(1000 + state.AccumulatedTicks * 12000 + hook.PartIndex * 100, -2000 + state.AccumulatedTicks * 101, 3000 - state.AccumulatedTicks * 6000);
        }
        public void BuildTargetTransform(IPlayerMeleeTarget target) => Events.AddRange([15, ((Target)target).Id]);
        public bool CollideWithTarget(IPlayerMeleeTarget target, FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook)
        { Events.AddRange([16, ((Target)target).Id, start.X, start.Y, start.Z, end.X, end.Y, end.Z, scale]); hook = new(0, 0, 0, 7); return settings[6] != 0; }
        public FixedVector3 GetTargetDynamicHookPosition(IPlayerMeleeTarget target, ActorHook hook)
        { Events.AddRange([28, ((Target)target).Id, hook.X, hook.Y, hook.Z, hook.PartIndex]); return target.Position + new FixedVector3(70, 80, 90); }
        public bool HitTarget(IPlayerMeleeTarget target, ActorHit hit)
        { Events.AddRange([18, ((Target)target).Id, hit.Flags, hit.PartIndex, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]); return settings[5] != 0; }
        public void PutCameraBehind(int argument) => Events.AddRange([17, argument]);
        public void CreateCombatImpactEffect(FixedVector3 position, int kind) => Events.AddRange([20, position.X, position.Y, position.Z, kind]);
        public bool CheckGroundGone() { Events.Add(21); return GroundGone; }
        public bool CheckJump() { Events.Add(22); return Jump; }
        public void SwitchToStandMode() => Events.Add(25);
        public void LockTargetTorsoAngle() => Events.Add(26);
        public void SetTargetTorsoAngle(FixedVector3 target) => Events.AddRange([27, target.X, target.Y, target.Z]);
    }
}
