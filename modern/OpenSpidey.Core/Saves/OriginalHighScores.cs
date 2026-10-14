using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Core.Saves;

/// <summary>One gChallenges 0x551118 entry (stride 0x10).</summary>
public sealed record OriginalChallenge(int Index, string Title, short LevelId, sbyte AreaId, sbyte ScoreUnits, bool HigherIsBetter);

/// <summary>Original SRecords (gGlobalRecords 0x550ED0): 3 initials bytes, then 23 challenges of 5 SScore rows.</summary>
public sealed class OriginalHighScores
{
    public const int Size = 0x242;
    public const int ChallengeCount = 23;
    public const int RowsPerChallenge = 5;
    public const int RowSize = 5;
    private readonly byte[] bytes;

    public OriginalHighScores(ReadOnlySpan<byte> records)
    {
        if (records.Length != Size) throw new ArgumentException("Original SRecords are exactly 0x242 bytes.", nameof(records));
        bytes = records.ToArray();
    }

    public ReadOnlySpan<byte> Bytes => bytes;
    public OriginalHighScores Clone() => new(bytes);
    public Span<byte> Challenge(int index) => (uint)index < ChallengeCount
        ? bytes.AsSpan(3 + index * RowsPerChallenge * RowSize, RowsPerChallenge * RowSize)
        : throw new ArgumentOutOfRangeException(nameof(index));

    public (string Initials, short Score)? Row(int challenge, int row)
    {
        if ((uint)row >= RowsPerChallenge) throw new ArgumentOutOfRangeException(nameof(row));
        ReadOnlySpan<byte> score = Challenge(challenge).Slice(row * RowSize, RowSize);
        if (score[0] == 0) return null;
        int length = score[..3].IndexOf((byte)0);
        return (Encoding.Latin1.GetString(score[..(length < 0 ? 3 : length)]), BinaryPrimitives.ReadInt16LittleEndian(score[3..]));
    }

    /// <summary>Load/save merge loop 0x49F858: file rows absorb the global rows, then the global table takes the file copy.</summary>
    public static void MergeInto(OriginalHighScores file, OriginalHighScores global, IReadOnlyList<OriginalChallenge> challenges)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(challenges);
        if (challenges.Count != ChallengeCount) throw new ArgumentException("The original merge covers all 23 challenges.", nameof(challenges));
        for (int index = 0; index < ChallengeCount; index++)
            Merge(file.Challenge(index), global.Challenge(index), challenges[index].HigherIsBetter ? 1 : 0);
        file.bytes.CopyTo(global.bytes, 0);
    }

    /// <summary>Merge 0x49EFF0: inserts each new nonempty row not already present, signed i16 order picked by the flag.</summary>
    public static void Merge(Span<byte> target, ReadOnlySpan<byte> other, int higherIsBetter)
    {
        if (target.Length != RowsPerChallenge * RowSize || other.Length != RowsPerChallenge * RowSize)
            throw new ArgumentException("Merge operates on one challenge's five SScore rows.");
        for (int source = 0; source < RowsPerChallenge; source++)
        {
            ReadOnlySpan<byte> row = other.Slice(source * RowSize, RowSize);
            bool fresh = true;
            for (int index = 0; index < RowsPerChallenge; index++)
            {
                ReadOnlySpan<byte> existing = target.Slice(index * RowSize, RowSize);
                if (existing[0] == 0 && row[0] == 0 || existing.SequenceEqual(row)) fresh = false;
            }
            if (row[0] == 0 || !fresh) continue;
            short score = BinaryPrimitives.ReadInt16LittleEndian(row[3..]);
            int position = 0;
            while (target[position * RowSize] != 0)
            {
                short current = BinaryPrimitives.ReadInt16LittleEndian(target[(position * RowSize + 3)..]);
                if (higherIsBetter != 0 ? score > current : score < current) break;
                if (++position >= RowsPerChallenge) break;
            }
            if (position >= RowsPerChallenge) continue;
            for (int index = RowsPerChallenge - 1; index > position; index--)
                target.Slice((index - 1) * RowSize, RowSize).CopyTo(target.Slice(index * RowSize, RowSize));
            row.CopyTo(target.Slice(position * RowSize, RowSize));
        }
    }
}
