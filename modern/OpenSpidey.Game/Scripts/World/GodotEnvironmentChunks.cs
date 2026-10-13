using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Whole source Chunk_ChunkItemByChecksum using the actual loaded ENV generation, command-point producer and canonical control body owner.</summary>
public sealed class GodotEnvironmentChunks : IEnvironmentChunkHost, IActorEnvironmentChunkHost
{
    private readonly GodotActorRegistry registry;
    private readonly GodotEnvironmentVisibility visibility;
    private readonly Func<string, ReadOnlyMemory<byte>> readOriginal;
    private readonly Action<uint> triggerCommandPoint;
    private readonly Func<uint> sourceClock;
    private ImportedLevelSource? tableSource;
    private PsxChunkTable? table;
    public GodotEnvironmentChunks(GodotActorRegistry registry, GodotEnvironmentVisibility visibility,
        Func<string, ReadOnlyMemory<byte>> readOriginal, Action<uint> triggerCommandPoint, Func<uint> sourceClock)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
        this.readOriginal = readOriginal ?? throw new ArgumentNullException(nameof(readOriginal));
        this.triggerCommandPoint = triggerCommandPoint ?? throw new ArgumentNullException(nameof(triggerCommandPoint));
        this.sourceClock = sourceClock ?? throw new ArgumentNullException(nameof(sourceClock));
    }
    public EnvironmentChunkControl? ChunkByChecksum(uint checksum)
    {
        GodotEnvironmentVisibilityItem first = visibility.SourceItems.FirstOrDefault() ??
            throw new InvalidOperationException("Original ENV chunk destruction requires its current source list.");
        ImportedLevelSource source = first.Source;
        visibility.PrepareChunkSource(readOriginal(source.AssetPath).Span);
        return EnvironmentChunkDestruction.ChunkByChecksum(checksum, this);
    }
    public void ChunkEnvironment(uint checksum) => ChunkByChecksum(checksum);
    public void TriggerCommandPoint(uint checksum) => triggerCommandPoint(checksum);
    public IEnvironmentChunkSourceItem FindEnvironmentItem(uint checksum) => visibility.SourceItems.FirstOrDefault(item => item.ModelChecksum == checksum) ??
        throw new InvalidDataException($"Original ENV checksum{checksum:X8} is missing from its actual source list.");
    public PsxChunkTable RequireChunkTable(IEnvironmentChunkSourceItem item)
    {
        GodotEnvironmentVisibilityItem actual = Require(item);
        if (!ReferenceEquals(tableSource, actual.Source))
        { table = PsxChunkTable.Read(readOriginal(actual.Source.AssetPath).Span); tableSource = actual.Source; }
        return table ?? throw new InvalidDataException("Actual ENV source bank has no Chnk packet.");
    }
    public EnvironmentChunkControl CreateControl(FixedVector3 position, ushort capacity) =>
        GodotEnvironmentChunkControl.Create(registry, visibility, sourceClock, position, capacity).State;
    public void Synchronize(IEnvironmentChunkSourceItem item) => visibility.SynchronizeSourceItem(Require(item));
    public int Random(int range) => registry.RandomSource(range);
    private GodotEnvironmentVisibilityItem Require(IEnvironmentChunkSourceItem item)
    {
        if (item is not GodotEnvironmentVisibilityItem actual || !visibility.SourceItems.Contains(actual) || !actual.RegionUsable)
            throw new InvalidOperationException("Chunk item does not belong to this actual live ENV source generation.");
        return actual;
    }
}
