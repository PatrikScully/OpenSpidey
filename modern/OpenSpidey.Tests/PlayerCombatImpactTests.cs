using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class PlayerCombatImpactTests
{
    public static void Run(Action<bool,string> assert)
    {
        using JsonDocument document=JsonDocument.Parse(typeof(PlayerCombatImpactTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-impact.json")!);
        assert(document.RootElement.GetProperty("originalPcEmissionVerified").GetBoolean(),
            "Player impact emission gold must be independently checked against the original PC4C6E10 body.");
        int index=0;
        foreach(JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            JsonElement[] input=test.GetProperty("input").EnumerateArray().ToArray();
            if(input[0].GetInt32()==1)
            {
                PlayerImpactLineCorner[] corners=PlayerImpactLineGeometry.Build(input[1].GetSingle(),input[2].GetSingle(),input[3].GetSingle(),input[4].GetSingle());
                float[] expected=test.GetProperty("events").EnumerateArray().Select(value=>value.GetSingle()).ToArray();
                for(int corner=0;corner<4;corner++)assert(Math.Abs(corners[corner].X-expected[2*corner])<.0001f&&Math.Abs(corners[corner].Y-expected[2*corner+1])<.0001f,
                    $"Player impact PC quad{index},corner{corner}: actual{corners[corner]}, expected{expected[2*corner]}/{expected[2*corner+1]}.");
            }
            else
            {
                long[] values=input.Select(value=>value.GetInt64()).ToArray();
                var host=new Host((uint)values[8],(uint)values[9]);
                PlayerCombatImpact.Create(new(unchecked((byte)values[1]),(int)values[2],(ushort)values[3]),new((int)values[5],(int)values[6],(int)values[7]),(int)values[4],host,host.Random);
                long[] expected=test.GetProperty("events").EnumerateArray().Select(value=>value.GetInt64()).ToArray();
                assert(host.Events.SequenceEqual(expected),$"Player impact source{index},input[{string.Join(',',values)}]: actual[{string.Join(',',host.Events)}] expected[{string.Join(',',expected)}].");
            }
            index++;
        }
    }
    private sealed class Host(uint clock,uint seed):IPlayerCombatImpactHost
    {
        private uint randomState=seed;
        public uint Clock=>clock;
        public List<long> Events{get;}=[];
        public int Random(int bound){randomState=unchecked(randomState*1664525+1013904223);int result=bound!=0?(int)(randomState%(uint)bound):0;Events.AddRange([1,bound,result]);return result;}
        public int GetGroundY(FixedVector3 position){Events.AddRange([3,position.X,position.Y,position.Z]);return-12345;}
        public void EmitBouncingRock(FixedVector3 position,int groundY,uint selector,Func<int,int> random)
        {Events.AddRange([4,position.X,position.Y,position.Z,groundY,selector]);foreach(int bound in new[]{200,4096,10,20,2})random(bound);}
        public void EmitRing(ThugCombatImpactRing ring)=>Events.AddRange([2,ring.Position.X,ring.Position.Y,ring.Position.Z,ring.Angle,ring.Scale,ring.MaximumScale,ring.GrowthPerTick,ring.LastClock,ring.PostScale,ring.CodeRgb,ring.FrigDeltaZ]);
        public void EmitLine(PlayerImpactLineParticle particle,LineParticleMotion motion)=>Events.AddRange([5,motion.Head.X,motion.Head.Y,motion.Head.Z,motion.Velocity.X,motion.Velocity.Y,motion.Velocity.Z,motion.RemainingTicks,motion.VerticalAcceleration,particle.StartCodeBgr,particle.EndCodeBgr,motion.LastClock]);
    }
}
