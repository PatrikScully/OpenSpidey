using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Combat;

public interface IEnvironmentCrate
{
    FixedVector3 Position { get; }
    uint ModelChecksum { get; }
}

public interface IEnvironmentCrateHost
{
    bool LowMemory { get; }
    bool Shatter(IEnvironmentCrate crate, bool checkSeen, bool markProcessed);
    void TriggerCommandPoint(uint checksum);
    int Random(int range);
    void CreateCrateExplosion(FixedVector3 position, bool small);
}

/// <summary>Actual PC Crate_Destroy ordering and recursion count. Its 16-bit neighbor-health shift disables propagation.</summary>
public sealed class EnvironmentCrateDestruction(int initialRecursion = 0)
{
    public int Recursion { get; private set; } = initialRecursion;

    public bool Destroy(IEnvironmentCrate crate, IEnvironmentCrateHost host)
    {
        ArgumentNullException.ThrowIfNull(crate);
        ArgumentNullException.ThrowIfNull(host);
        Recursion = unchecked(Recursion + 1);
        try
        {
            if (Recursion >= 10 || !host.Shatter(crate, true, true)) return false;
            host.TriggerCommandPoint(crate.ModelChecksum);
            if (!host.LowMemory)
                host.CreateCrateExplosion(crate.Position, host.Random(5) != 0);
            return true;
        }
        finally
        {
            // 430109 decrements after both successful and unsuccessful Shatter_Item calls.
            Recursion = unchecked(Recursion - 1);
        }
    }
}
