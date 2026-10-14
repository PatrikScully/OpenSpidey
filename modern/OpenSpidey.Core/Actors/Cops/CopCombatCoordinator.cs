using OpenSpidey.Core.Actors.Thugs;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>gCopGlobal (0x682C3C) and gCopList (0x682C40). The slot mask 0x682C18 is the thugs' shared word.</summary>
public sealed class CopCombatCoordinator(ThugCombatCoordinator sharedSlots)
{
    public CopState? PrimaryAttacker { get; set; }
    public CopState? FightOwner { get; set; }
    public bool HasFightOwner => FightOwner is not null;
    public ThugCombatCoordinator SharedSlots { get; } = sharedSlots ?? throw new ArgumentNullException(nameof(sharedSlots));

    /// <summary>CCop::ClearAttackFlags, inlined in FollowWaypoints, TakeHit and ~CCop.</summary>
    public void ClearAttackFlags(CopState state)
    {
        if (ReferenceEquals(PrimaryAttacker, state)) PrimaryAttacker = null;
        else if ((state.AttackOwnershipFlags & 2) != 0) SharedSlots.OccupiedSlots &= ~(uint)state.AttackSlotMask;
        state.AttackOwnershipFlags = 0;
        state.AttackSlotMask = 0;
    }

    /// <summary>The ~CCop (0x428980) release of both globals.</summary>
    public void Release(CopState state)
    {
        if (ReferenceEquals(FightOwner, state)) FightOwner = null;
        ClearAttackFlags(state);
    }
}
