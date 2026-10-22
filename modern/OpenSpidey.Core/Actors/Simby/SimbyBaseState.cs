using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

/// <summary>CSimbyBase (0x334 bytes, vtable 0x53C100): the "sym_base" puddle a CSimby shrinks into or regrows from.</summary>
public sealed class SimbyBaseState
{
    public const string ModelName = "sym_base";

    /// <summary>Inlined constructor: CBaddy (0x402C00) defaults, BaddyList attach, 0x46 bit 0x10 cleared, owner angles copied.</summary>
    public SimbyBaseState(FixedVector3 position, ActorAngles angles)
    {
        Position = position;
        Angles = angles;
    }

    /// <summary>CItem 0x8..0x10.</summary>
    public FixedVector3 Position { get; set; }
    /// <summary>CItem 0x14..0x18.</summary>
    public ActorAngles Angles { get; set; }
    /// <summary>CItem 0x28 / 0x2A / 0x2C, initialised to 4096 by the CItem constructor (0x460380).</summary>
    public short ScaleX { get; set; } = 4096;
    public short ScaleY { get; set; } = 4096;
    public short ScaleZ { get; set; } = 4096;
    /// <summary>CItem 0x4; 0x2 from CSuper (0x460720), 0x200 applies the scale to the 0x164 matrix.</summary>
    public ushort ItemFlags { get; set; } = 0x2;
    /// <summary>CBody 0x46: 0x16 from CBody (0x460080) and 0x200 from CBaddy, minus the constructor's 0x10.</summary>
    public ushort BodyFlags { get; set; } = 0x206;
    /// <summary>field_324: nonzero while the base eases toward <see cref="TargetScale"/> and its owner handle (0x32C) recovers.</summary>
    public int Following { get; set; }
    /// <summary>field_328.</summary>
    public int TargetScale { get; set; }
    public bool Dead => (BodyFlags & 0x40) != 0;
}
