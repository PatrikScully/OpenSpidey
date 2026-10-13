using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.PowerUps;

/// <summary>Actual CPowerUp's standalone CBody fields and pickup storage; unnamed values retain their original offsets.</summary>
public sealed class PowerUpState
{
    public ushort Type { get; set; }
    public ushort BodyFlags { get; set; } = 0x16;
    public ushort ItemFlags { get; set; }
    public uint Tint { get; set; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 WorkingPosition { get; set; } //110.
    public FixedVector3 Velocity { get; set; }
    public int AccelerationY { get; set; }
    public byte FrictionX { get; set; } = 1;
    public byte FrictionY { get; set; } = 1;
    public byte FrictionZ { get; set; } = 1;
    public short Yaw { get; set; }
    public short AngularVelocityY { get; set; }
    public short Health { get; set; }
    public ushort Radius { get; set; }
    public uint PlayerDistance { get; set; }
    public ActorFrameTiming Timing { get; } = new();
    public byte HasNode { get; set; }
    public ushort NodeId { get; set; }
    public ushort SignalNode { get; set; }
    public byte IsThreeDimensional { get; set; }
    public byte Dropping { get; set; }
    public byte Falling { get; set; } //103.
    public byte GroundCheckFinished { get; set; } //104.
    public byte GroundClearance { get; set; } //105.
    public int GroundHeight { get; set; } //10C.
    public FixedVector3 ShadowPosition { get; set; }
    public ushort ShadowScale { get; set; } = 32;
    public short ShadowDistance { get; set; }
    public short ShadowThreshold { get; set; } = 200;
    public ushort Field11E { get; set; }
    public ushort Field120 { get; set; }
    public short Field122 { get; set; }
    public byte FailedCollectionFlash { get; set; } //124.
    public int FadeCounter { get; set; } //128.
    public ushort Lifetime { get; set; }
    public uint LastSpeechClock { get; set; }
    public bool PickupBitPresent { get; set; }
    public bool GlowPresent { get; set; }
    public uint? ModelChecksum { get; set; }
}
