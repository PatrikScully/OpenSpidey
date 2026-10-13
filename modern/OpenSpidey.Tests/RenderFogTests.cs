using System.Text.Json;
using OpenSpidey.Core.Presentation;

internal static class RenderFogTests
{
    public static void Run(Action<bool,string> assert)
    {
        using Stream resource=typeof(RenderFogTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.render-fog.json")!;
        using JsonDocument data=JsonDocument.Parse(resource);
        int[] depths=[100,6000,7000,8000,10000,12000,12088,20000];
        int caseIndex=0;
        foreach(JsonElement test in data.RootElement.EnumerateArray())
        {
            var fog=new RenderFogState();
            int start=test.GetProperty("start").GetInt32(),end=test.GetProperty("end").GetInt32();
            fog.Set(0,start,(uint)(end-start));fog.FadeColour=test.GetProperty("colour").GetUInt32();
            fog.Set(test.GetProperty("duration").GetInt32(),test.GetProperty("minimum").GetInt32(),test.GetProperty("range").GetUInt32());
            int update=0;
            foreach(JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                fog.AdvanceRender();
                assert(fog.Start==expected[0].GetInt32() && fog.End==expected[1].GetInt32() && fog.RemainingSteps==expected[2].GetInt32() &&
                    fog.StartRate==expected[3].GetInt32() && fog.EndRate==expected[4].GetInt32() && fog.RangeShift==expected[5].GetInt32() &&
                    fog.StartCopy==expected[6].GetInt32() && fog.EndCopy==expected[7].GetInt32(),$"Source fog case{caseIndex}/render{update} integer transition/shift/copies mismatch.");
                assert(fog.WhiteOpacityFade==(expected[8].GetInt32()!=0) && fog.PcStart==expected[9].GetSingle() &&
                    fog.PcEnd==expected[10].GetSingle() && fog.PcColour==expected[11].GetUInt32(),$"Source fog case{caseIndex}/render{update} actual PC float params or packed color mismatch.");
                for(int index=0;index<depths.Length;index++)
                    assert(fog.VertexAlpha(255,1f/depths[index])==expected[12+index].GetInt32(),$"Source fog case{caseIndex}/render{update}/depth{depths[index]} reciprocal alpha mismatch.");
                update++;
            }
            caseIndex++;
        }
        var level=new RenderFogState {FadeColour=0x02FFFFFF};level.Set(10,8000,4096);
        for(int update=0;update<10;update++)level.AdvanceRender();
        assert(level.Start==8000 && level.End==12088 && level.RemainingSteps==0 && level.WhiteOpacityFade,
            "Original L1A1 Restart14 transition retains truncated404 end steps instead of inventing12096 final correction.");
        assert(level.VertexAlpha(255,1f/6000)==255 && level.VertexAlpha(255,1f/10000)>0 && level.VertexAlpha(255,1f/12088)==0,
            "Original white distance fade retains near geometry and reveals the genuine backdrop at the far end.");
        int savedStart=level.Start;
        try {level.Set(0,1,0);assert(false,"Zero source fog range cannot enter an infinite shift loop.");}
        catch(ArgumentOutOfRangeException){assert(level.Start==savedStart,"Modern zero-range rejection precedes state mutation.");}
        var background=new BackgroundState(0xDE834642,0);
        assert(background.Visible && background.BodyFlags==0x14 && background.AngularVelocity==default,
            "Matching background constructor clears body2, ignores velocity argument and starts an unculled source502 body.");
        background.ItemFlags=1;
        assert(background.Visible,"Actual PC background renderer ignores Backgrnd_Off bit1 and only checks signed high flag byte.");
        background.ItemFlags=0x8000;
        assert(!background.Visible,"Actual PC renderer rejects source8000 culled backdrop flag.");
        background.PrepareSourceUpdate();
        assert(background.BodyFlags==0x10,"Background EveryFrame clears initial bit4 before its first AI update.");
        background.Angles=new(32767,-32768,1);background.AngularVelocity=new(2,-2,3);background.Advance();
        assert(background.Angles==new OpenSpidey.Core.Actors.ActorAngles(-32767,32766,4),"Background AI adds velocity once with source signed-short wrapping.");
    }
}
