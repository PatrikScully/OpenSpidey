using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraShakeTests
{
    public static void Run(Action<bool,string> assert)
    {
        using Stream resource=typeof(CameraShakeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-shake.json")!;
        using JsonDocument data=JsonDocument.Parse(resource);
        int caseIndex=0;
        foreach(JsonElement test in data.RootElement.EnumerateArray())
        {
            var shake=new CameraShakeState {Amplitude=Axes(test.GetProperty("amplitude")),Speed=Axes(test.GetProperty("speed")),Decay=Decay(test.GetProperty("decay"))};
            int magnitude=test.GetProperty("magnitude").GetInt32();
            if(magnitude>=0)
            {
                JsonElement position=test.GetProperty("position");
                shake.Shake(new(position[0].GetInt32(),position[1].GetInt32(),position[2].GetInt32()),magnitude);
            }
            FixedQuaternion baseline=Quaternion(test.GetProperty("baseline"));
            uint time=test.GetProperty("time").GetUInt32();
            uint step=test.GetProperty("step").GetUInt32();
            int update=0;
            foreach(JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                CameraShakeAxes before=shake.Amplitude;
                FixedQuaternion preview=shake.Evaluate(baseline,time);
                assert(shake.Amplitude==before,$"Camera shake case{caseIndex}/update{update} render evaluation must not consume source decay.");
                FixedQuaternion display=shake.Advance(baseline,time);
                assert(display==Quaternion(expected,9) && display==preview && shake.DisplayRotation==display,
                    $"Camera shake case{caseIndex}/update{update}: qy*base*qx*qz exact source quaternion mismatch at time{time}.");
                assert(shake.Amplitude==Axes(expected) && shake.Decay==Decay(expected,3) && shake.Speed==Axes(expected,6),
                    $"Camera shake case{caseIndex}/update{update}: signed-short amplitude or byte decay/speed mismatch.");
                time=unchecked(time+step);update++;
            }
            caseIndex++;
        }
        var small=new CameraShakeState();small.Shake(default,2);
        assert(small.Amplitude==new CameraShakeAxes(0,0,25) && small.Decay==new CameraShakeDecay(3,0,3) && small.Speed==new CameraShakeAxes(600,0,600),
            "Manipulation collision Shake2 selects actual SmallShakeAmp/LandShakeDecay/LandShakeSpeed.");
        for(uint update=0;update<9;update++)small.Advance(FixedQuaternion.Identity,update*2);
        assert(!small.Active && small.Advance(new(1,2,3,4095),18)==new FixedQuaternion(1,2,3,4095),
            "Nine source camera updates finish small shake; inactive state returns the exact baseline without quaternion quantization.");
        CameraShakeAxes saved=small.Amplitude;
        try {small.Shake(default,4);assert(false,"Unknown source magnitude must reject before changing camera state.");}
        catch(ArgumentOutOfRangeException){assert(small.Amplitude==saved,"Rejected magnitude preserves source state.");}
    }
    private static CameraShakeAxes Axes(JsonElement value,int offset=0)=>new(value[offset].GetInt16(),value[offset+1].GetInt16(),value[offset+2].GetInt16());
    private static CameraShakeDecay Decay(JsonElement value,int offset=0)=>new(value[offset].GetByte(),value[offset+1].GetByte(),value[offset+2].GetByte());
    private static FixedQuaternion Quaternion(JsonElement value,int offset=0)=>new(value[offset].GetInt32(),value[offset+1].GetInt32(),value[offset+2].GetInt32(),value[offset+3].GetInt32());
}
