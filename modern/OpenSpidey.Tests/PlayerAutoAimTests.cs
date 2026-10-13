using System.Text.Json;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Mathematics;

internal static class PlayerAutoAimTests
{
    public static void Run(Action<bool,string> assert)
    {
        using Stream stream=typeof(PlayerAutoAimTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-autoaim.json")
            ?? throw new InvalidDataException("Source player autoaim fixture is missing.");
        using JsonDocument document=JsonDocument.Parse(stream);
        int index=0;
        foreach(JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] input=test.GetProperty("input").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
            int[] expected=test.GetProperty("expected").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
            var previous=new Target(default);
            var target=new Target(new(input[5],input[6],input[7]));
            var state=new PlayerAutoAimState {FieldE04=8,FieldE06=9,FieldE08=10,SwitchAimPoint=new(1,2,3),Target=previous};
            var host=new Host(input,target);
            byte result=PlayerAutoAimSelection.Update(state,host);
            int[] actual=[result,state.FieldE04,state.FieldE06,state.FieldE08,state.SwitchAimPoint is null?1:0,
                ReferenceEquals(state.Target,target)?1:ReferenceEquals(state.Target,previous)?2:0,host.HasReticle?1:0,
                host.HasReticle?host.Position.X:0,host.HasReticle?host.Position.Y:0,host.HasReticle?host.Position.Z:0,
                host.HasReticle?host.ReticleYaw:0,host.Events.Count,..host.Events];
            assert(actual.SequenceEqual(expected),$"Compiled source autoaim case{index++}: target retention, field resets, bounded yaw/height and native reticle ownership order match.");
        }
    }
    private sealed class Target(FixedVector3 position):IPlayerMeleeTarget
    {public object Identity=>this;public FixedVector3 Position=>position;public ushort Type=>304;public ushort ItemFlags=>2;public IPlayerMeleeTarget? Next=>null;}
    private sealed class Host(int[] input,IPlayerMeleeTarget target):IPlayerAutoAimHost
    {
        public byte SurfaceTransition=>unchecked((byte)input[0]);
        public int ElapsedTicks=>input[3];
        public bool HasReticle {get;private set;}=input[1]!=0;
        public short ReticleYaw {get;set;}=input[1]!=0?unchecked((short)input[4]):(short)0;
        public FixedVector3 Position {get;private set;}
        public List<int> Events {get;}=[];
        public IPlayerMeleeTarget? SelectTargetBaddy(int distance,int cone,int weight,int facing)
        {Events.AddRange([6,distance,cone,weight,facing]);return input[2]!=0?target:null;}
        public ushort GetTargetHeight(IPlayerMeleeTarget value)=>unchecked((ushort)input[8]);
        public void CreateReticle()
        {Events.AddRange([1,5,unchecked((int)0xB08EC1FB),3,2]);HasReticle=true;ReticleYaw=0;}
        public void DeleteReticle(){Events.AddRange([3,4]);HasReticle=false;}
        public void PositionReticle(FixedVector3 position)=>Position=position;
    }
}
