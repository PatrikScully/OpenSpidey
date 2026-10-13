using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

/// <summary>Private PC data imported as managed packets and immutable fight streams; never loads executable code.</summary>
public sealed record OriginalPlayerData(ActorHookPacket Hooks,ActorPoseLinkPacket PoseLinks,
    ReadOnlyMemory<byte> DistanceDefinitions,ReadOnlyMemory<byte> ComboDefinitions,
    IReadOnlyDictionary<ushort,IReadOnlyList<int>> AnimationSounds)
{
    public IReadOnlyList<byte> FistVariants {get;init;}=Array.Empty<byte>();
    public static OriginalPlayerData Read(ReadOnlySpan<byte> image)
    {
        if(image.Length<64 || Half(image,0)!=0x5A4D)throw new InvalidDataException("Original player data requires a bounded PC executable image.");
        int pe=checked((int)Word(image,60));
        if(Word(image,pe)!=0x4550 || Half(image,pe+4)!=0x14C)throw new InvalidDataException("Original player data requires the original x86 PE image.");
        int sections=Half(image,pe+6),optionalSize=Half(image,pe+20),optional=checked(pe+24);
        if(sections is <=0 or >96 || optionalSize<32 || Half(image,optional)!=0x10B)
            throw new InvalidDataException("Original player PE metadata is malformed.");
        uint imageBase=Word(image,optional+28);
        int table=checked(optional+optionalSize);
        Require(image,table,checked(sections*40));
        for(int index=0;index<sections;index++)
        {
            int section=checked(table+index*40);
            Require(image,checked((int)Word(image,section+20)),checked((int)Word(image,section+16)));
        }
        // Addresses exist only at this private-data import boundary. Runtime records retain no pointers or ABI.
        byte[] hooks=Packet(image,imageBase,table,sections,0x55644C,8);
        byte[] links=Packet(image,imageBase,table,sections,0x5564E4,12);
        var hookPacket=ActorHookPacket.Read(hooks,18);
        var linkPacket=ActorPoseLinkPacket.Read(links,18);
        if(hookPacket.Hooks.Count!=9 || linkPacket.Links.Count!=12)
            throw new InvalidDataException("Original player data differs from the source nine-hook/twelve-joint packets.");
        byte[] initialTable=VirtualSlice(image,imageBase,table,sections,0x6A82B8,300*4);
        var sounds=new Dictionary<ushort,IReadOnlyList<int>>();
        for(ushort animation=0;animation<300;animation++)
        {
            uint address=Word(initialTable,animation*4);
            if(address!=0)sounds.Add(animation,ReadSounds(image,imageBase,table,sections,address));
        }
        // The constructor installs these nine genuine lists, then masks each nonterminating word.
        foreach(var entry in new (ushort Animation,uint Address)[]{(21,0x5565A8),(59,0x5565B4),(52,0x5565BC),
            (50,0x5565C8),(51,0x5565D0),(60,0x5565D8),(63,0x5565E4),(192,0x5565F4),(198,0x556600)})
            sounds[entry.Animation]=ReadSounds(image,imageBase,table,sections,entry.Address);
        return new(hookPacket,linkPacket,Slice(image,imageBase,table,sections,0x569194,0x108),
            Slice(image,imageBase,table,sections,0x56929C,8192),
            new System.Collections.ObjectModel.ReadOnlyDictionary<ushort,IReadOnlyList<int>>(sounds))
            {FistVariants=OriginalPlayerFists.Decode(Slice(image,imageBase,table,sections,0x555A14,600))};
    }
    private static IReadOnlyList<int> ReadSounds(ReadOnlySpan<byte> image,uint imageBase,int table,int sections,uint address)
    {
        var words=new List<int>();
        for(int index=0;index<1024;index++)
        {
            uint word=Word(Slice(image,imageBase,table,sections,checked(address+(uint)index*4),4),0);
            if(word==uint.MaxValue)return Array.AsReadOnly(words.ToArray());
            words.Add(unchecked((int)(word&0xFFFF)));
        }
        throw new InvalidDataException("Original player animation sound list has no bounded terminator.");
    }
    private static byte[] VirtualSlice(ReadOnlySpan<byte> image,uint imageBase,int table,int sections,uint address,int count)
    {
        if(address<imageBase)throw new InvalidDataException("Original player data address precedes the PE image.");
        uint relative=address-imageBase;
        for(int index=0;index<sections;index++)
        {
            int section=checked(table+index*40);
            uint virtualSize=Word(image,section+8),virtualAddress=Word(image,section+12),rawSize=Word(image,section+16),rawOffset=Word(image,section+20);
            if(relative<virtualAddress || (ulong)relative+(uint)count>(ulong)virtualAddress+Math.Max(virtualSize,rawSize))continue;
            byte[] bytes=new byte[count];
            ulong displacement=(ulong)relative-virtualAddress;
            int stored=displacement<rawSize?checked((int)Math.Min((ulong)count,rawSize-displacement)):0;
            if(stored!=0)
            {
                int offset=checked((int)((ulong)rawOffset+displacement));Require(image,offset,stored);
                image.Slice(offset,stored).CopyTo(bytes);
            }
            return bytes; // PE uninitialized data is zero-filled metadata, never executable memory.
        }
        throw new InvalidDataException("Original player initial data table lies outside PE virtual section metadata.");
    }
    private static byte[] Packet(ReadOnlySpan<byte> image,uint imageBase,int table,int sections,uint address,int stride)
    {
        byte[] header=Slice(image,imageBase,table,sections,address,4);
        int count=Half(header,2);
        if(count is <=0 or >30)throw new InvalidDataException("Original player packet count is outside its source capacity.");
        return Slice(image,imageBase,table,sections,address,checked(4+count*stride));
    }
    private static byte[] Slice(ReadOnlySpan<byte> image,uint imageBase,int table,int sections,uint address,int count)
    {
        if(address<imageBase)throw new InvalidDataException("Original player data address precedes the PE image.");
        uint relative=address-imageBase;
        for(int index=0;index<sections;index++)
        {
            int section=checked(table+index*40);
            uint virtualAddress=Word(image,section+12),rawSize=Word(image,section+16),rawOffset=Word(image,section+20);
            if(relative<virtualAddress || (ulong)relative+unchecked((uint)count)>(ulong)virtualAddress+rawSize)continue;
            int offset=checked((int)((ulong)rawOffset+relative-virtualAddress));
            Require(image,offset,count);return image.Slice(offset,count).ToArray();
        }
        throw new InvalidDataException("Original player data lies outside stored PE section bytes.");
    }
    private static ushort Half(ReadOnlySpan<byte> source,int offset)
    {Require(source,offset,2);return BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]);}
    private static uint Word(ReadOnlySpan<byte> source,int offset)
    {Require(source,offset,4);return BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);}
    private static void Require(ReadOnlySpan<byte> source,int offset,int count)
    {if(offset<0 || count<0 || offset>source.Length-count)throw new InvalidDataException("Original player data is truncated or out of bounds.");}
}
