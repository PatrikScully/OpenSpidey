namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Head-first source AI scheduling with category replacement and deferred retirement.</summary>
public sealed class ActorProcedureList
{
    private sealed class Entry(IActorProcedure procedure, Entry? next)
    {
        public IActorProcedure Procedure { get; } = procedure;
        public Entry? Next { get; set; } = next;
    }

    private Entry? head;
    private int elapsedTicks;
    public int Count { get; private set; }
    public IReadOnlyList<IActorProcedure> SourceOrder
    {
        get
        {
            List<IActorProcedure> result = [];
            for (Entry? entry = head; entry is not null; entry = entry.Next) result.Add(entry.Procedure);
            return result.AsReadOnly();
        }
    }

    public void Attach(IActorProcedure procedure)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        for (Entry? entry = head; entry is not null; entry = entry.Next)
            if (ReferenceEquals(entry.Procedure, procedure))
                throw new InvalidOperationException("An AI procedure is already attached to this actor.");
        if ((procedure.SourceType & 0x40000) == 0) Mark(category: procedure.SourceType & 0xFF00);
        var added = new Entry(procedure, head);
        procedure.ExecuteNext = () => added.Next?.Procedure.Execute(elapsedTicks);
        head = added;
        Count++;
    }

    public void AttachMove(ActorMoveToProcedure movement)
    {
        Attach(movement);
        if (movement.Facing is not null) Attach(movement.Facing);
    }

    public void Mark(bool ordinary = false, int category = 0, int stopCategory = 0)
    {
        for (Entry? entry = head; entry is not null; entry = entry.Next)
        {
            IActorProcedure procedure = entry.Procedure;
            int family = procedure.SourceType & 0xFF00;
            if ((ordinary && (procedure.SourceType & 0x20000) == 0) || (category != 0 && family == category))
                procedure.Flags |= 1;
            else if (stopCategory != 0 && family == stopCategory) procedure.Flags |= 4;
        }
    }

    public void Execute(int ticks)
    {
        elapsedTicks = ticks;
        head?.Procedure.Execute(ticks);
        CleanUp();
    }

    public void CleanUp(bool all = false)
    {
        Entry? previous = null;
        Entry? entry = head;
        while (entry is not null)
        {
            Entry? next = entry.Next;
            if (all || (entry.Procedure.Flags & 1) != 0)
            {
                if (previous is null) head = next;
                else previous.Next = next;
                entry.Procedure.ExecuteNext = null;
                Count--;
            }
            else previous = entry;
            entry = next;
        }
    }
}
