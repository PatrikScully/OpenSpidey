using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

/// <summary>Bounded original sparse fist records and the exact200-record SortFistsData cycle sort into300 animation slots.</summary>
public static class OriginalPlayerFists
{
    public static IReadOnlyList<byte> Decode(ReadOnlySpan<byte> records)
    {
        if(records.Length!=600)throw new InvalidDataException("Original player fists require300 bounded ushort slots.");
        var entries=new ushort[300];
        for(int index=0;index<entries.Length;index++)entries[index]=BinaryPrimitives.ReadUInt16LittleEndian(records[(index*2)..]);
        for(int slot=0;slot<200;slot++)
        {
            int cycles=0;
            while((entries[slot]&0xFFF)!=slot && (entries[slot]&0xFFF)!=0)
            {
                int target=entries[slot]&0xFFF;
                if(target>=entries.Length || ++cycles>entries.Length)throw new InvalidDataException("Original fists contain an invalid animation index or cyclic record chain.");
                (entries[slot],entries[target])=(entries[target],entries[slot]);
            }
        }
        return Array.AsReadOnly(entries.Select(entry=>(byte)(entry>>14)).ToArray());
    }
}
