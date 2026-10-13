using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerAirborneMovementConfiguration
{
    public required Func<int> Difficulty { get; init; }
    public required Func<int> LevelId { get; init; }
}

/// <summary>Whole source2/4 over actual player impulse, body collisions, shared hit/death services and proven web selectors.</summary>
public sealed class GodotPlayerAirborneMovement : IPlayerAirborneMovementFrameHost, IPlayerAirborneEntryFrameHost, IPlayerAirborneMovementHost,
    IPlayerAirborneLandingHost, IPlayerAirborneWallAttachmentHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerSwing swing;
    private readonly GodotPlayerAirborneAttackSelection attacks;
    private readonly GodotActorWorldQuery world;
    private readonly GodotPlayerAirborneMovementConfiguration configuration;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Airborne movement requires the original posed melee owner.");
    public PlayerAirborneMovementState State { get; }
    public GodotPlayerAirborneEntry Entry { get; }
    internal int JumpingUpdates { get; private set; }
    internal int FallingUpdates { get; private set; }
    public uint Clock => player.Combat.Timer;
    public int Difficulty => configuration.Difficulty();
    public int LevelId => configuration.LevelId();
    public bool AnimationFinished => player.Model.Playback.IsFinished;
    public bool UseLevelSoundAliases => player.UseLevelFootstepAliases;
    public bool VibrationEnabled => player.Combat.VibrationEnabled;

    public GodotPlayerAirborneMovement(GodotPlayer player, GodotPlayerSwing swing, GodotPlayerAirborneAttackSelection attacks,
        GodotActorWorldQuery world, GodotPlayerAirborneMovementConfiguration configuration)
    {
        this.player = player; this.swing = swing; this.attacks = attacks; this.world = world; this.configuration = configuration;
        ArgumentNullException.ThrowIfNull(configuration.Difficulty); ArgumentNullException.ThrowIfNull(configuration.LevelId);
        State = new(Melee.State);
        Entry = new(player, swing.State.Targets.Transition.Angles);
    }

    public void UpdateJumping()
    {
        JumpingUpdates++;
        State.Jump = player.Controller.SourceJumpImpulse;
        PlayerJumpImpulse previous = State.Jump;
        PlayerAirborneMovement.UpdateJumping(State, this);
        if (State.Jump != previous) player.Controller.SetSwingJumpImpulse(State.Jump);
    }
    public void UpdateFalling(int verticalVelocityBeforePhysics)
    {
        FallingUpdates++;
        PlayerAirborneMovement.UpdateFalling(State, verticalVelocityBeforePhysics, this);
    }
    public bool CheckLanded() => PlayerAirborneLanding.Check(State, this);
    public bool CheckJump() => Entry.CheckJump();
    public bool CheckGroundGone() => Entry.CheckGroundGone();
    public bool CheckStickToWall() => PlayerAirborneWallAttachment.Check(Melee.State,
        player.Controller.LastTerrainStep.Sweep.BodyHit, PlayerTerrainController.SurfaceClearance, this);
    public bool CheckStickToCeiling() => swing.CheckStickToCeiling();
    public bool CheckJumpingSmashKick() => attacks.CheckJumpingSmashKick();
    public bool CheckJumpingSwingWeb() => swing.CheckJumpingSwingWeb();
    public bool CheckJumpingR1ZipWeb() => attacks.CheckJumpingR1ZipWeb();
    public bool CheckJumpingR2ZipWeb() => swing.CheckJumpingR2ZipWeb();
    public bool ShouldPlayerDropFlail() => world.GetGroundHeight(Melee.State.Combat.Position, 0, 4096, out _) != -1;
    public void PlayAnimation(ushort animation) => Melee.PlaySingleAnimation(animation, 0, -1);
    public void SwitchToDeathMode() => PlayerDeathRules.Enter(Melee.State.Combat, player.Combat);
    public void SetLevelStatus(int status) => player.Combat.SetLevelStatus(status);
    public int Random(int bound) => swing.Random(bound);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Melee.PlayPositionSound(sound, position, pitch);
    public void HitPlayer(ActorHit hit) => player.Combat.Hit(hit);
    public void ActuatorOn(int controller, ushort time, int argument, int strength) => player.Combat.ActuatorOn(controller, time, argument, strength);
    public int GroundHeight(FixedVector3 position, int above, int below) => world.GetGroundHeight(position, above, below, out _);
    public void OrientToNormal(bool useTarget, FixedVector3 target) => Melee.OrientToNormal(useTarget, target);
    public void PlaySound(ushort sound, int volume, int pitch) => Melee.PlaySound(sound, volume, pitch);
}
