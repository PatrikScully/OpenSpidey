using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>The actual native lookaround angle owner; camera-mode/path transitions remain required separate source leaves.</summary>
public sealed class GodotPlayerLookaroundAim
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerWebAttack attack;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Lookaround aim requires the genuine shared melee player.");
    public PlayerLookaroundState State { get; }

    public GodotPlayerLookaroundAim(GodotPlayer player, GodotPlayerWebAttack attack)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.attack = attack ?? throw new ArgumentNullException(nameof(attack));
        State = new(player.Combat.State);
        attack.AimLookaroundTorso = AimWebShot;
    }

    /// <summary>Call immediately after the genuine ReadAnalogueInput bridge; modes must execute the actual source camera transitions.</summary>
    public PlayerLookaroundButtons UpdateInput(PlayerLookaroundInputFrame frame, IPlayerLookaroundInputHost cameraModes)
    {
        ArgumentNullException.ThrowIfNull(cameraModes);
        PlayerLookaroundButtons result = PlayerLookaroundInput.Step(State, attack.State, Melee.Kick, frame,
            new InputHost(this, cameraModes));
        Melee.Kick.SurfaceTransition = State.Active ? (byte)1 : (byte)0;
        return result;
    }

    /// <summary>SetupLookaroundCamera's scalar slice; its caller still owns camera matrices, path disposal, collision and target selection.</summary>
    public bool AdvanceAngleSlice(int elapsedTicks)
    {
        var neck = player.Model.GetSourceJoint(1);
        var head = player.Model.GetSourceJoint(3);
        State.NeckPitch = neck.RotationX; State.NeckYaw = neck.RotationY;
        State.HeadPitch = head.RotationX; State.HeadYaw = head.RotationY;
        bool active = PlayerLookaroundAngles.Advance(State, EffectiveHeading, player.Combat.State.Field8E9 != 0, elapsedTicks);
        player.Model.SetSourceJoint(1, neck with { RotationX = State.NeckPitch, RotationY = State.NeckYaw });
        player.Model.SetSourceJoint(3, head with { RotationX = State.HeadPitch, RotationY = State.HeadYaw });
        Melee.Kick.SurfaceTransition = State.Active ? (byte)1 : (byte)0;
        return active;
    }

    public void AimWebShot() => Melee.SetTargetTorsoAngle(
        PlayerLookaroundAngles.WebShotHeading(State, EffectiveHeading, player.Combat.State.Field8E9 != 0), false);

    private short EffectiveHeading => player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
    private sealed class InputHost(GodotPlayerLookaroundAim owner, IPlayerLookaroundInputHost cameraModes) : IPlayerLookaroundInputHost
    {
        public short EffectiveHeading => owner.EffectiveHeading;
        public void EnterLookaroundMode() => cameraModes.EnterLookaroundMode();
        public void ExitLookaroundMode() => cameraModes.ExitLookaroundMode();
        public void SetTargetTorsoAngle(short wanted, bool fast) => owner.Melee.SetTargetTorsoAngle(wanted, fast);
    }
}
