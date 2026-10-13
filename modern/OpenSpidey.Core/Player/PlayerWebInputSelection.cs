using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerWebInputHost
{
    uint Clock { get; }
    byte FireWebbing { get; }
    int MovementRamp { get; }
    bool DecreaseWebbing(int amount);
    void PlayAnimation(ushort animation, int firstFrame, int lastFrame);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    PlayerKickHandle SelectZipDiveTarget();
    PlayerKickHandle CreateDome(byte fireVariant);
    void AimTorso(PlayerKickState kick);
}

/// <summary>Actual CPlayer::CheckWebShot edge latch, directional priority, supply gates and source action selection.</summary>
public static class PlayerWebInputSelection
{
    public static bool Check(PlayerKickState kick, PlayerWebAttackState state, IPlayerWebInputHost host)
    {
        PlayerMeleeState melee = kick.Melee;
        PlayerCombatState combat = melee.Combat;
        PlayerMeleeInput input = melee.Input;
        byte button = input.Pressed[17];
        byte wasDown = state.ButtonLatched;
        if ((wasDown != 0 && button == 0) || kick.HeldObject is not null)
        { state.ButtonLatched = 0; return false; }
        if (button != 0 && wasDown == 0)
        {
            state.ButtonLatched = 1;
            state.RecoveryFlag = (combat.ActionState & 1) == 0 && (input.Sideways != 0 || input.Forward != 0) && host.MovementRamp > 6
                ? (byte)1 : (byte)0;
        }
        if (state.ButtonLatched == 0) return false;
        state.BlockedVariants = 0;
        if (state.RecoveryFlag != 0)
            state.BlockedVariants = (input.Sideways > 0 ? 1u : 0) | (input.Sideways < 0 ? 2u : 0) |
                (input.Forward > 0 ? 4u : 0) | (input.Forward < 0 ? 8u : 0);

        if (input.Sideways > 0 && (state.BlockedVariants & 1) == 0)
        {
            melee.WebAttackMode = 2; melee.WebAttackSubstate = 0; combat.ActionState = 0x8000;
            host.PlayAnimation(combat.IsCrawling ? (ushort)0x106 : (ushort)0xFC, 0, -1);
            melee.WebFiringSide = input.Forward > 0 ? (byte)2 : input.Forward < 0 ? (byte)1 : (byte)0;
            Aim(kick, host); return true;
        }
        if (input.Sideways < 0 && (state.BlockedVariants & 2) == 0)
        {
            melee.WebAttackMode = 4; melee.WebAttackSubstate = 0; combat.ActionState = 0x10000;
            host.PlayAnimation(combat.IsCrawling ? (ushort)0x109 : (ushort)0xFF, 0, -1);
            Aim(kick, host); return true;
        }
        if (input.Forward < 0 && (state.BlockedVariants & 8) == 0 &&
            unchecked(host.Clock - combat.Field5B4) > 30 && !combat.IsCrawling)
        {
            if (!host.DecreaseWebbing(1024)) return false;
            host.PlayAnimation(0x11D, 0, -1); combat.ActionState = 0x800000; return true;
        }
        if (kick.SurfaceTransition == 0)
        {
            if (input.Forward > 0 && (state.BlockedVariants & 4) == 0)
            {
                if (combat.IsCrawling || !host.DecreaseWebbing(3072)) return false;
                combat.ActionState = 0x20000000;
                host.PlayAnimation(0x11B, 0, -1);
                state.DomeCreationClock = unchecked((int)host.Clock);
                host.PlayPositionSound(34, combat.Position, 0);
                state.DomeHandle = host.CreateDome(host.FireWebbing);
                return true;
            }
            if (!combat.IsCrawling && (input.Pressed[18] != 0 || input.Pressed[19] != 0))
            {
                melee.ZipDiveHandle = host.SelectZipDiveTarget(); melee.ZipDiveClock = unchecked((int)host.Clock);
                combat.ActionState = 0x02000000; host.PlayAnimation(0x78, 0, -1); return true;
            }
        }
        if (button == 0 || unchecked(host.Clock - combat.Field5B4) <= 30) return false;
        melee.WebAttackMode = 1; melee.WebAttackSubstate = 0; combat.ActionState = 0x4000;
        host.PlayAnimation(combat.IsCrawling ? (ushort)0x104 : (ushort)0xFA, 0, -1);
        Aim(kick, host); return true;
    }

    private static void Aim(PlayerKickState kick, IPlayerWebInputHost host)
    {
        kick.Melee.WebAttackStarted = kick.SurfaceTransition;
        host.AimTorso(kick);
        kick.Melee.AnimationFlags &= 0xFE;
    }
}
