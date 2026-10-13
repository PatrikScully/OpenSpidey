using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public enum MissionStartupPhase { NotStarted, Autoexec, Restart, Playing, CreatingInitialActors, SelectingRestart, Faulted }

/// <summary>Original autoexec barrier, initial actor census and restart command order.</summary>
public sealed class MissionStartupSequence : IDisposable
{
    private readonly TriggerFile triggers;
    private readonly MissionRuntime runtime;
    private readonly IMissionActorFactory actors;
    private readonly Func<ushort> selectRestart;
    private bool advancing, observing, disposed;
    public MissionStartupPhase Phase { get; private set; }
    public int InitialActorsCreated { get; private set; }
    public Exception? Failure { get; private set; }

    public MissionStartupSequence(TriggerFile triggers, MissionRuntime runtime,
        IMissionActorFactory actors, Func<ushort> selectRestart)
    {
        this.triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.selectRestart = selectRestart ?? throw new ArgumentNullException(nameof(selectRestart));
    }

    public void Begin() => Begin(alternativeAutoexec: false);
    public void Begin(bool alternativeAutoexec)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureHealthy();
        if (Phase != MissionStartupPhase.NotStarted) throw new InvalidOperationException("Mission startup has already begun.");
        Phase = MissionStartupPhase.Autoexec;
        runtime.Trace += ObserveMission;
        observing = true;
        try { runtime.QueueAutoexec(alternativeAutoexec); }
        catch (Exception error) { LatchFailure(error); throw; }
    }

    /// <summary>Run after the mission event phase; a blocked cinema retains the pending autoexec.</summary>
    public void Advance()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureHealthy();
        if (advancing)
        {
            var error = new InvalidOperationException("Mission startup cannot advance recursively.");
            LatchFailure(error);
            throw error;
        }
        if (Phase is MissionStartupPhase.NotStarted or MissionStartupPhase.Playing) return;
        if (runtime.PendingCount != 0 || runtime.IsBlocked) return;
        advancing = true;
        try
        {
            if (Phase == MissionStartupPhase.Autoexec)
            {
                Phase = MissionStartupPhase.CreatingInitialActors;
                foreach (TriggerNode node in triggers.Nodes)
                {
                    if (node.TypeCode != 1 || !TriggerActorDefinition.Read(node).InitializeOnLoad) continue;
                    actors.Spawn(node);
                    InitialActorsCreated++;
                    EnsureHealthy();
                }
                Phase = MissionStartupPhase.SelectingRestart;
                ushort restart = selectRestart();
                EnsureHealthy();
                if (restart >= triggers.Nodes.Count || triggers.Nodes[restart].TypeCode != (ushort)TriggerNodeKind.Restart)
                    throw new InvalidDataException("Mission startup has no authored restart node.");
                Phase = MissionStartupPhase.Restart;
                // Trig_ExecuteRestart drops constructor/selection pulses before running the authored restart.
                runtime.ClearPendingEvents();
                runtime.Queue(restart);
            }
            else if (Phase == MissionStartupPhase.Restart)
            {
                Phase = MissionStartupPhase.Playing;
                StopObserving();
            }
        }
        catch (Exception error) { LatchFailure(error); throw; }
        finally { advancing = false; }
    }

    private void ObserveMission(MissionTrace trace)
    {
        if (trace.Kind == MissionTraceKind.Faulted)
            LatchFailure(new InvalidOperationException($"Mission startup command execution failed at node{trace.NodeId}; reload the session."));
    }
    private void EnsureHealthy()
    {
        if (Phase == MissionStartupPhase.Faulted)
            throw new InvalidOperationException("Mission startup faulted; reload the session before continuing.", Failure);
    }
    private void LatchFailure(Exception error)
    {
        Failure ??= error;
        Phase = MissionStartupPhase.Faulted;
        StopObserving();
    }
    private void StopObserving()
    {
        if (!observing) return;
        runtime.Trace -= ObserveMission;
        observing = false;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StopObserving();
    }
}
