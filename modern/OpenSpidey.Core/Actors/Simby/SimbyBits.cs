using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

/// <summary>M3dZone_LineToItem result: hit point (dword_5FBEA4) and the hit face normal (word_5FBEB0).</summary>
public readonly record struct SimbyLineHit(FixedVector3 Position, short NormalX, short NormalY, short NormalZ);

/// <summary>Web_GetGroundY plus the dword_5FBEA0 item it hit; ItemVelocityY is CBody mVel.vy when item mFlags has 0x100.</summary>
public readonly record struct SimbyGround(int Y, int? ItemVelocityY);

/// <summary>gAnimTable slots the Simby quad bits texture from (slot names at 0x547E44).</summary>
public enum SimbyBitTexture
{
    WebSplat = 6,
    SymDrop = 24,
    Splats = 27
}

public interface ISimbyBitHost
{
    /// <summary>dword_6A9038, the player that heads MechList.</summary>
    bool PlayerPresent { get; }
    FixedVector3 PlayerPosition { get; }
    /// <summary>G_MIKE_CAMERA[0].Position (0x56F1B4), whole units.</summary>
    FixedVector3 CameraPosition { get; }
    int Random(int bound);
    /// <summary>M3dZone_LineToItem(info, 0) with dword_5FBEE4 set; null when dword_5FBEA0 stays null.</summary>
    SimbyLineHit? LineToItem(FixedVector3 start, FixedVector3 end);
    /// <summary>Web_GetGroundY (0x4F54A0).</summary>
    SimbyGround GroundY(FixedVector3 position);
    /// <summary>M3dColij_LineToSphere(from, to, &amp;hit, MechList, 0, 4096); null when no body is hit.</summary>
    FixedVector3? LineToPlayer(FixedVector3 from, FixedVector3 to);
    /// <summary>The hit body's virtual Hit (vtable +12); MechList only ever yields the player here.</summary>
    void HitPlayer(ActorHit hit);
    /// <summary>SFX_PlayPos(sound, position, 0).</summary>
    void PlaySound(int sound, FixedVector3 position);
    GlowFlashState CreateGlowFlash(GlowFlashParameters parameters);
    /// <summary>new CSimbyDrop (0x4A57E0); production hosts construct and list the <see cref="SimbyDrop"/>.</summary>
    void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int itemVelocityY);
    /// <summary>new CSimbyShotSplat (0x4A5C70).</summary>
    void SpawnShotSplat(FixedVector3 position, FixedVector3 normal);
    /// <summary>new CSimbyDropSplat (0x4A5470).</summary>
    void SpawnDropSplat(FixedVector3 position, int itemVelocityY);
    /// <summary>new CSimbyBlob (0x4A6B00) on BulletList; production hosts construct the <see cref="SimbyBlob"/>.</summary>
    void SpawnBlob(FixedVector3 position, FixedVector3 velocity, int groundY);
}

/// <summary>CQuadBit (0x408FA0) state shared by the Simby shot, drop and splat bits.</summary>
public abstract class SimbyQuadBit
{
    public const uint DefaultRenderFlags = 0x1C0001;
    private readonly FixedVector3[] corners = new FixedVector3[4];
    /// <summary>Corner order +0x10, +0x3C, +0x48, +0x54.</summary>
    public IReadOnlyList<FixedVector3> Corners => corners;
    public SimbyBitTexture Texture { get; private set; }
    public int TextureFrame { get; private set; }
    /// <summary>+0x64 render flags; SetSemiTransparent/SetOpaque rewrite 0x100/0x2C0.</summary>
    public uint RenderFlags { get; private set; } = DefaultRenderFlags;
    /// <summary>+0x6C code/colour word; SetTint clears the code byte.</summary>
    public uint ColourCode { get; private set; } = 0x808080;
    public short Age { get; protected set; }
    /// <summary>+0x37, set by CBit::Die (0x408930).</summary>
    public bool Dead { get; private set; }
    public bool SemiTransparent => (RenderFlags & 0x200) != 0;
    public abstract void Move(ISimbyBitHost host);

