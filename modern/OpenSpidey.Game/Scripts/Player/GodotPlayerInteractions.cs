using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerInteractionConfiguration
{
    public required Action SwitchToStandMode { get; init; }
    public required Action CheckJump { get; init; }
    public required Action<int> PutCameraBehind { get; init; }
}

/// <summary>Player interaction actions against actual session-owned manipulation bodies and weak control handles.</summary>
public sealed class GodotPlayerInteractions : IPlayerInteractionHost
{
    private readonly GodotPlayerMelee melee;
    private readonly GodotPlayerMeleeSession session;
    private readonly GodotPlayerMeleeDirectory directory;
    private readonly GodotPlayerInteractionConfiguration configuration;
    public GodotPlayerInteractions(GodotPlayerMelee melee, GodotPlayerMeleeSession session, GodotPlayerMeleeDirectory directory,
        GodotPlayerInteractionConfiguration configuration)
    {
        this.melee = melee ?? throw new ArgumentNullException(nameof(melee)); this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.directory = directory ?? throw new ArgumentNullException(nameof(directory)); this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ArgumentNullException.ThrowIfNull(configuration.SwitchToStandMode); ArgumentNullException.ThrowIfNull(configuration.CheckJump);
        ArgumentNullException.ThrowIfNull(configuration.PutCameraBehind);
    }
    public bool Update(ushort completedAnimation) => PlayerInteractionActions.Update(melee.Kick, completedAnimation, this);
    /// <summary>After genuine hook1/0 pose evaluation and any earlier swing/web attachment adjustment.</summary>
    public void UpdateHeldPose(FixedVector3 leftHand, FixedVector3 rightHand) => PlayerInteractionActions.UpdateHeldPose(melee.Kick, leftHand, rightHand, this);
    public PlayerKickObject? RecoverManipulation(ref PlayerKickHandle handle)
    {
        GodotScriptActor? actor = session.RecoverEnvironmentalHandle(ref handle);
        if (actor is null) return null;
        return RequireObject(actor).KickObject;
    }
    public object? RecoverSwitch(ref PlayerKickHandle handle)
    {
        GodotScriptActor? actor = directory.Recover(ref handle);
        if (actor is null) return null;
        if (actor is not GodotSwitchActor) throw new InvalidDataException("Player switch handle resolved to a different source actor type.");
        return actor;
    }
    public void Hold(PlayerKickObject item) => session.Hold(RequireObject(item.Identity));
    public void Pickup(object identity) => RequireObject(identity).Pickup();
    public void ThrowVelocity(object identity, FixedVector3 velocity) => RequireObject(identity).ThrowVelocity(velocity);
    public void ThrowAt(object identity, FixedVector3 target, int count) => RequireObject(identity).ThrowAt(target, count);
    public void Release(object identity) => session.Release(RequireObject(identity));
    /// <summary>CheckGroundGone's genuine CManipOb.Drop, followed by clearing the one shared held pointer.</summary>
    public void DropHeldObject(FixedVector3 velocity)
    {
        PlayerKickObject held = melee.Kick.HeldObject ?? throw new InvalidOperationException("The source ground-loss drop requires an actual held object.");
        GodotManipulationActor actor = RequireObject(held.Identity);
        actor.Drop(velocity);
        session.Release(actor);
        melee.Kick.HeldObject = null;
    }
    public void FlickSwitch(object identity)
    {
        if (identity is not GodotSwitchActor actor || !GodotObject.IsInstanceValid(actor)) throw new InvalidDataException("Player interaction requires a live original switch actor.");
        actor.Flick();
    }
    public void SwitchToStandMode() => configuration.SwitchToStandMode();
    public void CheckJump() => configuration.CheckJump();
    public void PutCameraBehind(int mode) => configuration.PutCameraBehind(mode);
    public void PlaySound(ushort sound, int volume, int pitch) => melee.PlaySound(sound, volume, pitch);
    public void PlayPositionalSound(ushort sound, FixedVector3 position, int pitch) => melee.PlayPositionSound(sound, position, pitch);
    public PlayerHeldPose ReadHeldPose(object identity)
    {
        GodotManipulationActor actor = RequireObject(identity);
        return new(actor.State.Parameter108, actor.Body.Angles.Y);
    }
    public void SetHeldPose(object identity, FixedVector3 position, short yaw)
    {
        GodotManipulationActor actor = RequireObject(identity);
        actor.SetHeldTransform(position, actor.Body.Angles with { Y = yaw });
    }
    private static GodotManipulationActor RequireObject(object identity) => identity is GodotManipulationActor actor && GodotObject.IsInstanceValid(actor)
        ? actor : throw new InvalidDataException("Player held object requires a genuine live type401 manipulation actor.");
}
