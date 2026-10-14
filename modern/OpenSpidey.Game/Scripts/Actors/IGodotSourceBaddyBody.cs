using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>CBaddy body metadata and virtuals for families ported after the thug/hostage/Black Cat adapters.</summary>
public interface IGodotSourceBaddyBody
{
    ActorBodyMotion Body { get; }
    ImportedAnimatedModel Model { get; }
    ushort ItemFlags { get; set; }
    ushort MinorRadius { get; }
    /// <summary>CBody 0x46.</summary>
    ushort BodyFlags { get; }
    /// <summary>CBaddy 0x218.</summary>
    uint BehaviorFlags { get; }
    short Health { get; }
    void ReceiveMessage(int code, object? sender, FixedVector3 vector = default);
    bool Hit(ActorHit hit);
    /// <summary>Inherited CBody::Die: zombie flag 0x40 only; the caller kills the lifecycle.</summary>
    void MarkSourceBodyDead();
}
