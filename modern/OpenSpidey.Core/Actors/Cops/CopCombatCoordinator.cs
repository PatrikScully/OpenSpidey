using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>gCopGlobal (0x682C3C) and gCopList (0x682C40). The slot mask 0x682C18 is the thugs' shared word.</summary>
public sealed class CopCombatCoordinator(ThugCombatCoordinator sharedSlots)
{
    public CopState? PrimaryAttacker { get; set; }
    public CopState? FightOwner { get; set; }
    public bool HasFightOwner => FightOwner is not null;
    public ThugCombatCoordinator SharedSlots { get; } = sharedSlots ?? throw new ArgumentNullException(nameof(sharedSlots));
    private readonly Dictionary<CopState, CopBrain> brains = new(ReferenceEqualityComparer.Instance);

    internal void Register(CopState state, CopBrain brain) => brains[state] = brain;

    private CopBrain BrainOf(CopState state) =>
        brains.TryGetValue(state, out CopBrain? brain) ? brain : throw new InvalidOperationException("Cop attacker has no AI.");

    /// <summary>CCop::ClearAttackFlags, inlined in FollowWaypoints, TakeHit and ~CCop.</summary>
    public void ClearAttackFlags(CopState state)
    {
        if (ReferenceEquals(PrimaryAttacker, state)) PrimaryAttacker = null;
        else if ((state.AttackOwnershipFlags & 2) != 0) SharedSlots.OccupiedSlots &= ~(uint)state.AttackSlotMask;
        state.AttackOwnershipFlags = 0;
        state.AttackSlotMask = 0;
    }

    /// <summary>CCop::SetAttackFlags (0x429090).</summary>
    public int SetAttackFlags(CopState state, FixedVector3 actorPosition, FixedVector3 playerPosition,
        Func<int, int> distanceToPlayer, Action<int, bool> setAnimMode)
    {
        ClearAttackFlags(state);
        int distance = distanceToPlayer(2);
        if (distance < 819200)
        {
            CopState? attacker = PrimaryAttacker;
            if (attacker is not null && NearPlayerHeight(attacker.GroundReferenceY))
            {
                // The primary's own cached DistanceToPlayer and DetermineFightState run here.
                if (distance < unchecked(BrainOf(attacker).DistanceToPlayer(2) - 150))
                {
                    if (attacker.State is 4 or 5) BrainOf(attacker).DetermineFightState();
                    CopState primary = PrimaryAttacker ?? throw new InvalidOperationException("Cop primary attacker vanished.");
                    PrimaryAttacker = null;
                    primary.AttackOwnershipFlags = 0;
                    primary.AttackSlotMask = 0;
                }
                attacker = PrimaryAttacker;
            }
            if (attacker is null && NearPlayerHeight(state.GroundReferenceY))
            {
                PrimaryAttacker = state;
                state.AttackOwnershipFlags = 1;
                return 1;
            }
        }

        bool positiveX = unchecked(actorPosition.X - playerPosition.X) > 0;
        bool positiveZ = unchecked(actorPosition.Z - playerPosition.Z) > 0;
        int direction = 1 << (state.AttackSlotCursor & 7);
        if (positiveX)
        {
            if (positiveZ)
            {
                if ((direction & 0x70) != 0) state.AttackSlotCursor = 7;
            }
            else if ((direction & 0xC1) != 0) state.AttackSlotCursor = 1;
        }
        else if (positiveZ)
        {
            if ((direction & 0x1C) != 0) state.AttackSlotCursor = 5;
        }
        else if ((direction & 7) != 0) state.AttackSlotCursor = 3;

        byte slot = state.AttackSlotCursor;
        for (int attempt = 0; attempt < 8; attempt++, slot = unchecked((byte)(slot + 1)))
        {
            int index = slot & 7;
            uint mask = 1u << index;
            if ((SharedSlots.OccupiedSlots & mask) != 0) continue;
            state.AttackSlotCursor = (byte)index;
            state.AttackSlotMask = (byte)mask;
            SharedSlots.OccupiedSlots |= mask;
            state.AttackOwnershipFlags = 2;
            setAnimMode(2, false);
            return 1;
        }
        state.AttackOwnershipFlags = 0;
        return 0;

        bool NearPlayerHeight(int groundReference) =>
            ThugPerception.SourceAbs(unchecked(playerPosition.Y - groundReference - 0x4000)) < 409600;
    }

    /// <summary>The ~CCop (0x428980) release of both globals.</summary>
    public void Release(CopState state)
    {
        if (ReferenceEquals(FightOwner, state)) FightOwner = null;
        ClearAttackFlags(state);
        brains.Remove(state);
    }
}
