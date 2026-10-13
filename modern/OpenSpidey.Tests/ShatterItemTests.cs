using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ShatterItemTests
{
    private sealed class Host:IShatterHost
    {
        public bool LowMemory {get;init;}
        public List<long[]> Pieces {get;}=[];
        public List<long[]> Traces {get;}=[];
        public int TraceGlassGround(FixedVector3 a,FixedVector3 b){Traces.Add([a.X,a.Y,a.Z,b.X,b.Y,b.Z]);return 1000<<12;}
        public void EmitChunk(ShatterFragment piece)
        {
            List<long> values=[0,piece.Position.X,piece.Position.Y,piece.Position.Z,piece.Velocity.X,piece.Velocity.Y,piece.Velocity.Z,piece.Lifetime,piece.Alive?1:0];
            foreach(FixedVector3 corner in piece.LocalCorners)values.AddRange([corner.X,corner.Y,corner.Z]);
            values.AddRange([piece.Angles.X,piece.Angles.Y,piece.Angles.Z,piece.Spin.X,piece.Spin.Y,piece.Spin.Z]);
            foreach(uint color in piece.Colours)values.Add(color);values.Add(piece.ShardColour);
            foreach(var uv in piece.Uvs)values.AddRange([uv.U,uv.V]);Pieces.Add(values.ToArray());
        }
        public void EmitGlass(ShatterGlassFragment piece)
        {
            List<long> values=[1,piece.Position.X,piece.Position.Y,piece.Position.Z,piece.Velocity.X,piece.Velocity.Y,piece.Velocity.Z,piece.Lifetime,piece.Alive?1:0];
            foreach(FixedVector3 corner in piece.Corners)values.AddRange([corner.X,corner.Y,corner.Z]);
            values.AddRange([piece.GroundY,piece.Age,piece.DefaultRed,piece.DefaultGreen,piece.DefaultBlue,piece.Red,piece.Green,piece.Blue]);Pieces.Add(values.ToArray());
        }
    }
    public static void Run(Action<bool,string> assert)
    {
        using Stream stream=typeof(ShatterItemTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.shatter-item.json")!;
        using JsonDocument fixture=JsonDocument.Parse(stream);
        assert(fixture.RootElement.GetProperty("originalPcItemFaceSplitEmissionVerified").GetBoolean(),"Full shatter item fixture executes original PC Item/Face/Split bodies and original constructors.");
        int caseIndex=0;
        foreach(JsonElement test in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            JsonElement input=test.GetProperty("input");int count=input[0].GetInt32();var faces=new PsxFace[count];
            for(int index=0;index<count;index++)
            {
                uint flags=input[11+index*3].GetUInt32(),color=input[12+index*3].GetUInt32();
                if((flags&0x800)==0)color&=0xFFFFFF;
                faces[index]=new((8u<<18)|flags,[0,1,2,3],color,(flags&1)!=0?0x1234u:null,new byte[8],input[13+index*3].GetUInt32()<<16);
            }
            PsxVertex[] vertices=[new(new(-301,-201,0),0),new(new(401,-201,0),0),new(new(-301,501,0),0),new(new(401,501,0),0)];
            var model=new PsxModel(0,ushort.MaxValue,vertices,faces){Normals=[new(new(input[7].GetInt32(),input[8].GetInt32(),input[9].GetInt32()),0)]};
            uint[] table=Enumerable.Range(0,256).Select(value=>(uint)(value|((value*3%256)<<8)|((value*5%256)<<16))).ToArray();
            var bank=new PsxAssetLibrary("source-original-pc-fixture",new(0x20003,[],[model],table),[new(0x1234,32,16,0,new byte[2048])],null);
            var item=new ShatterItem(bank,0);var host=new Host{LowMemory=input[3].GetInt32()!=0};
            uint rng=input[10].GetUInt32();List<int> ranges=[];
            int Random(int bound){ranges.Add(bound);rng=unchecked(rng*1664525+1013904223);return (int)(rng%(uint)bound);}
            bool result=item.Shatter(new(input[4].GetInt32(),input[5].GetInt32(),input[6].GetInt32()),default,input[1].GetInt32()!=0,input[2].GetInt32()!=0,host,Random);
            assert(result==(test.GetProperty("result").GetInt32()!=0) && item.ItemFlags==test.GetProperty("flags").GetUInt16(),$"Actual PC item{caseIndex} return/itemflags mismatch.");
            for(int index=0;index<count;index++)assert(item.FaceFlags(index)==test.GetProperty("faceFlags")[index].GetUInt32() && item.FaceState(index)==test.GetProperty("faceStates")[index].GetUInt16(),$"Actual PC item{caseIndex}/face{index} mutable flags/state mismatch.");
            JsonElement expected=test.GetProperty("pieces");
            assert(host.Pieces.Count==expected.GetArrayLength(),$"Actual PC item{caseIndex} emitted count: actual{host.Pieces.Count}, expected{expected.GetArrayLength()}.");
            for(int index=0;index<Math.Min(host.Pieces.Count,expected.GetArrayLength());index++)
                assert(host.Pieces[index].SequenceEqual(expected[index].EnumerateArray().Select(value=>value.GetInt64())),
                    $"Actual PC item{caseIndex}/piece{index}: actual[{string.Join(',',host.Pieces[index])}], expected{expected[index]}.");
            assert(ranges.SequenceEqual(test.GetProperty("ranges").EnumerateArray().Select(value=>value.GetInt32())),$"Actual PC item{caseIndex} full face/split/constructor RNG order mismatch.");
            JsonElement expectedTraces=test.GetProperty("traces");
            assert(host.Traces.Count==expectedTraces.GetArrayLength() && host.Traces.Select((trace,index)=>trace.SequenceEqual(expectedTraces[index].EnumerateArray().Select(value=>value.GetInt64()))).All(value=>value),$"Actual PC item{caseIndex} true glass ground trace origin/end/order mismatch.");
            caseIndex++;
        }
    }
}
