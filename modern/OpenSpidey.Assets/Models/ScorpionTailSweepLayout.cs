namespace OpenSpidey.Assets.Models;

/// <summary>Original489370's private SW palette and132 double-sided additive quads. The shipped PC has no confirmed sweep draw phase.</summary>
public static class ScorpionTailSweepLayout
{
    public const int VertexCount = 92;
    public const int FaceCount = 132;

    public static IReadOnlyList<uint> CreatePalette()
    {
        var palette = new uint[256];
        for (uint colour = 0; colour < palette.Length; colour++)
            palette[colour] = (colour >> 1) | ((colour | ((colour & ~1u) << 7)) << 8);
        return Array.AsReadOnly(palette);
    }

    public static IReadOnlyList<PsxFace> CreateFaces()
    {
        var faces = new PsxFace[FaceCount];
        for (int band = 0; band < 3; band++)
        {
            uint colour = band switch { 0 => 0x40406060u, 1 => 0x20204040u, _ => 0x00002020u };
            for (int segment = 0; segment < 22; segment++)
            {
                int first = band * 23 + segment;
                faces[band * 44 + segment] = new(0x8C0, [(byte)first, (byte)(first + 1), (byte)(first + 23), (byte)(first + 24)], colour, null, []);
                faces[band * 44 + 22 + segment] = new(0x8C0, [(byte)(first + 1), (byte)first, (byte)(first + 24), (byte)(first + 23)], colour, null, []);
            }
        }
        return Array.AsReadOnly(faces);
    }
}
