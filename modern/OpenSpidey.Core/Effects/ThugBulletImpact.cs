using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct ThugRicochetContact(FixedVector3 Start, FixedVector3 End, FixedVector3 Position, FixedVector3 Normal);

public interface IThugPingAttachmentHost
{
    object StoreImpactHandle(object actor);
    object? RecoverImpactHandle(object handle);
    FixedVector3 GetDynamicHookPosition(object actor, ActorHook hook);
}

public interface IThugBulletImpactHost : IThugPingAttachmentHost
{
    void PlayPositionSound(ushort sound, FixedVector3 position, int volume);
    bool CollideWithActor(object actor, FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook);
    void EmitLaserPing(ThugLaserPingState ping);
    void EmitAttachedPing(ThugAttachedPingState ping);
}

/// <summary>Impact side effects preceding the CThugBulletTracer ribbon constructor.</summary>
public static class ThugBulletImpact
{
    public static void Create(FixedVector3 start, FixedVector3 end, object? attachment, ThugRicochetContact? contact,
        IThugBulletImpactHost host, Func<int, int> random, byte red, byte green, byte blue)
    {
        if (contact is { } line) CreateRicochet(line, host, random, red, green, blue);
        if (attachment is not null && host.CollideWithActor(attachment, start, end, 4096, out ActorHook hook))
            host.EmitAttachedPing(new(attachment, hook, host, random));
    }

    public static void CreateRicochet(ThugRicochetContact line, IThugBulletImpactHost host, Func<int, int> random,
        byte red, byte green, byte blue)
    {
        host.PlayPositionSound(unchecked((ushort)(((red != 255 ? 12 : 39) + random(2)) | 0x8000)), line.Position, 0);
        FixedVector3 direction = FixedVectorMath.Normalize((line.Start - line.End) >> 12);
        FixedVector3 normal = new(unchecked((short)line.Normal.X), unchecked((short)line.Normal.Y), unchecked((short)line.Normal.Z));
        int twiceDot = unchecked(2 * (unchecked(direction.X * normal.X + direction.Y * normal.Y + direction.Z * normal.Z) >> 12));
        direction = new FixedVector3(unchecked(twiceDot * normal.X) >> 12, unchecked(twiceDot * normal.Y) >> 12,
            unchecked(twiceDot * normal.Z) >> 12) - direction;
        uint absX = unchecked((uint)GameWord.Absolute(direction.X)), absY = unchecked((uint)GameWord.Absolute(direction.Y)), absZ = unchecked((uint)GameWord.Absolute(direction.Z));
        FixedVector3 perpendicular;
        if (absY >= absX && absX <= absZ) perpendicular = new(0, unchecked(-direction.Z), direction.Y);
        else if (absY >= absX && absY > absX) perpendicular = new(unchecked(-direction.Y), direction.X, 0);
        else if (absY <= absZ) perpendicular = new(direction.Z, 0, unchecked(-direction.X));
        else perpendicular = new(unchecked(-direction.Y), direction.X, 0);
        // Original GTE op12 narrows each product/subtraction to int before the arithmetic shift.
        FixedVector3 cross = new(unchecked(direction.Y * perpendicular.Z - direction.Z * perpendicular.Y) >> 12,
            unchecked(direction.Z * perpendicular.X - perpendicular.Z * direction.X) >> 12,
            unchecked(perpendicular.Y * direction.X - direction.Y * perpendicular.X) >> 12);
        host.EmitLaserPing(new(line.Position, direction, perpendicular, red, green, blue));
        host.EmitLaserPing(new(line.Position, direction, cross, red, green, blue));
    }
}

/// <summary>Original CThugLaserPing world-space quad and strict spread lifetime.</summary>
public sealed class ThugLaserPingState
{
    public FixedVector3 Origin { get; }
    public FixedVector3 Direction { get; }
    public FixedVector3 Perpendicular { get; }
    public FixedVector3 Position { get; private set; }
    public FixedVector3 PositionB { get; private set; }
    public FixedVector3 PositionC { get; private set; }
    public FixedVector3 PositionD { get; private set; }
    public int Spread { get; private set; }
    public uint Tint { get; }
    public bool Alive { get; private set; } = true;
    public int TextureAnimationIndex => 10;
    public int TextureFrame => 1;
    public ushort SourceType => 34;

    public ThugLaserPingState(FixedVector3 origin, FixedVector3 direction, FixedVector3 perpendicular, byte red, byte green, byte blue)
    {
        Origin = origin; Direction = direction; Perpendicular = perpendicular;
        Tint = (uint)(red | green << 8 | blue << 16);
    }

    public void Advance()
    {
        if (!Alive) return;
        Spread = unchecked(Spread + 8);
        if (Spread > 32) { Alive = false; return; }
        PositionC = Origin + Perpendicular * Spread;
        PositionD = Origin - Perpendicular * Spread;
        Position = PositionC + Direction * unchecked(Spread * 4);
        PositionB = PositionD + Direction * unchecked(Spread * 4);
    }
}

/// <summary>Original fixed-frame CThugPing bound to a recoverable animated host hook.</summary>
public sealed class ThugAttachedPingState
{
    private readonly IThugPingAttachmentHost host;
    private readonly object handle;
    public ActorHook Hook { get; }
    public FixedVector3 Position { get; private set; }
    public uint CodeRgb { get; private set; } = 0x2E808080;
    public short Angle { get; }
    public short Scale => 400;
    public int AnimationIndex => 10;
    public int Frame => 1;
    public bool Alive { get; private set; } = true;

    public ThugAttachedPingState(object actor, ActorHook hook, IThugPingAttachmentHost host, Func<int, int> random)
    {
        this.host = host;
        Hook = hook;
        handle = host.StoreImpactHandle(actor);
        short angle = unchecked((short)random(1024));
        if (random(2) != 0) angle = unchecked((short)-angle);
        Angle = angle;
        SetPosition();
    }

    public void Advance()
    {
        if (!Alive) return;
        SetPosition();
        byte red = Reduce((byte)CodeRgb), green = Reduce((byte)(CodeRgb >> 8)), blue = Reduce((byte)(CodeRgb >> 16));
        CodeRgb = (CodeRgb & 0xFF000000) | (uint)(red | green << 8 | blue << 16);
        if ((CodeRgb & 0xFFFFFF) == 0) Alive = false;
    }

    private void SetPosition()
    {
        object? actor = host.RecoverImpactHandle(handle);
        if (actor is null) Alive = false;
        else Position = host.GetDynamicHookPosition(actor, Hook);
    }
    private static byte Reduce(byte value) => value >= 7 ? (byte)(value - 7) : (byte)0;
}
