using System.Buffers.Binary;

namespace OpenSpidey.Assets.Audio;

/// <summary>PCM data and sample clock used by the PC sound loader; 8-bit samples remain unsigned at this boundary.</summary>
public sealed record PcmWaveSample(int Channels, int SampleRate, int BitsPerSample, byte[] Data, int SourceDataLength)
{
    public int BytesPerSample => BitsPerSample / 8;
    public int FrameCount => Data.Length / (Channels * BytesPerSample);

    public static PcmWaveSample Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Sound file is not a RIFF WAVE container.");
        uint riffSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (riffSize < 4 || riffSize > bytes.Length - 8) throw new InvalidDataException("WAVE container is truncated.");
        int end = (int)riffSize + 8;
        int channels = 0, sampleRate = 0, bits = 0, dataOffset = -1, dataLength = 0;
        for (int position = 12; position < end;)
        {
            if (end - position < 8) throw new InvalidDataException("WAVE chunk header is truncated.");
            ReadOnlySpan<byte> id = bytes.Slice(position, 4);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(position + 4)..]);
            int payload = position + 8;
            if (chunkSize > end - payload) throw new InvalidDataException("WAVE chunk payload is truncated.");
            int length = (int)chunkSize;
            if (id.SequenceEqual("fmt "u8) && channels == 0)
            {
                if (length < 16) throw new InvalidDataException("WAVE format chunk is too short.");
                ReadOnlySpan<byte> format = bytes.Slice(payload, length);
                if (BinaryPrimitives.ReadUInt16LittleEndian(format) != 1) throw new NotSupportedException("The PC sound loader requires PCM WAVE data.");
                channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(format[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
                if (channels is not (1 or 2) || bits is not (8 or 16) || sampleRate <= 0)
                    throw new NotSupportedException("WAVE sample format requires mono/stereo 8-bit or 16-bit PCM and a positive sample rate.");
                if (BinaryPrimitives.ReadUInt16LittleEndian(format[12..]) != channels * (bits / 8))
                    throw new InvalidDataException("WAVE frame alignment does not match its sample format.");
            }
            else if (id.SequenceEqual("data"u8) && dataOffset < 0)
            {
                dataOffset = payload;
                dataLength = length;
            }
            position = checked(payload + length + (length & 1));
        }
        if (channels == 0 || dataOffset < 0 || dataLength == 0) throw new InvalidDataException("WAVE PCM format or samples are missing.");
        // loadWAV rounds the playback buffer to four bytes and zero-fills the tail, including unsigned 8-bit data.
        byte[] data = new byte[checked((dataLength + 3) & ~3)];
        bytes.Slice(dataOffset, dataLength).CopyTo(data);
        return new(channels, sampleRate, bits, data, dataLength);
    }
}
