using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Choppers;

/// <summary>Recovered CChopperMissile (0x128) gameplay fields used by AI and CommonInitialisation.</summary>
public sealed class ChopperMissileState
{
    public short ActorType { get; set; } = ChopperState.MissileType;
    public ushort ItemFlags { get; set; }
    public ushort BodyFlags { get; set; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 Velocity { get; set; }
    public ActorAngles Angles { get; set; }
    public ActorAngles AngularVelocity { get; set; }
    public ActorAngles AngularAcceleration { get; set; }
    public ActorAngles AngularFriction { get; set; } = new(4, 4, 4);
    public int Phase { get; set; }
    public int WaypointNode { get; set; }
    public int FinalTargetNode { get; set; }
    public FixedVector3 TargetPosition { get; set; }
    public int Speed { get; set; }
    public int DelayTicks { get; set; }
    public uint SoundHandle { get; set; }
    public bool Alive { get; set; } = true;
}
