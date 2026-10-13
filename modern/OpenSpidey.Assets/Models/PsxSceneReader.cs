using System.Buffers.Binary;
using System.Numerics;

namespace OpenSpidey.Assets.Models;

/// <summary>Decodes file-relative geometry. Does not mutate the source bytes into native pointers.</summary>
public static class PsxSceneReader
{
    public static PsxScene Read(ReadOnlySpan<byte> bytes)
    {
        Require(bytes, 0, 12);
        uint version = Word(bytes, 0);
        if (version is not (0x20003 or 0x20004 or 0x20006))
            throw new InvalidDataException($"Unsupported PSX version: {version:X8}");
        int instanceCount = checked((int)Word(bytes, 8));
        Require(bytes, 12, checked(instanceCount * 36 + 4));
        var instances = new PsxInstance[instanceCount];
        for (int index = 0; index < instanceCount; index++)
        {
            int offset = 12 + index * 36;
            instances[index] = new(Half(bytes, offset),
                new(SignedWord(bytes, offset + 4), SignedWord(bytes, offset + 8), SignedWord(bytes, offset + 12)),
                new(SignedHalf(bytes, offset + 16), SignedHalf(bytes, offset + 18), SignedHalf(bytes, offset + 20)),
                Half(bytes, offset + 22), Word(bytes, offset + 32));
        }

        int tableOffset = 12 + instanceCount * 36;
        int modelCount = checked((int)Word(bytes, tableOffset));
        Require(bytes, tableOffset + 4, checked(modelCount * 4));
        var checksums = ReadAssetChecksums(bytes, modelCount);
        var models = new PsxModel[modelCount];
        for (int index = 0; index < modelCount; index++)
            models[index] = ReadModel(bytes, checked((int)Word(bytes, tableOffset + 4 + index * 4)), checksums.Textures)
                with { Checksum = checksums.Models.Length == 0 ? null : checksums.Models[index] };
        foreach (PsxInstance instance in instances)
            if (instance.ModelIndex >= models.Length)
                throw new InvalidDataException("Scene instance references a missing model.");
        return new(version, Array.AsReadOnly(instances), Array.AsReadOnly(models), ReadColorTable(bytes));
    }

    private static IReadOnlyList<uint> ReadColorTable(ReadOnlySpan<byte> bytes)
    {
        int offset = checked((int)Word(bytes, 4));
        if (offset == 0) return [];
        while (true)
        {
            Require(bytes, offset, 4);
            uint kind = Word(bytes, offset);
            if (kind == uint.MaxValue) return [];
            Require(bytes, offset, 8);
            int size = checked((int)Word(bytes, offset + 4));
            Require(bytes, offset + 8, size);
            if (kind == 0x73424752)
            {
                if (size % 4 != 0) throw new InvalidDataException("Invalid RGBs color table size.");
                var colors = new uint[size / 4];
                for (int index = 0; index < colors.Length; index++) colors[index] = Word(bytes, offset + 8 + index * 4);
                return Array.AsReadOnly(colors);
            }
            offset = checked(offset + 8 + size);
        }
    }

    private static (uint[] Models, uint[] Textures) ReadAssetChecksums(ReadOnlySpan<byte> bytes, int modelCount)
    {
        int packetOffset = checked((int)Word(bytes, 4));
        if (packetOffset == 0) return ([], []); // Minimal geometry-only fixtures have no packet list.
        while (true)
        {
            Require(bytes, packetOffset, 4);
            if (Word(bytes, packetOffset) == uint.MaxValue) break;
            Require(bytes, packetOffset, 8);
            int size = checked((int)Word(bytes, packetOffset + 4));
            Require(bytes, packetOffset + 8, size);
            packetOffset = checked(packetOffset + 8 + size);
        }
        int modelOffset = checked(packetOffset + 4);
        Require(bytes, modelOffset, checked(modelCount * 4));
        var models = new uint[modelCount];
        for (int index = 0; index < models.Length; index++) models[index] = Word(bytes, modelOffset + index * 4);
        int countOffset = checked(modelOffset + modelCount * 4);
        Require(bytes, countOffset, 4);
        int textureCount = checked((int)Word(bytes, countOffset));
        Require(bytes, countOffset + 4, checked(textureCount * 4));
        var checksums = new uint[textureCount];
        for (int index = 0; index < textureCount; index++)
            checksums[index] = Word(bytes, countOffset + 4 + index * 4);
        return (models, checksums);
    }

