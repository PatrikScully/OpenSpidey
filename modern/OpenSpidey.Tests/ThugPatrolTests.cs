using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugPatrolTests
{
    public static void Run(Action<bool, string> assert)
    {
        CheckAnimations(assert);
        using Stream stream = typeof(ThugPatrolTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-patrol.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = ReadInput(test);
            var state = new ThugState(0, 0)
            {
                Substate = (int)input[2], PathPointIndex = (int)input[3], AlertTicks = (int)input[4],
                State = (int)input[5], PauseUpdates = (int)input[6], BodyFlags = (uint)input[7], CombatFlags = (uint)input[10]
            };
            state.SetThugType((int)input[1]);
            var actor = new ActorProcedureState(new ActorBodyMotion { MovementFlags = (uint)input[12] })
            {
                BehaviorFlags = (uint)input[8], CompletionFlags = (uint)input[9]
            };
            var procedures = new ActorProcedureList();
            var host = new Host(state, actor, procedures)
            {
                AnimationId = (int)input[14], AnimationFinished = (byte)input[15], RunFinished = (byte)input[16],
                SpitResult = input[18] != 0, FightResult = input[21] != 0, GlobalOwner = input[22] != 0,
                AttackMask = (uint)input[23], AttackFlags = (byte)input[24], OwnedMask = (byte)input[25]
            };
            ThugAnimationBehavior.SetMode(state, (int)input[13], false, host);
            host.Events.Clear();
            ActorWaypointRoute route = null!;
            var file = new TriggerFile([Node(0, input[19] != 0 ? [1] : []), Node(1, [])]);
            route = new(file, 0, _ => route.Flags = (uint)input[20]) { Flags = (uint)input[11] };
            ThugPatrolBehavior.Step(state, actor, procedures, route, host, bound =>
            {
                host.Events.Add(1000 + bound);
                return (int)input[17] % bound;
            });
            long[] actual = [state.Substate, state.PathPointIndex, state.AlertTicks, state.State, state.PauseUpdates,
                state.BodyFlags, actor.BehaviorFlags, actor.CompletionFlags, state.CombatFlags, route.Flags, route.CurrentNode,
                state.TurnRate, state.AnimationWord294, state.AnimationWord298, host.AnimationId, host.AnimationFrame,
                host.AnimationFinished, host.GlobalOwner ? 1 : 0, host.AttackMask, host.AttackFlags, host.OwnedMask,
                procedures.Count / 2];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(22).Select(value => value.GetInt64())),
                "Patrol states, path/chase branches, attack ownership, route flags and movement attachment match compiled FollowWaypoints.");
            assert(host.Events.SequenceEqual(expected[22].EnumerateArray().Select(value => value.GetInt32()).Where(value => value != 5)),
                "Patrol animation, fight queries, random choices and spit callbacks match source call order.");
            if (procedures.Count != 0)
            {
                FixedVector3 destination = input[3] != 0 ? host.GetPathPoint((int)input[3]) : new(409600, 0, 819200);
                for (int frame = 0; frame < 2000 && (actor.CompletionFlags & 1) == 0; frame++) procedures.Execute(2);
                assert(actor.Body.Position == destination && (actor.CompletionFlags & 1) != 0,
                    "Patrol attaches real facing and MoveTo procedures that reach the requested waypoint/path and signal arrival.");
            }
        }
    }

    private static void CheckAnimations(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugPatrolTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-animation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = ReadInput(test);
            var state = new ThugState(0, 0);
            state.SetThugType((int)input[1]);
            var host = new Host(state, new ActorProcedureState(new ActorBodyMotion()), new ActorProcedureList())
            {
                AnimationId = (int)input[6], AnimationFinished = (byte)input[7], RunFinished = (byte)input[8]
            };
            ThugAnimationBehavior.SetMode(state, (int)input[2], false, host);
            host.Events.Clear();
            bool result = false;
            if (input[0] == 0) result = ThugAnimationBehavior.SetMode(state, (int)input[3], input[4] != 0, host);
            else ThugAnimationBehavior.RunAppropriate(state, (uint)input[5], host);
            long[] actual = [result ? 1 : 0, state.AnimationWord294, state.AnimationWord298,
                host.AnimationId, host.AnimationFrame, host.AnimationFinished];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(6).Select(value => value.GetInt64())),
                "Thug animation modes, transition return/play behavior and movement flag priority match recovered C++.");
            assert(host.Events.SequenceEqual(expected[6].EnumerateArray().Select(value => value.GetInt32())),
                "Thug animation callbacks preserve clip selection and source order.");
        }
    }

    private static long[] ReadInput(JsonElement test) => test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private static TriggerNode Node(ushort id, ushort[] links)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)1000);
        writer.Write((ushort)links.Length);
        foreach (ushort link in links) writer.Write(link);
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write(100); writer.Write(0); writer.Write(200);
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)0);
        writer.Write((ushort)0x4100);
        return new(id, 1000, id * 64, stream.ToArray());
    }

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugPatrolHost
    {
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public bool SpitResult { get; init; }
        public bool FightResult { get; init; }
        public bool GlobalOwner { get; set; }
        public uint AttackMask { get; set; }
        public byte AttackFlags { get; set; }
        public byte OwnedMask { get; set; }
        public List<int> Events { get; } = [];
        public void Neutralize() { Events.Add(1); actor.Neutralize(procedures); }
        public FixedVector3 GetPathPoint(int sourceIndex) => new(409600 * sourceIndex, 0, -409600 * sourceIndex);
        public void ClearAttackOwnership()
        {
            if (GlobalOwner) GlobalOwner = false;
            else if ((AttackFlags & 2) != 0) AttackMask &= ~(uint)OwnedMask;
            AttackFlags = 0;
            OwnedMask = 0;
        }
        public bool DetermineFightState()
        {
            Events.Add(6);
            if (FightResult) { state.State = 6; state.Substate = 0; }
            return FightResult;
        }
        public void RunAnimation(ushort clip, int from = 0, int to = -1)
        {
            Events.AddRange([2, clip, from, to]);
            AnimationId = clip;
            AnimationFrame = from;
            AnimationFinished = RunFinished;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            Events.AddRange([3, clip, direction]);
            AnimationId = clip;
            AnimationFinished = 0;
        }
        public bool MonitorSpitPlease() { Events.Add(4); return SpitResult; }
    }
}
