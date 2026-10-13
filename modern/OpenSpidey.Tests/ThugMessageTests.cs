using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugMessageTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugMessageTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-messages.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] v = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                State = (int)v[1], Substate = (int)v[2], CombatFlags = (uint)v[4],
                YankOscillationTicks = (int)v[6], AlertTicks = (int)v[9], ActionCounter = (int)v[10]
            };
            state.SetThugType((int)v[0]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            { Position = new(123456, (int)v[8], -123456), Angles = new((short)v[7], 0, 0) });
            var procedures = new ActorProcedureList();
            var messages = new ThugMessageQueue();
            var host = new Host(messages, actor)
            {
                ItemFlags = (ushort)v[3], WaypointFlags = (uint)v[5], HasJoints = v[14] != 0,
                ReadinessConsumes = v[15] != 0, IsCurrentThugListOwner = v[16] != 0
            };
            host.Rolls[1] = (short)v[11];
            host.Rolls[3] = (short)v[12];
            host.Rolls[state.Type == 304 ? 7 : 6] = (short)v[13];
            foreach (JsonElement row in test.GetProperty("inputMessages").EnumerateArray())
            {
                int[] values = row.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                messages.Enqueue(new(values[0], values[3] == 0 ? null : values[3])
                { Flags = values[1], Age = values[2], Vector0 = new(-40, 50, -60) });
            }
            for (int tick = 0; tick < v[17]; tick++) ThugMessageProcessor.Process(state, actor, procedures, messages, host);
            long[] actual = [state.State, state.Substate, host.ItemFlags, state.CombatFlags, state.YankOscillationTicks,
                actor.Body.Angles.X, actor.Body.Position.Y, state.AlertTicks, state.ActionCounter,
                host.Rolls[1], host.Rolls[3], host.Rolls[state.Type == 304 ? 7 : 6], host.HasJoints ? 1 : 0,
                host.IsCurrentThugListOwner ? 1 : 0, procedures.Count];
            long[] expected = test.GetProperty("state").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            int[] expectedEvents = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var remaining = new List<int>();
            for (ThugMessage? message = messages.Head; message is not null; message = message.Next)
                remaining.AddRange([message.Code, message.Flags, message.Age]);
            int[] expectedMessages = test.GetProperty("messages").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            string label = $"Thug messages case {index++}, input [{string.Join(',', v)}]";
            assert(actual.SequenceEqual(expected), $"{label}: actual [{string.Join(',', actual)}], expected [{string.Join(',', expected)}]");
            assert(host.Events.SequenceEqual(expectedEvents), $"{label}: events [{string.Join(',', host.Events)}], expected [{string.Join(',', expectedEvents)}]");
            assert(remaining.SequenceEqual(expectedMessages), $"{label}: queue [{string.Join(',', remaining)}], expected [{string.Join(',', expectedMessages)}]");
        }
    }

    private sealed class Host(ThugMessageQueue queue, ActorProcedureState actor) : IThugMessageHost
    {
        public List<int> Events { get; } = [];
        public short[] Rolls { get; } = new short[8];
        public ushort ItemFlags { get; set; }
        public uint WaypointFlags { get; set; }
        public bool HasJoints { get; set; }
        public bool ReadinessConsumes { get; set; }
        public bool IsCurrentThugListOwner { get; set; }
        public short GetJointRoll(int index) => Rolls[index];
        public void SetJointRoll(int index, short roll) => Rolls[index] = roll;
        public void ApplyMessagePose(int actorType) { Events.AddRange([2, actorType]); HasJoints = true; }
        public void TrySendToMessageSender(ThugMessage message, int code)
        {
            if (message.SenderHandle is null) return;
            Events.AddRange([3, code]);
            if ((int)message.SenderHandle == 2) queue.Enqueue(new(code));
        }
        public void ReplyToStoredHostage(int code) => Events.AddRange([5, code]);
        public void GetReadyToShootHostage(ThugMessage message)
        { Events.Add(4); if (ReadinessConsumes) message.Flags |= 1; }
        public bool TryGetMessageSenderPosition(ThugMessage message, out FixedVector3 position)
        {
            position = message.SenderHandle is int handle && handle == 2 ? actor.Body.Position : new(-10, 20, -30);
            return message.SenderHandle is not null;
        }
        public void RunToWhereTheActionIs(FixedVector3 position) => Events.AddRange([1, position.X, position.Y, position.Z]);
        public void DeleteLaser() => Events.Add(6);
        public void ClearThugListOwner() { Events.Add(7); IsCurrentThugListOwner = false; }
        public void BurstTrapWeb() => Events.Add(8);
        public void BurstTugWeb() => Events.Add(9);
        public void PlayMessageSound(uint sound, FixedVector3 position, int pitchOffset) =>
            Events.AddRange([10, (int)sound, position.X, position.Y, position.Z, pitchOffset]);
        public void SendSelfMessage(int code) => queue.Enqueue(new(code));
        public void Neutralize() => Events.Add(11);
    }
}
