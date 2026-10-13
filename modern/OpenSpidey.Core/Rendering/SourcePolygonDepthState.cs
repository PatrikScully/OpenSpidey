namespace OpenSpidey.Core.Rendering;

public readonly record struct SourceOrderTablePushback(short First, short Second, short Third);

/// <summary>Original signed OT globals, per-render scratch and PC face/batch depth-bias gates.</summary>
public sealed class SourcePolygonDepthState
{
    // PlayAway resets the first slot; PShell_Cleanup establishes the second before gameplay.
    public SourceOrderTablePushback Globals { get; private set; } = new(1, -60, 0);
    public SourceOrderTablePushback RenderScratch { get; private set; }
    public uint RenderVisits { get; private set; }
    public SourcePolygonDepthState(SourceOrderTablePushback? initial = null)
    {
        if (initial is { } values) Globals = values;
    }
    public void SetFirstPushback(short value) => Globals = Globals with { First = value };
    public void SetSecondPushback(short value) => Globals = Globals with { Second = value };
    public void BeginRender()
    {
        RenderScratch = Globals;
        RenderVisits = unchecked(RenderVisits + 1);
    }
    /// <summary>RenderSuperItem temporarily replaces slots0/1 from actual CSuper100/102, retaining the third scene slot.</summary>
    public SourceOrderTablePushback ItemScratch(bool superItem, short first, short second) =>
        superItem ? new(first, second, RenderScratch.Third) : RenderScratch;
    public SourceOrderTablePushback ItemScratch(bool superItem, SourceSuperItemDepth item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ItemScratch(superItem, item.First, item.Second);
    }
    /// <summary>Original477A20..477A67 stores this unsigned word only in low-graphics mode.</summary>
    public static ushort FaceBias(uint packedFlags, bool lowGraphics, SourceOrderTablePushback scratch) =>
        lowGraphics ? unchecked((ushort)((packedFlags & 0x6000) switch
        {
            0x2000 => scratch.First, 0x4000 => scratch.Second, 0x6000 => scratch.Third, _ => 0
        })) : (ushort)0;
    public static int BatchSortKey(int normalModelBias, int lowGraphicsModelBias, ushort faceBias, bool lowGraphics) =>
        unchecked((lowGraphics ? lowGraphicsModelBias : normalModelBias) + faceBias);
    /// <summary>PCGfx506980 applies the batch key only in normal graphics mode, so the authored OT face contribution is zero in either quality path.</summary>
    public static float RasterDepthBias(int batchSortKey, bool lowGraphics, float depthScale) =>
        batchSortKey != 0 && !lowGraphics ? (float)batchSortKey * depthScale : 0;
}