    private static PsxModel ReadModel(ReadOnlySpan<byte> bytes, int offset, uint[] textureChecksums)
    {
        Require(bytes, offset, 28);
        ushort flags = Half(bytes, offset);
        int vertexCount = Half(bytes, offset + 2);
        int normalCount = Half(bytes, offset + 4);
        int faceCount = Half(bytes, offset + 6);
        int vertexOffset = checked(offset + 28);
        Require(bytes, vertexOffset, checked((vertexCount + normalCount) * 8));
        var vertices = new PsxVertex[vertexCount];
        for (int index = 0; index < vertexCount; index++)
        {
            int position = vertexOffset + index * 8;
            vertices[index] = new(new(SignedHalf(bytes, position), SignedHalf(bytes, position + 2),
                SignedHalf(bytes, position + 4)), Half(bytes, position + 6));
        }

        var normals = new PsxNormal[normalCount];
        for (int index = 0; index < normalCount; index++)
        {
            int position = vertexOffset + (vertexCount + index) * 8;
            normals[index] = new(new(SignedHalf(bytes, position), SignedHalf(bytes, position + 2),
                SignedHalf(bytes, position + 4)), Half(bytes, position + 6));
        }
        var faces = new PsxFace[faceCount];
        int faceOffset = checked(vertexOffset + (vertexCount + normalCount) * 8);
        for (int index = 0; index < faceCount; index++)
        {
            Require(bytes, faceOffset, 12);
            uint faceFlags = Word(bytes, faceOffset);
            int faceSize = checked((int)(faceFlags >> 18) * 4);
            bool textured = (faceFlags & 3) == 3;
            if (faceSize < (textured ? 28 : 12))
                throw new InvalidDataException("Invalid PSX face packet size.");
            Require(bytes, faceOffset, faceSize);
            byte[] indices = bytes.Slice(faceOffset + 4, 4).ToArray();
            int corners = (faceFlags & 0x10) != 0 ? 3 : 4;
            for (int corner = 0; corner < corners; corner++)
                if (indices[corner] >= vertexCount)
                    throw new InvalidDataException("Face references a missing vertex.");
            uint? textureChecksum = null;
            if (textured)
            {
                uint textureIndex = Word(bytes, faceOffset + 16);
                if (textureIndex >= textureChecksums.Length)
                    throw new InvalidDataException("Face references a missing texture checksum.");
                textureChecksum = textureChecksums[textureIndex];
            }
            int uvSize = ReadUvSize(bytes);
            if (textured && faceSize < 20 + uvSize) throw new InvalidDataException("Truncated PSX UV coordinates.");
            faces[index] = new(faceFlags, indices, Word(bytes, faceOffset + 8), textureChecksum,
                textured ? bytes.Slice(faceOffset + 20, uvSize).ToArray() : [],
                faceSize >= 16 ? Word(bytes, faceOffset + 12) : null);
            faceOffset = checked(faceOffset + faceSize);
        }
        return new(flags, Half(bytes, offset + 26), Array.AsReadOnly(vertices), Array.AsReadOnly(faces))
        { Normals = Array.AsReadOnly(normals) };
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static int ReadUvSize(ReadOnlySpan<byte> bytes) => Word(bytes, 0) == 0x20006 ? 16 : 8;
    private static int SignedWord(ReadOnlySpan<byte> bytes, int offset) => unchecked((int)Word(bytes, offset));
    private static ushort Half(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static short SignedHalf(ReadOnlySpan<byte> bytes, int offset) => unchecked((short)Half(bytes, offset));

    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException("PSX record extends beyond the file.");
    }
}
