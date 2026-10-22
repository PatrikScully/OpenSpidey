using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

/// <summary>CLizMan::DoAISwitchLogic (fight probe, warn, PlayerIsVisible) and ProcessMessages against the PC executable.</summary>
internal static class LizManAiTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(LizManAiTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.lizman-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        string[] fields = document.RootElement.GetProperty("fields").EnumerateArray().Select(field => field.GetString()!).ToArray();
        int index = 0, probes = 0, messages = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] values = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var input = fields.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);
            int[] expected = test.GetProperty("s").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            string[] expectedLog = test.GetProperty("log").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
            (int[] actual, List<string> log) = Execute(input);
            assert(actual.SequenceEqual(expected) && log.SequenceEqual(expectedLog),
                $"Actual PC CLizMan AI case {index} mode {input["mode"]}: actual=[{string.Join(',', actual)}] log=[{string.Join(',', log)}] " +
                $"expected=[{string.Join(',', expected)}] log=[{string.Join(',', expectedLog)}].");
            if (input["mode"] == 0) probes++; else messages++;
            index++;
        }
        assert(probes == 140 && messages == 80, "LizMan AI fixture keeps 140 DoAISwitchLogic and 80 ProcessMessages cases.");
    }

    private static (int[] State, List<string> Log) Execute(Dictionary<string, int> input)
    {
        var state = new LizManState(5, 0x505, 0, input["phase"])
        {
            State = input["state"], Substate = input["substate"], Health = (short)input["health"], Field390 = (byte)input["f390"],
            Field374 = input["f374"], Field380 = input["f380"], CombatFlags = unchecked((uint)input["f2A8"]),
            LastSeenPlayerPosition = new(input["sx"], input["sy"], input["sz"]), Field39C = (byte)input["f39C"],
            Field1F0 = input["f1F0"], Field1F4 = input["f1F4"], CachedPlayerDistance = input["cacheDist"],
            DistanceSampleClock = input["cacheClock"], BehaviorFlags = unchecked((uint)input["f218"]), Field1F8 = input["f1F8"]
        };
        var host = new Host(input, state);
        var ownership = new LizManAttackOwnership
        {
            GlobalLizMan = Identity(input["global"], host), Shooter = Identity(input["shooter"], host)
        };
        var queue = new ThugMessageQueue();
        int count = input["messages"];
        var sent = new ThugMessage[count];
        for (int slot = count - 1; slot >= 0; slot--)
        {
            sent[slot] = new ThugMessage(input[$"m{slot}code"], input[$"m{slot}sender"] != 0 ? host.Sender : null)
            {
                Vector0 = new(input["vx"], input["vy"], input["vz"])
            };
            queue.Enqueue(sent[slot]);
        }
        if (input["mode"] == 0) LizManPerception.DoAiSwitchLogic(state, ownership, host);
        else LizManMessages.Process(state, queue, host);
        if (input["mode"] != 0) host.Log.Add("U");
        int[] actual =
        [
            state.State, state.Substate, state.Field380, state.Field374, unchecked((int)state.CombatFlags),
            state.LastSeenPlayerPosition.X, state.LastSeenPlayerPosition.Y, state.LastSeenPlayerPosition.Z,
            state.CachedPlayerDistance, state.DistanceSampleClock, Ident(ownership.GlobalLizMan, host), Ident(ownership.Shooter, host),
            state.Field310, state.Field1F8, unchecked((int)host.RouteFlags), .. sent.Select(message => message.Flags)
        ];
        return (actual, host.Log);
    }

    private static object? Identity(int code, Host host) => code switch { 0 => null, 1 => host.AttackIdentity, _ => host.Others[0] };
    private static int Ident(object? value, Host host) => value is null ? 0 : ReferenceEquals(value, host.AttackIdentity) ? 1 : 2;

    private sealed class Host : ILizManMessageHost
    {
        private readonly Dictionary<string, int> input;
        private readonly int[] paths, adds;
        private int pathUsed, addUsed;
        public object[] Others { get; } = [new(), new()];
        public object Sender { get; } = new();
        public List<string> Log { get; } = [];

        public Host(Dictionary<string, int> input, LizManState state)
        {
            this.input = input;
            paths = [input["path0"], input["path1"], input["path2"], input["path3"]];
            adds = [input["add0"], input["add1"], input["add2"], input["add3"]];
            Body.Position = new(input["px"], input["py"], input["pz"]);
            Body.Angles = new(0, (short)input["angY"], 0);
            RouteFlags = unchecked((uint)input["f2F0"]);
        }

        public ActorBodyMotion Body { get; } = new();
        public int AttackClock => input["clock"];
        public int TotalTime => input["timer"];
        public FixedVector3 PlayerPosition => new(input["qx"], input["qy"], input["qz"]);
        public short PlayerHealth => (short)input["playerHealth"];
        public byte PlayerUnavailable => (byte)input["playerUnavailable"];
        public bool PlayerIsDead => (input["playerFlags"] & 0x40) != 0;
        public object AttackIdentity { get; } = new();
        public uint RouteFlags { get; set; }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to)
        {
            bool player = to == PlayerPosition;
            Log.Add(player ? "V0" : "V1");
            return (player ? input["losPlayer"] : input["losOther"]) != 0;
        }
        public int PathCheck(FixedVector3 from, FixedVector3 to)
        {
            int result = paths[pathUsed++ & 3];
            Log.Add($"P{to.X}:{to.Z}:{result}");
            return result;
        }
        public ActorPathCheckResult PathCheckWithHit(FixedVector3 from, FixedVector3 to)
        {
            int result = PathCheck(from, to);
            return new(result, result != 0 ? new FixedVector3(input["hx"], input["hy"], input["hz"]) : to);
        }
        public bool AddPointToPath(FixedVector3 point, int distanceLimit)
        {
            int result = adds[addUsed++ & 3];
            Log.Add($"A{point.X}:{point.Y}:{point.Z}:{distanceLimit}:{result}");
            return result != 0;
        }
        public void Neutralize() => Log.Add("N");
        public void SendSignal() => Log.Add("G");
        public IEnumerable<LizManWarnCandidate> OtherLizMen()
        {
            for (int slot = 0; slot < input["others"]; slot++)
            {
                if (input[$"o{slot}type"] != 317) continue;
                yield return new(Others[slot], new(input[$"o{slot}x"], input[$"o{slot}y"], input[$"o{slot}z"]), input[$"o{slot}t"]);
            }
        }
        public void SendMessage(object target, int code) => Log.Add($"M{Array.IndexOf(Others, target)}:{code}");
        public ActorProcedureState Actor => throw new InvalidOperationException();
        public ActorProcedureList Procedures => throw new InvalidOperationException();
        public int AnimationId => throw new InvalidOperationException();
        public int AnimationFrame => throw new InvalidOperationException();
        public bool AnimationFinished => throw new InvalidOperationException();
        public ushort ItemFlags { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }
        public int ElapsedTicks => throw new InvalidOperationException();
        public bool HasMessages => throw new InvalidOperationException();
        public bool HasLinkedClimbNode => throw new InvalidOperationException();
        public bool TryAdvanceWaypoint() => throw new InvalidOperationException();
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => throw new InvalidOperationException();
        public void SetHeight(bool initial, int above, int below) => throw new InvalidOperationException();
        public void RunAnimation(int animation) => throw new InvalidOperationException();
        public void BuildTransform() => throw new InvalidOperationException();
        public void ProcessMessages() => throw new InvalidOperationException();
        public void UpdateFootstepBits() => throw new InvalidOperationException();
        public void PlaySounds() => throw new InvalidOperationException();
        public ActorBodyMotion? RecoverPlayerBody() => throw new InvalidOperationException();
        public void SetAnimationFrame(int frame) => throw new InvalidOperationException();
        public void KillShadow() => throw new InvalidOperationException();
        public int ScanNearbyNodesForJumpTarget() => throw new InvalidOperationException();
        public FixedVector3 TriggerPosition(int node) => throw new InvalidOperationException();
        public void PlayPositionalSound(int id) => throw new InvalidOperationException();
        public int Random(int bound) => throw new InvalidOperationException();
        public FixedVector3? RecoverSenderPosition(object? handle) =>
            handle is null ? null : new FixedVector3(input["senderX"], input["senderY"], input["senderZ"]);
    }
}
