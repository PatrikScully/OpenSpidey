using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace OpenSpidey.Core.Player;

public sealed record PlayerComboBranch(ushort Move, ushort ExitTick, ushort HeadStart, ushort TransitionAnimation, ushort Flags);

public sealed record PlayerComboMove(ushort Id, ushort Animation, ushort Damage, ushort CollisionStart, ushort CollisionEnd,
    byte HitFlags, ushort InputStart, ushort InputEnd, ushort LateInputStart, ushort LateInputEnd,
    short SlideDistance, ushort SlideFrames, bool UsesLateWindow, IReadOnlyList<PlayerComboBranch> Branches,
    IReadOnlyList<byte> Input, IReadOnlyList<byte> CollisionHooks, IReadOnlyList<byte> Frames);

/// <summary>Immutable decoding of the original PC fight blobs at569194 and56929C, before pointer relocation.</summary>
public sealed class PlayerFightData
{
    private readonly IReadOnlyDictionary<ushort, PlayerComboMove> moves;
    private readonly IReadOnlyDictionary<ushort, IReadOnlyList<byte>> distances;
    public IReadOnlyDictionary<ushort, PlayerComboMove> Moves => moves;
    public IReadOnlyDictionary<ushort, IReadOnlyList<byte>> Distances => distances;
    private PlayerFightData(Dictionary<ushort, PlayerComboMove> moves, Dictionary<ushort, IReadOnlyList<byte>> distances)
    { this.moves = new ReadOnlyDictionary<ushort, PlayerComboMove>(moves); this.distances = new ReadOnlyDictionary<ushort, IReadOnlyList<byte>>(distances); }

    public static PlayerFightData Decode(ReadOnlySpan<byte> distanceBlob, ReadOnlySpan<byte> moveBlob)
    {
        var distances = new Dictionary<ushort, IReadOnlyList<byte>>();
        int cursor = 0;
        while (Read16(distanceBlob, cursor) != 6666)
        {
            ushort id = Read16(distanceBlob, cursor); cursor += 2;
            if (id >= 300) throw new InvalidDataException("Original distance id exceeds its300-slot lookup.");
            distances.Add(id, Stream(distanceBlob, ref cursor));
            cursor = checked((cursor + 3) & ~3);
        }
        var moves = new Dictionary<ushort, PlayerComboMove>();
        cursor = 0;
        while (Read32(moveBlob, cursor) != 6666)
        {
            int record = cursor;
            ushort id = Read16(moveBlob, record), parts = Read16(moveBlob, record + 0x20);
            if (id >= 32) throw new InvalidDataException("Original combo id exceeds its32-slot lookup.");
            var branches = new PlayerComboBranch[parts];
            for (int i = 0; i < parts; i++)
            {
                int offset = checked(record + 0x24 + i * 12);
                uint destination = Read32(moveBlob, offset);
                if (destination >= 32) throw new InvalidDataException("Combo data must be supplied before native pointer relocation.");
                branches[i] = new((ushort)destination, Read16(moveBlob, offset + 4), Read16(moveBlob, offset + 6),
                    Read16(moveBlob, offset + 8), Read16(moveBlob, offset + 10));
            }
            cursor = checked(record + 0x24 + parts * 12);
            var input = Stream(moveBlob, ref cursor);
            var hooks = Stream(moveBlob, ref cursor);
            var frames = Stream(moveBlob, ref cursor);
            moves.Add(id, new(id, Read16(moveBlob, record + 2), Read16(moveBlob, record + 8),
                Read16(moveBlob, record + 0xC), Read16(moveBlob, record + 0xE), moveBlob[record + 0x10],
                Read16(moveBlob, record + 0x12), Read16(moveBlob, record + 0x14), Read16(moveBlob, record + 0x16),
                Read16(moveBlob, record + 0x18), unchecked((short)Read16(moveBlob, record + 0x1A)),
                Read16(moveBlob, record + 0x1C), Read16(moveBlob, record + 0x22) != 0, Array.AsReadOnly(branches), input, hooks, frames));
            cursor = checked((cursor + 3) & ~3);
        }
        foreach (PlayerComboMove move in moves.Values)
            foreach (PlayerComboBranch branch in move.Branches)
                if (!moves.ContainsKey(branch.Move)) throw new InvalidDataException("Original combo branch references an absent move.");
        return new(moves, distances);
    }

    private static IReadOnlyList<byte> Stream(ReadOnlySpan<byte> blob, ref int cursor)
    {
        int start = cursor;
        while (cursor < blob.Length && blob[cursor++] != 0xFF) { }
        if (cursor == start || cursor > blob.Length || blob[cursor - 1] != 0xFF)
            throw new InvalidDataException("Original fight stream is missing itsFF sentinel.");
        return Array.AsReadOnly(blob[start..cursor].ToArray());
    }
    private static ushort Read16(ReadOnlySpan<byte> blob, int at) => BinaryPrimitives.ReadUInt16LittleEndian(blob.Slice(at, 2));
    private static uint Read32(ReadOnlySpan<byte> blob, int at) => BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(at, 4));
}
