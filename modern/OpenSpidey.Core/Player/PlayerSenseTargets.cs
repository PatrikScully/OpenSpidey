using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original global rebuild/refresh clocks, shared across the player's indicator list.</summary>
public sealed class PlayerSenseHistory
{
    public uint RebuildClock { get; set; }
    public uint RefreshClock { get; set; }
}
public sealed class PlayerSenseIndicator
{
    public FixedVector3 Direction { get; set; }
    public PlayerKickHandle Handle { get; set; }
    public int Age { get; set; }
}
public sealed class PlayerSenseState(PlayerCombatState combat, PlayerKillQuips quips, PlayerSenseHistory history)
{
    public PlayerCombatState Combat { get; } = combat ?? throw new ArgumentNullException(nameof(combat));
    public PlayerKillQuips Quips { get; } = quips ?? throw new ArgumentNullException(nameof(quips));
    public PlayerSenseHistory History { get; } = history ?? throw new ArgumentNullException(nameof(history));
    public uint MaximumTargetDistance { get; set; } // Source +0x8BC.
    public uint MinimumTargetDistance { get; set; } // Source +0x8C0.
    public int HasCombatTarget { get; set; } // Source +0xEC0.
    public PlayerSenseIndicator[] Indicators { get; } = Enumerable.Range(0, 6).Select(_ => new PlayerSenseIndicator()).ToArray();
}
public interface IPlayerSenseHost
{
    uint Clock { get; }
    FixedVector3 ReferencePositionWholeUnits { get; }
    FixedJointTransform ReferenceRotation { get; }
    IEnumerable<PlayerTargetCandidate> SourceBaddyOrder { get; }
    PlayerKickHandle MakeBaddyHandle(PlayerTargetCandidate target);
    PlayerTargetCandidate? RecoverBaddy(ref PlayerKickHandle handle);
}

/// <summary>Original source count bookkeeping and six offscreen threat slots; rendering is a separate source stage.</summary>
public static class PlayerSenseTargets
{
    public static void Rebuild(PlayerSenseState state, IPlayerSenseHost host)
    {
        if (Due(state.History.RebuildClock, host.Clock, 20))
        {
            state.History.RebuildClock = host.Clock;
            state.Combat.Field528 = 0;
            state.MaximumTargetDistance = 0;
            state.MinimumTargetDistance = uint.MaxValue;
            state.HasCombatTarget = 0;
            foreach (PlayerTargetCandidate target in host.SourceBaddyOrder)
            {
                if (target.Radius == 0 || (target.BodyFlags & 0x200) == 0) continue;
                if ((target.BehaviorFlags & 0x20) != 0)
                {
                    state.MaximumTargetDistance = Math.Max(state.MaximumTargetDistance, target.CachedPlayerDistance);
                    state.MinimumTargetDistance = Math.Min(state.MinimumTargetDistance, target.CachedPlayerDistance);
                    state.HasCombatTarget = 1;
                    state.Combat.Field528 = unchecked(state.Combat.Field528 + 1);
                }
                if (!EligibleIndicator(target)) continue;
                PlayerKickHandle handle = host.MakeBaddyHandle(target);
                // Original duplicate check compares only the handle validation word of occupied slots.
                if (state.Indicators.Any(indicator => indicator.Handle.Target is not null && indicator.Handle.Validation == handle.Validation)) continue;
                PlayerSenseIndicator? free = state.Indicators.FirstOrDefault(indicator => indicator.Handle.Target is null);
                // Capacity ends the entire source actor scan, including later count/min/max candidates.
                if (free is null) break;
                free.Age = 0;
                free.Handle = handle;
                free.Direction = FixedVectorMath.Normalize(PlayerTargetSelection.Rotate(Local(target.Target.Position, host.ReferencePositionWholeUnits), host.ReferenceRotation) with { Z = 0 });
            }
        }
        state.Quips.UpdateTargetCount(state.Combat.Field528);
    }

    public static void Refresh(PlayerSenseState state, IPlayerSenseHost host)
    {
        if (!Due(state.History.RefreshClock, host.Clock, 3)) return;
        state.History.RefreshClock = host.Clock;
        foreach (PlayerSenseIndicator indicator in state.Indicators)
        {
            if (indicator.Handle.Target is null) continue;
            PlayerKickHandle handle = indicator.Handle;
            PlayerTargetCandidate? target = host.RecoverBaddy(ref handle);
            indicator.Handle = handle;
            if (target is null) continue;
            if (target.Radius == 0 || !EligibleIndicator(target))
            {
                indicator.Handle = indicator.Handle with { Target = null };
                continue;
            }
            FixedVector3 local = PlayerTargetSelection.Rotate(Local(target.Target.Position, host.ReferencePositionWholeUnits), host.ReferenceRotation);
            // Both original PC stages flatten rotated depth before normalization.
            indicator.Direction = FixedVectorMath.Normalize(local with { Z = 0 });
        }
    }

    private static bool EligibleIndicator(PlayerTargetCandidate target) => (target.Target.ItemFlags & 0x8000) != 0 &&
        target.CanShowSenseIndicator != 0 && (target.BodyFlags & 0x40) == 0 && (target.BodyFlags & 0x10) != 0;
    private static bool Due(uint previous, uint clock, uint interval) => previous < unchecked(clock - interval) || previous > clock;
    private static FixedVector3 Local(FixedVector3 position, FixedVector3 reference) => new(
        unchecked((position.X >> 12) - reference.X), unchecked((position.Y >> 12) - reference.Y), unchecked((position.Z >> 12) - reference.Z));
}
