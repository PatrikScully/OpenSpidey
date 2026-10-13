using OpenSpidey.Assets.Audio;

internal static class PcmWaveTests
{
    public static void Run(Action<bool, string> assert)
    {
        byte[] bytes = Wave(1, 8, [0, 128, 255]);
        PcmWaveSample sample = PcmWaveSample.Read(bytes);
        assert(sample.Channels == 1 && sample.SampleRate == 22050 && sample.BitsPerSample == 8 &&
            sample.SourceDataLength == 3 && sample.FrameCount == 4 && sample.Data.SequenceEqual(new byte[] { 0, 128, 255, 0 }),
            "Original unsigned PCM8 samples and four-byte playback-buffer padding are preserved.");
        sample = PcmWaveSample.Read(Wave(2, 16, [1, 2, 3, 4, 5, 6, 7, 8]));
        assert(sample.Channels == 2 && sample.BitsPerSample == 16 && sample.FrameCount == 2 && sample.Data.Length == 8,
            "Stereo PCM16 retains complete sample pairs and its source sample clock.");
        Reject<InvalidDataException>(() => PcmWaveSample.Read(bytes[..^1]));
        byte[] truncatedChunk = (byte[])bytes.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(truncatedChunk.AsSpan(40), uint.MaxValue);
        Reject<InvalidDataException>(() => PcmWaveSample.Read(truncatedChunk));
        byte[] compressed = (byte[])bytes.Clone(); compressed[20] = 17;
        Reject<NotSupportedException>(() => PcmWaveSample.Read(compressed));
        byte[] alignment = (byte[])bytes.Clone(); alignment[32] = 2;
        Reject<InvalidDataException>(() => PcmWaveSample.Read(alignment));
        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, "Malformed/unsupported source WAV fails at its import boundary."); return; }
            throw new Exception($"Expected {typeof(T).Name} for source WAV.");
        }
    }
    private static byte[] Wave(short channels, short bits, byte[] samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(0); writer.Write("WAVE"u8);
        writer.Write("fmt "u8); writer.Write(16); writer.Write((short)1); writer.Write(channels);
        writer.Write(22050); writer.Write(22050 * channels * bits / 8); writer.Write((short)(channels * bits / 8)); writer.Write(bits);
        writer.Write("data"u8); writer.Write(samples.Length); writer.Write(samples);
        if ((samples.Length & 1) != 0) writer.Write((byte)0);
        // Unknown metadata is bounded and skipped, independent of its application-specific contents.
        writer.Write("LIST"u8); writer.Write(4); writer.Write("TEST"u8);
        stream.Position = 4; writer.Write((int)stream.Length - 8);
        return stream.ToArray();
    }
}
