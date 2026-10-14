namespace OpenSpidey.Assets.Models;

/// <summary>One typed dynamic slot generation; source geometry and palette are released by its owner's callback, without inventing an archive bank.</summary>
public sealed class PsxDynamicRegionLease : IDisposable
{
    private readonly PsxAssetRegions owner;
    private readonly Action releaseResources;
    private bool released;
    public byte Slot { get; }
    public string SourceName { get; }
    public bool IsActive => !released && owner.Contains(this);

    internal PsxDynamicRegionLease(PsxAssetRegions owner, byte slot, string sourceName, Action releaseResources)
    {
        this.owner = owner;
        Slot = slot;
        SourceName = sourceName;
        this.releaseResources = releaseResources;
    }

    public void Dispose()
    {
        if (released) return;
        released = true;
        owner.Release(this, releaseResources);
    }
}
