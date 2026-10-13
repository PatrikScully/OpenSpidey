using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Web-action scratch fields: source8E1/8E4,DD4 and374/AB8; input and player pointers remain shared.</summary>
public sealed class PlayerWebAttackState
{
    public byte ButtonLatched { get; set; }
    public byte RecoveryFlag { get; set; }
    public uint BlockedVariants { get; set; }
    public short WebTimer { get; set; }
    public int DomeCreationClock { get; set; }
    public PlayerKickHandle DomeHandle { get; set; }
}

public interface IPlayerWebAttackHost
{
    uint Clock { get; }
    byte FireWebbing { get; }
    bool AnimationFinished { get; }
    FixedVector3 ExplicitAimPosition { get; }
    ActorAngles ExplicitAimNormal { get; }
    ActorAngles TrajectoryAngles { get; }
    bool CheckJump();
    bool DecreaseWebbing(int amount);
    WebStrandOwner CreateStrand();
    void DeleteStrand(WebStrandOwner strand);
    void SwitchStrandToBlob(WebStrandOwner strand);
    bool StrandTargetsCurrentAim(WebStrandOwner strand);
    int FireWeb(PlayerFireWebRequest request);
    void StopSound(int handle);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void PlayAnimation(ushort animation, int firstFrame, int lastFrame);
    void RunAnimation(ushort animation, int firstFrame, int lastFrame);
    void SetAnimationFrame(int frame);
    void SwitchToStandMode();
    PlayerKickHandle SelectZipDiveTarget();
    PlayerKickHandle CreateDome(byte fireVariant);
}

/// <summary>Actual action4000 trap-web windup/held firing and8000 directional web release.</summary>
public static class PlayerWebAttackBehavior
{
    public static void UpdateWindup(PlayerKickState kick, PlayerFireWebState webs, PlayerWebAttackState state,
        IPlayerWebAttackHost host)
    {
        PlayerMeleeState melee = kick.Melee;
        PlayerCombatState combat = melee.Combat;
        ushort animation = combat.AnimationId;
        bool airborne = animation == 0x104;
        combat.FieldEA6 = 0;
        if (!airborne && animation != 0xFA)
        {
            if (animation is not (0xFB or 0x105) || host.CheckJump() || !host.AnimationFinished) return;
            state.RecoveryFlag = 0;
            host.SwitchToStandMode(); return;
        }

        if (combat.AnimationFrame < 5)
        {
            int leanX = melee.Input.Sideways, leanY = melee.Input.Forward;
            if (leanX > 0 && (state.BlockedVariants & 1) == 0)
            {
                melee.WebAttackMode = 2; melee.WebAttackSubstate = 0;
                combat.ActionState = 0x8000;
                host.PlayAnimation(airborne ? (ushort)0x106 : (ushort)0xFC, 0, -1);
                melee.WebFiringSide = leanY > 0 ? (byte)2 : leanY < 0 ? (byte)1 : (byte)0;
                melee.AnimationFlags &= 0xFE; return;
            }
            if (leanX < 0 && (state.BlockedVariants & 2) == 0)
            {
                melee.WebAttackMode = 4; melee.WebAttackSubstate = 0;
                combat.ActionState = 0x10000;
                host.PlayAnimation(airborne ? (ushort)0x109 : (ushort)0xFF, 0, -1);
                melee.AnimationFlags &= 0xFE; return;
            }
            if (!airborne)
            {
                bool plainWeb = false;
                if (leanY < 0 && (state.BlockedVariants & 8) == 0 &&
                    unchecked(host.Clock - combat.Field5B4) > 30 && !combat.IsCrawling)
                {
                    if (!host.DecreaseWebbing(0x400)) return;
                    host.PlayAnimation(0x11D, 0, -1); combat.ActionState = 0x800000; return;
                }
                if (leanY > 0 && (state.BlockedVariants & 4) == 0)
                {
                    if (combat.IsCrawling || !host.DecreaseWebbing(0xC00)) plainWeb = true;
                    else
                    {
                        host.PlayPositionSound(0x22, combat.Position, 0);
                        combat.ActionState = 0x20000000;
                        host.PlayAnimation(0x11B, 0, -1);
                        state.DomeCreationClock = unchecked((int)host.Clock);
                        state.DomeHandle = host.CreateDome(host.FireWebbing); return;
                    }
                }
                if (!plainWeb && kick.SurfaceTransition == 0 &&
                    (melee.Input.Pressed[18] != 0 || melee.Input.Pressed[19] != 0))
                {
                    melee.ZipDiveHandle = host.SelectZipDiveTarget();
                    melee.ZipDiveClock = unchecked((int)host.Clock);
                    combat.ActionState = 0x02000000; host.PlayAnimation(0x78, 0, -1); return;
                }
            }
        }

        if (host.AnimationFinished)
        {
            if (webs.CurrentStrand is { } old) { StopTrapSound(webs, host); ReleaseBlob(webs, old, host); }
            host.PlayAnimation(airborne ? (ushort)0x105 : (ushort)0xFB, 0, -1); return;
        }
        if (combat.AnimationFrame < 5) return;
        if (webs.CurrentStrand is null)
        {
            if (melee.WebAttackSubstate != 0) return;
            melee.WebAttackSubstate = 1;
            CreateStrand(webs, host, 0);
            int result = host.FireWeb(Request(melee, host, 0x100, explicitHit: true, explicitNormal: true));
            if ((result & 1) != 0) { DeleteCurrent(webs, host); StopTrapSound(webs, host); return; }
            if ((result & 2) != 0) return;
            StopTrapSound(webs, host);
            if (webs.CurrentStrand is { } current) ReleaseBlob(webs, current, host);
            return;
        }
        if (melee.Input.Pressed[17] == 0) return;
        if (!host.StrandTargetsCurrentAim(webs.CurrentStrand))
        { StopTrapSound(webs, host); ReleaseBlob(webs, webs.CurrentStrand, host); }
        if (webs.CurrentStrand is null) return;
        int heldResult = host.FireWeb(Request(melee, host, 0x80, explicitHit: false, explicitNormal: false));
        if ((heldResult & 1) != 0) { DeleteCurrent(webs, host); StopTrapSound(webs, host); }
        else if ((heldResult & 2) == 0)
        {
            StopTrapSound(webs, host);
            if (webs.CurrentStrand is { } current) ReleaseBlob(webs, current, host);
        }
        if (webs.CurrentStrand is null) return;
        combat.AnimationFrame = 5; host.SetAnimationFrame(5);
    }

