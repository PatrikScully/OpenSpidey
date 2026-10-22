using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

/// <summary>CVenom fight selection, jump, hit reactions, trap web and AI message loop against the PC executable.</summary>
internal static class VenomCombatTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VenomCombatTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.venom-combat.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        int[][] nodes = root.GetProperty("nodes").EnumerateArray().Select(Words).ToArray();
        int[] random = Words(root.GetProperty("random"));
        string[] inputs = root.GetProperty("inputs").EnumerateArray().Select(field => field.GetString()!).ToArray();
        string[] outputs = root.GetProperty("outputs").EnumerateArray().Select(field => field.GetString()!).ToArray();
        int index = 0;
        var modes = new int[12];
        foreach (JsonElement test in root.GetProperty("cases").EnumerateArray())
        {
            long[] values = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var input = inputs.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);
            int[] expected = Words(test.GetProperty("s"));
            int[] expectedPath = Words(test.GetProperty("path"));
            string[] expectedLog = test.GetProperty("log").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
            (int[] actual, int[] path, List<string> log) = Execute(nodes, random, outputs, input);
            assert(actual.SequenceEqual(expected) && path.SequenceEqual(expectedPath) && log.SequenceEqual(expectedLog),
                $"Actual PC CVenom combat case {index} mode {input["mode"]}: " +
                $"diff=[{string.Join(',', outputs.Select((name, slot) => slot < actual.Length && actual[slot] != expected[slot] ? $"{name}:{actual[slot]}!={expected[slot]}" : "").Where(text => text.Length != 0))}] " +
                $"log=[{string.Join(',', log)}] expected=[{string.Join(',', expectedLog)}] pathOk={path.SequenceEqual(expectedPath)}.");
            modes[input["mode"]]++;
            index++;
        }
        assert(modes.Sum() == 1616 && modes[6] == 300 && modes[7] == 220,
            "Venom combat fixture keeps 1616 cases including 300 Hit and 220 AI message-loop cases.");
    }

    private static (int[] State, int[] Path, List<string> Log) Execute(int[][] nodes, int[] random, string[] outputs, Dictionary<string, long> input)
    {
        var rig = new Rig(nodes, random, input);
        rig.Apply();
        VenomState state = rig.State;
        Host host = rig.Host;
        int result = 0;
        switch (input["mode"])
        {
            case 0: VenomFight.EnterFightMode(state, host, host); break;
            case 1: VenomJump.Jump(state, host, host); break;
            case 2: VenomFight.GetWithinAttackRange(state, host); break;
            case 3: VenomFight.TakeHit(state, host, host); break;
            case 4: VenomFight.GetTrapped(state, host, host); break;
            case 5: VenomFight.VenomDie(state, host); break;
            case 6:
                result = VenomFight.Hit(state, host, host, new ActorHit((byte)input["hFlags"], 0, (int)input["hKind"], (ushort)input["hDmg"],
                    new((int)input["hdx"], (int)input["hdy"], (int)input["hdz"]), (short)input["hSlideD"], (ushort)input["hSlideF"])) ? 1 : 0;
                break;
            case 7:
                new VenomBrain(state, host, new CinematicActorScriptRuntime(
                    new ActorScriptCursor(ReadOnlyMemory<byte>.Empty, 0), host, CinematicActorScriptProfile.Venom)).Advance();
                break;
            case 8: result = VenomJump.ScanNodesForJumpTarget(state, host); break;
            case 9:
                state.JumpPath = null;
                VenomJump.CalculateJumpPositionArray(state, host, state.JumpTarget, (int)input["count"]);
                break;
            case 10: VenomFight.CheckSlideParams(state, host, host); break;
            default: result = unchecked((int)VenomFight.CalculateGetWithinAttackRangePoint(state, host)); break;
        }
        int[] actual = outputs.Select(name => rig.Read(name, result)).ToArray();
        var path = new List<int>();
        if (input["mode"] == 9 && state.JumpPath is { } points)
            foreach (FixedVector3 point in points.Take(48)) path.AddRange([point.X, point.Y, point.Z]);
        return (actual, path.ToArray(), host.Log);
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => (int)value.GetInt64()).ToArray();

    private sealed class Rig
    {
        private readonly Dictionary<string, long> input;
        public VenomState State { get; }
        public Host Host { get; }
        private readonly List<ThugMessage> messages = [];

        public Rig(int[][] nodes, int[] random, Dictionary<string, long> input)
        {
            this.input = input;
            State = new(1, (int)input["level"], (int)input["difficulty"], 0);
            Host = new(nodes, random, input, State);
        }

        public void Apply()
        {
            VenomState s = State;
            Host h = Host;
            ActorBodyMotion body = h.Body;
            foreach ((string name, long raw) in input)
            {
                int v = unchecked((int)raw);
                switch (name)
                {
                    case "ai": s.AiState = v; break;
                    case "sub": s.Substate = v; break;
                    case "f218": s.Field218 = unchecked((uint)v); break;
                    case "item": h.ItemFlags = (ushort)v; break;
                    case "body": s.BodyFlags = (ushort)v; break;
                    case "minor": s.MinorRadius = (ushort)v; break;
                    case "health": s.Health = (short)v; break;
                    case "frame": h.Frame = (short)v; break;
                    case "anim": h.Anim = (ushort)v; break;
                    case "finished": h.Finished = (byte)v != 0; break;
                    case "px": body.Position = body.Position with { X = v }; break;
                    case "py": body.Position = body.Position with { Y = v }; break;
                    case "pz": body.Position = body.Position with { Z = v }; break;
                    case "ax": body.Angles = body.Angles with { X = (short)v }; break;
                    case "ay": body.Angles = body.Angles with { Y = (short)v }; break;
                    case "az": body.Angles = body.Angles with { Z = (short)v }; break;
                    case "vx": body.Velocity = body.Velocity with { X = v }; break;
                    case "vy": body.Velocity = body.Velocity with { Y = v }; break;
                    case "vz": body.Velocity = body.Velocity with { Z = v }; break;
                    case "tx": s.TargetPosition = s.TargetPosition with { X = v }; break;
                    case "ty": s.TargetPosition = s.TargetPosition with { Y = v }; break;
                    case "tz": s.TargetPosition = s.TargetPosition with { Z = v }; break;
                    case "retAi": s.ReturnAiState = (short)v; break;
                    case "retSub": s.ReturnSubstate = (short)v; break;
                    case "f39C": s.Field39C = v; break;
                    case "f3E0": s.Field3E0 = v; break;
                    case "f388": s.Field388 = v; break;
                    case "f368": s.AccumulatedDamage = v; break;
                    case "f36C": s.InterruptedAiState = v; break;
                    case "f370": s.InterruptedSubstate = v; break;
                    case "f374": s.LastHitTime = v; break;
                    case "slide": s.SlideTicks = v; break;
                    case "sx": s.SlideVelocity = s.SlideVelocity with { X = v }; break;
                    case "sy": s.SlideVelocity = s.SlideVelocity with { Y = v }; break;
                    case "sz": s.SlideVelocity = s.SlideVelocity with { Z = v }; break;
                    case "f318": s.HitKind = v; break;
                    case "jumpIdx": s.JumpIndex = v; break;
                    case "jumpCount": s.JumpCount = v; break;
                    case "jx": s.JumpTarget = s.JumpTarget with { X = v }; break;
                    case "jy": s.JumpTarget = s.JumpTarget with { Y = v }; break;
                    case "jz": s.JumpTarget = s.JumpTarget with { Z = v }; break;
                    case "trapCtr": s.TrapCounter = v; break;
                    case "stretch": s.StretchTimer = v; break;
                    case "f330": s.Field330 = v; break;
                    case "webT": h.TrapHeld = v != 0; break;
                    case "webG": h.TugHeld = v != 0; break;
                    case "webObj": s.WebHeld = v != 0; break;
                    case "elec": s.ElectrifiedPresent = v != 0; break;
                    case "f33D": s.Field33D = (byte)v; break;
                    case "f324": s.Field324 = v; break;
                    case "pAction": h.PlayerAction = unchecked((uint)v); break;
                    case "pHealth": h.PlayerHp = (short)v; break;
                    case "p36C": h.Player36C = v; break;
                    case "pCrawl": h.Crawl = (byte)v != 0; break;
                    case "p8E8": h.P8E8 = (byte)v; break;
                    case "p8E9": h.P8E9 = (byte)v; break;
                    case "qx": h.Player = h.Player with { X = v }; break;
                    case "qy": h.Player = h.Player with { Y = v }; break;
                    case "qz": h.Player = h.Player with { Z = v }; break;
                }
            }
            if (input["mode"] != 9 && s.JumpCount > 0)
            {
                s.JumpPath = new FixedVector3[s.JumpCount];
                for (int i = 0; i < s.JumpCount; i++)
                    s.JumpPath[i] = new((int)(i * 100 + input["pathSeed"]), i * 7 - 50, 13 * i);
            }
            foreach (string name in new[] { "m2", "m1", "m0" })
            {
                int code = (int)input[name];
                if (code == 0) continue;
                var message = new ThugMessage(code);
                messages.Insert(0, message);
                h.Messages.Enqueue(message);
            }
        }

        public int Read(string name, int result)
        {
            VenomState s = State;
            Host h = Host;
            ActorBodyMotion body = h.Body;
            switch (name)
            {
                case "ret": return result;
                case "used": return h.RandomUsed;
                case "ai": return s.AiState;
                case "sub": return s.Substate;
                case "f218": return unchecked((int)s.Field218);
                case "item": return h.ItemFlags;
                case "body": return (ushort)s.BodyFlags;
                case "minor": return s.MinorRadius;
                case "health": return s.Health;
                case "anim": return h.Anim;
                case "frame": return h.Frame;
                case "px": return body.Position.X;
                case "py": return body.Position.Y;
                case "pz": return body.Position.Z;
                case "ax": return body.Angles.X;
                case "ay": return body.Angles.Y;
                case "az": return body.Angles.Z;
                case "vx": return body.Velocity.X;
                case "vy": return body.Velocity.Y;
                case "vz": return body.Velocity.Z;
                case "tx": return s.TargetPosition.X;
                case "ty": return s.TargetPosition.Y;
                case "tz": return s.TargetPosition.Z;
                case "retAi": return s.ReturnAiState;
                case "retSub": return s.ReturnSubstate;
                case "f3E0": return s.Field3E0;
                case "f388": return s.Field388;
                case "f368": return s.AccumulatedDamage;
                case "f36C": return s.InterruptedAiState;
                case "f370": return s.InterruptedSubstate;
                case "f374": return s.LastHitTime;
                case "slide": return s.SlideTicks;
                case "sx": return s.SlideVelocity.X;
                case "sy": return s.SlideVelocity.Y;
                case "sz": return s.SlideVelocity.Z;
                case "f318": return s.HitKind;
                case "jumpIdx": return s.JumpIndex;
                case "jumpCount": return s.JumpCount;
                case "jx": return s.JumpTarget.X;
                case "jy": return s.JumpTarget.Y;
                case "jz": return s.JumpTarget.Z;
                case "trapCtr": return s.TrapCounter;
                case "stretch": return s.StretchTimer;
                case "f330": return s.Field330;
                case "webT": return h.TrapHeld ? 1 : 0;
                case "webG": return h.TugHeld ? 1 : 0;
                case "webObj": return s.WebHeld ? 1 : 0;
                case "elec": return s.ElectrifiedPresent ? 1 : 0;
                case "f33D": return s.Field33D;
                case "f324": return s.Field324;
                case "o198": return h.Arm0.X;
                case "o200": return h.Arm0.Y;
                case "o202": return h.Arm0.Z;
                case "o210": return h.Arm1.X;
                case "o212": return h.Arm1.Y;
                case "o214": return h.Arm1.Z;
                case "qx": return h.Player.X;
                case "qy": return h.Player.Y;
                case "qz": return h.Player.Z;
                case "msg0": return messages.Count > 0 && input["m0"] != 0 ? messages.First(m => m.Code == (int)input["m0"]).Flags & 0xFF : 0;
                case "msg1": return input["m1"] != 0 ? FlagsOfSlot(1) : 0;
                case "msg2": return input["m2"] != 0 ? FlagsOfSlot(2) : 0;
                case "pathFreed": return s.JumpPath is null ? 1 : 0;
                default: throw new InvalidDataException($"Unmapped output {name}.");
            }
        }

        private int FlagsOfSlot(int slot)
        {
            // Messages were inserted at index 0 for m2, m1, m0, so the list is [m0, m1, m2] without the zero codes.
            int position = 0;
            for (int earlier = 0; earlier < slot; earlier++) if (input[$"m{earlier}"] != 0) position++;
            return messages[position].Flags & 0xFF;
        }
    }

    private sealed class Host : IVenomHost, IVenomCombatHost
    {
        private readonly int[][] nodes;
        private readonly TriggerNode[] triggers;
        private readonly int[] random;
        private readonly Dictionary<string, long> input;
        private readonly VenomState state;
        private int losCalls;
        private int seenProcedures;
        public List<string> Log { get; } = [];
        public int RandomUsed { get; private set; }
        public ushort Anim { get; set; }
        public short Frame { get; set; }
        public bool Finished { get; set; }
        public bool TrapHeld { get; set; }
        public bool TugHeld { get; set; }
        public uint PlayerAction { get; set; }
        public short PlayerHp { get; set; }
        public int Player36C { get; set; }
        public bool Crawl { get; set; }
        public byte P8E8 { get; set; }
        public byte P8E9 { get; set; }
        public FixedVector3 Player { get; set; }
        public FixedVector3 Arm0 { get; private set; }
        public FixedVector3 Arm1 { get; private set; }

        public Host(int[][] nodes, int[] random, Dictionary<string, long> input, VenomState state)
        {
            this.nodes = nodes;
            this.random = random;
            this.input = input;
            this.state = state;
            triggers = nodes.Select((node, id) => new TriggerNode((ushort)id, (ushort)node[0], 0,
                Pack((short)node[0], (short)node[1], 0, 0))).ToArray();
        }

        public IVenomCombatHost? Combat => this;
        public ActorBodyMotion Body { get; } = new();
        private ActorProcedureState? actorState;
        public ActorProcedureState Actor => actorState ??= new(Body);
        public ActorProcedureList Procedures { get; } = new();
        public int SurfaceClearance => 100;
        public int AnimationId => Anim;
        public bool AnimationFinished => Finished;
        public int AnimationFrame => Frame;
        public ushort ItemFlags { get; set; }
        public int TriggerNodeCount => nodes.Length;
        public TriggerNode TriggerNode(int node) => triggers[node];
        public ushort TriggerNodeTypeCode(int node) => (ushort)nodes[node][0];
        public short TriggerNodeSubtype(int node) => (short)nodes[node][1];
        public IEnumerable<VenomControlSwitch> EnumerateControlSwitches() => [];
        public int LevelId => (int)input["level"];
        public int ElapsedTicks => (int)input["ticks"];
        public int TimerRelated => (int)input["timer"];
        public FixedVector3 PlayerPosition => Player;
        public FixedVector3 PrimaryAimTarget => Player;
        public FixedVector3 SecondaryAimTarget => Player;
        public FixedVector3? MechListPosition => Player;
        public bool FastForwardRequested => false;
        public int DifficultyLevel => (int)input["difficulty"];
        public int LastFootstepSound { get; set; }
        public ThugMessageQueue Messages { get; } = new();
        public int Random(int bound)
        {
            int value = random[((int)input["seed"] + RandomUsed++) % random.Length];
            return bound > 0 ? value % bound : 0;
        }
        public void PlayPositionalSound(int sound) => Log.Add($"S{sound}:0");
        public void PlayPositionSpeech(int bank, int track, FixedVector3 position) => Log.Add($"X{bank}:{track}:0");
        public bool LineOfSight(FixedVector3 from, FixedVector3 to)
        {
            int bit = losCalls < 32 ? losCalls : 31;
            losCalls++;
            return ((uint)input["losMask"] >> bit & 1) != 0;
        }
        public int SwitchState(object? control) => 0;
        public void FlickSwitch(object? control) { }
        public void SetPlayerFocusLockTarget() { }
        public void CreateCompass(FixedVector3 position) => Log.Add($"C{position.X}:{position.Z}");
        public void SendPulseToNode(int node) => Log.Add($"P{node}");
        public void AdjustWaterModel(int waterLevel) => throw new InvalidOperationException("Fixture keeps field_33C clear.");
        public void Die() => Log.Add("D0");
        public void BuildTransform() { }
        public void RunAnimation(int animation)
        {
            if (Procedures.Count > seenProcedures)
            {
                Log.Add("Z14");
                seenProcedures = Procedures.Count;
            }
            Log.Add($"A{animation}:0:-1");
            Anim = (ushort)animation;
        }
        public void CycleAnimation(int animation) => throw new InvalidOperationException();
        public FixedVector3 TriggerPosition(int node) => new(nodes[node][2] << 12, nodes[node][3] << 12, nodes[node][4] << 12);
        public IReadOnlyList<ushort> TriggerLinks(int node) => throw new InvalidOperationException();
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        {
            int mode = (int)input["groundMode"], parameter = (int)input["groundParam"];
            if (mode == 0) return -1;
            if (mode == 1) return parameter << 12;
            int x = position.X >> 12, z = position.Z >> 12;
            return ((x + z) & 7) == 5 ? -1 : ((x & 15) << 12) - (parameter << 12);
        }
        public void PlayVoice(short bank, short track, short priority) => throw new InvalidOperationException();
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source) => throw new InvalidOperationException();

        public short PlayerHealth => PlayerHp;
        uint IVenomCombatHost.PlayerActionState => PlayerAction;
        int IVenomCombatHost.PlayerField36C => Player36C;
        bool IVenomCombatHost.PlayerCrawling => Crawl;
        byte IVenomCombatHost.PlayerField8E8 => P8E8;
        byte IVenomCombatHost.PlayerField8E9 => P8E9;
        public void SetAnimationFrame(int frame) => Frame = (short)frame;
        public bool WebHeld(bool tug) => tug ? TugHeld : TrapHeld;
        public void BurstWeb(bool tug)
        {
            if (!WebHeld(tug)) return;
            bool recoverable = tug ? input["recG"] != 0 : input["recT"] >= 0;
            if (recoverable) Log.Add(tug ? "B1" : "B0");
            if (tug) TugHeld = false; else TrapHeld = false;
        }
        public int TrapWebHookCount()
        {
            if (!TrapHeld) return -1;
            if (input["recT"] < 0)
            {
                TrapHeld = false;
                return -1;
            }
            return (int)input["recT"];
        }
        public void SetArmPose(FixedVector3 first, FixedVector3 second)
        {
            Arm0 = first;
            Arm1 = second;
        }
        public void SendSelfMessage(int code) => Log.Add($"Q{code}");
        public VenomLineHit? LineToItem(FixedVector3 start, FixedVector3 end)
        {
            int length = VenomLineProbe.Length(start, end);
            return input["lineHit"] != 0 && length > 0 ? new VenomLineHit((int)input["lineDist"], length) : null;
        }
        public void SetElectrified(bool present)
        {
            if (!present) Log.Add("V");
        }
        public void DeleteWeb() => Log.Add("V");

        private static byte[] Pack(params short[] words)
        {
            byte[] bytes = new byte[words.Length * 2];
            for (int index = 0; index < words.Length; index++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
            return bytes;
        }
    }
}
