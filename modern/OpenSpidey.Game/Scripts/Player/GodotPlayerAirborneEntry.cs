using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Source jump/ground-loss guards over the actual impulse, contact grace, shared input and held401 actor.</summary>
public sealed class GodotPlayerAirborneEntry : IPlayerAirborneEntryHost, IPlayerAirborneEntryFrameHost
{
    private readonly GodotPlayer player;
    private readonly PlayerLookaroundState lookaround;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Airborne entry requires the original posed player and held-object owner.");
    public PlayerJumpImpulse Jump { get => player.Controller.SourceJumpImpulse; set => player.Controller.SetSwingJumpImpulse(value); }
    public int SpeedRamp { get => player.Controller.SpeedRamp; set => player.Controller.SetSourceSpeedRamp(value); }
    public byte GroundContactGrace { get => player.Controller.SourceGroundContactGrace; set => player.Controller.SourceGroundContactGrace = value; }
    public int? SupportVerticalVelocity => player.Controller.SupportBody?.Velocity.Y;
    public short InputHeading => player.Controller.MovementInput.Heading;

    public GodotPlayerAirborneEntry(GodotPlayer player, PlayerLookaroundState lookaround)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.lookaround = lookaround ?? throw new ArgumentNullException(nameof(lookaround));
        if (player.Melee is null) throw new InvalidOperationException("Airborne entry requires the same genuine melee/input services as the player.");
    }
    public bool CheckJump() => PlayerAirborneEntrySelection.CheckJump(Melee.State, lookaround, this);
    public bool CheckGroundGone() => PlayerAirborneEntrySelection.CheckGroundGone(Melee.State, this);
    public void PlaySingleAnimation(ushort animation, int firstFrame) => Melee.PlaySingleAnimation(animation, firstFrame, -1);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Melee.PlayPositionSound(sound, position, pitch);
    public void OrientToNormal(bool useTarget, FixedVector3 target) => Melee.OrientToNormal(useTarget, target);
    public void DropHeldObject(FixedVector3 velocity) =>
        (Melee.Interactions ?? throw new InvalidOperationException("Ground-loss drop requires the real manipulation/session interaction host.")).DropHeldObject(velocity);
}
