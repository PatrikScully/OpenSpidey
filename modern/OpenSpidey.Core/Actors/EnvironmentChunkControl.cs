using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

public interface IEnvironmentChunkItem
{
    ushort ItemFlags { get; set; }
    FixedVector3 Position { get; set; }
    ActorAngles Angles { get; set; }
}

public sealed class EnvironmentChunkEntry(IEnvironmentChunkItem item)
{
    public IEnvironmentChunkItem? Item { get; internal set; } = item;
    public FixedVector3 Velocity { get; internal set; }
    public ActorAngles AngularVelocity { get; internal set; }
    public uint Lifetime { get; internal set; }
}

/// <summary>Original CChunkControl427230/4272F0: per-AI movement, source RNG order, unsigned lifetime gates and signed-short angle addition.</summary>
public sealed class EnvironmentChunkControl(FixedVector3 position, ushort capacity) : IDisposable
{
    private readonly List<EnvironmentChunkEntry> entries = [];
    private bool disposed;
    public FixedVector3 Position { get; } = position;
    public ushort Capacity { get; } = capacity;
    public ushort BodyFlags { get; set; } = 0x16;
    public ActorFrameTiming Timing { get; } = new();
    public IReadOnlyList<EnvironmentChunkEntry> Entries => entries;
    public bool EntriesDisposed => disposed;

    public EnvironmentChunkEntry? AddChunk(IEnvironmentChunkItem item, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(item); ArgumentNullException.ThrowIfNull(random);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (entries.Count >= Capacity) return null;
        var entry = new EnvironmentChunkEntry(item); entries.Add(entry);
        int heading = random(4096) & 4095, speed = unchecked(random(32) + 32);
        var angle = GameAngle.Heading(heading);
        int vertical = unchecked((-48 - random(32)) << 12);
        entry.Velocity = new(unchecked(speed * angle.Sine), vertical, unchecked(speed * angle.Cosine));
        entry.AngularVelocity = new(unchecked((short)(random(512) - 256)), unchecked((short)(random(512) - 256)),
            unchecked((short)(random(512) - 256)));
        entry.Lifetime = unchecked((uint)(random(120) + 120));
        return entry;
    }

    public void Update(uint elapsedTicks)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bool allRetired = true;
        foreach (EnvironmentChunkEntry entry in entries)
        {
            if (entry.Item is not { } item) continue;
            if (entry.Lifetime <= elapsedTicks)
            {
                item.ItemFlags |= 0x21; entry.Item = null;
                continue;
            }
            allRetired = false;
            entry.Lifetime = unchecked(entry.Lifetime - elapsedTicks);
            // The PC adds both vectors. Recovered chunk.cpp incorrectly replaces the item's position and angles.
            item.Position += entry.Velocity;
            entry.Velocity = entry.Velocity with { Y = unchecked(entry.Velocity.Y + 0x8000) };
            item.Angles = new(unchecked((short)(item.Angles.X + entry.AngularVelocity.X)),
                unchecked((short)(item.Angles.Y + entry.AngularVelocity.Y)), unchecked((short)(item.Angles.Z + entry.AngularVelocity.Z)));
        }
        if (allRetired) BodyFlags |= 0x40;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; entries.Clear();
    }
}