    protected void SetCorner(int index, FixedVector3 position) => corners[index] = position;
    protected void SetTexture(SimbyBitTexture texture, int frame) { Texture = texture; TextureFrame = frame; }
    protected void SetTint(byte red, byte green, byte blue) => ColourCode = (uint)(red | green << 8 | blue << 16);
    protected void SetSemiTransparent() => RenderFlags = (RenderFlags & ~0x100u) | 0x2C0;
    protected void SetOpaque() => RenderFlags = (RenderFlags & ~0x2C0u) | 0x80;
    protected void Die() => Dead = true;

    /// <summary>Bit_ReduceRGB (0x408810): each channel saturates at zero, the code byte survives.</summary>
    protected void ReduceRgb(int amount)
    {
        int red = (byte)ColourCode, green = (byte)(ColourCode >> 8), blue = (byte)(ColourCode >> 16);
        red = red < amount ? 0 : red - amount;
        green = green < amount ? 0 : green - amount;
        blue = blue < amount ? 0 : blue - amount;
        ColourCode = (ColourCode & 0xFF000000u) | (uint)(byte)red | (uint)(byte)green << 8 | (uint)(byte)blue << 16;
    }

    /// <summary>CQuadBit::OrientUsing (0x409560).</summary>
    protected void OrientUsing(FixedVector3 center, FixedVector3 normal, int width, int height, int roll)
    {
        (FixedVector3 perpendicular, FixedVector3 cross) = SimbyBitMath.PerpendicularBasis(normal);
        (short sine, short cosine) = GameAngle.Heading(roll);
        FixedVector3 u = (cross * sine + perpendicular * cosine) >> 12;
        FixedVector3 v = (cross * cosine + perpendicular * -sine) >> 12;
        u *= width;
        v *= height;
        corners[0] = center - u - v;
        corners[1] = center + u - v;
        corners[2] = center - u + v;
        corners[3] = center + u + v;
    }
}

/// <summary>Utils/CVector helpers the Simby bits call, on the PC GTE emulation registers.</summary>
public static class SimbyBitMath
{
    /// <summary>CVector::Length (0x4E74A0).</summary>
    public static int Length(FixedVector3 vector) => FixedVectorMath.OriginalSquareRoot(Squared(vector >> 12));

    /// <summary>Utils_CalcUnitFacingCamera (0x4E7090).</summary>
    public static FixedVector3 UnitFacingCamera(FixedVector3 from, FixedVector3 to, FixedVector3 camera)
    {
        FixedVector3 delta = (to - from) >> 12;
        if (delta.X > 500 || delta.Y > 500 || delta.Z > 500) delta >>= 4;
        FixedVector3 eye = camera - (from >> 12);
        FixedVector3 result = Cross(delta, eye);
        int magnitude = FixedVectorMath.OriginalSquareRoot(Squared(result >> 8));
        if (magnitude < 5) return default;
        return new(16 * (result.X / magnitude), 16 * (result.Y / magnitude), 16 * (result.Z / magnitude));
    }

    /// <summary>sub_4E5E20: an axis-swapped perpendicular and normal x perpendicular (gte_op12).</summary>
    public static (FixedVector3 Perpendicular, FixedVector3 Cross) PerpendicularBasis(FixedVector3 normal)
    {
        uint x = (uint)Math.Abs((long)normal.X), y = (uint)Math.Abs((long)normal.Y), z = (uint)Math.Abs((long)normal.Z);
        FixedVector3 perpendicular;
        if (y < x)
            perpendicular = y <= z ? new(normal.Z, 0, unchecked(-normal.X)) : new(unchecked(-normal.Y), normal.X, 0);
        else if (x > z)
            perpendicular = y > x || y > z ? new(unchecked(-normal.Y), normal.X, 0) : new(normal.Z, 0, unchecked(-normal.X));
        else
            perpendicular = new(0, unchecked(-normal.Z), normal.Y);
        return (perpendicular, Cross(normal, perpendicular) >> 12);
    }

