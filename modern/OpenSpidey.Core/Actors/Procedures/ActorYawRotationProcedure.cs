namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Original CAIProc_RotY constructor and PC0x401110 Execute; stops source procedure traversal.</summary>
public sealed class ActorYawRotationProcedure : IActorProcedure
{
    private readonly ActorProcedureState actor;
    public int SourceType => 260;
    public int Flags { get; set; }
    public int RemainingFrames { get; private set; }
    public int YawPerInvocation { get; }
    public uint CompletionMask { get; private set; }
    public Action? ExecuteNext { get; set; }

    public ActorYawRotationProcedure(ActorProcedureState actor, int yawAmount, int frames, uint completionMask)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (frames <= 0) throw new ArgumentOutOfRangeException(nameof(frames), "Source RotY requires a positive frame divisor.");
        this.actor = actor;
        RemainingFrames = frames;
        YawPerInvocation = yawAmount / frames;
        CompletionMask = completionMask;
        actor.CompletionFlags &= ~completionMask;
    }

    public void Execute(int elapsedTicks)
    {
        // The binary neither calls Wait nor invokes mNext: even premarked procedures do this final rotation.
        actor.Body.Angles = actor.Body.Angles with { Y = unchecked((short)(actor.Body.Angles.Y + YawPerInvocation)) };
        actor.Body.MovementFlags |= YawPerInvocation < 0 ? 0x10000u : 0x20000u;
        RemainingFrames = unchecked(RemainingFrames - 1);
        if (RemainingFrames > 0) return;
        if (CompletionMask != 0)
        {
            actor.CompletionFlags |= CompletionMask;
            CompletionMask &= 0xF0F0;
        }
        Flags |= 1;
    }
}
