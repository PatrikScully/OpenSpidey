using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>The original field_E20 snap-release latch, retained across action updates.</summary>
public sealed class PlayerWebTugState(PlayerCombatState combat)
{
    public int SnapReleased { get => combat.ActionProgress; set => combat.ActionProgress = value; }
}

public interface IPlayerWebTugHost
{
    bool AnimationFinished { get; }
    bool CheckJump();
    IReadOnlyList<FixedVector3>? CalculatePath(WebStrandOwner strand);
    void Snap(WebStrandOwner strand, FixedVector3 direction, IReadOnlyList<FixedVector3>? path);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void PlayAnimation(ushort animation, int firstFrame, int lastFrame);
    void SwitchToStandMode();
}

/// <summary>Original action 0x20000: directional release frames, shared web ownership and recovery clips.</summary>
public static class PlayerWebTugBehavior
{
    public static void Step(PlayerMeleeState melee, PlayerFireWebState webs, PlayerWebTugState state,
        IPlayerWebTugHost host)
    {
        PlayerCombatState combat = melee.Combat;
        combat.FieldEA6 = 0;
        if (state.SnapReleased != 0 && host.CheckJump()) return;

        ushort animation = combat.AnimationId;
        if (webs.CurrentStrand is { } strand)
        {
            FixedVector3 direction = default;
            IReadOnlyList<FixedVector3>? path = null;
            bool release = false;
            if (animation == 0x94 && combat.AnimationFrame >= 14)
            {
                path = host.CalculatePath(strand);
                direction = combat.ForwardDirection * 32 + combat.UpDirection * 16;
                release = true;
            }
            else if (animation is 0x102 or 0x10C && combat.AnimationFrame >= 9)
            {
                direction = melee.RightDirection * 24 + combat.UpDirection * 16 + combat.ForwardDirection * 24;
                release = true;
            }
            else if (animation is 0x101 or 0x10B && combat.AnimationFrame >= 9)
            {
                direction = melee.RightDirection * -24 + combat.UpDirection * 16 + combat.ForwardDirection * 24;
                release = true;
            }
            else if (animation is 0x103 or 0x10D && combat.AnimationFrame >= 9)
            {
                direction = combat.ForwardDirection * 32 + combat.UpDirection * 16;
                release = true;
            }
            if (release)
            {
                host.Snap(strand, direction, path);
                webs.CurrentStrand = null;
                state.SnapReleased = 1;
            }
        }
        if (!host.AnimationFinished) return;
        if (animation == 0x94) host.OrientToNormal(true, combat.ForwardDirection * -1);
        if (animation is 0xFE or 0x108)
        {
            ushort recovery = melee.WebFiringSide == 0 ? (ushort)0x103 :
                melee.WebFiringSide == 1 ? (ushort)0x102 : (ushort)0x101;
            if (animation == 0x108) recovery += 10;
            host.PlayAnimation(recovery, 0, -1);
            return;
        }
        host.SwitchToStandMode();
    }
}