    /// <summary>sub_4E5EE0: unit side (normal x up) and its second axis, falling back to sub_4E5E20.</summary>
    public static (FixedVector3 Side, FixedVector3 Second) SurfaceBasis(FixedVector3 normal)
    {
        FixedVector3 side = (Cross(normal, new(0, 4096, 0)) >> 12) << 8;
        int length = FixedVectorMath.OriginalSquareRoot(Squared(side >> 12));
        if (length == 0) return PerpendicularBasis(normal);
        side /= length;
        return (side, Cross(side, normal) >> 12);
    }

    private static FixedVector3 Cross(FixedVector3 first, FixedVector3 second) => new(
        unchecked(first.Y * second.Z - first.Z * second.Y),
        unchecked(first.Z * second.X - second.Z * first.X),
        unchecked(second.Y * first.X - first.Y * second.X));
    private static int Squared(FixedVector3 vector) => unchecked(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
}

/// <summary>CSimbyShot (0x4A5FD0 ctor, 0x4A6520 Move): the Simby's slime bolt travelling toward the player.</summary>
public sealed class SimbyShot : SimbyQuadBit
{
    public FixedVector3 Origin { get; private set; }
    public FixedVector3 Direction { get; private set; }
    public int Length { get; private set; }
    public int Back { get; private set; }
    public int Front { get; private set; }
    public bool HitEnvironment { get; private set; }
    public FixedVector3 Normal { get; private set; }

    public SimbyShot(FixedVector3 hand, ISimbyBitHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!host.PlayerPresent) { Die(); return; }
        SetTexture(SimbyBitTexture.WebSplat, 0);
        SetSemiTransparent();
        SetTint(100, 0, 100);
        FixedVector3 toPlayer = host.PlayerPosition - hand;
        Length = SimbyBitMath.Length(toPlayer);
        if (Length == 0) { Die(); return; }
        Direction = toPlayer / Length;
        FixedVector3 end = hand + Direction * 5000;
        if (host.LineToItem(hand, end) is { } hit)
        {
            HitEnvironment = true;
            end = hit.Position;
            Normal = new(hit.NormalX, hit.NormalY, hit.NormalZ);
        }
        Length = SimbyBitMath.Length(end - hand);
        if (Length == 0) { Die(); return; }
        Front = -host.Random(200);
        Back = Front - 250;
        Origin = hand;
        Place(host);
    }

    public override void Move(ISimbyBitHost host)
    {
        if (Dead) return;
        Back = unchecked(Back + 150);
        Front = unchecked(Front + 150);
        Place(host);
        if (host.LineToPlayer(Corners[0], Corners[2]) is { } hit)
        {
            // byte1 of the stack SHitInfo is never written.
            host.HitPlayer(new ActorHit(6, 0, 28, 10, default));
            host.PlaySound(SplatSound(host), Corners[0]);
            GlowFlashState glow = host.CreateGlowFlash(new(hit, 5, 255, 255, 255, 0, 100, 0, 100, 0, 9, 0, 1, 12, 40, 6, 20, 1, 1));
            glow.Angle = unchecked((ushort)host.Random(4096));
            SimbyDrop.MakeDroplets(hit, 10, host);
            Die();
            return;
        }
        if (Back <= Length) return;
        if (HitEnvironment) Burst(host);
        Die();
    }

