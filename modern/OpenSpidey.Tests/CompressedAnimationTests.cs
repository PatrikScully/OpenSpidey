using OpenSpidey.Assets.Animation;

internal static class CompressedAnimationTests
{
    public static void Run(Action<bool, string> assert)
    {
        byte[] source = Build([2, 0, 2]);
        CompressedAnimationLibrary library = PsxCompressedAnimationReader.Read(source)!;
        assert(library.CalculationOrder.SequenceEqual(new ushort[] { 2, 0, 1 }),
            "Skeleton parents precede children even when the root is not part zero.");
        assert(library.Clips.Count == 2, "Animation table preserves original clip IDs.");
        DecodedAnimationClip first = library.Clips[0].Decode();
        assert(first.GetJoint(0, 0).RotationX == 10 && first.GetJoint(1, 0).RotationX == 20,
            "Frame-major poses retain the channel sample order.");
        assert(first.GetJoint(1, 2) == new RawJointPose(), "Each part retains its own six channels.");
        assert(first.EncodedSize - first.BytesConsumed == 2, "Alignment bytes are excluded from decoded channel data.");
        assert(library.Clips[1].Decode().GetJoint(1, 1).TranslationY == 14,
            "Second clip uses its own stream boundary and translation channels.");
        Array.Fill(source, (byte)0);
        assert(library.Clips[1].Decode().GetJoint(1, 1).TranslationY == 14,
            "Animation data survives releasing or changing the source buffer.");
        Reject(() => PsxCompressedAnimationReader.Read(Build([0, 1, 2])), "Multiple roots");
        Reject(() => PsxCompressedAnimationReader.Read(Build([1, 0, 2])), "Disconnected parent cycle");
        Reject(() => PsxCompressedAnimationReader.Read(Build([1, 2, 0])), "Missing root");
        Reject(() => PsxCompressedAnimationReader.Read(Build([2, 3, 2])), "Missing parent");
        byte[] truncated = Build([2, 0, 2]);
        Reject(() => PsxCompressedAnimationReader.Read(truncated.AsSpan(0, truncated.Length - 1)), "Truncated packet terminator");
        byte[] badOffset = Build([2, 0, 2]);
        // Animation payload starts at byte 36; first stream offset is at payload + 4.
        BitConverter.GetBytes(4u).CopyTo(badOffset, 40);
        Reject(() => PsxCompressedAnimationReader.Read(badOffset), "Clip offset overlaps table");

        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected, message + " is rejected with a format diagnostic.");
        }
    }

    private static byte[] Build(ushort[] parents)
    {
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2u);
            writer.Write(20u); writer.Write(2u);
            writer.Write(44u); writer.Write(2u);
            writer.Write(new byte[] { 0, 10, 0, 20, 0 });
            writer.Write(Enumerable.Repeat((byte)15, 17).ToArray());
            writer.Write((ushort)0); // Two alignment bytes between clips.
            for (short channel = 0; channel < 18; channel++)
            {
                writer.Write((byte)14);
                writer.Write((short)(channel + 4));
            }
        }
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x20006u); writer.Write(12u); writer.Write((uint)parents.Length);
            writer.Write(0x52454948u); writer.Write(8u);
            foreach (ushort parent in parents) writer.Write(parent);
            writer.Write((ushort)0);
            writer.Write(44u); writer.Write((uint)payload.Length); writer.Write(payload.ToArray());
            writer.Write(uint.MaxValue);
        }
        return output.ToArray();
    }
}
