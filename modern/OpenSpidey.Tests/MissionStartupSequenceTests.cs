using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cinematics;
using OpenSpidey.Core.Missions;

/// <summary>Real mission interpreter, cinema command and checkpoint selection around the original startup phase order.</summary>
internal static class MissionStartupSequenceTests
{
    public static void Run(Action<bool, string> assert)
    {
        VerifyBlockingOrder(assert);
        VerifyAlternativeAutoexec(assert);
        VerifyActorFailure(assert);
        VerifyRestartFailures(assert);
        VerifyCommandFailure(assert, restart: false);
        VerifyCommandFailure(assert, restart: true);
        VerifyReentrancy(assert);
        VerifyMalformedActor(assert);
        VerifyEmptyStartupAndDisposal(assert);
        VerifyConstructorPulsesAreCleared(assert);
    }

    private static void VerifyConstructorPulsesAreCleared(Action<bool, string> assert)
    {
        using var scene = new Scene();
        scene.Factory.OnCreate = _ => scene.Runtime.Queue(0);
        scene.Startup.Begin(); scene.FinishAutoexec(); scene.Startup.Advance();
        assert(scene.Factory.Attempts.Count == 3 && scene.Selections == 1 &&
            scene.Startup.Phase == MissionStartupPhase.Restart && scene.Runtime.PendingCount == 1,
            "Trig_ExecuteRestart clears all constructor pulses after selection and leaves only the genuine authored restart list.");
        scene.Runtime.ExecutePending(); scene.Playback.Complete(); scene.Runtime.ExecutePending(); scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.Playing && scene.Playback.Started.SequenceEqual(new ushort[] { 11, 12 }) &&
            scene.Factory.Attempts.Count == 3 && scene.Selections == 1,
            "A constructor-queued autoexec cannot replay its cinema or actor census before the selected restart completes.");
    }

    private static void VerifyBlockingOrder(Action<bool, string> assert)
    {
        using var scene = new Scene();
        scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.NotStarted && scene.Runtime.PendingCount == 0 && scene.Events.Count == 0,
            "Polling before Begin has no source script, factory or checkpoint side effects.");
        scene.Startup.Begin();
        assert(scene.Startup.Phase == MissionStartupPhase.Autoexec && scene.Runtime.PendingCount == 2 && scene.Factory.Created.Count == 0,
            "Startup queues all normal autoexec nodes before initializing any actor, matching Trig_LoadTRG order.");
        scene.Runtime.ExecutePending();
        assert(scene.Events.SequenceEqual(new[] { "level:1", "movie:11" }) && scene.Runtime.IsBlocked &&
            scene.Startup.InitialActorsCreated == 0 && scene.Selections == 0,
            "The actual RunCinema handler holds the remainder of autoexec, initial actor census and saved restart selection.");
        for (int frame = 0; frame < 8; frame++) { scene.Runtime.ExecutePending(); scene.Startup.Advance(); }
        assert(scene.Playback.Started.Count == 1 && scene.Factory.Attempts.Count == 0 && scene.Selections == 0,
            "Repeated blocked frame polling never replays movie setup or creates source actors early.");
        scene.Playback.Complete(); scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.Autoexec && scene.Runtime.PendingCount == 2 && scene.Factory.Attempts.Count == 0,
            "Cinema completion alone cannot bypass the still-pending command continuation.");
        scene.Runtime.ExecutePending(1); scene.Startup.Advance();
        assert(scene.Events.SequenceEqual(new[] { "level:1", "movie:11", "level:2" }) && scene.Runtime.PendingCount == 1 && scene.Selections == 0,
            "The source event budget retains the second autoexec and prevents a premature initial actor census.");
        scene.Runtime.ExecutePending(); scene.Startup.Advance();
        assert(scene.Events.SequenceEqual(new[] { "level:1", "movie:11", "level:2", "level:3", "actor:1", "actor:5", "actor:7", "select:9" }),
            "Normal autoexec, marker1 actors in original node-index order and saved restart selection match trig.cpp/front.cpp ordering.");
        assert(scene.Factory.Created.Keys.SequenceEqual(new ushort[] { 1, 5, 7 }) && scene.Startup.InitialActorsCreated == 3 &&
            scene.Factory.Phases.All(phase => phase == MissionStartupPhase.CreatingInitialActors) &&
            scene.SelectionPhase == MissionStartupPhase.SelectingRestart && scene.Startup.Phase == MissionStartupPhase.Restart && scene.Runtime.PendingCount == 1,
            "Only kind1/marker1 records initialize; kind7 with marker1 and kind1 without marker1 remain pulse-only, and callbacks observe safe intermediate phases.");
        scene.Runtime.ExecutePending(); scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.Restart && scene.Events.TakeLast(2).SequenceEqual(new[] { "level:4", "movie:12" }) &&
            scene.Factory.Attempts.Count == 3 && scene.Selections == 1,
            "A restart cinema keeps gameplay closed without repeating constructors or saved checkpoint selection.");
        scene.Playback.Complete(); scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.Restart,
            "An unblocked restart still waits for its final genuine command to execute.");
        scene.Runtime.ExecutePending(); scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.Playing && scene.Level.GameLevel == 5 && scene.Startup.Failure is null,
            "Playing becomes available only after the restart command continuation drains successfully.");
        for (int frame = 0; frame < 8; frame++) scene.Startup.Advance();
        Throws<InvalidOperationException>(scene.Startup.Begin, assert, "Begin is once-only after successful startup.");
        assert(scene.Factory.Attempts.Count == 3 && scene.Selections == 1 && scene.Playback.Started.SequenceEqual(new ushort[] { 11, 12 }),
            "Completed startup cannot duplicate either cinema, any constructor or restart selection.");

        // A later mission error belongs to gameplay; the completed startup observer must already be detached.
        scene.Level.ThrowOnValue = 4;
        scene.Runtime.Queue(9);
        Throws<InvalidOperationException>(() => scene.Runtime.ExecutePending(), assert, "A later real mission command can still fail independently.");
        scene.Startup.Advance();
        assert(scene.Startup.Phase == MissionStartupPhase.Playing && scene.Startup.Failure is null,
            "Successful startup releases its runtime trace subscription and never changes phase because of a later gameplay failure.");
    }

    private static void VerifyAlternativeAutoexec(Action<bool, string> assert)
    {
        using var scene = new Scene();
        scene.Startup.Begin(alternativeAutoexec: true);
        scene.Runtime.ExecutePending(); scene.Startup.Advance();
        assert(scene.Events.Take(1).SequenceEqual(new[] { "level:9" }) &&
            scene.Events.Count(entry => entry.StartsWith("level:", StringComparison.Ordinal)) == 1 &&
            scene.Factory.Created.Keys.SequenceEqual(new ushort[] { 1, 5, 7 }),
            "Source alternative startup selects only the first kind15 node, skips all kind4 nodes and still performs the same initial actor census.");
        using var fallback = new Scene(alternativeNodes: false);
        fallback.Startup.Begin(alternativeAutoexec: true); fallback.FinishAutoexec(); fallback.Startup.Advance();
        assert(fallback.Events.Take(4).SequenceEqual(new[] { "level:1", "movie:11", "level:2", "level:3" }),
            "Source alternative mode falls back to every normal autoexec when no kind15 node exists.");
    }

    private static void VerifyActorFailure(Action<bool, string> assert)
    {
        using var scene = new Scene();
        var failure = new InvalidOperationException("Source factory5 failed after factory1 completed.");
        scene.Factory.FailureNode = 5; scene.Factory.Failure = failure;
        scene.Startup.Begin(); scene.FinishAutoexec();
        Throws<InvalidOperationException>(scene.Startup.Advance, assert, "A genuine factory exception stops the startup census.");
        assert(scene.Startup.Phase == MissionStartupPhase.Faulted && ReferenceEquals(scene.Startup.Failure, failure) &&
            scene.Startup.InitialActorsCreated == 1 && scene.Factory.Attempts.SequenceEqual(new ushort[] { 1, 5 }) &&
            scene.Factory.Created.Keys.SequenceEqual(new ushort[] { 1 }) && scene.Selections == 0 && scene.Runtime.PendingCount == 0,
            "Partial constructor failure retains the first completed side effect and its original exception, skips later actors and never queues a restart.");
        for (int frame = 0; frame < 3; frame++)
        {
            InvalidOperationException error = Throws<InvalidOperationException>(scene.Startup.Advance, assert, "Faulted startup cannot replay partially completed constructors.");
            assert(ReferenceEquals(error.InnerException, failure), "Repeated fault reports retain the first source factory exception.");
        }
        Throws<InvalidOperationException>(scene.Startup.Begin, assert, "A failed startup requires a fresh session rather than Begin again.");
        assert(scene.Factory.Attempts.Count == 2 && scene.Selections == 0, "Failure polling performs no new factory or restart side effects.");
    }

    private static void VerifyRestartFailures(Action<bool, string> assert)
    {
        foreach (ushort invalid in new ushort[] { 0, ushort.MaxValue })
        {
            using var scene = new Scene(); scene.RestartOverride = invalid;
            scene.Startup.Begin(); scene.FinishAutoexec();
            Throws<InvalidDataException>(scene.Startup.Advance, assert, "Startup rejects a missing or non-restart authored node.");
            assert(scene.Startup.Phase == MissionStartupPhase.Faulted && scene.Startup.InitialActorsCreated == 3 &&
                scene.Selections == 1 && scene.Runtime.PendingCount == 0,
                "Invalid restart selection latches failure after the original actor census and never queues another node's commands.");
            Throws<InvalidOperationException>(scene.Startup.Advance, assert, "Invalid restart selection cannot be retried implicitly.");
            assert(scene.Selections == 1 && scene.Factory.Attempts.Count == 3, "Failed selection is once-only and does not reconstruct actors.");
        }
        using var thrown = new Scene();
        var failure = new InvalidOperationException("Checkpoint selection failed."); thrown.SelectionFailure = failure;
        thrown.Startup.Begin(); thrown.FinishAutoexec();
        Throws<InvalidOperationException>(thrown.Startup.Advance, assert, "Startup latches a thrown checkpoint selection failure.");
        assert(ReferenceEquals(thrown.Startup.Failure, failure) && thrown.Startup.Phase == MissionStartupPhase.Faulted && thrown.Selections == 1,
            "The first checkpoint exception remains inspectable without rolling back or replaying successfully constructed source actors.");
    }

    private static void VerifyCommandFailure(Action<bool, string> assert, bool restart)
    {
        using var scene = new Scene();
        scene.Startup.Begin();
        scene.Level.ThrowOnValue = restart ? 5 : 3;
        if (restart) { scene.FinishAutoexec(); scene.Startup.Advance(); }
        Throws<InvalidOperationException>(() =>
        {
            while (true)
            {
                scene.Runtime.ExecutePending();
                if (!scene.Runtime.IsBlocked) break;
                scene.Playback.Complete();
            }
        }, assert, "A real mission command failure is reported by the interpreter.");
        assert(scene.Runtime.PendingCount == (restart ? 1 : 0) && scene.Startup.Phase == MissionStartupPhase.Faulted && scene.Startup.Failure is not null,
            "Startup observes the interpreter's terminal fault both with an empty queue and a retained blocked-program continuation.");
        int factories = scene.Factory.Attempts.Count, selections = scene.Selections, commands = scene.Events.Count;
        Throws<InvalidOperationException>(scene.Startup.Advance, assert, "An empty queue after command failure cannot unlock constructors or gameplay.");
        assert(scene.Factory.Attempts.Count == factories && scene.Selections == selections && scene.Events.Count == commands &&
            factories == (restart ? 3 : 0), "Autoexec and restart failures freeze the appropriate phase without replaying earlier side effects.");
    }

    private static void VerifyReentrancy(Action<bool, string> assert)
    {
        using var scene = new Scene();
        scene.Factory.OnCreate = _ => scene.Startup.Advance();
        scene.Startup.Begin(); scene.FinishAutoexec();
        Throws<InvalidOperationException>(scene.Startup.Advance, assert, "A constructor callback cannot recursively restart the actor census.");
        assert(scene.Startup.Phase == MissionStartupPhase.Faulted && scene.Factory.Attempts.SequenceEqual(new ushort[] { 1 }) && scene.Selections == 0,
            "Reentrant constructor callbacks fail before duplicate creation, later actors or restart selection.");
    }

    private static void VerifyMalformedActor(Action<bool, string> assert)
    {
        using var scene = new Scene(malformedActor: true);
        scene.Startup.Begin(); scene.FinishAutoexec();
        Throws<InvalidDataException>(scene.Startup.Advance, assert, "Malformed source actor metadata stops initial creation.");
        assert(scene.Startup.Phase == MissionStartupPhase.Faulted && scene.Startup.InitialActorsCreated == 1 && scene.Factory.Attempts.SequenceEqual(new ushort[] { 1 }),
            "Metadata failure cannot silently skip an initial object or replay the previously completed constructor.");
    }

    private static void VerifyEmptyStartupAndDisposal(Action<bool, string> assert)
    {
        using var empty = new Scene(autoexecNodes: false, initialActors: false);
        empty.Startup.Begin(); empty.Startup.Advance();
        assert(empty.Startup.Phase == MissionStartupPhase.Restart && empty.Startup.InitialActorsCreated == 0 && empty.Selections == 1,
            "A source file with no autoexec or initial actors proceeds directly to its authored restart without dummy commands.");
        empty.Runtime.ExecutePending(); empty.Playback.Complete(); empty.Runtime.ExecutePending(); empty.Startup.Advance();
        assert(empty.Startup.Phase == MissionStartupPhase.Playing, "An empty startup still respects the genuine restart barrier.");

        using var disposed = new Scene();
        disposed.Startup.Begin(); disposed.Runtime.ExecutePending(); disposed.Startup.Dispose(); disposed.Startup.Dispose();
        Throws<ObjectDisposedException>(disposed.Startup.Advance, assert, "Disposed startup cannot advance or create source actors.");
        Throws<ObjectDisposedException>(disposed.Startup.Begin, assert, "Disposed startup cannot begin again.");
        disposed.Playback.Complete(); disposed.Level.ThrowOnValue = 3;
        Throws<InvalidOperationException>(() => disposed.Runtime.ExecutePending(), assert, "Interpreter ownership remains separate after coordinator disposal.");
        assert(disposed.Startup.Phase == MissionStartupPhase.Autoexec && disposed.Startup.Failure is null && disposed.Factory.Attempts.Count == 0,
            "Idempotent disposal releases the trace observer, leaving no later callback or constructor side effect on the retired coordinator.");
    }

    private static T Throws<T>(Action action, Action<bool, string> assert, string evidence) where T : Exception
    {
        try { action(); }
        catch (T error) { assert(true, evidence); return error; }
        throw new InvalidDataException("Startup sequence proof: " + evidence);
    }

    private sealed class Scene : IDisposable
    {
        public List<string> Events { get; } = [];
        public Factory Factory { get; }
        public Playback Playback { get; }
        public Level Level { get; }
        public MissionRuntime Runtime { get; }
        public MissionStartupSequence Startup { get; }
        public int Selections { get; private set; }
        public MissionStartupPhase SelectionPhase { get; private set; }
        public ushort? RestartOverride { get; set; }
        public Exception? SelectionFailure { get; set; }

        public Scene(bool alternativeNodes = true, bool malformedActor = false, bool autoexecNodes = true, bool initialActors = true)
        {
            TriggerNode[] nodes =
            [
                Program(0, autoexecNodes ? (ushort)4 : (ushort)255, [147, 1, 190, 11, 147, 2, 65535]),
                Actor(1, 1, initialActors ? new byte[] { 1, 2 } : new byte[] { 2 }, 300),
                Program(2, alternativeNodes ? (ushort)15 : (ushort)255, [147, 9, 65535]),
                Actor(3, 1, [2, 4], 80),
                Actor(4, 7, [1, 2], 200),
                malformedActor ? new(5, 1, 40, new byte[] { 1, 0, 56, 1 }) : Actor(5, 1, initialActors ? new byte[] { 2, 1, 4 } : new byte[] { 2, 4 }, 40),
                Program(6, alternativeNodes ? (ushort)15 : (ushort)255, [147, 8, 65535]),
                Actor(7, 1, initialActors ? new byte[] { 1, 1, 2 } : new byte[] { 2 }, 160),
                Program(8, autoexecNodes ? (ushort)4 : (ushort)255, [147, 3, 65535]),
                Restart(9, [147, 4, 190, 12, 147, 5, 65535]),
                Program(10, 255, [65535])
            ];
            var file = new TriggerFile(nodes);
            Factory = new(Events); Playback = new(Events); Level = new(Events);
            Runtime = new(file, Factory, [new GameLevelCommandHandler(Level), new RunCinemaCommandHandler(Playback)]);
            var checkpoints = new MissionCheckpoints(file);
            Startup = new(file, Runtime, Factory, () =>
            {
                Selections++; SelectionPhase = Startup!.Phase;
                if (SelectionFailure is not null) throw SelectionFailure;
                ushort id = RestartOverride ?? checkpoints.Select("RE_RESTART01").Point.NodeId;
                Events.Add($"select:{id}"); return id;
            });
            Factory.CurrentPhase = () => Startup.Phase;
        }
        public void FinishAutoexec()
        {
            do { Runtime.ExecutePending(); if (Runtime.IsBlocked) Playback.Complete(); }
            while (Runtime.PendingCount != 0);
        }
        public void Dispose() => Startup.Dispose();
    }

    private sealed class Factory(List<string> events) : IMissionActorFactory
    {
        public List<ushort> Attempts { get; } = [];
        public Dictionary<ushort, TriggerActorDefinition> Created { get; } = [];
        public List<MissionStartupPhase> Phases { get; } = [];
        public Func<MissionStartupPhase> CurrentPhase { get; set; } = null!;
        public Action<TriggerNode>? OnCreate { get; set; }
        public ushort? FailureNode { get; set; }
        public Exception? Failure { get; set; }
        public void Spawn(TriggerNode node)
        {
            Attempts.Add(node.Id); Phases.Add(CurrentPhase());
            if (FailureNode == node.Id) throw Failure!;
            TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
            if (!definition.InitializeOnLoad || definition.ActorType != 312 || !Created.TryAdd(node.Id, definition))
                throw new InvalidDataException("Startup attempted a duplicate or noninitial source actor.");
            events.Add($"actor:{node.Id}"); OnCreate?.Invoke(node);
        }
    }
    private sealed class Playback(List<string> events) : ICinemaPlayback
    {
        public bool IsPlaying { get; private set; }
        public List<ushort> Started { get; } = [];
        public void Play(CinemaDefinition cinema)
        { if (IsPlaying) throw new InvalidOperationException("Cinema playback overlaps another source barrier."); IsPlaying = true; Started.Add(cinema.Id); events.Add($"movie:{cinema.Id}"); }
        public void Complete() => IsPlaying = false;
    }
    private sealed class Level(List<string> events) : IMissionGameLevelHost
    {
        private int level;
        public int? ThrowOnValue { get; set; }
        public int GameLevel
        {
            get => level;
            set { level = value; events.Add($"level:{value}"); if (ThrowOnValue == value) throw new InvalidOperationException("Original scalar host failed after its write."); }
        }
    }

    private static TriggerNode Program(ushort id, ushort kind, ushort[] words)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write(kind); foreach (ushort word in words) writer.Write(word);
        return new(id, kind, 0, output.ToArray());
    }
    private static TriggerNode Actor(ushort id, ushort kind, byte[] markers, int sourceOffset)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write(kind); writer.Write((short)312); writer.Write((ushort)0); writer.Write((ushort)0);
        writer.Write(markers); writer.Write((byte)255);
        while (((sourceOffset + output.Position) & 3) != 0) writer.Write((byte)0);
        writer.Write(100 + id); writer.Write(-200); writer.Write(300);
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)0);
        writer.Write((ushort)0x4100); writer.Write(ushort.MaxValue);
        return new(id, kind, sourceOffset, output.ToArray());
    }
    private static TriggerNode Restart(ushort id, ushort[] commands)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write((ushort)8); writer.Write((ushort)0);
        writer.Write(100); writer.Write(-200); writer.Write(300);
        writer.Write((short)0); writer.Write((short)1024); writer.Write((short)0);
        writer.Write(System.Text.Encoding.Latin1.GetBytes("Re_Restart01\0"));
        while ((output.Position & 1) != 0) writer.Write((byte)0);
        foreach (ushort word in commands) writer.Write(word);
        return new(id, 8, 0, output.ToArray());
    }
}
