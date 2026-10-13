using OpenSpidey.Core.Actors;

namespace OpenSpidey.Core.Presentation;

/// <summary>Original CBackground body502 and source signed-short angular update.</summary>
public sealed class BackgroundState(uint checksum,short index)
{
    public uint Checksum {get;}=checksum;
    public short Index {get;}=index;
    public ushort ItemFlags {get;set;}
    public uint BodyFlags {get;set;}=0x14;
    public ActorAngles Angles {get;set;}
    public ActorAngles AngularVelocity {get;set;}
    // The actual PC background renderer checks signed high flag byte only (8000), not On/Off bit1.
    public bool Visible=>(ItemFlags&0x8000)==0;
    // CBody::EveryFrame runs before CBackground::AI and clears the constructor's initial-frame flag.
    public void PrepareSourceUpdate()=>BodyFlags&=~4u;
    // The original matching constructor does not copy its ang_vel pointer into mAngVel.
    public void Advance()=>Angles=new(unchecked((short)(Angles.X+AngularVelocity.X)),unchecked((short)(Angles.Y+AngularVelocity.Y)),unchecked((short)(Angles.Z+AngularVelocity.Z)));
}
