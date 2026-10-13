using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Scripting;

internal static class ActorWaypointTests
{
    public static void Run(Action<bool, string> assert)
    {
        var file = new TriggerFile([Node(0, 1, [1, 3]), Node(1, 1000, [2]), Node(2, 12, [3]), Node(3, 1002, [])]);
        using Stream stream = typeof(ActorWaypointTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-waypoints.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int parsed = 0, last = -1;
            ActorWaypointRoute route = null!;
            route = new(file, test.GetProperty("initial").GetInt32(), cursor =>
            {
                parsed++;
                last = cursor.SourceOffset == ScriptOffset(file.Nodes[1]) ? 1 : 3;
                while (cursor.PeekWord() != 0x4100)
                    if (!route.TryExecuteCommand(cursor.ReadWord(), cursor)) throw new Exception("Waypoint command was not recognized.");
            }) { Flags = test.GetProperty("flags").GetUInt32(), Parameter = test.GetProperty("parameter").GetUInt16() };
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                bool advanced = route.Advance();
                long[] actual = [advanced ? 1 : 0, route.CurrentNode, route.Flags, route.Parameter, parsed, last];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    "Actor waypoint first-link traversal, script kinds, flag/parameter reset and terminal behavior match recovered GetNextWaypoint.");
            }
        }
        var host = new ActorScriptTests.Host(actorType: 312);
        var runtime = new ActorScriptRuntime(ActorScriptTests.Words(0x4104, 0, 0x2120, 0, 7, 0x4100), host);
        runtime.Start();
        runtime.ParseAt(ActorScriptTests.Words(0x4101, 0, 0x4100));
        assert(!host.ScriptState.Active && host.Variables.Registers[0] == 7,
            "External waypoint parsing retains labels and does not reactivate an actor script that already stopped.");
        runtime.ParseAt(ActorScriptTests.Words(0x2120, 0, 8, 0x4100));
        assert(host.Variables.Registers[0] == 8 && runtime.SourcePosition == 6,
            "A new waypoint cursor executes immediately at its source address without recreating the actor or variable bank.");
    }

    public static void VerifyFirstLevel(TriggerFile file, Action<bool, string> assert)
    {
        foreach (int actor in new[] { 9, 10, 16, 19 })
        {
            ActorWaypointRoute route = null!;
            var host = new Host(() => route);
            var definition = TriggerActorDefinition.Read(file.Nodes[actor]);
            var runtime = new ActorScriptRuntime(new(definition.BehaviorData,
                file.Nodes[actor].SourceOffset + definition.ObjectDataOffset + 18), host);
            route = new(file, actor, cursor => runtime.ParseAt(cursor));
            runtime.Start();
            assert(host.Variables.Health == 90 && host.Variables.Damage == 10 && !host.ScriptState.Active,
                "Original first-level thug constructor script initializes health/damage before waypoint behavior.");
            assert(route.Advance() && route.PauseAtNextPoint && route.Position() != default,
                "Original first-level thug first-link waypoint resolves its placement and runs its real pause script.");
            int previous = route.CurrentNode;
            bool advanced = route.Advance();
            assert(actor == 16 ? !advanced && route.CurrentNode == previous : advanced && route.PauseAtNextPoint,
                "Original thug routes preserve terminal endpoints and linked patrol scripts.");
        }
    }
    private static int ScriptOffset(TriggerNode node) => TriggerPlacement.Read(node).ReadScriptAfterAngles(node).SourceOffset;
    private static TriggerNode Node(ushort id, ushort kind, ushort[] links)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(kind);
        if (kind == 1) { writer.Write((short)312); writer.Write((ushort)0); }
        if (kind == 1002) writer.Write((ushort)0);
        writer.Write((ushort)links.Length);
        foreach (ushort link in links) writer.Write(link);
        if (kind == 1) writer.Write((byte)255);
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write(1); writer.Write(2); writer.Write(3);
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)0);
        writer.Write((ushort)0x4703); writer.Write((ushort)0x4707); writer.Write((ushort)123); writer.Write((ushort)0x4100);
        return new(id, kind, id * 64, stream.ToArray());
    }
    private sealed class Host(Func<ActorWaypointRoute> route) : IActorScriptHost
    {
        private readonly ActorScriptTests.Host common = new(actorType: 312);
        public ActorScriptState ScriptState => common.ScriptState;
        public ActorScriptVariables Variables => common.Variables;
        public short ReadVariable(ushort variable, ActorScriptCursor operands) => common.ReadVariable(variable, operands);
        public void WriteVariable(ushort variable, ActorScriptCursor operands) => common.WriteVariable(variable, operands);
        public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
        {
            if (route().TryExecuteCommand(opcode, operands)) return true;
            throw new NotSupportedException($"Waypoint effect {opcode:X4} is not ported.");
        }
        public ActorScriptCursor ScriptAtNode(ushort id) => common.ScriptAtNode(id);
        public bool PlayerCeilingCheck(short x, short z) => false;
        public void FlashCompass() => common.FlashCompass();
        public void SendDeathPulse() => common.SendDeathPulse();
        public void Retire() => common.Retire();
    }
}
