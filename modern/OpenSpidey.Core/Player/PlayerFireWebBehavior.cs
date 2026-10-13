using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerFireWebTarget : IWebStrandTarget
{
    FixedVector3 Position { get; }
}

public readonly record struct PlayerFireWebContact(FixedVector3 Position, ActorAngles Normal,
    uint FaceFlags, IPlayerFireWebTarget? SwitchTarget);

public readonly record struct PlayerImpactWebRequest(FixedVector3 Position, ActorAngles Direction,
    int Speed, int Damage, int Lifetime);

/// <summary>The two retained pointers and sound handle owned by CPlayer::FireWeb.</summary>
public sealed class PlayerFireWebState
{
    private readonly PlayerCombatState combat;
    private WebStrandOwner? currentStrand;
    public PlayerFireWebState(PlayerCombatState combat) => this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
    public WebStrandOwner? CurrentStrand
    {
        get => currentStrand;
        set { currentStrand = value; combat.WebPresent = value is not null; }
    }
    public FixedVector3? SwitchAimPosition { get; set; }
    public int TrapSoundHandle { get => combat.LoopingSoundHandle; set => combat.LoopingSoundHandle = value; }

    public void DetachStrand(WebStrandOwner strand)
    {
        if (ReferenceEquals(CurrentStrand, strand)) CurrentStrand = null;
    }
}

public interface IPlayerFireWebHost
{
    FixedVector3 Position { get; }
    FixedVector3 ForwardDirection { get; }
    FixedVector3 CameraPositionWholeUnits { get; }
    IPlayerFireWebTarget? AimTarget { get; }
    int WebShot { get; }
    ushort Animation { get; }
    byte FireWebbing { get; }
    bool VibrationEnabled { get; }
    int Random(int bound);
    bool DecreaseWebbing(int amount);
    FixedVector3 GetPlayerHookPosition(int hook);
    PlayerKickSwitchSelection SelectTargetSwitch(int maximumDistance, int minimumFacing, int weight, int facingWeight);
    PlayerFireWebContact? TraceEnvironment(FixedVector3 start, FixedVector3 end);
    bool SwitchEnabled(IPlayerFireWebTarget target);
    void FlickSwitch(IPlayerFireWebTarget target);
    bool TrapWeb(IPlayerFireWebTarget target);
    void AddTrapWebEffect(IPlayerFireWebTarget target, byte fireVariant);
    int TugWeb(IPlayerFireWebTarget target);
    void CreateImpactWeb(PlayerImpactWebRequest request);
    void CreateFireImpactWeb(FixedVector3 position, ActorAngles direction, int damage);
    int PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void PlaySound(ushort sound, int volume, int pitch);
    int ActuatorTime(int controller, int actuator);
    void ActivateActuator(int controller, int duration, int actuator, int strength);
}

/// <summary>Actual CPlayer::FireWeb: target resolution, strand/splat branches, cost and source virtual calls.</summary>
public static class PlayerFireWebBehavior
{
    public static int Fire(PlayerFireWebState state, PlayerFireWebRequest request, IPlayerFireWebHost host)
    {
        IPlayerFireWebTarget? held = host.AimTarget;
        IPlayerFireWebTarget? switchTarget = held?.Type == 407 ? held : null;
        FixedVector3 aim;
        bool hitSomething = request.HitSomething;
        ActorAngles normal = request.Normal;

        if (!request.UseHeldTarget) aim = request.Target;
        else if (held is not null)
        {
            hitSomething = false;
            aim = held.Position;
        }
        else
        {
            PlayerKickSwitchSelection selected = host.SelectTargetSwitch(3072, 2896, 4096, 4096);
            state.SwitchAimPosition = selected.AutoAimPosition;
            aim = selected.AutoAimPosition is { } point
                ? point + ((point - host.Position) >> 4)
                : host.GetPlayerHookPosition(2) - host.ForwardDirection * 2048;
            PlayerFireWebContact? contact = host.TraceEnvironment(host.Position, aim);
            hitSomething = contact is not null;
            if (contact is { } hit)
            {
                aim = hit.Position;
                normal = hit.Normal;
                if ((hit.FaceFlags & 0x02000000) != 0)
                    switchTarget = hit.SwitchTarget is { } target && host.SwitchEnabled(target) ? target : null;
            }
        }

        if (state.CurrentStrand is { } strand)
        {
            if (!host.DecreaseWebbing(request.Cost)) return 1;
            IPlayerFireWebTarget? attach = host.WebShot == 8 || host.AimTarget?.Type == 407 ? null : host.AimTarget;
            strand.Fire(host.GetPlayerHookPosition(strand.HookIndex), aim, attach, hitSomething,
                normal, host.CameraPositionWholeUnits, host.Random);

            if (host.VibrationEnabled)
            {
                if (host.WebShot == 1 && host.ActuatorTime(0, 1) <= 2) host.ActivateActuator(0, 4, 1, 120);
                else if (host.WebShot == 2 && host.ActuatorTime(0, 1) <= 2) host.ActivateActuator(0, 8, 1, 160);
            }
            if (switchTarget is not null)
            {
                host.FlickSwitch(switchTarget);
                host.PlayPositionSound(21, host.Position, 0);
                return 4;
            }

            IPlayerFireWebTarget? hitTarget = host.AimTarget;
            if (hitTarget is not null)
            {
                if (host.WebShot == 1)
                {
                    if (!host.TrapWeb(hitTarget)) return 0;
                    host.AddTrapWebEffect(hitTarget, host.FireWebbing);
                    if (state.TrapSoundHandle == 0)
                    {
                        host.PlayPositionSound(21, host.Position, 0);
                        state.TrapSoundHandle = host.PlayPositionSound(33, host.Position, 0);
                    }
                    return 2;
                }
                if (host.WebShot != 2)
                {
                    host.PlayPositionSound(21, host.Position, 0);
                    return 0;
                }
                if (host.TugWeb(hitTarget) == 1)
                {
                    hitTarget.BehaviorFlags |= 8;
                    host.PlayPositionSound(21, host.Position, 0);
                    return 2;
                }
            }
            else if (host.WebShot != 2)
            {
                host.PlayPositionSound(21, host.Position, 0);
                return 0;
            }
            strand.SwitchToBlob();
            state.CurrentStrand = null;
            host.PlaySound(22, 0x2000, 0);
            return 8;
        }

        bool shortShot = host.Animation == 139;
        if (!host.DecreaseWebbing(shortShot ? 300 : 900)) return 1;
        if (host.VibrationEnabled && host.ActuatorTime(0, 0) <= 2) host.ActivateActuator(0, 4, 0, 1);
        FixedVector3 firstHand = host.GetPlayerHookPosition(0);
        FixedVector3 muzzle = firstHand + ((host.GetPlayerHookPosition(1) - firstHand) >> 1);
        AimAngles aimAngles = FixedAim.Calculate(muzzle, aim);
        var direction = new ActorAngles(aimAngles.Pitch, aimAngles.Yaw, 0);
        if (host.FireWebbing == 0)
        {
            host.CreateImpactWeb(new(muzzle, direction, 32, 50, shortShot ? 30 : 120));
            host.PlayPositionSound(21, host.Position, 0);
        }
        else
        {
            host.CreateFireImpactWeb(muzzle, direction, 50);
            host.PlayPositionSound(0x80DC, host.Position, 0);
        }
        return 0;
    }
}
