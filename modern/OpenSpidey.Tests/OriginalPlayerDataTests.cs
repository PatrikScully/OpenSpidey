using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;

internal static class OriginalPlayerDataTests
{
    private const string Hooks="000009000000000000000b00000000000000060000000000000000000000c800000005000000c80000000a000000000000000e00000000000000110000000000000006000000000000000700";
    private const string Links="00000c000000ffff00000000000000000200000000000000000000000100020000000000000000000700010000000000000000000400010000000000000000000300040000000000000000000600030000000000000000000500030000000000000000000900010000000000000000000800090000000000000000000b00080000000000000000000a0008000000000000000000";
    public static void Run(Action<bool,string> assert,string? assets=null)
    {
        using Stream fixture=typeof(OriginalPlayerDataTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-melee.json")
            ?? throw new InvalidDataException("Original player fight fixture is missing.");
        using JsonDocument document=JsonDocument.Parse(fixture);
        byte[] distances=Convert.FromHexString(document.RootElement.GetProperty("distanceHex").GetString()!);
        byte[] moves=Convert.FromHexString(document.RootElement.GetProperty("movesHex").GetString()!);
        byte[] image=new byte[0x20200];
        Half(0,0x5A4D);Word(60,0x80);Word(0x80,0x4550);Half(0x84,0x14C);Half(0x86,2);
        Half(0x94,0xE0);Half(0x98,0x10B);Word(0xB4,0x400000);
        Word(0x178+12,0x150000);Word(0x178+16,0x20000);Word(0x178+20,0x200);
        Word(0x1A0+8,0x10000);Word(0x1A0+12,0x2A0000);
        Put(0x55644C,Convert.FromHexString(Hooks));Put(0x5564E4,Convert.FromHexString(Links));
        Put(0x569194,distances);Put(0x56929C,moves);
        int[][] expectedSounds=[[1,11],[4],[7,15],[6],[6],[6,15],[7,19,26],[2,14],[1,21]];
        uint[] soundAddresses=[0x5565A8,0x5565B4,0x5565BC,0x5565C8,0x5565D0,0x5565D8,0x5565E4,0x5565F4,0x556600];
        ushort[] soundAnimations=[21,59,52,50,51,60,63,192,198];
        for(int index=0;index<soundAddresses.Length;index++)
        {
            byte[] words=new byte[(expectedSounds[index].Length+1)*4];
            for(int word=0;word<expectedSounds[index].Length;word++)
                BinaryPrimitives.WriteInt32LittleEndian(words.AsSpan(word*4),expectedSounds[index][word]);
            BinaryPrimitives.WriteInt32LittleEndian(words.AsSpan(words.Length-4),-1);Put(soundAddresses[index],words);
        }
        Verify(OriginalPlayerData.Read(image));
        foreach(int count in new[]{0,63,0x80,0x98,0x19F,0x201FF})Reject(()=>OriginalPlayerData.Read(image.AsSpan(0,count)));
        byte[] corrupt=image.ToArray();corrupt[0x98]=0x0B;corrupt[0x99]=2;Reject(()=>OriginalPlayerData.Read(corrupt));
        corrupt=image.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(0x178+20),uint.MaxValue);Reject(()=>OriginalPlayerData.Read(corrupt));
        corrupt=image.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(corrupt.AsSpan(0x664C+2),31);Reject(()=>OriginalPlayerData.Read(corrupt));
        if(assets is not null)
        {
            string path=Path.Combine(assets,"SpideyPC.exe");
            if(!File.Exists(path))throw new FileNotFoundException("Asset-backed original player data proof requires the private PC executable.",path);
            OriginalPlayerData original=OriginalPlayerData.Read(File.ReadAllBytes(path));
            Verify(original);
            assert(original.FistVariants.Count==300 && original.FistVariants[0]==3 && original.FistVariants[21]==3 && original.FistVariants[50]==0,
                "Private original fists choose source idle/run variant3 and jump variant0 after exact sparse cycle sorting.");
        }
        void Verify(OriginalPlayerData data)
        {
            assert(data.FistVariants.Count==300,"Original player fists are a bounded managed300-animation lookup.");
            ActorHook[] expected=[new(0,0,0,11),new(0,0,0,6),new(0,0,0,0),new(0,200,0,5),new(0,200,0,10),
                new(0,0,0,14),new(0,0,0,17),new(0,0,0,6),new(0,0,0,7)];
            assert(data.Hooks.Hooks.SequenceEqual(expected),"Original player hook packet retains all nine signed hooks and source part indices.");
            assert(data.PoseLinks.Links.Select(link=>(int)link.Part).SequenceEqual(new[]{0,2,1,7,4,3,6,5,9,8,11,10}),
                "Original player pose has twelve source links across its eighteen-part animation model.");
            assert(data.DistanceDefinitions.Span[..distances.Length].SequenceEqual(distances) && data.ComboDefinitions.Span[..moves.Length].SequenceEqual(moves),
                "Private PC fight bytes match independently captured immutable compiled-source reference data before relocation.");
            PlayerFightData fight=PlayerFightData.Decode(data.DistanceDefinitions.Span,data.ComboDefinitions.Span);
            assert(fight.Moves.Count==12 && fight.Moves[0].Animation==100 && fight.Moves[1].Animation==107,
                "Named managed player fight records decode without mapping code or retaining executable pointers.");
            for(int index=0;index<soundAnimations.Length;index++)
                assert(data.AnimationSounds[soundAnimations[index]].SequenceEqual(expectedSounds[index]),
                    "Player sound defaults decode the original constructor lists, with real terminators and source frame words.");
        }
        void Half(int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(at),value);
        void Word(int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(at),value);
        void Put(uint address,byte[] bytes)=>bytes.CopyTo(image,checked((int)(address-0x550000+0x200)));
        void Reject(Action read)
        {
            try{read();assert(false,"Malformed original player PE data must be rejected before accessing its records.");}
            catch(Exception error)when(error is InvalidDataException or OverflowException)
            {assert(true,"Malformed original player data is bounded and rejected.");}
        }
    }
}
