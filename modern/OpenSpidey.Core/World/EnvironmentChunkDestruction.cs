using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.World;

public interface IEnvironmentChunkFace
{
    uint Flags { get; set; }
    uint CollisionWord { get; set; }
    ushort RendererFlags { get; set; }
}

public interface IEnvironmentChunkModel
{
    ushort Flags { get; set; }
    uint RendererFlags { get; set; }
    IReadOnlyList<IEnvironmentChunkFace> Faces { get; }
}

public interface IEnvironmentChunkSourceItem : IEnvironmentChunkItem
{
    IEnvironmentChunkModel Model { get; }
}

public interface IEnvironmentChunkHost
{
    void TriggerCommandPoint(uint checksum);
    IEnvironmentChunkSourceItem FindEnvironmentItem(uint checksum);
    PsxChunkTable RequireChunkTable(IEnvironmentChunkSourceItem item);
    EnvironmentChunkControl CreateControl(FixedVector3 position, ushort capacity);
    void Synchronize(IEnvironmentChunkSourceItem item);
    int Random(int range);
}

/// <summary>Whole PC4273D0 order: queue the original checksum, hide its item, then reveal static/moving Chnk pieces with distinct source masks.</summary>
public static class EnvironmentChunkDestruction
{
    public static EnvironmentChunkControl? ChunkByChecksum(uint checksum, IEnvironmentChunkHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.TriggerCommandPoint(checksum);
        IEnvironmentChunkSourceItem item = host.FindEnvironmentItem(checksum);
        item.ItemFlags |= 0x21;
        host.Synchronize(item);
        PsxChunkRecord? record = host.RequireChunkTable(item).Find(checksum);
        if (record is null) return null; // The PC has already queued the point and hidden the item before rejecting a missing entry.
        EnvironmentChunkControl? control = record.MovingRuinChunks.Count == 0 ? null :
            host.CreateControl(item.Position, checked((ushort)record.MovingRuinChunks.Count));
        foreach (uint piece in record.StaticRuinModels)
        {
            IEnvironmentChunkSourceItem ruin = host.FindEnvironmentItem(piece);
            ruin.ItemFlags &= 0xEFDE;
            ruin.Model.Flags &= 0xFFCF;
            RevealFaces(ruin.Model, true);
            host.Synchronize(ruin);
        }
        foreach (uint piece in record.MovingRuinChunks)
        {
            IEnvironmentChunkSourceItem ruin = host.FindEnvironmentItem(piece);
            control!.AddChunk(ruin, host.Random);
            ruin.ItemFlags &= 0xEFFE;
            ruin.Model.Flags &= 0xFFDF;
            RevealFaces(ruin.Model, false);
            host.Synchronize(ruin);
        }
        return control;
    }

    private static void RevealFaces(IEnvironmentChunkModel model, bool stationary)
    {
        model.RendererFlags = (model.RendererFlags & ~0x100u) | 0x4000;
        foreach (IEnvironmentChunkFace face in model.Faces)
        {
            if ((face.RendererFlags & 0xC0) == 0) face.RendererFlags |= 0x80;
            face.Flags |= 0x80;
            if (stationary) face.CollisionWord &= ~0x10000u;
        }
    }
}
