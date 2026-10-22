using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CSimbyDroplet (0x4A3B50 ctor, 0x4A3C50 Move): gAnimTable24 frame1 flat bit with vertical fall.</summary>
public sealed class SimbyDropletState
{
    public const short SourceScale = 800;
    public const byte SemiTransparencyRate = 32;

    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public short VerticalAcceleration { get; }
    public int GroundY { get; }
    public ushort OwnerNode { get; }
    public int Age { get; private set; }
    public short Scale => SourceScale;
    public short Angle => 0;
    public uint PostScale => 0x10001000;
    public uint CodeBgr => 0x2C808080;
    public bool Alive { get; private set; } = true;

    public SimbyDropletState(TriggerNode node, Func<FixedVector3, int> groundY)
    {
        ArgumentNullException.ThrowIfNull(groundY);
        if (node.Id == ushort.MaxValue) throw new InvalidDataException("Bad NodeIndex sent to CSimbyDroplet.");
        OwnerNode = node.Id;
        TriggerPlacement placement = TriggerPlacement.Read(node);
        TriggerPosition source = placement.Position;
        Position = new FixedVector3(source.X, source.Y, source.Z) << 12;
        ReadOnlySpan<byte> bytes = node.Data.Span;
        int accelOffset = placement.PositionOffset + 24;
        if (accelOffset + 2 > bytes.Length) throw new InvalidDataException($"Node {node.Id}: droplet acceleration extends beyond its record.");
        VerticalAcceleration = BinaryPrimitives.ReadInt16LittleEndian(bytes[accelOffset..]);
        GroundY = groundY(Position);
    }

    public void Move()
    {
        if (!Alive) throw new InvalidOperationException("A deleted CSimbyDroplet cannot Move.");
        Position = Position with { Y = unchecked(Position.Y + Velocity.Y) };
        Velocity = Velocity with { Y = unchecked(Velocity.Y + VerticalAcceleration) };
        Age = unchecked(Age + 1);
        if (Position.Y > GroundY || Age > 60) Alive = false;
    }
}
