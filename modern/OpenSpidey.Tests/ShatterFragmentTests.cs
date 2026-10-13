using System.Numerics;
using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ShatterFragmentTests
{
    public static void Run(Action<bool,string> assert)
    {
        using Stream resource=typeof(ShatterFragmentTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.shatter-fragments.json")!;
        using JsonDocument fixture=JsonDocument.Parse(resource);
        assert(fixture.RootElement.GetProperty("originalPcNumericalStateAndRandomOrderVerified").GetBoolean(),"Shatter fixture proves original PC constructor/motion and every random call.");
        int caseIndex=0;
        foreach(JsonElement test in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            JsonElement input=test.GetProperty("input");uint rng=input[24].GetUInt32();List<int> ranges=[];
            int Random(int bound){ranges.Add(bound);rng=unchecked(rng*1664525+1013904223);return (int)(rng%(uint)bound);}
            FixedVector3 Vector(int index)=>new(input[index].GetInt32(),input[index+1].GetInt32(),input[index+2].GetInt32());
            uint rgb=input[23].GetUInt32();
            ShatterFragment? chunk=null;ShatterGlassFragment? glass=null;
            if(input[0].GetInt32()==0)chunk=new(Vector(1),Vector(4),Vector(7),Vector(10),Vector(13),Vector(16),rgb,0x1234,0,[(0,0),(1,0),(0,1)],Random);
            else glass=new(Vector(10),Vector(19),input[22].GetInt32(),(byte)rgb,(byte)(rgb>>8),(byte)(rgb>>16),Random);
            int update=0;
            foreach(JsonElement expected in test.GetProperty("states").EnumerateArray())
            {
                if(update!=0){chunk?.Advance();glass?.Advance();}
                var actual=new List<long>();
                void Add(FixedVector3 vector){actual.Add(vector.X);actual.Add(vector.Y);actual.Add(vector.Z);}
                if(chunk is not null)
                {
                    Add(chunk.Position);Add(chunk.Velocity);actual.Add(chunk.Lifetime);actual.Add(chunk.Alive?1:0);
                    foreach(FixedVector3 corner in chunk.LocalCorners)Add(corner);
                    actual.AddRange([chunk.Angles.X,chunk.Angles.Y,chunk.Angles.Z,chunk.Spin.X,chunk.Spin.Y,chunk.Spin.Z]);
                    foreach(uint colour in chunk.Colours)actual.Add(colour);
                }
                else
                {
                    Add(glass!.Position);Add(glass.Velocity);actual.Add(glass.Lifetime);actual.Add(glass.Alive?1:0);
                    foreach(FixedVector3 corner in glass.Corners)Add(corner);actual.Add(glass.GroundY);actual.Add(glass.Age);
                    actual.AddRange([glass.DefaultRed,glass.DefaultGreen,glass.DefaultBlue,glass.Red,glass.Green,glass.Blue]);
                }
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value=>value.GetInt64())),
                    $"Actual PC shatter case{caseIndex}/update{update}: expected{expected}, actual[{string.Join(',',actual)}].");
                update++;
            }
            assert(ranges.SequenceEqual(test.GetProperty("ranges").EnumerateArray().Select(value=>value.GetInt32())),$"Actual PC shatter case{caseIndex} every RNG bound/order mismatch.");
            caseIndex++;
        }
        VerifyItem(assert);
    }
    private sealed class Host:IShatterHost
    {
        public bool LowMemory {get;set;}
        public List<ShatterFragment> Chunks {get;}=[];
        public List<ShatterGlassFragment> Glass {get;}=[];
        public List<(FixedVector3 Start,FixedVector3 End)> Traces {get;}=[];
        public int TraceGlassGround(FixedVector3 start,FixedVector3 end){Traces.Add((start,end));return 1000<<12;}
        public void EmitChunk(ShatterFragment fragment)=>Chunks.Add(fragment);
        public void EmitGlass(ShatterGlassFragment fragment)=>Glass.Add(fragment);
    }
    private static void VerifyItem(Action<bool,string> assert)
    {
        PsxVertex[] vertices=[new(new(-300,-200,0),0),new(new(400,-200,0),0),new(new(-300,500,0),0),new(new(400,500,0),0)];
        PsxFace Face(uint flags,uint? texture)=>new(flags,[0,1,2,3],0x00CC8844,texture,new byte[8],8u<<16);
        PsxAssetLibrary Bank(PsxFace[] faces)=>new("source-shatter-fixture",new(0x20003,[],[new(0,ushort.MaxValue,vertices,faces){Normals=[new(new(0,0,4096),0)]}],[]),[new(0x1234,32,16,0,new byte[32*16*4])],null);
        int Random(int bound)=>bound==5?1:bound/2;
        var item=new ShatterItem(Bank(Enumerable.Range(0,8).Select(_=>Face(0x13,0x1234)).ToArray()),0);var host=new Host();
        bool result=item.Shatter(default,default,true,true,host,Random);
        assert(result && item.ItemFlags==1 && host.Chunks.Count==6,"Actual item loop limits expensive recursive-result1 faces to6 while marking every source face.");
        assert(Enumerable.Range(0,8).All(index=>item.FaceState(index)==9 && (item.FaceFlags(index)&0x180)==0),"Each item's seen/processed face flags persist across invocations.");
        assert(!item.Shatter(default,default,true,true,host,Random) && host.Chunks.Count==6,"Second seen-checked invocation cannot emit already processed faces.");
        var other=new ShatterItem(Bank([Face(0x13,0x1234)]),0);other.Shatter(default,default,true,true,host,Random);
        assert(host.Chunks.Count==7,"Another original item instance has independent mutable face state.");
        ShatterFragment piece=host.Chunks[0];
        assert(piece.Uvs.SequenceEqual(new (byte,byte)[]{(0,0),(31,0),(0,15)}),"Shatter uses source texture rectangle corners rather than face UVs.");
        var quad=new ShatterItem(Bank([Face(0x40,null)]),0);var glass=new Host();
        quad.Shatter(new(0,-1,0),new ActorAngles(),false,true,glass,Random);
        assert(glass.Glass.Count==30 && glass.Traces.Count==1 && glass.Traces[0].End-glass.Traces[0].Start==new FixedVector3(0,3000<<12,0),"Original quad emits30 real glass pieces and traces downward3000 units.");
        assert(glass.Glass.All(fragment=>fragment.DefaultRed==0x11 && fragment.DefaultGreen==0x33 && fragment.DefaultBlue==0x33),"Actual PC quad glass passes R,B,B rather than recovered R,G,B.");
        var low=new Host{LowMemory=true};var lowItem=new ShatterItem(Bank([Face(0x40,null)]),0);
        assert(lowItem.Shatter(default,default,false,true,low,Random) && low.Glass.Count==0 && low.Traces.Count==0,"Source low-memory path still returns1/marks item without effect construction.");
    }
}
