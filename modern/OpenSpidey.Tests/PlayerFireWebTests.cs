using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerFireWebTests
{
    private sealed class Target(ushort type) : IPlayerFireWebTarget
    {
        public object Identity => this;
        public ushort Type => type;
        public FixedVector3 Position => new(1048576, -327680, 1572864);
        public uint BehaviorFlags { get; set; }
        public IReadOnlyList<FixedVector3>? ActiveTrapStrandEndpoints => null;
    }

    private sealed class Host(int[] input, PlayerFireWebState state) : IPlayerFireWebHost, IWebStrandOwnerHost
    {
        public List<int> Events { get; } = [];
        public Target Baddy { get; } = new(304);
        public Target Control { get; } = new(407);
        public object PlayerIdentity { get; } = new();
        public int PlayerWebShot => WebShot;
        public FixedVector3 Position => new(123456, -654321, 765432);
        public FixedVector3 ForwardDirection => new(0, 0, 4096);
        public FixedVector3 CameraPositionWholeUnits => new(0, 0, -1000);
        public IPlayerFireWebTarget? AimTarget => input[3] switch { 0 => null, 1 => Baddy, _ => Control };
        public int WebShot => input[5];
        public ushort Animation => (ushort)input[6];
        public byte FireWebbing => (byte)input[7];
        public bool VibrationEnabled => input[8] != 0;
        public bool SplatCreated { get; private set; }
        public ActorAngles SplatNormal { get; private set; }
        public int DetachCount { get; private set; }
        public int Random(int bound) => 0;
        public bool DecreaseWebbing(int amount) { Events.AddRange([3, amount]); return input[9] != 0; }
        public FixedVector3 GetPlayerHookPosition(int hook)
        {
            Events.AddRange([4, hook]);
            return new(100000 + hook * 8193, -300000 - hook * 4097, 500000 + hook * 12289);
        }
        public PlayerKickSwitchSelection SelectTargetSwitch(int maximumDistance, int minimumFacing, int weight, int facingWeight)
        {
            Events.AddRange([1, maximumDistance, minimumFacing, weight, facingWeight]);
            return new(default, input[10] != 0 ? new FixedVector3(2097152, -655360, 3145728) : null);
        }
        public PlayerFireWebContact? TraceEnvironment(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([2, 1, 1, start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            return input[11] == 0 ? null : new(new(-1234567, -2345678, 3456789), new(0, -4096, 0), 0x02000000, Control);
        }
        public bool SwitchEnabled(IPlayerFireWebTarget target) => ReferenceEquals(target, Control) && input[12] != 0;
        public void FlickSwitch(IPlayerFireWebTarget target)
        {
            if (!ReferenceEquals(target, Control)) throw new InvalidDataException("FireWeb lost the genuine selected switch identity.");
            Events.Add(9);
        }
        public bool TrapWeb(IPlayerFireWebTarget target) { Events.Add(10); return input[13] != 0; }
        public void AddTrapWebEffect(IPlayerFireWebTarget target, byte fireVariant) => Events.AddRange([12, fireVariant]);
        public int TugWeb(IPlayerFireWebTarget target) { Events.Add(11); return input[14]; }
        public void CreateImpactWeb(PlayerImpactWebRequest request) => Events.AddRange([13,
            request.Position.X, request.Position.Y, request.Position.Z, request.Direction.X, request.Direction.Y,
            request.Direction.Z, request.Speed, request.Damage, request.Lifetime]);
        public void CreateFireImpactWeb(FixedVector3 position, ActorAngles direction, int damage) =>
            Events.AddRange([14, 2, position.X, position.Y, position.Z, direction.X, direction.Y, direction.Z, damage, 0]);
        public int PlayPositionSound(ushort sound, FixedVector3 position, int pitch)
        { Events.AddRange([7, sound, position.X, position.Y, position.Z, pitch]); return 42; }
        public void PlaySound(ushort sound, int volume, int pitch) => Events.AddRange([8, sound, volume, pitch]);
        public int ActuatorTime(int controller, int actuator) { Events.AddRange([5, controller, actuator]); return input[15]; }
        public void ActivateActuator(int controller, int duration, int actuator, int strength) =>
            Events.AddRange([6, controller, duration, actuator, strength]);
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => target is null ? default : new(target, 123);
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => handle.Target as IWebStrandTarget;
        public void AttachOwner(WebStrandOwner owner) { }
        public void DetachOwner(WebStrandOwner owner) { DetachCount++; state.DetachStrand(owner); }
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random)
        { SplatCreated = true; SplatNormal = normal; }
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) { }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerFireWebTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-fire-web.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var combat = new PlayerCombatState(100, 100);
            var state = new PlayerFireWebState(combat) { TrapSoundHandle = input[16] };
            var host = new Host(input, state);
            WebStrandOwner? strand = input[4] == 0 ? null : new(host) { HookIndex = 1 };
            state.CurrentStrand = strand;
            int result = PlayerFireWebBehavior.Fire(state, new(input[0] != 0, input[1],
                new(4194304, -1048576, 6291456), input[2] != 0, new(7, -8, 9)), host);
            int[] actual = [result, state.CurrentStrand is null ? 0 : 1, state.SwitchAimPosition is null ? 0 : 1,
                state.TrapSoundHandle, (int)host.Baddy.BehaviorFlags, strand?.Mode ?? 0,
                strand?.FirePosition.X ?? 0, strand?.FirePosition.Y ?? 0, strand?.FirePosition.Z ?? 0,
                strand?.TargetPosition.X ?? 0, strand?.TargetPosition.Y ?? 0, strand?.TargetPosition.Z ?? 0,
                strand?.TargetHandle.Target is IPlayerFireWebTarget target ? target.Type : 0, host.SplatCreated ? 1 : 0];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(14).Select(value => value.GetInt32())),
                $"FireWeb source case {index}: retained current strand/switch pointer, hook/aim/splat, trap audio handle and target tug flag.");
            assert(!host.SplatCreated || new[] { host.SplatNormal.X, host.SplatNormal.Y, host.SplatNormal.Z }
                .Select(value => (int)value).SequenceEqual(expected.EnumerateArray().Skip(14).Take(3).Select(value => value.GetInt32())),
                $"FireWeb source case {index}: explicit/environment contact normal reaches the actual strand-splat boundary.");
            assert(host.Events.SequenceEqual(expected[17].EnumerateArray().Select(value => value.GetInt32())),
                $"FireWeb source case {index}: exact target ray/cost, posed midpoint shot, virtual trap/tug, switch, vibration and ordered audio callbacks.");
            assert(combat.LoopingSoundHandle == state.TrapSoundHandle && combat.WebPresent == (state.CurrentStrand is not null),
                "FireWeb and player Hit share actual source5E4/E6C fields, so damage can stop its loop and release the genuine current web.");
            strand?.Dispose();
            assert(state.CurrentStrand is null && host.DetachCount == (strand is null ? 0 : 1),
                "The genuine CWeb destructor detaches its owner and clears only its matching player current-web pointer.");
            index++;
        }
    }
}
