using System.Buffers.Binary;
using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Assets.Models;

/// <summary>Original SLight two padded short matrices and signed ambient channels.</summary>
public sealed record SourceModelLight(FixedJointTransform Directions, FixedJointTransform Colours,
    int AmbientRed, int AmbientGreen, int AmbientBlue)
{
    public static SourceModelLight Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 52) throw new InvalidDataException("Source SLight requires its exact52-byte record.");
        return new(Matrix(bytes), Matrix(bytes[20..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[40..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[44..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[48..]));
    }
    private static FixedJointTransform Matrix(ReadOnlySpan<byte> bytes) => new(Half(bytes, 0), Half(bytes, 2), Half(bytes, 4),
        Half(bytes, 6), Half(bytes, 8), Half(bytes, 10), Half(bytes, 12), Half(bytes, 14), Half(bytes, 16), 0, 0, 0);
    private static short Half(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
}
