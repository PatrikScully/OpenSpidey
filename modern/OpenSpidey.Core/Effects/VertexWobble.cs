using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Per-vertex snapshot for CVertexWobble (0x43B7C0), 22 bytes.</summary>
public sealed class VertexWobbleEntry
{
    public short Vx { get; init; }
    public short Vy { get; init; }
    public short Vz { get; init; }
    public short Dx { get; set; }
    public short Dy { get; set; }
    public short Dz { get; set; }
    public byte VertexIndex { get; init; }
    public short Distance { get; set; }
    public ushort Amplitude { get; init; }
    public short PhaseSpeed { get; init; }
    public short Phase { get; set; }
}

/// <summary>Mutable i16 model vertices that CVertexWobble writes at SModel+0x1C.</summary>
public sealed class VertexWobbleModel
{
    private readonly short[] positions;

    public VertexWobbleModel(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        positions = new short[checked(count * 3)];
    }

    public VertexWobbleModel(IReadOnlyList<(short X, short Y, short Z)> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count == 0) throw new ArgumentException("CVertexWobble requires at least one vertex.", nameof(source));
        positions = new short[checked(source.Count * 3)];
        for (int i = 0; i < source.Count; i++)
        {
            positions[i * 3] = source[i].X;
            positions[i * 3 + 1] = source[i].Y;
            positions[i * 3 + 2] = source[i].Z;
        }
    }

    public int Count => positions.Length / 3;

    public (short X, short Y, short Z) this[int index]
    {
        get
        {
            int o = checked(index * 3);
            return (positions[o], positions[o + 1], positions[o + 2]);
        }
        set
        {
            int o = checked(index * 3);
            positions[o] = value.X;
            positions[o + 1] = value.Y;
            positions[o + 2] = value.Z;
        }
    }
}

/// <summary>CVertexWobble (0x43B7C0 / 0x43BB30): radial sine wobble of selected SModel vertices.</summary>
public sealed class VertexWobbleState
{
    public const int SimbyModelIndex = 1;
    /// <summary>MakeVertexWibbler a5/a6/a7/a8 (0x50, 0x50, 0xC8, 0x96).</summary>
    public const int SimbyA5 = 0x50;
    public const int SimbyA6 = 0x50;
    public const int SimbyA7 = 0xC8;
    public const int SimbyA8 = 0x96;
    public static readonly byte[] SimbyIndicesA = [2, 3, 4, 9, 8, 7];
    public static readonly byte[] SimbyIndicesB = [0xB, 0xA, 0, 1, 6, 5];

    private readonly VertexWobbleModel model;
    public IReadOnlyList<VertexWobbleEntry> Entries { get; }
    public short CentreX { get; }
    public short CentreY { get; }
    public short CentreZ { get; }

    /// <summary>Original args after region/model/count/indices: a5+Rnd(a6) amplitude, a7+Rnd(a8) phase speed, Rnd(a7+a8) phase.</summary>
    public VertexWobbleState(VertexWobbleModel model, ReadOnlySpan<byte> indices, int a5, int a6, int a7, int a8,
        Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(random);
        if (indices.Length == 0) throw new ArgumentException("Zero NumVertices sent to CVertexWobble.", nameof(indices));
        this.model = model;
        var entries = new VertexWobbleEntry[indices.Length];
        short sumX = 0, sumY = 0, sumZ = 0;
        for (int i = 0; i < indices.Length; i++)
        {
            byte index = indices[i];
            if (index >= model.Count) throw new ArgumentOutOfRangeException(nameof(indices), "Vertex index out of range.");
            (short vx, short vy, short vz) = model[index];
            entries[i] = new()
            {
                Vx = vx, Vy = vy, Vz = vz, VertexIndex = index,
                Amplitude = unchecked((ushort)(a5 + random(a6))),
                PhaseSpeed = unchecked((short)(a7 + random(a8))),
                Phase = unchecked((short)random(unchecked(a7 + a8)))
            };
            sumX = unchecked((short)(sumX + vx));
            sumY = unchecked((short)(sumY + vy));
            sumZ = unchecked((short)(sumZ + vz));
        }
        CentreX = unchecked((short)(sumX / indices.Length));
        CentreY = unchecked((short)(sumY / indices.Length));
        CentreZ = unchecked((short)(sumZ / indices.Length));
        for (int i = 0; i < entries.Length; i++)
        {
            VertexWobbleEntry entry = entries[i];
            short dx = unchecked((short)(entry.Vx - CentreX));
            short dy = unchecked((short)(entry.Vy - CentreY));
            short dz = unchecked((short)(entry.Vz - CentreZ));
            entries[i].Dx = dx;
            entries[i].Dy = dy;
            entries[i].Dz = dz;
            entries[i].Distance = unchecked((short)FixedVectorMath.OriginalSquareRoot(
                unchecked(dx * dx + dy * dy + dz * dz)));
        }
        Entries = entries;
    }

    public void Move()
    {
        foreach (VertexWobbleEntry entry in Entries)
        {
            entry.Phase = unchecked((short)(entry.Phase + entry.PhaseSpeed));
            int sin = GameAngle.Heading(entry.Phase).Sine;
            int amplitude = entry.Amplitude;
            int distance = unchecked((ushort)entry.Distance);
            int newRadius = unchecked(amplitude + distance + SignedDivideBy4096(unchecked(sin * amplitude)));
            model[entry.VertexIndex] = (
                unchecked((short)(CentreX + entry.Dx * newRadius / distance)),
                unchecked((short)(CentreY + entry.Dy * newRadius / distance)),
                unchecked((short)(CentreZ + entry.Dz * newRadius / distance)));
        }
    }

    /// <summary>MSVC6 signed idiv-by-4096 shape used at 0x43BB91 (cdq / and 0xFFF / add / sar 12).</summary>
    internal static int SignedDivideBy4096(int value) => unchecked((value + ((value >> 31) & 0xFFF)) >> 12);
}

/// <summary>MakeVertexWibbler (0x4A2790): two CVertexWobble bits on symbi model index1.</summary>
public static class MakeVertexWibbler
{
    public static VertexWobbleState[] Create(VertexWobbleModel model, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(random);
        return
        [
            new(model, VertexWobbleState.SimbyIndicesA, VertexWobbleState.SimbyA5, VertexWobbleState.SimbyA6,
                VertexWobbleState.SimbyA7, VertexWobbleState.SimbyA8, random),
            new(model, VertexWobbleState.SimbyIndicesB, VertexWobbleState.SimbyA5, VertexWobbleState.SimbyA6,
                VertexWobbleState.SimbyA7, VertexWobbleState.SimbyA8, random)
        ];
    }
}
