namespace OpenSpidey.Core.Actors;

/// <summary>One source world list; each owner supplies its real timing, lifecycle and AI update.</summary>
public sealed class SourceActorList
{
    private sealed class Entry(object identity, Action update)
    {
        public object Identity { get; } = identity;
        public Action Update { get; } = update;
        public Entry? Previous { get; set; }
        public Entry? Next { get; set; }
    }
    private readonly Dictionary<object, Entry> entries = new(ReferenceEqualityComparer.Instance);
    private Entry? head;
    public int Count => entries.Count;
    public object? Head => head?.Identity;
    public object? ReadNext(object identity) => entries.TryGetValue(identity, out Entry? current)
        ? current.Next?.Identity : throw new InvalidOperationException("The source traversal identity is no longer attached to this list.");
    public IReadOnlyList<object> SourceOrder
    {
        get
        {
            List<object> result = [];
            for (Entry? entry = head; entry is not null; entry = entry.Next) result.Add(entry.Identity);
            return result.AsReadOnly();
        }
    }

    public void Attach(object identity, Action update)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(update);
        if (entries.ContainsKey(identity)) throw new InvalidOperationException("A source actor is already attached.");
        Entry entry = new(identity, update) { Next = head };
        if (head is not null) head.Previous = entry;
        head = entry;
        entries.Add(identity, entry);
    }

    public bool Detach(object identity)
    {
        if (!entries.Remove(identity, out Entry? entry)) return false;
        if (entry.Previous is null) head = entry.Next;
        else entry.Previous.Next = entry.Next;
        if (entry.Next is not null) entry.Next.Previous = entry.Previous;
        return true;
    }

    public void Update()
    {
        Entry? current = head;
        while (current is not null)
        {
            // Ob_AI (460FC0) captures the next CBody before its lifecycle, timing and AI calls.
            Entry? next = current.Next;
            current.Update();
            current = next;
        }
    }

    public void Clear()
    {
        head = null;
        entries.Clear();
    }
}
