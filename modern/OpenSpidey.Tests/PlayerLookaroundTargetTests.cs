using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerLookaroundTargetTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLookaroundTargetTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-lookaround-target.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { Position = new((int)input[9], (int)input[10], (int)input[11]),
                IsCrawling = input[12] != 0, ActionState = (uint)input[13] };
            var melee = new PlayerMeleeState(combat) { WebAttackStarted = 77 };
            var angles = new PlayerLookaroundState(combat) { Active = true, ActivateTarget = (byte)input[14], Yaw = (int)input[16] };
            var transition = new PlayerLookaroundTransitionState(angles) { ReticleType = 88 };
            var state = new PlayerLookaroundTargetState(transition, melee) { SurfaceNormal = new(21, -22, 23), TargetPosition = new(11, -12, 13),
                ZipOrigin = new(31, -32, 33), SwingFacingTarget = new(41, -42, 43), ReticleColour = 0xABCDEF };
            var host = new Host(input, angles);
            var body = new FixedQuaternion((int)input[0], (int)input[1], (int)input[2], (int)input[3]).ToMatrix();
            var raw = OpenSpidey.Assets.Animation.FixedJointTransform.FromJoint(new((short)input[4], (short)input[5], 0, 0, 0, 0));
            PlayerLookaroundTargetSelection.Step(state, new(true, new((int)input[6], (int)input[7], (int)input[8]), raw), body, host);
            long[] actual = [combat.ActionState, combat.IsCrawling ? 1 : 0, melee.WebAttackStarted, angles.Active ? 1 : 0,
                angles.ActivateTarget, transition.ReticleType, state.ReticleColour, state.SurfaceNormal.X, state.SurfaceNormal.Y, state.SurfaceNormal.Z,
                state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z, state.ZipOrigin.X, state.ZipOrigin.Y, state.ZipOrigin.Z,
                state.SwingFacingTarget.X, state.SwingFacingTarget.Y, state.SwingFacingTarget.Z, ReferenceEquals(state.TorsoTarget, host.Body) ? 1 : ReferenceEquals(state.TorsoTarget, host.Control) ? 2 : 0];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Original PC lookaround target[{string.Join(',', input)}]: true body/switch target, full-vector normal/interaction fields, original reticle colour/type and zip/swing action/animation/crawl selection; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] expectedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(expectedEvents), $"Original PC lookaround target[{string.Join(',', input)}]: original raw target ray, LOS flag, switch/zip/swing priority, source heading and animation/exit/2D-target call order; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', expectedEvents)}].");
        }
    }

    private sealed class Target(FixedVector3 position, ushort type) : IPlayerMeleeTarget
    {
        public object Identity => this;
        public FixedVector3 Position => position;
        public ushort Type => type;
        public ushort ItemFlags => 0;
        public IPlayerMeleeTarget? Next => null;
    }
    private sealed class Host(long[] input, PlayerLookaroundState angles) : IPlayerLookaroundTargetHost
    {
        public IPlayerMeleeTarget Body { get; } = new Target(new(100000, -200000, 300000), 304);
        public IPlayerMeleeTarget Control { get; } = new Target(new(-100000, 200000, -300000), 407);
        public List<long> Events { get; } = [];
        public uint Clock => (uint)input[23];
        public short EffectiveHeading { get { Events.Add(7); return (short)input[15]; } }
        public PlayerLookaroundSphereHit? TraceBaddySphere(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([1, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 0, 4096]);
            return input[17] == 0 ? null : new(Body, input[17] == 1 ? (ushort)0x10 : (ushort)0, start + (end - start) / 2);
        }
        public PlayerLookaroundWorldContact? TraceTargetWorld(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([2, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1, 1]);
            return input[18] == 0 ? null : new(this, end - new FixedVector3(111, 222, 333), new(-123, 2345, -3456), (uint)input[19], 777);
        }
        public PlayerLookaroundSwitchContact ResolveSwitch(object itemIdentity) { Events.Add(3); return new(Control, (int)input[20]); }
        public bool CheckZipAvailability(PlayerLookaroundWorldContact contact, int range) { Events.AddRange([4, range]); return input[21] != 0; }
        public bool CheckSwingAvailability(PlayerLookaroundTargetState state, PlayerLookaroundWorldContact contact)
        { Events.Add(5); if (input[22] != 0) state.SurfaceNormal = new(4096, -2048, 1024); return input[22] != 0; }
        public void BeginZip(PlayerLookaroundWorldContact contact) { }
        public void BeginSwing() { }
        public void SelectAndRunAnimation(ushort animation) => Events.AddRange([6, animation, 0, -1]);
        public void SetTargetTorsoAngle(short wanted, bool fast) => Events.AddRange([8, wanted, fast ? 1 : 0]);
        public void ExitLookaroundMode() { Events.Add(9); angles.Active = false; }
        public void SetTargetVisible(bool visible) => Events.AddRange([10, visible ? 1 : 0]);
        public void SetScreenTarget(FixedVector3 position, ushort radius, short angle) => Events.AddRange([11, position.X, position.Y, position.Z, radius, angle]);
    }
}