    public static void UpdateFiring(PlayerMeleeState melee, PlayerFireWebState webs, PlayerWebAttackState state,
        IPlayerWebAttackHost host)
    {
        PlayerCombatState combat = melee.Combat;
        ushort animation = combat.AnimationId;
        combat.FieldEA6 = 0;
        if (animation == 0x94)
        {
            if (melee.WebAttackSubstate != 0)
            {
                if (combat.AnimationFrame >= 7) host.RunAnimation(0x9A, unchecked((short)combat.AnimationFrame), -1);
                return;
            }
            if (combat.AnimationFrame < 5 || webs.CurrentStrand is not null) return;
            melee.WebAttackSubstate = 1; state.WebTimer = 40;
            CreateStrand(webs, host, 0);
            int result = host.FireWeb(new(true, 600, default, false, host.TrajectoryAngles));
            if ((result & 1) != 0) { DeleteCurrent(webs, host); return; }
            if ((result & 8) == 0 && melee.AimTarget is not null)
            { combat.ActionState = 0x20000; combat.ActionProgress = 0; return; }
            if (webs.CurrentStrand is { } current) ReleaseBlob(webs, current, host);
            return;
        }
        if (animation is 0xFC or 0x106)
        {
            bool airborne = animation == 0x106;
            if (melee.WebAttackSubstate == 0 && combat.AnimationFrame >= (airborne ? 9 : 5) && webs.CurrentStrand is null)
            {
                state.WebTimer = airborne ? (short)64 : (short)40; melee.WebAttackSubstate = 1;
                CreateStrand(webs, host, 1);
                int result = host.FireWeb(Request(melee, host, 600, explicitHit: true, explicitNormal: true));
                if ((result & 1) != 0) DeleteCurrent(webs, host);
            }
            if (!host.AnimationFinished) return;
            if (melee.AimTarget?.Type == 407) melee.AimTarget = null;
            state.WebTimer = unchecked((short)(state.WebTimer - unchecked((short)combat.ElapsedTicks)));
            ushort lose = airborne ? (ushort)0x107 : (ushort)0xFD;
            if (webs.CurrentStrand is null) { host.PlayAnimation(lose, 0, -1); return; }
            if (melee.AimTarget is not null)
            {
                host.PlayAnimation(airborne ? (ushort)0x108 : (ushort)0xFE, 0, -1);
                combat.ActionState = 0x20000; combat.ActionProgress = 0; return;
            }
            ReleaseBlob(webs, webs.CurrentStrand, host); host.PlayAnimation(lose, 0, -1); return;
        }
        if (animation is not (0xFD or 0x107 or 0x9A) || host.CheckJump() || !host.AnimationFinished) return;
        host.SwitchToStandMode();
    }

    private static PlayerFireWebRequest Request(PlayerMeleeState melee, IPlayerWebAttackHost host, int cost,
        bool explicitHit, bool explicitNormal) => melee.WebAttackStarted == 0
        ? new(true, cost, default, false, host.TrajectoryAngles)
        : new(false, cost, host.ExplicitAimPosition, explicitHit, explicitNormal ? host.ExplicitAimNormal : host.TrajectoryAngles);
    private static void CreateStrand(PlayerFireWebState webs, IPlayerWebAttackHost host, ushort hook)
    {
        WebStrandOwner strand = host.CreateStrand(); webs.CurrentStrand = strand;
        strand.HookIndex = hook; strand.FireVariant = host.FireWebbing;
    }
    private static void DeleteCurrent(PlayerFireWebState webs, IPlayerWebAttackHost host)
    { if (webs.CurrentStrand is { } strand) host.DeleteStrand(strand); webs.CurrentStrand = null; }
    private static void ReleaseBlob(PlayerFireWebState webs, WebStrandOwner strand, IPlayerWebAttackHost host)
    { host.SwitchStrandToBlob(strand); webs.CurrentStrand = null; }
    private static void StopTrapSound(PlayerFireWebState webs, IPlayerWebAttackHost host)
    { if (webs.TrapSoundHandle != 0) { host.StopSound(webs.TrapSoundHandle); webs.TrapSoundHandle = 0; } }
}
