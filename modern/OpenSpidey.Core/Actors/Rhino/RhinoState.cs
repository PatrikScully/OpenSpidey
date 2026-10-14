using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>Source CRhino (type 307, module "rhino") constructor fields at 0x47DE40.</summary>
public sealed class RhinoState(int node, int attackRelatedSnapshot, int sourceConstructionOrdinal)
{
    public const short Type = 307;
    public const string ModuleName = "rhino";
    public const string ModelName = "rhino";
    public const uint LightAddress = 0x552080;
    public const uint StrangeInitAddress = 0x5520B8;
    public const uint StateFlagsAddress = 0x5521D0;
    public const uint DataAddress = 0x552208;
    public const int DataCount = 0x17;
    public const int DazedStarCount = 5;
    public const uint SetupWord0 = 0x201;
    public const uint SetupWord1 = 0;

    public int SourceNode { get; } = node;
    public int AttackRelatedSnapshot { get; set; } = attackRelatedSnapshot;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public byte ActorPhase => unchecked((byte)SourceConstructionOrdinal);
    public short SurfaceClearance { get; } = 100;
    public ushort MinorRadius { get; set; } = 175;
    public short PushValue { get; } = 64;
    public short Field216 { get; } = 32;
    public int ShadowScale { get; set; }
    public FixedVector3 ShadowPosition { get; set; }
    public int ShadowGroundY { get; set; }
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; }
    /// <summary>CBaddy field_218 charge/contact bits used by ChargePlayer.</summary>
    public uint CombatFlags { get; set; }
    public int State { get; set; }
    public int Substate { get; set; }
    public int Field310 { get; set; }
    public int Field324 { get; set; }
    public int Field328 { get; set; }
    public int Field32C { get; set; }
    public int Field330 { get; set; }
    public int Field334 { get; set; }
    public int Field338 { get; set; }
    public int Field344 { get; set; }
    public int Field348 { get; set; }
    public int Field358 { get; set; }
    public int Field35C { get; set; }
    public int Field388 { get; set; }
    public int Field3D0 { get; set; }
    public int Field3D4 { get; set; }
    /// <summary>CRhino field_3DC recent XA slot bitmask updated by PlayXAPlease (0x47E0A0).</summary>
    public int Field3DC { get; set; }
    /// <summary>CRhino field_2FC pre-physics position for GonnaHitWall / charge sphere tests.</summary>
    public FixedVector3 PreviousPosition { get; set; }
    public byte BossCameraEnabled { get; set; }
    public uint MovementAnimations { get; set; } = SetupWord0;
    public uint IdleAnimations { get; set; } = SetupWord1;
    public short Health { get; set; } = 1400;
    public int[] DazedStarSpeeds { get; } = new int[DazedStarCount];
    public bool IsDead => (BodyFlags & 0x40) != 0;
}
