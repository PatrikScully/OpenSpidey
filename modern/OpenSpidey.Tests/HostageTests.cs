using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;

internal static class HostageTests
{
    private sealed record Handle(int Id);
    private sealed class Host(HostageState state, HostageMessageQueue messages, uint seed) : IHostageHost, IHostageInitializationHost
    {
        private uint randomState = seed;
        public object Identity { get; } = new();
        public readonly object CandidateOne = new(), CandidateTwo = new();
        public List<int> Events { get; } = [];
        public FixedVector3 PlayerPosition { get; set; }
        public int AttackClock { get; set; }
        public int Difficulty { get; set; }
        public int AnimationId { get; set; }
        public byte AnimationFinished { get; set; }
        public uint WaypointFlags { get; set; }
        public uint SpeechClock { get; set; }
        public SpeechRequest? CurrentSpeech { get; set; }
        public IReadOnlyList<ActorCollisionBody> SourceBaddyOrder { get; set; } = [];
        public bool NextWaypoint, SpeechAccepted, ShooterPresent, DieResult;
        public int Ground;
        private readonly ActorProcedureState shooter = new(new());
        public int Random(int range)
        {
            Events.AddRange([13, range]); randomState = unchecked(randomState * 1664525 + 1013904223);
            return range == 0 ? 0 : (int)(randomState % (uint)range);
        }
        public void DoPhysics(int mode) => Events.AddRange([1, mode]);
        public void BuildTransform() => Events.Add(2);
        public int GroundHeight(FixedVector3 position, int above, int below) { Events.AddRange([3, position.X, position.Y, position.Z, above, below]); return Ground; }
        public FixedVector3 WaypointPosition(int node) { Events.AddRange([19, node]); return new(409600, 0, 409600); }
        public bool GetNextWaypoint() { Events.Add(7); if (NextWaypoint) { state.CurrentWaypoint++; WaypointFlags = 0; } return NextWaypoint; }
        public void RunAnimation(ushort clip, int from = 0, int to = -1) { Events.AddRange([5, clip, from, to]); AnimationId = clip; AnimationFinished = 0; }
        public void CycleAnimation(ushort clip, sbyte direction = 1) { Events.AddRange([4, clip, direction]); AnimationId = clip; AnimationFinished = 0; }
        public int SetHeight(bool initial, int above, int below) { Events.AddRange([6, initial ? 1 : 0, above, below]); return 0; }
        public bool PlayPositionalSpeech(int bank, int slot, FixedVector3 position, int priority) { Events.AddRange([8, bank, slot, position.X, position.Y, position.Z, priority]); return SpeechAccepted; }
        public void StopSpeech() => Events.Add(10);
        public void SendSignal() => Events.Add(9);
        public void SendMessage(object target, int code)
        {
            Events.AddRange([12, ReferenceEquals(target, Identity) ? 0 : ReferenceEquals(target, CandidateOne) ? 1 : 2, code]);
            if (ReferenceEquals(target, Identity)) messages.Enqueue(new(code, new Handle(0)));
        }
        public ActorProcedureState? RecoverShooter(object? handle) { Events.AddRange([20, handle is Handle h ? h.Id : 0]); return ShooterPresent ? shooter : null; }
        public ActorBodyMotion? RecoverBody(ActorProcedureState value) => ReferenceEquals(value, shooter) && ShooterPresent ? shooter.Body : null;
        public void KillShadow() => Events.Add(11);
        public bool Die(int stage) { Events.AddRange([14, stage]); return DieResult; }
        public void ReportUnknownSubstate(HostageBehaviorState current, int substate) => Events.Add(21);
        public void AttachToSourceBaddyList() => Events.Add(15);
        public void EnableShadow() => Events.Add(16);
        public void ParseScript(ActorScriptCursor script) => Events.Add(17);
        public void InitializeModel(string model) => Events.AddRange([18, model == "hostage" ? 305 : 315]);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(HostageTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.hostage.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new HostageState((int)input[39]); var actor = new ActorProcedureState(new());
            var sourceSignals = new ActorScriptState { NodeId = unchecked((ushort)input[39]) };
            state.BindSourceSignals(sourceSignals);
            var procedures = new ActorProcedureList(); var messages = new HostageMessageQueue(); var speech = new ActorSpeechAttachment();
            var host = new Host(state, messages, (uint)input[38]);
            if (input[0] == 2)
            {
                var definition = new TriggerActorDefinition((ushort)input[39], 1, (short)input[1], [], [],
                    new((int)input[19], (int)input[20], (int)input[21]), new(123, -456, 789), 0, new byte[2]);
                HostageInitialization.Construct(state, actor, definition, host);
                state.SetHostageType((int)input[1], host.InitializeModel);
                Check(test.GetProperty("expected")[0], 0);
                assert(state.ModelName == (input[1] == 305 ? "hostage" : "hostagef") && state.SurfaceClearance == 100 && state.ShadowScale == 48 &&
                    state.GeneralParameter == 32 && state.PushValue == 64 && actor.Body.Angles == new ActorAngles(123, -456, 789),
                    "Hostage constructor preserves original placements, shadow/defaults, script-before-factory model selection and only305/315 assets.");
            }
            else
            {
                state.SetHostageType((int)input[1], _ => { });
                state.State = (HostageBehaviorState)input[2]; state.Substate = (int)input[3]; state.WaitUpdates = (int)input[4]; state.DeathUpdates = (int)input[5];
                state.RescueFlags = (uint)input[6]; actor.BehaviorFlags = (uint)input[7]; actor.CompletionFlags = (uint)input[8]; state.BodyFlags = (uint)input[9];
                state.ItemFlags = (ushort)input[10]; state.InputFlags = (ushort)input[11]; state.GroundReferenceY = (int)input[22]; state.GroundHeight = (int)input[23];
                state.DistanceSampleClock = (int)input[24]; state.CachedPlayerDistance = (int)input[25]; state.ShooterHandle = new Handle((int)input[43]);
                actor.Body.Position = Vector(19); host.PlayerPosition = Vector(16); host.Ground = (int)input[12]; host.AnimationId = (int)input[13];
                host.AnimationFinished = (byte)input[14]; host.Difficulty = (int)input[15]; host.AttackClock = (int)input[26]; host.SpeechClock = (uint)input[27];
                speech.Attach((uint)input[28], (int)input[29], (int)input[30]);
                host.CurrentSpeech = input[31] >= 0 && input[32] >= 0 ? new((int)input[31], (int)input[32], 0) : null;
                host.WaypointFlags = (uint)input[33]; host.NextWaypoint = input[34] != 0; host.SpeechAccepted = input[35] != 0; host.ShooterPresent = input[36] != 0; host.DieResult = input[44] != 0;
                host.SourceBaddyOrder = [new(host.Identity, state.Type, actor.Body.Position, 128, state.BodyFlags, state.Health),
                    new(host.CandidateOne, input[37] != 0 ? 304 : 312, actor.Body.Position + new FixedVector3(409600, 0, 0), 96, 0, 0),
                    new(host.CandidateTwo, input[37] != 0 ? 304 : 312, actor.Body.Position + new FixedVector3(204800, 0, 0), 96, 0, 0)];
                if (input[40] >= 0) messages.Enqueue(new((int)input[40], new Handle((int)input[43])) { Age = (int)input[41], Flags = (int)input[42] });
                var brain = new HostageBrain(state, actor, procedures, messages, speech, host, host.Random);
                int frame = 0;
                foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
                {
                    host.Events.Clear(); brain.Advance(2); Check(expected, frame++); host.AttackClock++; host.SpeechClock += 2;
                }
            }
            index++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            void Check(JsonElement expected, int frame)
            {
                long[] actual = [(int)state.State, state.Substate, state.WaitUpdates, state.DeathUpdates, state.RescueFlags, actor.BehaviorFlags, actor.CompletionFlags,
                    state.BodyFlags, state.ItemFlags, state.TintColor, state.Transparency, state.MinorRadius, state.CurrentWaypoint, state.GroundHeight, state.GroundReferenceY,
                    state.DistanceSampleClock, state.CachedPlayerDistance, state.ShooterHandle is Handle h ? h.Id : 0, procedures.Count, host.AnimationId, host.AnimationFinished,
                    speech.AttachedClock, speech.Bank, speech.Slot, actor.Body.Position.X, actor.Body.Position.Y, actor.Body.Position.Z, state.ShadowPosition.X, state.ShadowPosition.Y, state.ShadowPosition.Z];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(30).Select(value => value.GetInt64())) && host.Events.SequenceEqual(expected[30].EnumerateArray().Select(value => value.GetInt32())),
                    $"Hostage{index} frame{frame} preserves source AI stage order/state routing, seven leaf handlers, animation/height/signal/message effects, source motion procedure attachment, accepted XA ownership and exact death flash/fade.");
                var remaining = new List<long[]>();
                for (HostageMessage? message = messages.Head; message is not null; message = message.Next)
                    remaining.Add([message.Code, message.Age, message.Flags, message.SenderHandle is Handle sender ? sender.Id : 0]);
                assert(remaining.Count == expected[31].GetArrayLength() && remaining.SelectMany(values => values).SequenceEqual(expected[31].EnumerateArray().SelectMany(value => value.EnumerateArray().Select(field => field.GetInt64()))),
                    "Hostage messages preserve old-age postincrement, begging-age reset, disabled/death gating, deferred cleanup and newly inserted self-message retention.");
                assert(sourceSignals.PendingSignals == (ushort)input[11] && state.InputFlags == sourceSignals.PendingSignals,
                    "Original hostage AI keeps the signal ushort retained through each source frame; its waiting/rescue gate observes the same canonical actor field.");
            }
        }
        bool unsupported = false;
        try { new HostageState(0).SetHostageType(314, _ => { }); } catch (NotSupportedException) { unsupported = true; }
        assert(unsupported, "Genuine hostage family accepts only original305/315; Carnage314 is not invented as a hostage.");
        var retained = new ActorScriptState { NodeId = 354, PendingSignals = 0x8002 };
        var linkedState = new HostageState(354);
        linkedState.BindSourceSignals(retained);
        retained.PendingSignals |= 1;
        assert(linkedState.InputFlags == 0x8003, "A real source signal preserves other input bits while immediately reaching the hostage rescue field.");
        linkedState.InputFlags = 0x4001;
        assert(retained.PendingSignals == 0x4001, "Hostage source-field writes retain the same actor signal owner instead of creating a copied input bank.");
        bool movedOwner = false;
        try { linkedState.BindSourceSignals(new() { NodeId = 354 }); } catch (InvalidOperationException) { movedOwner = true; }
        assert(movedOwner && linkedState.InputFlags == 0x4001, "A captured hostage input field cannot be rebound to a replacement body's identity.");
    }
}
