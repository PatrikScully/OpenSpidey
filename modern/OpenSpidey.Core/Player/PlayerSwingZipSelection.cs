using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerSwingZipSelectionHost : IPlayerStraightZipSelectionHost
{
    bool PracticeMode { get; }
    byte AirborneReleaseLatch { get; }
    int Random(int bound);
}

/// <summary>Actual CheckJumpingR2ZipWeb4C1460: swing/practice gate, random airborne reach and shared current-CWeb.</summary>
public static class PlayerSwingZipSelection
{
    public static bool Check(PlayerStraightZipState state, IPlayerSwingZipSelectionHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        if (combat.LookaroundActive || combat.HeldObjectPresent || host.SwingHeldLatch != 0 ||
            !PlayerSwingSelection.ButtonPermits(melee, host.PracticeMode, host.AirborneReleaseLatch)) return false;
        int reach = (combat.ActionState & 6) != 0 ? unchecked(256 - host.Random(512)) : 512;
        FixedVector3 end = combat.Position - combat.ForwardDirection * 2048 + combat.UpDirection * reach;
        PlayerStraightZipContact? traced = host.TraceWorld(combat.Position, end);
        if (traced is not { } contact || (contact.FaceFlags & 0x40000) != 0 && contact.Normal.Y >= -2600 ||
            !host.CheckZipAvailability(contact, 2048)) return false;
        state.TargetPosition = contact.Position;
        state.SurfaceNormal = contact.Normal;
        melee.WebAttackStarted = 0;
        state.Origin = combat.Position;
        combat.HeadingTurnTicksRemaining = 0;
        if ((combat.ActionState & 1) != 0) host.SelectAndRunAnimation(combat.IsCrawling ? (ushort)260 : (ushort)250, 0);
        else
        {
            WebStrandOwner strand = host.CreateCurrentStrand();
            state.Fire.CurrentStrand = strand;
            strand.HookIndex = 0;
            strand.FireVariant = state.Inventory.FireWebbing;
            melee.WebAttackMode = 8;
            state.Inventory.EmptyUseOverride = 1;
            try
            {
                host.FireWeb(new(false, 128, state.TargetPosition, true,
                    new(unchecked((short)contact.Normal.X), unchecked((short)contact.Normal.Y), unchecked((short)contact.Normal.Z))));
            }
            finally { state.Inventory.EmptyUseOverride = 0; }
            host.SelectAndRunAnimation(270, 13);
        }
        combat.ActionState = 0x40000;
        host.BeginZip(contact);
        return true;
    }
}
