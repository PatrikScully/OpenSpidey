using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Real posed contacts, animated hooks, weak native handles and SFX for thug bullet impacts.</summary>
public sealed class GodotThugBulletHost(GodotThugResources resources, GodotThugImpactEffects effects,
    SoundChannelController sounds, Func<SoundListener> listener) : IThugBulletImpactHost
{
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset)
    {
        // Actual PC positional playSFX ignores the source pitch offset.
        sounds.PlayAt(sound, position, listener());
    }

    public bool CollideWithActor(object actor, FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook)
    {
        if (actor is GodotPlayer player && GodotObject.IsInstanceValid(player))
            return player.Combat.CollideWithPlayer(start, end, scale, out hook);
        if (actor is GodotHostageActor hostage && GodotObject.IsInstanceValid(hostage))
            return hostage.CollideWithPartBoxes(start, end, scale, out hook);
        if (actor is GodotThugActor thug && GodotObject.IsInstanceValid(thug))
        {
            IReadOnlyList<ActorPartBox> boxes = resources.CollisionBoxes(thug.State.ModelName!);
            var pose = new FixedJointTransform[boxes.Count];
            for (int part = 0; part < pose.Length; part++) pose[part] = thug.Model.GetSourceAnimationPartPose(part);
            var angles = thug.Actor.Body.Angles;
            FixedJointTransform rotation = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
            return ActorPartCollision.TryCollide(thug.Position, rotation, pose, boxes, 0, start, end, scale, out hook);
        }
        throw new NotSupportedException("This impact host requires a live native player, thug or hostage; other actor families need their posed-contact adapter.");
    }

    public object StoreImpactHandle(object actor)
    {
        if (actor is not GodotObject native || !GodotObject.IsInstanceValid(native))
            throw new ArgumentException("A source impact handle requires a live native actor.", nameof(actor));
        return new WeakReference<GodotObject>(native);
    }

    public object? RecoverImpactHandle(object handle) => handle is WeakReference<GodotObject> weak &&
        weak.TryGetTarget(out GodotObject? native) && GodotObject.IsInstanceValid(native) ? native : null;

    public FixedVector3 GetDynamicHookPosition(object actor, ActorHook hook)
    {
        if (actor is GodotThugActor thug) return thug.GetDynamicHookPosition(hook);
        if (actor is GodotHostageActor hostage) return hostage.GetDynamicHookPosition(hook);
        if (actor is GodotPlayer player)
        {
            var pose = new FixedJointTransform[18];
            for (int part = 0; part < pose.Length; part++) pose[part] = player.Model.GetSourcePartPose(part);
            return ActorHookPosition.Evaluate(player.Controller.Position, player.Controller.Position >> 12,
                player.Controller.Orientation.Basis, pose, hook);
        }
        throw new NotSupportedException("Impact hooks require an original native player, thug or hostage pose.");
    }

    public void EmitLaserPing(ThugLaserPingState ping) => effects.EmitLaserPing(ping);
    public void EmitAttachedPing(ThugAttachedPingState ping) => effects.EmitAttachedPing(ping);
}