    private void Burst(ISimbyBitHost host)
    {
        (FixedVector3 side, FixedVector3 second) = SimbyBitMath.SurfaceBasis(Normal);
        SimbyGround ground = host.GroundY(Corners[0] + Normal * 100);
        int itemVelocity = ground.ItemVelocityY ?? 0;
        for (int i = 10; i != 0; i--)
        {
            (short sine, short cosine) = GameAngle.Heading(host.Random(8193) - 5120);
            FixedVector3 spread = (side * cosine + second * sine) >> 12;
            (sine, cosine) = GameAngle.Heading(host.Random(512));
            FixedVector3 velocity = ((spread * cosine + Normal * sine) >> 12) * (host.Random(40) + 30);
            host.SpawnDrop(Corners[0], velocity, ground.Y, itemVelocity);
        }
        host.SpawnShotSplat(Corners[0], Normal);
        host.PlaySound(SplatSound(host), Corners[0]);
    }

    private static int SplatSound(ISimbyBitHost host) => (361 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000;

    private void Place(ISimbyBitHost host)
    {
        FixedVector3 back = Along(Back), front = Along(Front);
        SetCorner(0, back);
        SetCorner(2, front);
        FixedVector3 side = SimbyBitMath.UnitFacingCamera(back, front, host.CameraPosition);
        SetCorner(1, back + side * 20);
        SetCorner(3, front + side * 20);
    }

    private FixedVector3 Along(int distance) =>
        distance < 0 ? Origin : Origin + Direction * (distance <= Length ? distance : Length);
}

/// <summary>CSimbyDrop (0x4A57E0 ctor, 0x4A5980 Move): a falling slime streak that splats on the ground.</summary>
public sealed class SimbyDrop : SimbyQuadBit
{
    public FixedVector3 Velocity { get; private set; }
    public int GroundY { get; private set; }
    public bool NeedsGround { get; private set; }
    public int ItemVelocityY { get; private set; }

    public SimbyDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int itemVelocityY, ISimbyBitHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        SetTexture(SimbyBitTexture.SymDrop, 0);
        SetTint(59, 11, 55);
        SetSemiTransparent();
        GroundY = groundY;
        Velocity = velocity;
        SetCorner(2, position);
        SetCorner(0, position);
        Orient(host);
        NeedsGround = true;
        ItemVelocityY = itemVelocityY;
    }

    /// <summary>Simby_MakeDroplets (0x4A5B40).</summary>
    public static void MakeDroplets(FixedVector3 position, int count, ISimbyBitHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        SimbyGround ground = host.GroundY(position);
        int itemVelocity = ground.ItemVelocityY ?? 0;
        for (; count > 0; count--)
        {
            int pitch = host.Random(1024), yaw = host.Random(4096), speed = host.Random(20) + 10;
            (short pitchSine, short pitchCosine) = GameAngle.Heading(pitch);
            (short yawSine, short yawCosine) = GameAngle.Heading(yaw);
            var velocity = new FixedVector3(unchecked(speed * pitchSine * yawSine) >> 12, unchecked(-(speed * pitchCosine)),
                unchecked(speed * pitchSine * yawCosine) >> 12);
            host.SpawnDrop(position, velocity, ground.Y, itemVelocity);
        }
    }

    public override void Move(ISimbyBitHost host)
    {
        FixedVector3 head = Corners[2];
        SetCorner(0, head);
        head += Velocity;
        Velocity = Velocity with { Y = unchecked(Velocity.Y + 29584) };
        if (NeedsGround && head.Y > GroundY)
        {
            SimbyGround ground = host.GroundY(Corners[0]);
            GroundY = ground.Y;
            if (ground.ItemVelocityY is { } item) ItemVelocityY = item;
            NeedsGround = false;
        }
        if (head.Y > GroundY) head = head with { Y = GroundY };
        SetCorner(2, head);
        Orient(host);
        if (Corners[2].Y == GroundY && Corners[0].Y == GroundY)
        {
            host.SpawnDropSplat(Corners[2], ItemVelocityY);
            Die();
        }
        Age = unchecked((short)(Age + 1));
        if (Age > 60) Die();
    }

    private void Orient(ISimbyBitHost host)
    {
        FixedVector3 side = SimbyBitMath.UnitFacingCamera(Corners[2], Corners[0], host.CameraPosition);
        SetCorner(3, Corners[2] + side * 10);
        SetCorner(1, Corners[0] + side * 10);
    }
}

