using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Effects;

public interface ITrapWebEffectTarget
{
    object Identity { get; }
    FixedVector3 Position { get; }
    ushort ItemFlags { get; set; }
    uint TintColor { get; set; }
    byte UpperExtent { get; }
    byte LowerExtent { get; }
    bool Collide(FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook);
    FixedVector3 HookPosition(ActorHook hook);
    void BuildTransform();
}

public interface ITrapWebEffectHost
{
    PlayerKickHandle StoreTarget(ITrapWebEffectTarget target);
    ITrapWebEffectTarget? RecoverTarget(ref PlayerKickHandle handle);
    bool ContactExists(ITrapWebEffectTarget target, byte type);
    void BindContact(ITrapWebEffectTarget target, byte type, TrapWebEffect effect);
    void ClearContact(ITrapWebEffectTarget target, byte type);
    void AttachEffect(TrapWebEffect effect);
    void DetachEffect(TrapWebEffect effect);
    void CreateLine(TrapWebLine line);
    void DeleteLine(TrapWebLine line);
    void CreateStrand(TrapWebEffect effect, TrapWebTriangle strand);
    void DeleteStrand(TrapWebTriangle strand);
    int GroundY(FixedVector3 position);
    void CreateFragment(ImpactWebFragmentRequest request, Func<int, int> random);
    void EmitFirePulse(FixedVector3 position, int scale);
}

public sealed class TrapWebLine(TrapWebEffect owner)
{
    public TrapWebEffect Owner { get; } = owner;
    public bool Protected => false;
}

public sealed class TrapWebTriangle(byte newest, byte previous, byte widest)
{
    public byte Newest { get; } = newest;
    public byte Previous { get; } = previous;
    public byte Widest { get; } = widest;
    public uint TextureChecksum => 0x35023093;
    public FixedVector3 A { get; internal set; }
    public FixedVector3 B { get; internal set; }
    public FixedVector3 C { get; internal set; }
    public FixedVector3 D => C;
    public uint Tint { get; internal set; } = 0x808080;
}

/// <summary>Original CTrapWebEffect hook projector, wrapping strands, burning tint, Move and Burst ownership.</summary>
public sealed class TrapWebEffect : IThugTrapWeb, IDisposable
{
    private readonly ITrapWebEffectHost host;
    private readonly Func<int, int> random;
    private readonly ActorHook[] hooks = new ActorHook[81];
    private readonly short[] heights = new short[81];
    private readonly FixedVector3[] points = new FixedVector3[81];
    private readonly uint[] colours = Enumerable.Repeat(0x52FFFFFFu, 80).ToArray();
    private readonly List<TrapWebTriangle> triangles = [];
    private PlayerKickHandle targetHandle;
    public byte Type { get; }
    public byte FireVariant { get; set; }
    public int HookCount { get; private set; }
    public int StrandCount { get; private set; }
    public int ProjectorHeight { get; private set; }
    public int ProjectorRadius => 400;
    public int ProjectorAngle { get; private set; }
    public byte ProjectorCountdown { get; private set; } = 6;
    public sbyte ProjectorSpeed { get; private set; }
    public ushort BurnAngle { get; private set; }
    public short BurnRamp { get; private set; }
    public uint StartColour { get; private set; } = 0x52FFFFFF;
    public FixedVector3 Start { get; private set; }
    public IReadOnlyList<ActorHook> Hooks { get; }
    public IReadOnlyList<short> Heights { get; }
    public IReadOnlyList<FixedVector3> Points { get; }
    public IReadOnlyList<uint> Colours { get; }
    public IReadOnlyList<TrapWebTriangle> Triangles { get; }
    public TrapWebLine Line { get; }
    public bool Alive { get; private set; } = true;
    public bool IsDisposed { get; private set; }

    public TrapWebEffect(ITrapWebEffectTarget target, byte type, ITrapWebEffectHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (type > 1) throw new ArgumentOutOfRangeException(nameof(type));
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        Type = type;
        Hooks = Array.AsReadOnly(hooks); Heights = Array.AsReadOnly(heights);
        Points = Array.AsReadOnly(points); Colours = Array.AsReadOnly(colours);
        Triangles = triangles.AsReadOnly();
        host.AttachEffect(this);
        targetHandle = host.StoreTarget(target);
        host.ContactExists(target, type); // The PC recovery feeds its diagnostic even when the slot is empty.
        host.BindContact(target, type, this);
        int height = type == 0 ? -(random(target.UpperExtent >> 1) + (target.UpperExtent >> 1)) : target.UpperExtent / -3;
        ProjectorSpeed = unchecked((sbyte)(height / 6));
        Line = new(this);
        host.CreateLine(Line);
    }

    public void MoveProjector()
    {
        CheckAllocated();
        ProjectorHeight = unchecked(ProjectorHeight + ProjectorSpeed);
        if (ProjectorCountdown != 0) ProjectorCountdown--;
        if (ProjectorCountdown == 0)
        {
            ITrapWebEffectTarget target = RequireTarget();
            int wanted = Type == 0 ? random(target.LowerExtent + target.UpperExtent) - target.UpperExtent
                : random(target.UpperExtent * 2 / 3) - target.UpperExtent / 3;
            ProjectorCountdown = 6;
            ProjectorSpeed = unchecked((sbyte)((wanted - ProjectorHeight) / 6));
        }
        ProjectorAngle = (ProjectorAngle + 256) & 4095;
    }

