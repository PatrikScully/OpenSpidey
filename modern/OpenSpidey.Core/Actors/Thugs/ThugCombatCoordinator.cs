using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugCombatParticipant
{
    ThugState State { get; }
    FixedVector3 Position { get; }
    int DistanceToPlayer(int cacheTolerance);
    void DetermineFightState();
    void SetAnimationMode(int mode, bool playAnimation);
}

/// <summary>Shared primary attacker, ranged fight owner and eight surrounding attack slots.</summary>
public sealed class ThugCombatCoordinator
{
    public IThugCombatParticipant? PrimaryAttacker { get; set; }
    public IThugCombatParticipant? FightOwner { get; set; }
    public uint OccupiedSlots { get; set; }

    public void ClearAttackOwnership(IThugCombatParticipant participant)
    {
        ThugState state = participant.State;
        if (ReferenceEquals(PrimaryAttacker, participant)) PrimaryAttacker = null;
        else if ((state.AttackOwnershipFlags & 2) != 0) OccupiedSlots &= ~(uint)state.AttackSlotMask;
        state.AttackOwnershipFlags = 0;
        state.AttackSlotMask = 0;
    }

    public bool AssignAttackPosition(IThugCombatParticipant participant, FixedVector3 playerPosition, int playerUnavailableFlag)
    {
        ClearAttackOwnership(participant);
        if (playerUnavailableFlag != 0) return false;
        int distance = participant.DistanceToPlayer(2);
        if (distance < 819200)
        {
            IThugCombatParticipant? attacker = PrimaryAttacker;
            if (attacker is not null && NearPlayerHeight(attacker.State.GroundReferenceY))
            {
                if (distance < unchecked(attacker.DistanceToPlayer(2) - 150))
                {
                    if (attacker.State.State is 4 or 5) attacker.DetermineFightState();
                    // A transition may release or replace the primary actor before returning.
                    if (PrimaryAttacker is not null) ClearAttackOwnership(PrimaryAttacker);
                }
            }
            if (PrimaryAttacker is null && NearPlayerHeight(participant.State.GroundReferenceY))
            {
                PrimaryAttacker = participant;
                participant.State.AttackOwnershipFlags = 1;
                return true;
            }
        }

        ThugState state = participant.State;
        bool positiveX = unchecked(participant.Position.X - playerPosition.X) > 0;
        bool positiveZ = unchecked(participant.Position.Z - playerPosition.Z) > 0;
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
            if ((OccupiedSlots & mask) != 0) continue;
            state.AttackSlotCursor = (byte)index;
            state.AttackSlotMask = (byte)mask;
            OccupiedSlots |= mask;
            state.AttackOwnershipFlags = 2;
            participant.SetAnimationMode(2, false);
            return true;
        }
        state.AttackOwnershipFlags = 0;
        return false;

        bool NearPlayerHeight(int groundReference) =>
            ThugPerception.SourceAbs(unchecked(playerPosition.Y - groundReference - 0x4000)) < 409600;
    }

    public void Remove(IThugCombatParticipant participant)
    {
        ClearAttackOwnership(participant);
        if (ReferenceEquals(FightOwner, participant)) FightOwner = null;
    }
}
