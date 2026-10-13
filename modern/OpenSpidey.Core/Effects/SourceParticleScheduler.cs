namespace OpenSpidey.Core.Effects;

/// <summary>
/// Owns the source bit lists. The PC executable moves and deletes them in different orders.
/// Effect constructors insert at the head; destructors detach their owned companions.
/// </summary>
public sealed class SourceParticleScheduler
{
    // Bit_Move (411B30), rather than the reordered recovered C++ implementation.
    private static readonly SourceParticleList[] MoveOrder =
    [
        SourceParticleList.Pixel, SourceParticleList.TextBox, SourceParticleList.Flat,
        SourceParticleList.Glass, SourceParticleList.GenPoly, SourceParticleList.Chunk,
        SourceParticleList.Glow, SourceParticleList.NonRendered, SourceParticleList.Quad,
        SourceParticleList.Linked2EndedLeftover, SourceParticleList.PolyLine,
        SourceParticleList.GPolyLine, SourceParticleList.GLine, SourceParticleList.Special
    ];

    // Bit_RemoveDeadBits (408610), including its four calls to RemoveDeadBits (4087E0).
    private static readonly SourceParticleList[] CleanupOrder =
    [
        SourceParticleList.NonRendered, SourceParticleList.TextBox, SourceParticleList.Flat,
        SourceParticleList.Linked2EndedLeftover, SourceParticleList.Pixel,
        SourceParticleList.PolyLine, SourceParticleList.GPolyLine, SourceParticleList.Quad,
        SourceParticleList.GenPoly, SourceParticleList.Chunk, SourceParticleList.Glow,
        SourceParticleList.Glass, SourceParticleList.GLine, SourceParticleList.Special
    ];

    private sealed class Entry(SourceParticleList list, SourceParticleRegistration registration)
    {
        public SourceParticleList List { get; } = list;
        public SourceParticleRegistration Registration { get; } = registration;
        public Entry? Previous { get; set; }
        public Entry? Next { get; set; }
    }

    private readonly Entry?[] heads = new Entry?[14];
    private readonly Dictionary<object, Entry> entries = new(ReferenceEqualityComparer.Instance);

    public int Count => entries.Count;

    /// <summary>Releases registrations after the scene's native owners have destroyed their effects.</summary>
    public void Clear()
    {
        Array.Clear(heads);
        entries.Clear();
    }

    public IReadOnlyList<object> Identities(SourceParticleList list)
    {
        if (!Enum.IsDefined(list)) throw new ArgumentOutOfRangeException(nameof(list));
        List<object> result = [];
        for (Entry? current = heads[(int)list]; current is not null; current = current.Next)
            result.Add(current.Registration.Identity);
        return result;
    }

    public void Attach(SourceParticleList list, SourceParticleRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(registration.Identity);
        ArgumentNullException.ThrowIfNull(registration.Move);
        ArgumentNullException.ThrowIfNull(registration.IsAlive);
        ArgumentNullException.ThrowIfNull(registration.Destroy);
        if (!Enum.IsDefined(list)) throw new ArgumentOutOfRangeException(nameof(list));
        if (entries.ContainsKey(registration.Identity))
            throw new InvalidOperationException("A source bit is already attached.");

        Entry entry = new(list, registration) { Next = heads[(int)list] };
        if (entry.Next is not null) entry.Next.Previous = entry;
        heads[(int)list] = entry;
        entries.Add(registration.Identity, entry);
    }

    /// <summary>Unlinks a bit already destroyed by its owner, without invoking its destructor.</summary>
    public bool Detach(object identity)
    {
        if (!entries.Remove(identity, out Entry? entry)) return false;
        if (entry.Previous is null) heads[(int)entry.List] = entry.Next;
        else entry.Previous.Next = entry.Next;
        if (entry.Next is not null) entry.Next.Previous = entry.Previous;
        // CBit::DeleteFrom (408900) leaves the removed bit's own links intact.
        // The active Move traversal can still read its Next after the callback.
        return true;
    }

    public void Move(uint sourceClock)
    {
        foreach (SourceParticleList list in MoveOrder)
        {
            Entry? current = heads[(int)list];
            while (current is not null)
            {
                if (current.Registration.IsAlive()) current.Registration.Move(sourceClock);
                current = current.Next;
            }
        }
    }

    public void RemoveDead()
    {
        foreach (SourceParticleList list in CleanupOrder)
        {
            Entry? current = heads[(int)list];
            while (current is not null)
            {
                Entry? next = current.Next;
                // A destructor may already have deleted the captured companion. Managed ownership
                // replaces the original dangling pointer with an inert traversal entry.
                if (entries.TryGetValue(current.Registration.Identity, out Entry? attached) &&
                    ReferenceEquals(attached,current) && !current.Registration.IsAlive())
                {
                    current.Registration.Destroy();
                    Detach(current.Registration.Identity);
                }
                current = next;
            }
        }
    }

    /// <summary>Actual Bit_DeleteAll: capture next before destructors, preserve protected bits and require empty source heads.</summary>
    public void DeleteAll()
    {
        foreach (SourceParticleList list in CleanupOrder)
        {
            Entry? current = heads[(int)list];
            while (current is not null)
            {
                Entry? next = current.Next;
                if (entries.TryGetValue(current.Registration.Identity, out Entry? attached) &&
                    ReferenceEquals(attached, current) && current.Registration.IsProtected?.Invoke() != true)
                {
                    current.Registration.Destroy();
                    Detach(current.Registration.Identity);
                }
                current = next;
            }
        }
        if (entries.Count != 0)
            throw new InvalidOperationException($"Bit_DeleteAll retained {entries.Count} source bits: " +
                string.Join(", ", CleanupOrder.Where(list => heads[(int)list] is not null)));
    }
}
