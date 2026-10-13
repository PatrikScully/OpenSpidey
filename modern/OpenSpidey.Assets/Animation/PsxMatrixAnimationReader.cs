using System.Buffers.Binary;

namespace OpenSpidey.Assets.Animation;

public static class PsxMatrixAnimationReader
{
    public static MatrixAnimationLibrary? Read(ReadOnlySpan<byte> source)
    {
        int parts = PsxAnimationPackets.ReadPartCount(source);
        ReadOnlySpan<byte> animation = PsxAnimationPackets.Find(source, 42);
        if (animation.IsEmpty) return null;
        if (parts is <= 0 or > ushort.MaxValue) throw new InvalidDataException("Invalid matrix animation part count.");
        int count = checked((int)Word(animation, 0));
        int tableEnd = checked(4 + count * 8);
        Require(animation, 0, tableEnd);
        if (count > ushort.MaxValue) throw new InvalidDataException("Matrix clip count exceeds original animation IDs.");
        var offsets = new int[count];
        var frameWords = new uint[count];
        for (int clip = 0; clip < count; clip++)
        {
            offsets[clip] = checked((int)Word(animation, 4 + clip * 8));
            frameWords[clip] = Word(animation, 8 + clip * 8);
            if (offsets[clip] < tableEnd || offsets[clip] >= animation.Length || (frameWords[clip] & 65535) == 0)
                throw new InvalidDataException($"Invalid matrix clip {clip} table entry.");
        }
        int[] boundaries = offsets.Append(animation.Length).Distinct().Order().ToArray();
        var clips = new MatrixAnimationClip[count];
        var sharedKeys = new Dictionary<(int Offset, int SampleCount), short[]>();
        for (int clip = 0; clip < count; clip++)
        {
            int frames = (int)(frameWords[clip] & 65535);
            int interval = (int)(frameWords[clip] >> 16) + 1;
            int keyCount = (frames + interval - 1) / interval;
            int sampleCount = checked(keyCount * parts * 12);
            if (sampleCount > 16_000_000) throw new InvalidDataException($"Matrix animation {clip} exceeds the decoded sample limit.");
            int next = boundaries[Array.BinarySearch(boundaries, offsets[clip]) + 1];
            ReadOnlySpan<byte> encoded = animation.Slice(offsets[clip], next - offsets[clip]);
            Require(encoded, 0, checked(sampleCount * 2));
            if (!sharedKeys.TryGetValue((offsets[clip], sampleCount), out short[]? samples))
            {
                samples = new short[sampleCount];
                for (int index = 0; index < samples.Length; index++)
                    samples[index] = BinaryPrimitives.ReadInt16LittleEndian(encoded[(index * 2)..]);
                sharedKeys.Add((offsets[clip], sampleCount), samples);
            }
            clips[clip] = new(clip, frames, parts, interval, samples);
        }
        // Matrix poses are already in model space. A star hierarchy gives the editor a connected rig without reapplying pivots.
        var parents = new ushort[parts];
        ushort[] order = Enumerable.Range(0, parts).Select(part => (ushort)part).ToArray();
        return new(Array.AsReadOnly(parents), Array.AsReadOnly(order), Array.AsReadOnly(clips));
    }

    private static uint Word(ReadOnlySpan<byte> source, int offset)
    {
        Require(source, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
    }

    private static void Require(ReadOnlySpan<byte> source, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > source.Length - count)
            throw new InvalidDataException("Matrix animation range exceeds source data.");
    }
}
