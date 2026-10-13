using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerStraightZipContact(object Identity, FixedVector3 Position, FixedVector3 Normal, uint FaceFlags);
public sealed class PlayerStraightZipState(PlayerMeleeState melee, PlayerFireWebState fire, PlayerWebInventory inventory)
{
    public PlayerMeleeState Melee { get; } = melee;
    public PlayerFireWebState Fire { get; } = fire;
    public PlayerWebInventory Inventory { get; } = inventory;
    public FixedVector3 TargetPosition { get; set; }
    public FixedVector3 SurfaceNormal { get; set; }
    public FixedVector3 Origin { get; set; }
}

public interface IPlayerStraightZipSelectionHost
{
    byte SwingHeldLatch { get; }
    bool SwingPresent { get; }
    PlayerStraightZipContact? TraceWorld(FixedVector3 start, FixedVector3 end);
    bool CheckZipAvailability(PlayerStraightZipContact contact, int range);
    void ReleaseSwing(bool createSwingBack);
    void SetCameraFocusHook(int hook);
    WebStrandOwner CreateCurrentStrand();
    int FireWeb(PlayerFireWebRequest request);
    void SelectAndRunAnimation(ushort animation, int firstFrame);
    /// <summary>Connect the actual accepted source contact to the modern movement owner after the source action write.</summary>
    void BeginZip(PlayerStraightZipContact contact);
}

/// <summary>Actual CheckJumpingR1ZipWeb4C0EE0: held source6, real target gate and shared currentCWeb/empty-supply override.</summary>
public static class PlayerStraightZipSelection
{
    public static bool Check(PlayerStraightZipState state, IPlayerStraightZipSelectionHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        if (combat.LookaroundActive || combat.HeldObjectPresent || host.SwingHeldLatch != 0 || melee.Input.Pressed[6] == 0)
            return false;
        PlayerStraightZipContact? traced = host.TraceWorld(combat.Position, combat.Position + combat.UpDirection * 3072);
        if (traced is not { } contact || ((contact.FaceFlags & 0x40000) != 0 && contact.Normal.Y >= -2600) ||
            !host.CheckZipAvailability(contact, 3072)) return false;
        state.TargetPosition = contact.Position;
        state.SurfaceNormal = contact.Normal;
        melee.WebAttackStarted = 0;
        state.Origin = combat.Position;
        combat.HeadingTurnTicksRemaining = 0;
        if ((combat.ActionState & 1) != 0)
            host.SelectAndRunAnimation(combat.IsCrawling ? (ushort)260 : (ushort)250, 0);
        else
        {
            if (host.SwingPresent)
            {
                host.ReleaseSwing(false);
                combat.Field54C = 0;
                host.SetCameraFocusHook(-1);
            }
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
