using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Mathematics;

/// <summary>CVenom node states, AI fade tail, scripted walkabout exit and PulseL6A4Node against the PC executable.</summary>
internal static class VenomAiTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VenomAiTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.venom-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        int[][] nodes = root.GetProperty("nodes").EnumerateArray().Select(Words).ToArray();
        int[] random = Words(root.GetProperty("random"));
        string[] fields = root.GetProperty("fields").EnumerateArray().Select(field => field.GetString()!).ToArray();
        int index = 0;
        var modes = new int[6];
        foreach (JsonElement test in root.GetProperty("cases").EnumerateArray())
        {
            int[] values = Words(test.GetProperty("input"));
            var input = fields.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);
            int[] expected = Words(test.GetProperty("s"));
            string[] expectedLog = test.GetProperty("log").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
            (int[] actual, List<string> log) = Execute(nodes, random, input);
            assert(actual.SequenceEqual(expected) && log.SequenceEqual(expectedLog),
                $"Actual PC CVenom AI case {index} mode {input["mode"]}: actual=[{string.Join(',', actual)}] log=[{string.Join(',', log)}] " +
                $"expected=[{string.Join(',', expected)}] log=[{string.Join(',', expectedLog)}].");
            modes[input["mode"]]++;
            index++;
        }
        assert(modes.SequenceEqual([24, 24, 36, 40, 30, 2]),
            "Venom AI fixture keeps 24 LookingToAttack, 24 Walkabout, 36 GoToSwitch, 40 AI tail, 30 scripted walkabout and 2 pulse cases.");
    }

    private static (int[] State, List<string> Log) Execute(int[][] nodes, int[] random, Dictionary<string, int> input)
    {
        int mode = input["mode"];
        VenomState state = new(1, input["level"], input["difficulty"], 0)
        {
            AiState = mode switch { 0 => 2, 1 => 4, 2 => 8, _ => 0x4000 },
            Substate = input["substate"], Field218 = unchecked((uint)input["flags218"]), Field3B4 = input["f3B4"],
            SwitchNodeCount = input["switchCount"], WaypointNode = (short)input["node"], Field224 = (short)input["f224"],
            TargetPosition = new(input["tx"], input["ty"], input["tz"]), BodyFlags = (ushort)input["bodyFlags"],
            Field3AC = input["f3AC"], Field454 = input["f454"], Field458 = input["f458"], Field45C = input["f45C"],
            Field33D = (byte)input["f33D"], Field388 = input["f388"], Field324 = input["f324"], Field34D = (byte)input["f34D"],
            Field34C = (byte)input["f34C"], MinorRadius = unchecked((ushort)input["minor"]), Field3B8 = input["f3B8"],
            Field330 = input["f330"], ItemColor = 0
        };
        var host = new Host(nodes, random, input);
        for (int slot = 0; slot < 4; slot++)
        {
            state.SwitchTargets[slot] = host.Switches[slot];
            state.SwitchNodeIndices[slot] = 11 + 2 * (slot & 1);
        }
        for (int slot = 0; slot < 5; slot++) state.LastSpeech[slot] = input[$"l{slot}"];
        switch (mode)
        {
            case 0: VenomNodeStates.LookingToAttack(state, host); break;
            case 1: VenomNodeStates.Walkabout(state, host); break;
            case 2: VenomNodeStates.GoToSwitch(state, host); break;
            case 3:
                new VenomBrain(state, host, new CinematicActorScriptRuntime(
                    new ActorScriptCursor(ReadOnlyMemory<byte>.Empty, 0), host, CinematicActorScriptProfile.Venom)).Advance();
                break;
            case 4: VenomWalkabout.Tick(state, host, () => host.Log.Add("Y")); break;
            default: VenomPulse.PulseL6A4Node(host, input["pulse"] != 0); break;
        }
        ActorBodyMotion body = host.Body;
        int[] actual =
        [
            state.AiState, state.Substate, unchecked((int)state.Field218), body.Position.X, body.Position.Y, body.Position.Z,
            body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Angles.Y, state.WaypointNode, state.Field224,
            state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z, state.Field3E0, state.Field3B8,
            .. state.LastSpeech,
            host.ItemFlags, unchecked((ushort)state.BodyFlags), state.Field3AC, state.Field454, state.Field458, state.Field45C,
            state.Field33D, unchecked((short)state.MinorRadius), unchecked((int)state.ItemColor), state.OutlineEnabled ? 1 : 0,
            state.OutlineSemiTransparent ? 1 : 0, state.OutlineColor.R, state.OutlineColor.G, state.OutlineColor.B,
            state.Field388, state.Field330, state.Field324, host.LastFootstepSound, state.Field34D, state.Field354,
            state.Field338, host.RandomUsed
        ];
        return (actual, host.Log);
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class Host : IVenomHost
    {
        private readonly int[][] nodes;
        private readonly TriggerNode[] triggers;
        private readonly int[] random;
        private readonly Dictionary<string, int> input;
        private readonly int[] switchStates;
        public object[] Switches { get; } = [new(), new(), new(), new()];
        public List<string> Log { get; } = [];
        public int RandomUsed { get; private set; }

        public Host(int[][] nodes, int[] random, Dictionary<string, int> input)
        {
            this.nodes = nodes;
            this.random = random;
            this.input = input;
            switchStates = [input["s0"], input["s1"], input["s2"], input["s3"]];
            triggers = nodes.Select((node, id) => new TriggerNode((ushort)id, (ushort)node[0], 0,
                Pack((short)node[0], (short)node[1], (short)node[5], (short)node[6]))).ToArray();
            Body.Position = new(input["px"], input["py"], input["pz"]);
            Body.Angles = new((short)input["angX"], (short)input["angY"], 0);
            ItemFlags = (ushort)input["itemFlags"];
            AnimationId = input["anim"];
            LastFootstepSound = input["lastStep"];
        }

        public ActorBodyMotion Body { get; } = new();
        public int SurfaceClearance => 100;
        public int AnimationId { get; private set; }
        public bool AnimationFinished => input["finished"] != 0;
        public int AnimationFrame => input["frame"];
        public ushort ItemFlags { get; set; }
        public int TriggerNodeCount => nodes.Length;
        public TriggerNode TriggerNode(int node) => triggers[node];
        public ushort TriggerNodeTypeCode(int node) => (ushort)nodes[node][0];
        public short TriggerNodeSubtype(int node) => (short)nodes[node][1];
        public IEnumerable<VenomControlSwitch> EnumerateControlSwitches() => [];
        public int LevelId => input["level"];
        public int ElapsedTicks => input["ticks"];
        public int TimerRelated => 4242;
        public FixedVector3 PlayerPosition => new(input["qx"], input["qy"], input["qz"]);
        public FixedVector3 PrimaryAimTarget => PlayerPosition;
        public FixedVector3 SecondaryAimTarget => PlayerPosition;
        public FixedVector3? MechListPosition => PlayerPosition;
        public bool FastForwardRequested => false;
        public int DifficultyLevel => input["difficulty"];
        public int LastFootstepSound { get; set; }
        public ThugMessageQueue Messages { get; } = new();
        public int Random(int bound)
        {
            int value = random[(input["seed"] + RandomUsed++) % random.Length];
            return bound > 0 ? value % bound : 0;
        }
        public void PlayPositionalSound(int sound) => Log.Add($"S{sound}:0");
        public void PlayPositionSpeech(int bank, int track, FixedVector3 position) => Log.Add($"X{bank}:{track}:0");
        public bool LineOfSight(FixedVector3 from, FixedVector3 to) => input["los"] != 0;
        public int SwitchState(object? control) => switchStates[Array.IndexOf(Switches, control)];
        public void FlickSwitch(object? control) => Log.Add($"F{Array.IndexOf(Switches, control)}");
        public void SetPlayerFocusLockTarget() => Log.Add("L");
        public void CreateCompass(FixedVector3 position) => Log.Add($"C{position.X}:{position.Z}");
        public void SendPulseToNode(int node) => Log.Add($"P{node}");
        public void AdjustWaterModel(int waterLevel) => throw new InvalidOperationException("Fixture keeps field_33C clear.");
        public void Die() => Log.Add("D0");
        public void BuildTransform() { }
        public void RunAnimation(int animation)
        {
            Log.Add($"A{animation}:0:-1");
            AnimationId = animation;
        }
        public void CycleAnimation(int animation) => throw new InvalidOperationException();
        public FixedVector3 TriggerPosition(int node) => new(nodes[node][2] << 12, nodes[node][3] << 12, nodes[node][4] << 12);
        public IReadOnlyList<ushort> TriggerLinks(int node) => throw new InvalidOperationException();
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => input["ground"];
        public void PlayVoice(short bank, short track, short priority) => throw new InvalidOperationException();
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source) => throw new InvalidOperationException();

        private static byte[] Pack(params short[] words)
        {
            byte[] bytes = new byte[words.Length * 2];
            for (int index = 0; index < words.Length; index++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
            return bytes;
        }
    }
}
