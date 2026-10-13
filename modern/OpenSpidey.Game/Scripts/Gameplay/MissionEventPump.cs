using Godot;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Executes queued story events after actor and bit cleanup at the original two-tick cadence.</summary>
public partial class MissionEventPump : Node
{
    private int physicsTicks;
    public MissionRuntime Runtime { get; init; } = null!;
    public event Action<Exception>? Faulted;
    public event Action? SourceFrameCompleted;
    public override void _Ready() => ProcessPhysicsPriority = 120;
    public override void _PhysicsProcess(double delta)
    {
        if (++physicsTicks % 2 != 0) return;
        try
        {
            Runtime.ExecutePending();
            SourceFrameCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            SetPhysicsProcess(false);
            GD.PushError(exception.ToString());
            Faulted?.Invoke(exception);
        }
    }
}
