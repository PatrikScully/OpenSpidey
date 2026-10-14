using System.Runtime.CompilerServices;

namespace OpenSpidey.Game.Frontend;

/// <summary>The shell loops' Pause/PCSHELL_Relax boundary: one await per original loop iteration, resumed once per shell tick.</summary>
public sealed class FrontendFrames
{
    private Action? next;
    public bool Waiting => next is not null;
    public long Count { get; private set; }

    public Awaiter Next() => new(this);

    public void Resume()
    {
        Action? continuation = next ?? throw new InvalidOperationException("The shell is not waiting for a frame.");
        next = null;
        Count++;
        continuation();
    }

    public readonly struct Awaiter(FrontendFrames owner) : INotifyCompletion
    {
        public Awaiter GetAwaiter() => this;
        public bool IsCompleted => false;
        public void GetResult() { }
        public void OnCompleted(Action continuation)
        {
            if (owner.next is not null) throw new InvalidOperationException("Two shell loops awaited the same frame.");
            owner.next = continuation;
        }
    }
}
