using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Combat;

public interface IProjectileBlastBody
{
    FixedVector3 Position { get; }
    IProjectileBlastBody? NextBody { get; }
    void Hit(ActorHit hit);
}

public interface IProjectileBlastEnvironment
{
    FixedVector3 Position { get; }
    IProjectileBlastEnvironment? NextEnvironment { get; }
    /// <summary>Source Exp_HitEnvItem(item,null,damage), including its own eligibility rules.</summary>
    void HitEnvironment(ushort damage);
}

/// <summary>Recovered CBullet object blast falloff and environmental item traversal.</summary>
public static class ProjectileBlastDamage
{
    public static void GiveScaledDamageToObjects(FixedVector3 position, ushort damage, int radius,
        int slideDistance, int slideFrames, int attackKind, IProjectileBlastBody? head)
    {
        if (radius == 0) throw new ArgumentOutOfRangeException(nameof(radius), "Source blast falloff divides by radius.");
        for (IProjectileBlastBody? body = head; body is not null; body = body.NextBody)
        {
            FixedVector3 difference = body.Position - position;
            int length = FixedAim.Distance(difference, default);
            if (length > radius) continue;
            // Deliberate crash fix outside the source-valid domain: original
            // v10 / vecLen divides by zero here. Deliver full direct damage,
            // clearing flag8 so a zero direction cannot start knockback physics.
            if (length == 0) { body.Hit(new(6, 0, attackKind, damage, default, 0, 1)); continue; }
            ushort hitDamage = unchecked((ushort)(damage - unchecked((damage >> 1) * length) / radius));
            short distance = unchecked((short)(slideDistance - unchecked(slideDistance * length) / radius));
            ushort frames = unchecked((ushort)(slideFrames - unchecked(slideFrames * length) / radius));
            if (frames == 0) frames = 1;
            // Flags14 does not read the unspecified source SHitInfo part byte.
            body.Hit(new(14, 0, attackKind, hitDamage, difference / length, distance, frames));
        }
    }

    public static void GiveScaledDamageToEnviro(FixedVector3 position, ushort damage, int radius,
        IProjectileBlastEnvironment? head)
    {
        for (IProjectileBlastEnvironment? item = head; item is not null; item = item.NextEnvironment)
        {
            FixedVector3 target = item.Position;
            int dx = Abs(unchecked(target.X - position.X));
            if (dx >= unchecked(radius << 12)) continue;
            int dz = Abs(unchecked(target.Z - position.Z));
            if (dz >= unchecked(radius << 12)) continue;
            if (ApproximateDistance(position, target) < radius) item.HitEnvironment(damage);
        }
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;

    private static int ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = Abs(unchecked(first.X - second.X));
        int middle = Abs(unchecked(first.Y - second.Y));
        int smallest = Abs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
    }
}