/// <summary>Growing surface splat shared by CSimbyDropSplat and CSimbyShotSplat.</summary>
public abstract class SimbySurfaceSplat : SimbyQuadBit
{
    public FixedVector3 Center { get; protected set; }
    public int TargetScale { get; protected set; }
    public int Scale { get; protected set; }
    public FixedVector3 AxisU { get; private set; }
    public FixedVector3 AxisV { get; private set; }

    protected void CaptureAxes()
    {
        AxisU = (Corners[1] - Corners[0]) >> 1;
        AxisV = (Corners[2] - Corners[0]) >> 1;
    }

    protected void Grow(int shift)
    {
        Scale = unchecked(Scale + ((TargetScale - Scale) >> shift));
        FixedVector3 a = AxisU * Scale, b = AxisV * Scale;
        SetCorner(0, Center - b - a);
        SetCorner(1, a + (Center - b));
        SetCorner(2, b + Center - a);
        SetCorner(3, a + b + Center);
        Age = unchecked((short)(Age + 1));
        if ((ushort)Age > 30)
        {
            SetSemiTransparent();
            ReduceRgb(5);
        }
        if ((ColourCode & 0xFFFFFF) == 0) Die();
    }
}

/// <summary>CSimbyDropSplat (0x4A5470 ctor, 0x4A5660 Move): a floor splat riding its item's vertical velocity.</summary>
public sealed class SimbyDropSplat : SimbySurfaceSplat
{
    public int VelocityY { get; private set; }

    public SimbyDropSplat(FixedVector3 position, int itemVelocityY, ISimbyBitHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        int frame = host.Random(3);
        if (frame is < 0 or > 2) throw new InvalidOperationException("Rand(3) returned outside the three splats frames.");
        SetTexture(SimbyBitTexture.Splats, frame);
        SetOpaque();
        SetTint(47, 9, 44);
        TargetScale = host.Random(10) + 20;
        Center = position;
        OrientUsing(Center, new(0, -4096, 0), 1, 1, host.Random(4096));
        CaptureAxes();
        Move(host);
        VelocityY = itemVelocityY;
    }

    public override void Move(ISimbyBitHost host)
    {
        Center = Center with { Y = unchecked(Center.Y + VelocityY) };
        Grow(2);
    }
}

/// <summary>CSimbyShotSplat (0x4A5C70 ctor, 0x4A5E40 Move): the wall splat a shot leaves on the item it hit.</summary>
public sealed class SimbyShotSplat : SimbySurfaceSplat
{
    public SimbyShotSplat(FixedVector3 position, FixedVector3 normal, ISimbyBitHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        SetTexture(SimbyBitTexture.WebSplat, 0);
        SetSemiTransparent();
        SetTint(59, 11, 55);
        TargetScale = host.Random(20) + 30;
        Center = position;
        // The constructor reads the low word of each CVector component as an SVECTOR.
        var words = new FixedVector3(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z));
        OrientUsing(Center, words, 1, 1, host.Random(4096));
        CaptureAxes();
        Move(host);
    }

    public override void Move(ISimbyBitHost host) => Grow(1);
}

/// <summary>CSimbyBlob (0x4A6B00 ctor, AI 0x4A6C90): a spinning "symblob" CBody thrown out by Simby_SplattyDeath.</summary>
public sealed class SimbyBlob
{
    public const string ModelName = "symblob";
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public short AngleX { get; private set; }
    public short AngleY { get; private set; }
    public short AngleZ { get; private set; }
    /// <summary>CItem 0x28..0x2C scale, applied because the constructor sets mFlags 0x200.</summary>
    public short ScaleX { get; private set; } = 256;
    public short ScaleY { get; private set; } = 256;
    public short ScaleZ { get; private set; } = 256;
    public short SpinX { get; }
    public short SpinY { get; }
    public short SpinZ { get; }
    public int GroundY { get; }
    public int Age { get; private set; }
    public bool Landed { get; private set; }
    /// <summary>Set by the inherited CBody::Die (vtable +4, 0x4606F0).</summary>
    public bool Dead { get; private set; }