    public bool CalculateHook(int index)
    {
        CheckAllocated();
        if ((uint)index > 80) throw new ArgumentOutOfRangeException(nameof(index));
        ITrapWebEffectTarget target = RequireTarget();
        var angle = GameAngle.Heading(ProjectorAngle);
        FixedVector3 start = target.Position + new FixedVector3(angle.Cosine * ProjectorRadius,
            unchecked(ProjectorHeight << 12), angle.Sine * ProjectorRadius);
        FixedVector3 end = target.Position + new FixedVector3(0, unchecked(ProjectorHeight << 12), 0);
        bool hit = target.Collide(start, end, 4096, out hooks[index]);
        if (hit) heights[index] = unchecked((short)ProjectorHeight);
        return hit;
    }

    public void AddAnotherStrand()
    {
        CheckAllocated();
        if (HookCount == 80) return;
        MoveProjector();
        if (HookCount == 0)
        {
            if (!CalculateHook(0)) return;
            MoveProjector();
        }
        if (!CalculateHook(HookCount + 1)) return;
        HookCount++;
        if (HookCount < 2 || random(4) != 0 || triangles.Count >= 20) return;
        int newest = HookCount, widest = newest - 1, biggest = 0;
        for (int index = 0; index < newest - 2; index++)
        {
            int gap = Math.Abs(heights[newest] - heights[index]);
            if (gap > biggest) { biggest = gap; widest = index; }
        }
        var strand = new TrapWebTriangle((byte)newest, (byte)(newest - 2), (byte)widest);
        host.CreateStrand(this, strand);
        triangles.Add(strand);
    }

    public void AdvanceBits()
    {
        CheckAllocated();
        if (!Alive || HookCount == 0) return;
        ITrapWebEffectTarget target = RequireTarget();
        for (int index = 0; index <= HookCount; index++) points[index] = target.HookPosition(hooks[index]);
        Start = points[0];
        if (FireVariant != 0)
        {
            int intensity = Math.Min(255, (100 * GameAngle.Heading(BurnAngle).Sine >> 12) + 206);
            BurnAngle = unchecked((ushort)(BurnAngle + 80));
            target.ItemFlags |= 0x400;
            target.TintColor = unchecked((uint)((BurnRamp * (intensity - 32) >> 8) + 32 |
                ((BurnRamp * ((123 * intensity >> 8) - 32) >> 8) + 32) << 8 |
                (32 - (BurnRamp >> 3)) << 16));
            BurnRamp = unchecked((short)(BurnRamp + 8));
            if (BurnRamp > 256) BurnRamp = 256;
            for (int index = 0; index < HookCount; index++) colours[index] = FireColour(random(192));
            StartColour = FireColour(random(192));
            host.EmitFirePulse(points[random(HookCount) + 1], 256);
            foreach (TrapWebTriangle strand in triangles)
            {
                UpdateTriangle(strand);
                strand.Tint = FireColour(random(192)) & 0xFFFFFF;
            }
        }
        else
        {
            int maximum = 2 * (byte)target.TintColor;
            if ((target.ItemFlags & 0x400) == 0 || (target.ItemFlags & 0x80) != 0 || maximum > 256) maximum = 256;
            for (int index = 0; index < HookCount; index++) colours[index] = GreyColour(random(maximum));
            StartColour = GreyColour(random(maximum));
            byte tint = unchecked((byte)(maximum != 0 ? maximum - 1 : maximum));
            foreach (TrapWebTriangle strand in triangles)
            {
                UpdateTriangle(strand);
                strand.Tint = (uint)(tint | tint << 8 | tint << 16);
            }
        }
        StrandCount = HookCount;
    }

    public void Burst()
    {
        CheckAllocated();
        ITrapWebEffectTarget? target = host.RecoverTarget(ref targetHandle);
        if (target is not null)
        {
            if (HookCount != 0)
            {
                target.BuildTransform();
                // Actual PC stores hook0 in the line start and hooks1..N in its separate segment array.
                Start = points[0] = target.HookPosition(hooks[0]);
                for (int index = 0; index < HookCount; index++) points[index + 1] = target.HookPosition(hooks[index + 1]);
                int ground = host.GroundY(target.Position);
                FixedVector3 previous = Start;
                for (int index = 0; index < HookCount - 1; index += 2)
                {
                    host.CreateFragment(new(ground, previous, points[index + 1], points[index + 2], target.Position, 25, 0), random);
                    previous = points[index + 2];
                }
            }
            if (FireVariant != 0) target.ItemFlags = unchecked((ushort)(target.ItemFlags & ~0x400));
            host.ClearContact(target, Type);
        }
        Alive = false;
    }

    private void UpdateTriangle(TrapWebTriangle strand)
    {
        strand.A = points[strand.Newest]; strand.B = points[strand.Previous]; strand.C = points[strand.Widest];
    }
    private static uint GreyColour(int value) => 0x52000000u | (uint)((byte)value * 0x010101);
    private static uint FireColour(int value) => 0x52000000u | (uint)((255 * value >> 8) + 63 | ((123 * value >> 8) + 30) << 8);
    private ITrapWebEffectTarget RequireTarget() => host.RecoverTarget(ref targetHandle) ??
        throw new InvalidOperationException("Original trap-web path requires its allocated target actor.");
    private void CheckAllocated() => ObjectDisposedException.ThrowIf(IsDisposed, this);
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        host.DeleteLine(Line);
        foreach (TrapWebTriangle strand in triangles) host.DeleteStrand(strand);
        triangles.Clear(); host.DetachEffect(this);
    }
}
