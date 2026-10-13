using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerAirborneAttackSelectionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerAirborneAttackSelectionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.airborne-attack-selection.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            bool smash = test.GetProperty("kind").GetString() == "smash";
            var combat = new PlayerCombatState(100, 100) { Position = Vector(0), ActionState = (uint)input[smash ? 20 : 14], Field54C = 77,
                LookaroundActive = input[smash ? 10 : 6] != 0 };
            var melee = new PlayerMeleeState(combat);
            var fire = new PlayerFireWebState(combat);
            var inventory = new PlayerWebInventory { FireWebbing = smash ? (byte)0 : (byte)input[17], EmptyUseOverride = smash ? (byte)0 : (byte)input[21] };
            var host = new Host(input, smash, melee, inventory);
            long[] actual;
            if (smash)
            {
                melee.AimTarget = input[6] == 0 ? null : new Target(Vector(3), (ushort)input[7]);
                melee.Input.Triggered[18] = (byte)input[8]; melee.Input.Triggered[19] = (byte)input[9];
                combat.Field8DC = (int)input[17];
                var state = new PlayerSmashAttackState(melee) { Field54D = 88, EntryClock = (int)input[12], PreviousEntryClock = (int)input[13],
                    TargetPosition = Vector(14), Field8D8 = (byte)input[18] };
                bool accepted = PlayerAirborneAttackSelection.CheckSmash(state, host);
                actual = [accepted ? 1 : 0, combat.ActionState, combat.Field54C, state.Field54D, host.FocusHook,
                    host.SwingPresent ? 1 : 0, combat.Field8DC, state.EntryClock, state.PreviousEntryClock, state.Field8D8,
                    state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z, melee.Input.Triggered[18], melee.Input.Triggered[19]];
            }
            else
            {
                combat.UpDirection = Vector(3); combat.HeldObjectPresent = input[7] != 0; combat.IsCrawling = input[15] != 0;
                combat.HeadingTurnTicksRemaining = (int)input[19];
                melee.Input.Pressed[6] = (byte)input[9]; melee.WebAttackStarted = (byte)input[18]; melee.WebAttackMode = (byte)input[20];
                var state = new PlayerStraightZipState(melee, fire, inventory) { TargetPosition = new(11, -12, 13), SurfaceNormal = new(21, -22, 23), Origin = new(31, -32, 33) };
                bool accepted = PlayerStraightZipSelection.Check(state, host);
                actual = [accepted ? 1 : 0, combat.ActionState, combat.Field54C, host.FocusHook, host.SwingPresent ? 1 : 0,
                    melee.WebAttackStarted, combat.HeadingTurnTicksRemaining, melee.WebAttackMode, inventory.EmptyUseOverride,
                    fire.CurrentStrand is null ? 0 : 1, fire.CurrentStrand?.HookIndex ?? -1, fire.CurrentStrand?.FireVariant ?? -1,
                    state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z, state.SurfaceNormal.X, state.SurfaceNormal.Y,
                    state.SurfaceNormal.Z, state.Origin.X, state.Origin.Y, state.Origin.Z];
            }
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC {(smash ? "smash" : "R1zip")}[{string.Join(',', input)}] body/input gate and exact source shared fields; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC {(smash ? "smash" : "R1zip")}[{string.Join(',', input)}] genuine source swing destructor/SwingBack distinction, original target ray/normal, animation/torso order and E10 override around FireWeb; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
            fire.CurrentStrand?.Dispose();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
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

    private sealed class Host : IPlayerSmashAttackSelectionHost, IPlayerStraightZipSelectionHost, IWebStrandOwnerHost
    {
        private readonly long[] input;
        private readonly PlayerMeleeState melee;
        private readonly PlayerWebInventory inventory;
        public Host(long[] input, bool smash, PlayerMeleeState melee, PlayerWebInventory inventory)
        { this.input = input; this.melee = melee; this.inventory = inventory; SwingPresent = input[smash ? 11 : 16] != 0; Clock = smash ? (uint)input[19] : 0; }
        public List<long> Events { get; } = [];
        public int FocusHook { get; private set; } = 2;
        public uint Clock { get; }
        public bool SwingPresent { get; private set; }
        public byte SwingHeldLatch => (byte)input[8];
        public object PlayerIdentity => this;
        public int PlayerWebShot => melee.WebAttackMode;
        public void SetCameraFocusHook(int hook) => FocusHook = hook;
        public void ReleaseSwing(bool createSwingBack) { if (createSwingBack) Events.Add(1); Events.Add(2); SwingPresent = false; }
        public void SetTargetTorsoAngle(FixedVector3 point) => Events.AddRange([3, point.X, point.Y, point.Z]);
        public void SelectAndRunAnimation(ushort animation) => SelectAndRunAnimation(animation, 0);
        public void SelectAndRunAnimation(ushort animation, int firstFrame) => Events.AddRange([4, animation, firstFrame, -1]);
        public void CreateSmashAttackTrails() => Events.Add(5);
        public PlayerStraightZipContact? TraceWorld(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([6, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1]);
            return input[10] == 0 ? null : new(this, end - new FixedVector3(111, 222, 333), new(-123, (short)input[12], -3456), (uint)input[11]);
        }
        public bool CheckZipAvailability(PlayerStraightZipContact contact, int range) { Events.AddRange([7, range]); return input[13] != 0; }
        public WebStrandOwner CreateCurrentStrand() { Events.Add(8); return new(this); }
        public int FireWeb(PlayerFireWebRequest request)
        {
            Events.AddRange([10, request.UseHeldTarget ? 1 : 0, request.Cost, request.Target.X, request.Target.Y, request.Target.Z,
                request.HitSomething ? 1 : 0, request.Normal.X, request.Normal.Y, request.Normal.Z, melee.WebAttackMode, inventory.EmptyUseOverride]);
            return 0;
        }
        public void BeginZip(PlayerStraightZipContact contact) { }
        public void AttachOwner(WebStrandOwner owner) => Events.Add(9);
        public void DetachOwner(WebStrandOwner owner) { }
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => throw new InvalidOperationException("The guard proof observes FireWeb's separately verified typed boundary.");
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => throw new InvalidOperationException("The guard proof does not move source web actors.");
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) => throw new InvalidOperationException("The guard proof does not draw a separately verified FireWeb splat.");
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) => throw new InvalidOperationException("Source R1zip has no baddy yank target.");
    }
}
