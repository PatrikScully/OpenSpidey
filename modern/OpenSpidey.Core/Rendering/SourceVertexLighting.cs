using System.Numerics;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Rendering;

public readonly record struct SourceLightColour(float Red, float Green, float Blue);
public readonly record struct SourceRgb24(byte Red, byte Green, byte Blue);

/// <summary>Actual global stitch-colour scratch: the cursor resets per actor, stored colours persist between actors and frames.</summary>
public sealed class SourceVertexLightingState
{
    private readonly SourceLightColour[] colours = new SourceLightColour[256];
    public int AttachmentCursor { get; private set; }
    public void BeginActor() => AttachmentCursor = 0;
    public SourceLightColour ReadAttachment(int index) => (uint)index < colours.Length ? colours[index] :
        throw new InvalidDataException("Source lighting references a stitch-colour index outside256 records.");
    public void StoreAttachment(SourceLightColour colour)
    {
        if (AttachmentCursor >= colours.Length) throw new InvalidDataException("Source lighting exceeds its256 stitch-colour records.");
        colours[AttachmentCursor++] = colour;
    }
}

/// <summary>PC RenderSuperItem light basis plus DCModel_RenderModel reverse-light accumulation and source RGB clamp.</summary>
public static class SourceVertexLighting
{
    public static bool IsEnabled(PsxModel model, ushort itemFlags) => (model.Flags & 0x20) == 0 && model.Faces.Count != 0 &&
        ((model.Flags & 4) != 0 || (itemFlags & 0x80) != 0) && unchecked((ushort)(model.Vertices.Count + model.Faces.Count)) <= model.Normals.Count;

    public static FixedJointTransform LightBasis(SourceModelLight light, FixedJointTransform actor, FixedJointTransform part) =>
        Multiply(light.Directions, Multiply(actor, part));

    public static SourceLightColour Calculate(SourceModelLight light, FixedJointTransform lightBasis, Vector3 normal)
    {
        float red = light.AmbientRed / 4096f, green = light.AmbientGreen / 4096f, blue = light.AmbientBlue / 4096f;
        // Original4776E2..477768 traverses source lights2,1,0, retaining individual float stores.
        Add(lightBasis.M20, lightBasis.M21, lightBasis.M22, light.Colours.M02, light.Colours.M12, light.Colours.M22);
        Add(lightBasis.M10, lightBasis.M11, lightBasis.M12, light.Colours.M01, light.Colours.M11, light.Colours.M21);
        Add(lightBasis.M00, lightBasis.M01, lightBasis.M02, light.Colours.M00, light.Colours.M10, light.Colours.M20);
        return new(red, green, blue);
        void Add(short x, short y, short z, short r, short g, short b)
        {
            float dot = (float)((double)(x / 4096f) * normal.X + (double)(z / 4096f) * normal.Z + (double)(y / 4096f) * normal.Y);
            if (!(dot >= 0)) return;
            float rProduct = dot * (r / 4096f), gProduct = dot * (g / 4096f), bProduct = dot * (b / 4096f);
            red += rProduct; green += gProduct; blue += bProduct;
        }
    }

    public static SourceRgb24 Pack(SourceLightColour colour) => new(Channel(colour.Red), Channel(colour.Green), Channel(colour.Blue));
    private static byte Channel(float value) => (byte)Math.Clamp(GameWord.TruncateToInt32((double)value * 255), 0, 255);

    public static SourceRgb24[] CalculatePart(PsxModel model, ReadOnlySpan<Vector3> normals, SourceModelLight light,
        FixedJointTransform lightBasis, SourceVertexLightingState state)
    {
        if (normals.Length < model.Vertices.Count) throw new InvalidDataException("Lit source parts require one normalized normal per original vertex.");
        var output = new SourceRgb24[model.Vertices.Count];
        for (int index = 0; index < output.Length; index++)
        {
            PsxVertex vertex = model.Vertices[index];
            SourceLightColour colour;
            if ((vertex.Flags & 2) != 0)
            {
                int attachment = Math.Min(unchecked((short)((int)vertex.Position.Y * 8)) >> 3, 255);
                colour = state.ReadAttachment(attachment);
            }
            else
            {
                colour = Calculate(light, lightBasis, normals[index]);
                if ((vertex.Flags & 1) != 0) state.StoreAttachment(colour);
            }
            output[index] = Pack(colour);
        }
        return output;
    }

    public static Vector3[] ReadPlainNormals(PsxModel model)
    {
        var output = new Vector3[model.Normals.Count];
        for (int index = 0; index < output.Length; index++)
        {
            PsxNormal source = model.Normals[index];
            if ((source.Flags & 3) != 0) throw new NotSupportedException("This source normal requires the original asset-conversion stitch-normal owner.");
            // Original432266 uses stored float0.000244 at53B6F4, not the exact reciprocal of4096.
            Vector3 normal = source.Direction * 0.000244f;
            float square = (float)((double)normal.X * normal.X + (double)normal.Y * normal.Y + (double)normal.Z * normal.Z);
            if (square < 9.9999997e-10f) output[index] = Vector3.UnitY;
            else
            {
                float inverse = 1f / MathF.Sqrt(square);
                output[index] = normal * inverse;
            }
        }
        return output;
    }

    public static FixedJointTransform Multiply(FixedJointTransform a, FixedJointTransform b) => new(
        Dot(a.M00, a.M01, a.M02, b.M00, b.M10, b.M20), Dot(a.M00, a.M01, a.M02, b.M01, b.M11, b.M21), Dot(a.M00, a.M01, a.M02, b.M02, b.M12, b.M22),
        Dot(a.M10, a.M11, a.M12, b.M00, b.M10, b.M20), Dot(a.M10, a.M11, a.M12, b.M01, b.M11, b.M21), Dot(a.M10, a.M11, a.M12, b.M02, b.M12, b.M22),
        Dot(a.M20, a.M21, a.M22, b.M00, b.M10, b.M20), Dot(a.M20, a.M21, a.M22, b.M01, b.M11, b.M21), Dot(a.M20, a.M21, a.M22, b.M02, b.M12, b.M22), 0, 0, 0);
    private static short Dot(short a, short b, short c, short x, short y, short z) => unchecked((short)(unchecked(a * x + b * y + c * z) >> 12));
}
