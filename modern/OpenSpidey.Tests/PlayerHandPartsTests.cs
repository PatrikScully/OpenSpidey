using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;

internal static class PlayerHandPartsTests
{
    public static void Run(Action<bool,string> assert)
    {
        using Stream resource=typeof(PlayerHandPartsTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-hand-parts.json")!;
        using JsonDocument data=JsonDocument.Parse(resource);
        foreach(JsonElement test in data.RootElement.EnumerateArray())
        {
            uint mask=test.GetProperty("mask").GetUInt32();byte variant=test.GetProperty("variant").GetByte();
            uint result=PlayerHandParts.Select(mask,variant);
            assert(result==test.GetProperty("expected").GetUInt32(),"Original CreateFists masks exactly one model per hand pair and preserves unrelated bits.");
            assert(((result>>5)&3) is 1 or 2 && ((result>>10)&3) is 1 or 2,"Every source variant selects exactly one of models5/6 and10/11.");
        }
        byte[] records=new byte[600];
        BinaryPrimitives.WriteUInt16LittleEndian(records,0xC000);
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(2),0xC015);
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(4),50);
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(6),0x8116);
        var variants=OriginalPlayerFists.Decode(records);
        assert(variants.Count==300 && variants[0]==3 && variants[21]==3 && variants[50]==0 && variants[278]==2,
            "Sparse source fists sort all200 input slots into300 animation destinations, including entries above199.");
        assert(PlayerHandParts.Select(PlayerHandParts.InitialHiddenMask,variants[0])==0x840 &&
            PlayerHandParts.Select(PlayerHandParts.InitialHiddenMask,variants[21])==0x840 &&
            PlayerHandParts.Select(PlayerHandParts.InitialHiddenMask,variants[50])==0x420,"Idle/run retain models5/10; jump switches to models6/11.");
        Reject(()=>OriginalPlayerFists.Decode(records.AsSpan(0,599)));
        byte[] bad=records.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(2),4095);Reject(()=>OriginalPlayerFists.Decode(bad));
        void Reject(Action action)
        {try{action();assert(false,"Malformed fist records must fail before a destination access.");}catch(InvalidDataException){assert(true,"Malformed fist records are bounded.");}}
    }
}
