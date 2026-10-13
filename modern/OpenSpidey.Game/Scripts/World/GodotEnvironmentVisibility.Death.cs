namespace OpenSpidey.Game.World;

public sealed partial class GodotEnvironmentVisibility
{
    /// <summary>Source SendKill's exact item flag1 write can occur before chunk preparation.</summary>
    internal void SynchronizeSourceItemFlags(GodotEnvironmentVisibilityItem item)
    {
        RequireItem(item.Source, item.InstanceIndex);
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed) claimed.Synchronize();
        else SynchronizeNative(item, item.ItemFlags);
    }
}