    public SimbyBlob(FixedVector3 position, FixedVector3 velocity, int groundY, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        AngleX = unchecked((short)random(4096));
        AngleY = unchecked((short)random(4096));
        AngleZ = unchecked((short)random(4096));
        SpinX = unchecked((short)random(200));
        SpinY = unchecked((short)random(200));
        Position = position;
        Velocity = velocity;
        GroundY = groundY;
    }

    public void Move(Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (Dead) return;
        Position += Velocity;
        Velocity = Velocity with { Y = unchecked(Velocity.Y + 29584) };
        if (Position.Y > GroundY)
        {
            Position = Position with { Y = GroundY };
            Velocity = new(Velocity.X >> 1, Velocity.Y, Velocity.Z >> 1);
            if (!Landed)
            {
                // The blob flattens into a ground splat: Y scale 0 and a fresh random heading.
                ScaleY = 0;
                AngleX = 0;
                AngleY = unchecked((short)random(4096));
                AngleZ = 0;
            }
            Landed = true;
        }
        if (!Landed)
        {
            AngleX = unchecked((short)(AngleX + SpinX));
            AngleY = unchecked((short)(AngleY + SpinY));
            AngleZ = unchecked((short)(AngleZ + SpinZ));
        }
        Age = unchecked(Age + 1);
        if (Age > 20) Dead = true;
    }
}

/// <summary>Simby_SplattyDeath (0x4A7050): glow, ten part blobs each with three drops, then three body blobs.</summary>
public static class SimbySplattyDeath
{
    /// <summary>unk_5555C8: the Simby model parts the blobs burst from.</summary>
    public static readonly int[] Parts = [2, 3, 4, 5, 7, 8, 9, 10, 12, 15];

    public static void Run(FixedVector3 simbyPosition, Func<int, FixedVector3> partHookPosition, ISimbyBitHost host)
    {
        ArgumentNullException.ThrowIfNull(partHookPosition);
        ArgumentNullException.ThrowIfNull(host);
        GlowFlashState glow = host.CreateGlowFlash(new(simbyPosition, 5, 255, 255, 255, 0, 100, 0, 100, 0, 9, 0, 1, 36, 120, 18, 60, 1, 2));
        glow.Angle = unchecked((ushort)host.Random(4096));
        SimbyGround ground = host.GroundY(simbyPosition);
        int itemVelocity = ground.ItemVelocityY ?? 0;
        foreach (int part in Parts)
        {
            FixedVector3 hook = partHookPosition(part);
            host.SpawnBlob(hook, BlobVelocity(host), ground.Y);
            for (int drop = 3; drop != 0; drop--)
            {
                int pitch = host.Random(1024), yaw = host.Random(4096), speed = host.Random(20) + 20;
                (short pitchSine, short pitchCosine) = GameAngle.Heading(pitch);
                (short yawSine, short yawCosine) = GameAngle.Heading(yaw);
                var velocity = new FixedVector3(unchecked(speed * pitchSine * yawSine) >> 12, unchecked(-(speed * pitchCosine)),
                    unchecked(speed * pitchSine * yawCosine) >> 12);
                host.SpawnDrop(hook, velocity, ground.Y, itemVelocity);
            }
        }
        for (int blob = 3; blob != 0; blob--) host.SpawnBlob(simbyPosition, BlobVelocity(host), ground.Y);
    }

    private static FixedVector3 BlobVelocity(ISimbyBitHost host)
    {
        int x = host.Random(31) - 15;
        int y = -host.Random(40);
        int z = host.Random(31) - 15;
        return new FixedVector3(x, y, z) << 12;
    }
}
