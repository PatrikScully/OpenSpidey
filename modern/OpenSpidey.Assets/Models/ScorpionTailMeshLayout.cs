using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Assets.Models;

/// <summary>Original489050 primary-tail quad topology and byte UV tiling. Runtime S/SW spool-region allocation has a separate owner.</summary>
public static class ScorpionTailMeshLayout
{
    public const int FaceCount = 88;
    public const uint PaletteColour = 0x808080;
    public const string TextureName = "new_scorp_tail";

    public static IReadOnlyList<PsxFace> CreateFaces(DecodedTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.Width is <= 1 or > 256 || texture.Height is <= 1 or > 256)
            throw new InvalidDataException("The source tail texture requires a nonzero byte UV range.");
        int width = (texture.Width - 1) >> 2, height = (texture.Height - 1) / 4;
        var faces = new PsxFace[FaceCount];
        for (int segment = 0; segment < 22; segment++)
        for (int side = 0; side < 4; side++)
        {
            int first = segment * 4 + side, adjacent = segment * 4 + ((side + 1) & 3);
            int left = side * width, right = (side + 1) * width;
            int top = (segment & 3) * height, bottom = ((segment & 3) + 1) * height;
            faces[segment * 4 + side] = new(0x887, [(byte)first, (byte)(first + 4), (byte)adjacent, (byte)(adjacent + 4)],
                0, texture.Checksum, [(byte)left, (byte)top, (byte)left, (byte)bottom, (byte)right, (byte)top, (byte)right, (byte)bottom]);
        }
        return Array.AsReadOnly(faces);
    }
}
