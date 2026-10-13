using System.Buffers.Binary;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Assets.Models;

public sealed record PreparedEnvironmentModel(PsxModel Model, uint RendererFlags, IReadOnlyList<ushort> RendererFaceFlags);

/// <summary>Source M3dInit_ParsePSX model/face edits and DCModel_CreateFromSModel draw flags, retaining immutable archive records.</summary>
public static class PsxEnvironmentPreparation
{
    public static IReadOnlyList<PreparedEnvironmentModel> Prepare(PsxScene scene, ReadOnlySpan<byte> bytes,
        IReadOnlyList<DecodedTexture> textures)
    {
        if (bytes.Length < 12 || Word(bytes, 0) != scene.Version || Word(bytes, 8) != scene.Instances.Count)
            throw new InvalidDataException("ENV preparation requires its actual source scene bytes.");
        var pulseColors = new HashSet<byte>();
        int pulseCount = 0;
        var wibbleModels = new HashSet<ushort>();
        int cursor = checked((int)Word(bytes, 4));
        while (cursor != 0 && Word(bytes, cursor) != uint.MaxValue)
        {
            uint kind = Word(bytes, cursor);
            int size = checked((int)Word(bytes, cursor + 4));
            ReadOnlySpan<byte> packet = Slice(bytes, cursor + 8, size);
            if (kind == 7)
            {
                int offset = 0;
                while (offset < packet.Length)
                {
                    ReadOnlySpan<byte> header = Slice(packet, offset, 2);
                    if (header[1] == 0 || pulseCount++ >= 127) throw new InvalidDataException("Invalid source color-pulse list.");
                    pulseColors.Add(header[0]);
                    offset = checked(offset + 4 + header[1] * 4);
                    if (offset > packet.Length) throw new InvalidDataException("Truncated source color-pulse list.");
                }
            }
            if (kind == 6)
            {
                int offset = 0;
                while (true)
                {
                    uint sourceOffset = Word(packet, offset);
                    if (sourceOffset == 0) break;
                    Slice(packet, offset, 16);
                    if (sourceOffset < 12 || (sourceOffset - 12) % 36 != 0) throw new InvalidDataException("Invalid source texture-wibble item offset.");
                    int item = checked((int)((sourceOffset - 12) / 36));
                    if ((uint)item >= scene.Instances.Count) throw new InvalidDataException("Texture-wibble item is outside its actual scene.");
                    wibbleModels.Add(scene.Instances[item].ModelIndex);
                    offset = checked(offset + 16 * (checked((int)Word(packet, offset + 12)) + 1));
                }
            }
            cursor = checked(cursor + 8 + size);
        }
        var result = new PreparedEnvironmentModel[scene.Models.Count];
        for (int index = 0; index < result.Length; index++)
        {
            PsxModel source = scene.Models[index];
            ushort flags = source.Flags;
            if (source.Vertices.Any(vertex => (vertex.Flags & 0x10) != 0)) flags |= 0x100;
            PsxFace[] faces = source.Faces.Select(face => face with
            {
                Flags = PsxRenderPolicy.PrepareFaceFlags(face.Flags),
                CollisionWord = face.CollisionWord is { } packed ? (packed & 0xFFFF0000) | unchecked((ushort)((short)packed * 8)) : null,
                Color = (face.Flags & 0x800) == 0 ? face.Color & 0xFFFFFF : face.Color
            }).ToArray();
            uint aggregate = 0; ushort lodMask = ushort.MaxValue;
            foreach (PsxFace face in faces)
            {
                aggregate |= face.Flags;
                // M3dInit reads the word at+0C on every actual source ENV face.
                if (face.CollisionWord is not { } packed) throw new InvalidDataException("Source ENV face has no model preparation/collision word.");
                lodMask &= unchecked((ushort)(packed >> 16));
            }
            if ((lodMask & 1) != 0) flags |= 0x10;
            if ((aggregate & 0xC0) == 0) flags |= 0x20;
            if ((aggregate & 0x1000) != 0) flags |= 0x40;
            if ((aggregate & 4) != 0) flags |= 4;
            uint renderer = scene.Version == 0x20006 ? 0u : 0x400u;
            if (source.Vertices.Any(vertex => (vertex.Flags & 3) != 0)) renderer |= 1;
            if (source.Vertices.Any(vertex => (vertex.Flags & 0x10) != 0)) renderer |= 4;
            if (faces.Where(face => (face.Flags & 1) != 0).Select(face => face.TextureChecksum).Distinct().Count() > 1) renderer |= 2;
            if (faces.Any(face => (face.Flags & 0x800) != 0 && Enumerable.Range(0, 3)
                .Any(component => pulseColors.Contains(unchecked((byte)(face.Color >> (component * 8))))))) renderer |= 0x80;
            foreach (uint checksum in faces.Select(face => face.TextureChecksum).OfType<uint>())
            {
                DecodedTexture texture = textures.FirstOrDefault(texture => texture.Checksum == checksum) ??
                    throw new InvalidDataException($"Source ENV texture{checksum:X8} has no actual source texture owner.");
                // PVR keeps archive flags. Palette texture constructors initialize900/901 and only add800, so their bit10 remains clear.
                if (texture.SourceFormat is not (16 or 256 or 0x10000)) throw new InvalidDataException("Source ENV texture format provenance is missing.");
                if (texture.SourceFormat == 0x10000 && (texture.Flags & 0x10) != 0) renderer |= 0x800;
            }
            if (wibbleModels.Contains(checked((ushort)index))) renderer |= 0x200;
            PsxVertex[] vertices = source.Vertices.Select(vertex => (vertex.Flags & 2) != 0 ? vertex with
            { Position = new(unchecked((short)((short)vertex.Position.Y * 8)), 0, vertex.Position.Z) } : vertex).ToArray();
            PsxModel prepared = source with { Flags = flags, Faces = Array.AsReadOnly(faces), Vertices = Array.AsReadOnly(vertices) };
            result[index] = new(prepared, renderer, Array.AsReadOnly(faces.Select(face => unchecked((ushort)face.Flags)).ToArray()));
        }
        return Array.AsReadOnly(result);
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Slice(bytes, offset, 4));
    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException("Source ENV preparation packet exceeds its original bank.");
        return bytes.Slice(offset, length);
    }
}
