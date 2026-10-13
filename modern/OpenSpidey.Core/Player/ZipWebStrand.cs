using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered base/inner web polylines and the sixteen-tick release contraction.</summary>
public sealed class ZipWebStrand
{
    private readonly FixedVector3 anchor;
    private FixedVector3 hook;
    private readonly FixedVector3[] outer;
    private readonly FixedVector3[] inner;
    private readonly FixedVector3[] knots;
    private readonly byte[] knotFractions;
    private readonly sbyte[] sideOffsets;
    private int releaseTicks;
    public IReadOnlyList<FixedVector3> Outer { get; }
    public IReadOnlyList<FixedVector3> Inner { get; }
    public IReadOnlyList<FixedVector3> Knots { get; }
    public bool Released { get; private set; }
    public bool Finished { get; private set; }

    public ZipWebStrand(FixedVector3 anchor, FixedVector3 hook, Func<int, int>? random = null)
    {
        this.anchor = anchor;
        this.hook = hook;
        random ??= Random.Shared.Next;
        int segments = Math.Clamp(FixedAim.Distance(hook, anchor) / 80, 1, 40);
        outer = new FixedVector3[segments + 1];
        inner = new FixedVector3[segments * 2 + 1];
        knots = new FixedVector3[segments];
        knotFractions = new byte[segments];
        sideOffsets = new sbyte[segments];
        for (int index = 0; index < segments; index++)
        {
            // The constructor also draws sway speed/phase before the knot placement values.
            random(100);
            random(4096);
            knotFractions[index] = (byte)(random(192) + 64);
            sideOffsets[index] = (sbyte)(random(19) - 9);
        }
        Outer = Array.AsReadOnly(outer);
        Inner = Array.AsReadOnly(inner);
        Knots = Array.AsReadOnly(knots);
    }

    public void Release() => Released = true;

    public void Step(FixedVector3 currentHook, FixedVector3 camera, int ticks)
    {
        if (Finished) return;
        FixedVector3 start = anchor, end = currentHook;
        if (Released)
        {
            releaseTicks = unchecked(releaseTicks + (ushort)ticks);
            if (releaseTicks > 16) { Finished = true; return; }
            FixedVector3 delta = ((anchor - hook) >> 12) * GameAngle.Heading(releaseTicks << 6).Cosine;
            start = anchor - delta;
            end = anchor;
        }
        else hook = currentHook;
        FixedVector3 segment = (end - start) / knots.Length;
        outer[0] = start;
        for (int index = 1; index < outer.Length; index++) outer[index] = outer[index - 1] + segment;
        FixedVector3 side = FacingCamera(start, end, camera);
        inner[0] = start;
        for (int index = 0; index < knots.Length; index++)
        {
            FixedVector3 previous = outer[index];
            FixedVector3 point = previous + (((outer[index + 1] - previous) * knotFractions[index]) >> 8);
            knots[index] = point;
            FixedVector3 middle = index == 0 ? (start + point) >> 1 : previous;
            inner[index * 2 + 1] = middle + side * sideOffsets[index];
            inner[index * 2 + 2] = point;
        }
    }

    private static FixedVector3 FacingCamera(FixedVector3 start, FixedVector3 end, FixedVector3 camera)
    {
        FixedVector3 line = (end - start) >> 12;
        if (line.X > 500 || line.Y > 500 || line.Z > 500) line >>= 4;
        FixedVector3 view = (camera >> 12) - (start >> 12);
        var cross = new FixedVector3(unchecked(line.Y * view.Z - line.Z * view.Y),
            unchecked(line.Z * view.X - line.X * view.Z), unchecked(line.X * view.Y - line.Y * view.X));
        FixedVector3 scaled = cross >> 8;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked(scaled.X * scaled.X + scaled.Y * scaled.Y + scaled.Z * scaled.Z));
        return length < 5 ? default : (cross / length) << 4;
    }
}
