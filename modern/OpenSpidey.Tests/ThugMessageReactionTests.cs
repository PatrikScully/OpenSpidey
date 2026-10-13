using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugMessageReactionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugMessageReactionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-message-reactions.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0) { State = (int)input[1], Substate = (int)input[2], AlertTicks = (int)input[3] };
            var actor = new ActorProcedureState(new ActorBodyMotion { Position = Vector(10) }) { BehaviorFlags = (uint)input[4] };
            var procedures = new ActorProcedureList();
            object? senderHandle = input[7] != 0 ? new object() : null;
            var message = new ThugMessage(13, senderHandle) { Flags = (int)input[6] };
            var host = new Host
            {
                WaypointFlags = (uint)input[5], SenderPosition = Vector(13), LineOfSight = input[8] != 0,
                PlayerUnavailableFlag = (int)input[9], PlayerPosition = Vector(16), Hit = Vector(19),
                PathCodes = [(int)input[22], (int)input[23]], WriteHit = input[24] != 0,
                AddResults = [input[25] != 0, input[26] != 0, input[27] != 0]
            };
            int result = -1;
            if (input[0] == 0) ThugMessageReactions.GetReadyToShootHostage(state, actor, procedures, message, host);
            else if (input[0] == 1) ThugMessageReactions.RunToWhereTheActionIs(state, actor, Vector(13), host);
            else result = ThugMessageReactions.TryAddingCollidePointToPath(actor, Vector(13), host) ? 1 : 0;
            int procedureCount = procedures.Count;
            int resultingState = state.State;
            // Observe the real attached procedure's message code rather than assuming it.
            if (procedureCount != 0)
            {
                procedures.Execute(1);
                assert(host.SentMessages.Count == 0, "Hostage procedure waits for a changed state");
                state.State = 28;
                procedures.Execute(1);
                assert(procedures.Count == 0, "Hostage state-switch message procedure retires after reply");
            }
            long[] actual = [resultingState, state.Substate, state.AlertTicks, actor.BehaviorFlags, host.WaypointFlags,
                message.Flags, host.StoredHandle is not null ? 1 : 0, procedureCount, host.SentMessages.SingleOrDefault(), result];
            long[] expected = test.GetProperty("state").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            int[] expectedEvents = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            string label = $"Thug message reaction case {caseIndex++}, input [{string.Join(',', input)}]";
            assert(actual.SequenceEqual(expected), $"{label}: actual [{string.Join(',', actual)}], expected [{string.Join(',', expected)}]");
            assert(host.Events.SequenceEqual(expectedEvents), $"{label}: events [{string.Join(',', host.Events)}], expected [{string.Join(',', expectedEvents)}]");
            if (host.StoredHandle is not null) assert(ReferenceEquals(host.StoredHandle, senderHandle), "Hostage ownership copies the original recoverable sender handle");

            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private sealed class Host : IThugMessageReactionHost
    {
        private int pathIndex;
        private int addIndex;
        public List<int> Events { get; } = [];
        public List<int> SentMessages { get; } = [];
        public FixedVector3 PlayerPosition { get; set; }
        public int PlayerUnavailableFlag { get; set; }
        public uint WaypointFlags { get; set; }
        public FixedVector3 SenderPosition { get; set; }
        public FixedVector3 Hit { get; set; }
        public bool LineOfSight { get; set; }
        public bool WriteHit { get; set; }
        public int[] PathCodes { get; set; } = [];
        public bool[] AddResults { get; set; } = [];
        public object? StoredHandle { get; private set; }
        public bool TryGetMessageSenderPosition(ThugMessage message, out FixedVector3 position)
        { position = SenderPosition; return message.SenderHandle is not null; }
        public bool MessageLineOfSight(FixedVector3 from, FixedVector3 to)
        { Events.AddRange([2, from.X, from.Y, from.Z, to.X, to.Y, to.Z, 0]); return LineOfSight; }
        public void StoreHostageHandle(object? senderHandle) => StoredHandle = senderHandle;
        public void TrySendToMessageSender(ThugMessage message, int code) => Events.AddRange([4, code]);
        public void SendSelfMessage(int code) => SentMessages.Add(code);
        public void Neutralize() => Events.Add(5);
        public bool AddPointToPath(FixedVector3 point, int distanceLimit)
        { Events.AddRange([6, point.X, point.Y, point.Z, distanceLimit]); return AddResults[Math.Min(addIndex++, 2)]; }
        public ActorPathCheckResult CheckMessagePath(FixedVector3 from, FixedVector3 to, bool wantHitPoint,
            int clearance, FixedVector3 initialHit)
        {
            Events.AddRange([7, from.X, from.Y, from.Z, to.X, to.Y, to.Z, wantHitPoint ? 1 : 0, clearance,
                initialHit.X, initialHit.Y, initialHit.Z]);
            return new(PathCodes[Math.Min(pathIndex++, 1)], wantHitPoint && WriteHit ? Hit : initialHit);
        }
    }
}
