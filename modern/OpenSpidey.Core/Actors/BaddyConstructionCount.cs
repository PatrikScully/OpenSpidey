namespace OpenSpidey.Core.Actors;

/// <summary>Original signed NumBaddies field: each base constructor captures Count++, and its destructor decrements Count.</summary>
public sealed class BaddyConstructionCount(int initialCount = 0)
{
    public int Count { get; private set; } = initialCount;

    public BaddyConstructionLease BeginConstruction()
    {
        int ordinal = Count;
        Count = unchecked(Count + 1);
        return new(this, ordinal);
    }

    internal void Release() => Count = unchecked(Count - 1);
}

/// <summary>One actual base constructor/destructor pair, including constructor unwinding.</summary>
public sealed class BaddyConstructionLease : IDisposable
{
    private BaddyConstructionCount? owner;
    public int Ordinal { get; }
    public byte Phase => unchecked((byte)Ordinal);
    public bool Released => owner is null;

    internal BaddyConstructionLease(BaddyConstructionCount owner, int ordinal)
    {
        this.owner = owner;
        Ordinal = ordinal;
    }

    public void Dispose()
    {
        BaddyConstructionCount? current = owner;
        if (current is null) return;
        owner = null;
        current.Release();
    }
}
