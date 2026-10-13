using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>The source handle's pointer and validation word have independent lifetimes.</summary>
public readonly record struct PlayerKickHandle(object? Target, uint Validation);
public sealed record PlayerKickObject(object Identity, ushort Type, FixedVector3 Position, int ManipulationFlags);
public readonly record struct PlayerKickTrace(object? Item, int Distance);
public readonly record struct PlayerKickSwitchSelection(PlayerKickHandle Handle, FixedVector3? AutoAimPosition);

/// <summary>CheckKick scratch fields, sharing the original input and combat state with the combo controller.</summary>
public sealed class PlayerKickState(PlayerMeleeState melee)
{
    public PlayerMeleeState Melee { get; } = melee ?? throw new ArgumentNullException(nameof(melee));
    public byte SurfaceTransition { get; set; } // Source +0x8EA.
    public PlayerKickObject? HeldObject { get; set; }
    public PlayerKickObject? TorsoTarget { get; set; } // Source +0xDCC.
    public PlayerKickHandle NearbyBaddy { get; set; } // Source +0xE5C.
    public PlayerKickHandle ManipulationTarget { get; set; } // Source +0xE4C.
    public PlayerKickHandle SwitchTarget { get; set; } // Source +0xE54.
    public int SwitchActionProgress { get => Melee.Combat.ActionProgress; set => Melee.Combat.ActionProgress = value; } // Source +0xE20.
}

public interface IPlayerKickHost
{
    uint Clock { get; }
    IReadOnlyList<PlayerKickObject> SourceBaddyOrder { get; }
    IReadOnlyList<PlayerKickObject> SourceEnvironmentalOrder { get; }
    PlayerKickHandle MakeHandle(PlayerKickObject target);
    PlayerKickTrace LineToItem(FixedVector3 start, FixedVector3 end, int mode);
    PlayerKickSwitchSelection SelectTargetSwitch(int maximumDistance, int minimumFacing, int weight, int facingWeight);
    /// <summary>Select source SFX entry and clear every nonterminating word's high sixteen bits.</summary>
    void SelectAndResetAnimationSounds(ushort animation);
    void RunAnimation(ushort animation, int firstFrame, int lastFrame);
    void SetTargetTorsoAngle(FixedVector3 position);
    /// <summary>Forward to PlayerMeleeController.Initiate, preserving its real target/pose/input work.</summary>
    void InitiateCombo(ushort move, int headStart);
}

/// <summary>CPlayer::CheckKick: source kick/punch entry and competing throw, pickup and switch actions.</summary>
public static class PlayerKickSelection
{
    public static bool Check(PlayerKickState state, IPlayerKickHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        PlayerMeleeInput input = melee.Input;
        if ((input.Triggered[18] == 0 && input.Triggered[19] == 0) || combat.IsCrawling || state.SurfaceTransition != 0)
            return false;
        bool punch = input.Triggered[19] != 0;
        input.Triggered[18] = input.Mirrors[18] = input.Triggered[19] = input.Mirrors[19] = 0;
        if (punch && state.HeldObject is not null) return false;
        if (state.HeldObject is { } held)
        {
            combat.ActionState = 0x200000;
            Play((ushort)((held.ManipulationFlags & 8) != 0 ? 201 : 195));
            if (state.TorsoTarget is { } target) host.SetTargetTorsoAngle(target.Position);
            return true;
        }

        bool foundBaddy = false;
        int bestDistance = 512;
        state.NearbyBaddy = state.NearbyBaddy with { Target = null };
        foreach (PlayerKickObject baddy in host.SourceBaddyOrder)
        {
            int distance = PlayerTargetSelection.Distance(combat.Position, baddy.Position);
            if (distance < bestDistance && baddy.Type != 305 && baddy.Type != 316)
            {
                foundBaddy = true;
                bestDistance = distance;
                state.NearbyBaddy = host.MakeHandle(baddy);
            }
        }
        if (!foundBaddy)
        {
            if (!punch)
            {
                bool alternateAnimation = false;
                state.ManipulationTarget = state.ManipulationTarget with { Target = null };
                foreach (PlayerKickObject item in host.SourceEnvironmentalOrder)
                {
                    if (item.Type != 401 || PlayerTargetSelection.Distance(combat.Position, item.Position) >= 768) continue;
                    int facing = unchecked(((combat.Position.X - item.Position.X) >> 12) * combat.ForwardDirection.X
                        + ((combat.Position.Y - item.Position.Y) >> 12) * combat.ForwardDirection.Y
                        + ((combat.Position.Z - item.Position.Z) >> 12) * combat.ForwardDirection.Z);
                    if (facing <= 0) continue;
                    PlayerKickTrace trace = host.LineToItem(combat.Position, item.Position, 1);
                    if (!ReferenceEquals(trace.Item, item.Identity) || trace.Distance > 256) continue;
                    state.ManipulationTarget = host.MakeHandle(item);
                    alternateAnimation = ((item.ManipulationFlags >> 3) & 1) != 0;
                    break;
                }
                if (state.ManipulationTarget.Target is not null)
                {
                    combat.ActionState = 0x100000;
                    Play((ushort)(alternateAnimation ? 196 : 190));
                    combat.HeadingTurnTicksRemaining = 0;
                    return true;
                }
            }
            PlayerKickSwitchSelection selection = host.SelectTargetSwitch(256, 2896, 4096, 4096);
            state.SwitchTarget = selection.Handle;
            // PC 0x4C0454 tests the returned auto-aim pointer, not the stored handle.
            if (selection.AutoAimPosition is not null)
            {
                Play(37);
                combat.ActionState = 0x80000000;
                state.SwitchActionProgress = 0;
                return true;
            }
        }
        combat.ActionState = 0x800;
        melee.AttackEntryClock = unchecked((int)host.Clock);
        combat.HeadingTurnTicksRemaining = 0;
        host.InitiateCombo((ushort)(punch ? 1 : 0), 0);
        return true;

        void Play(ushort animation)
        {
            host.SelectAndResetAnimationSounds(animation);
            host.RunAnimation(animation, 0, -1);
        }
    }

}
