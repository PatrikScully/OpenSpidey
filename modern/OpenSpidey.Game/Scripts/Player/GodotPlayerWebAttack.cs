using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Actual4000/8000 web branches using the same posed player, held strand, aim and sound fields.</summary>
public sealed class GodotPlayerWebAttack(GodotPlayer player, GodotPlayerFireWeb fireWeb,
    Func<byte, PlayerKickHandle> createDome) : IPlayerWebAttackHost, IPlayerWebInputHost
{
    public PlayerWebAttackState State { get; } = new();
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Web windup/firing requires the genuine shared melee player.");
    public uint Clock => fireWeb.Clock;
    public byte FireWebbing => fireWeb.FireWebbing;
    public bool AnimationFinished => fireWeb.AnimationFinished;
    public FixedVector3 ExplicitAimPosition => fireWeb.ExplicitAimPosition;
    public ActorAngles ExplicitAimNormal => fireWeb.ExplicitAimNormal;
    public ActorAngles TrajectoryAngles => fireWeb.TrajectoryAngles;
    public int MovementRamp => player.Controller.SpeedRamp;
    /// <summary>Bind to the source lookaround yaw owner before allowing field8EA aiming.</summary>
    public Action? AimLookaroundTorso { get; set; }
    public bool CheckWebShot() => PlayerWebInputSelection.Check(Melee.Kick, State, this);
    public void AimTorso(PlayerKickState kick)
    {
        if (kick.SurfaceTransition != 0)
            (AimLookaroundTorso ?? throw new InvalidOperationException("Lookaround web aim requires the actual source yaw and heading target owner."))();
        else if (kick.TorsoTarget is { } target) Melee.SetTargetTorsoAngle(target.Position);
        else Melee.LockTargetTorsoAngle();
    }
    public void UpdateWindup() => PlayerWebAttackBehavior.UpdateWindup(Melee.Kick, fireWeb.State, State, this);
    public void UpdateFiring() => PlayerWebAttackBehavior.UpdateFiring(Melee.State, fireWeb.State, State, this);
    public bool CheckJump() => Melee.CheckJump();
    public bool DecreaseWebbing(int amount) => fireWeb.DecreaseWebbing(amount);
    public WebStrandOwner CreateStrand() => new(fireWeb);
    public void DeleteStrand(WebStrandOwner strand) => strand.Dispose();
    public void SwitchStrandToBlob(WebStrandOwner strand) => strand.SwitchToBlob();
    public bool StrandTargetsCurrentAim(WebStrandOwner strand)
    {
        PlayerKickHandle handle = strand.TargetHandle;
        IWebStrandTarget? target = fireWeb.RecoverTarget(ref handle);
        return ReferenceEquals(target?.Identity, Melee.State.AimTarget?.Identity);
    }
    public int FireWeb(PlayerFireWebRequest request) => fireWeb.FireWeb(request);
    public void StopSound(int handle) => player.Combat.StopSound(handle);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Melee.PlayPositionSound(sound, position, pitch);
    public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Melee.PlaySingleAnimation(animation, firstFrame, lastFrame);
    public void RunAnimation(ushort animation, int firstFrame, int lastFrame) => Melee.RunAnimation(animation, firstFrame, lastFrame);
    public void SetAnimationFrame(int frame) => player.Model.SetSourceAnimationFrame(frame);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public PlayerKickHandle SelectZipDiveTarget() => Melee.StoreTargetHandle(Melee.SelectTargetBaddy(190, -4096, 4096, 0));
    public PlayerKickHandle CreateDome(byte fireVariant) => createDome(fireVariant);
}
