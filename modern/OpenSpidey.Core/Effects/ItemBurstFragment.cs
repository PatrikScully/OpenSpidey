using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Actual CItemFrag textured quad: retained source face, grounded velocity and eight independent render jitters.</summary>
public sealed class ItemBurstFragment
{
    private readonly FixedVector3[] points;
    private readonly FixedVector3[] corners = new FixedVector3[4];
    public PsxFace Face { get; }
    public IReadOnlyList<FixedVector3> Points => points;
    public IReadOnlyList<FixedVector3> Corners => corners;
    public FixedVector3 Velocity { get; private set; }
    public int GroundY { get; }
    public short Age { get; private set; }
    public ushort Lifetime { get; }
    public uint Tint { get; private set; } = 0x808080;
    public bool Alive { get; private set; } = true;
    public ushort Type => 23;
    public uint QuadCode => 0x1C02C1;

    public ItemBurstFragment(PsxFace face, IReadOnlyList<FixedVector3> vertices, FixedVector3 velocity,
        int groundY, Func<int, int> random)
    {
        Face = face ?? throw new ArgumentNullException(nameof(face));
        if (face.IsTriangle || face.VertexIndices.Length != 4) throw new ArgumentException("CItem::Burst creates original four-corner face fragments only.", nameof(face));
        points = face.VertexIndices.Select(index => vertices[index]).ToArray();
        Velocity = velocity; GroundY = groundY; Lifetime = unchecked((ushort)(random(30) + 1));
    }
    public void AdvanceBits(Func<int, int> random)
    {
        if (!Alive) return;
        for (int index = 0; index < points.Length; index++) points[index] += Velocity;
        Velocity += new FixedVector3(0, 8192, 0);
        if (points[0].Y > GroundY)
        {
            int delta = unchecked(GroundY - points[0].Y);
            for (int index = 0; index < points.Length; index++) points[index] = points[index] with { Y = unchecked(points[index].Y + delta) };
            Velocity = new(Velocity.X >> 1, unchecked(-Velocity.Y) >> 2, Velocity.Z >> 1);
        }
        for (int index = 0; index < corners.Length; index++)
            corners[index] = points[index] + new FixedVector3(unchecked((random(41) - 20) << 12), 0, unchecked((random(41) - 20) << 12));
        Age = unchecked((short)(Age + 1));
        if (Age >= Lifetime) { Alive = false; return; }
        int colour = unchecked((Lifetime - Age) << 7) / Lifetime;
        Tint = unchecked((uint)(colour | colour << 8 | colour << 16));
    }
}

public interface IItemBurstHost
{
    int GetGroundY(FixedVector3 position);
    void Emit(ItemBurstFragment fragment);
}

/// <summary>Actual CItem::Burst45FDC0 geometry/RNG leaf; distinct from Shatter_Item's chunks and glass.</summary>
public static class ItemBurst
{
    public static void Create(PsxModel model, FixedVector3 position, ActorAngles angles, int speed, int randomSpeed,
        IItemBurstHost host, Func<int, int> random)
    {
        int ground = host.GetGroundY(position);
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        FixedVector3[] vertices = model.Vertices.Select(vertex =>
            (FixedRotation.Transform(rotation, new FixedVector3((short)vertex.Position.X, (short)vertex.Position.Y,
                (short)vertex.Position.Z)) + (position >> 12)) << 12).ToArray();
        foreach (PsxFace face in model.Faces)
        {
            if (face.IsTriangle) continue;
            FixedVector3 direction = vertices[face.VertexIndices[0]] - position;
            FixedVector3 whole = direction >> 12;
            int length = FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
            if (length == 0) continue;
            FixedVector3 velocity = direction * unchecked(speed + random(randomSpeed)) / length;
            host.Emit(new(face, vertices, velocity, ground, random));
        }
    }
}
