using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

internal static class MissionStoryCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(MissionStoryCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-story-commands.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0, hits = 0, chopperCalls = 0, venomCalls = 0, reinterpreted = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            JsonElement input = fixture.GetProperty("input"), expected = fixture.GetProperty("expected");
            var nodes = new List<TriggerNode>();
            foreach (JsonElement node in input.GetProperty("nodes").EnumerateArray())
            {
                int offset = node[0].GetInt32();
                byte[] bytes = node[1].EnumerateArray().Select(value => (byte)value.GetInt32()).ToArray();
                nodes.Add(new((ushort)nodes.Count, (ushort)(bytes[0] | bytes[1] << 8), offset, bytes));
            }
            var file = new TriggerFile(nodes);
            var host = new Host(input);
            var reverb = new SourceReverbState();
            var camera = input.GetProperty("camera").GetInt32() != 0 ? new CameraModeState { ItemHidingEnabled = 0x77 } : null;
            var brightness = new PlayerBrightnessState(Light());
            short reference = 0x6666;
            IMissionCommandHandler[] handlers =
            [
                new MissionActorDeathCommandHandler(11, file, host), MissionActorDeathCommandHandler.CreateLoudHandler(file, host),
                .. ChopperCommandHandler.CreateHandlers(file, host), .. VenomWaitCommandHandler.CreateHandlers(host),
                new ReverbTypeCommandHandler(reverb),
                new CameraAngleCommandHandler((heading, duration) => host.Events.Add([7, heading, duration]), () => camera is not null),
                new CameraLineOfSightCommandHandler(() => camera), new PlayerLightBrightnessCommandHandler(brightness),
                new PlayerMotionAngleOffsetCommandHandler(value => reference = value), new EndLevelNodeCommandHandler(host)
            ];
            var runtime = new MissionRuntime(file, new NoActors(), handlers);
            var completed = new List<ushort>();
            runtime.Trace += trace => { if (trace.Kind == MissionTraceKind.CommandCompleted && trace.Opcode is { } opcode) completed.Add(opcode); };
            bool rejected = false;
            runtime.Queue(0);
            try { runtime.ExecutePending(); }
            catch (InvalidOperationException) { rejected = true; }

            bool reinterpretation = input.GetProperty("reinterpreted").GetBoolean();
            reinterpreted += reinterpretation ? 1 : 0;
            var wanted = expected.GetProperty("events").EnumerateArray()
                .Select(call => call.EnumerateArray().Select(value => value.GetInt64()).ToArray()).ToArray();
            hits += wanted.Count(call => call[0] is 1 or 8);
            chopperCalls += wanted.Count(call => call[0] is >= 2 and <= 4);
            venomCalls += wanted.Count(call => call[0] is 5 or 6);
            assert(host.Events.Count == wanted.Length && host.Events.Zip(wanted).All(pair => pair.First.SequenceEqual(pair.Second)),
                $"Whole original PC story-command case{index} makes the same Die/Hit, chopper, Venom, camera and ENV calls in order.");
            assert(rejected == reinterpretation && (reinterpretation ||
                    host.EndNode == expected.GetProperty("endNode").GetInt32() && completed[^1] == (ushort)MissionOpcode.EndLevelNode),
                $"Whole original PC story-command case{index} consumes every operand, and refuses221's Venom-less operand reinterpretation.");
            int[][] bodies = expected.GetProperty("bodies").EnumerateArray()
                .Select(body => body.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray();
            assert(host.Bodies.Select(body => body.Fields).Zip(bodies).All(pair => pair.First.SequenceEqual(pair.Second)) &&
                host.Specials.Select(special => special.Killed ? 1 : 0).SequenceEqual(expected.GetProperty("specials").EnumerateArray().Select(value => value.GetInt32())) &&
                host.ItemFlags.SequenceEqual(expected.GetProperty("flags").EnumerateArray().Select(value => value.GetInt32())),
                $"Whole original PC story-command case{index} writes chopper fields 218/1F4/348/350/358, special-display and ENV flags.");
            int light = expected.GetProperty("light").GetInt32(), sight = expected.GetProperty("lineOfSight").GetInt32();
            int reverbByte = expected.GetProperty("reverb").GetInt32();
            bool lightWritten = completed.Contains((ushort)MissionOpcode.SetPlayerLightBrightness);
            bool reverbWritten = completed.Contains((ushort)MissionOpcode.SetReverbType);
            assert((lightWritten ? brightness.LightSurfaceTarget == (uint)light : light == 0x66666666) &&
                reference == expected.GetProperty("reference").GetInt16() &&
                (camera is null ? sight == -1 : camera.ItemHidingEnabled == sight) &&
                (reverbWritten ? (reverb.Enabled ? 1 : 0) == reverbByte : reverbByte == 0xA5),
                $"Whole original PC story-command case{index++} stores player578/E34, camera F9 and the reverb byte exactly.");
        }
        assert(index == 600 && hits > 40 && chopperCalls > 100 && venomCalls > 40 && reinterpreted > 10,
            $"The PC story-command fixture covers loud hits ({hits}), chopper ({chopperCalls}) and Venom ({venomCalls}) calls and {reinterpreted} reinterpretations.");

        var bare = new TriggerNode(0, 4, 100, new byte[] { 4, 0, 255, 255 });
        var empty = new TriggerFile([bare]);
        var none = new Host(default);
        foreach (var (handler, wrong) in new (IMissionCommandHandler, byte[])[]
        {
            (new ChopperCommandHandler(MissionOpcode.ChopperContinue, empty, none), new byte[2]),
            (new ChopperCommandHandler(MissionOpcode.ChopperSetFlag, empty, none), new byte[2]),
            (new ChopperCommandHandler(MissionOpcode.SetChopperAngle, empty, none), new byte[4]),
            (new VenomWaitCommandHandler(MissionOpcode.VenomEnterWaitState, none), new byte[2]),
            (new VenomWaitCommandHandler(MissionOpcode.VenomExitWaitState, none), new byte[2]),
            (new ReverbTypeCommandHandler(new()), new byte[4]),
            (new CameraAngleCommandHandler((_, _) => { }, () => true), new byte[2]),
            (new CameraLineOfSightCommandHandler(() => new()), new byte[0]),
            (new PlayerLightBrightnessCommandHandler(new(Light())), new byte[2]),
            (new PlayerMotionAngleOffsetCommandHandler(_ => { }), new byte[4]),
            (MissionActorDeathCommandHandler.CreateLoudHandler(empty, none), new byte[2])
        })
        {
            bool rejected = false;
            try { handler.Execute(new(bare, 0), new(handler.Opcode, 102, wrong)); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected && none.Events.Count == 0, $"Command{handler.Opcode} accepts only its exact original operand encoding.");
        }
        bool missingCamera = false;
        try { new CameraLineOfSightCommandHandler(() => null).Execute(new(bare, 0), new(217, 102, new byte[2])); }
        catch (InvalidOperationException) { missingCamera = true; }
        assert(missingCamera, "Command217 never invents a camera for the PC's unchecked camera-head write.");
    }

    private static SourceModelLight Light() =>
        new(new(-2047, -2896, -2047, 0, 0, 4096, 0, 0, -4096, 0, 0, 0), new(1520, 2000, 1840, 1440, 1920, 1760, 1440, 1920, 1760, 0, 0, 0),
            1760, 1600, 1600);

    private sealed class NoActors : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("Story commands never construct an actor.");
    }

    private sealed class Body(int id, SourceMissionDeathList list, ushort type, ushort node, ushort health, List<long[]> events)
        : IMissionChopper, IMissionVenom
    {
        public int Id => id;
        public SourceMissionDeathList List => list;
        public ushort Type => type;
        public ushort Node => node;
        public ushort Health => health;
        public int[] Fields { get; } = [0x2468000 + 0x218, 0x2468000 + 0x1F4, 0x2468000 + 0x348, 0x2468000 + 0x350, 0x2468000 + 0x358];
        public int Flags218 { get => Fields[0]; set => Fields[0] = value; }
        public int Waypoint1F4 { set => Fields[1] = value; }
        public int Velocity348 { set => Fields[2] = value; }
        public int MinimumHeight350 { set => Fields[3] = value; }
        public int TargetAngle358 { set => Fields[4] = value; }
        public void FireMissileAtWaypoint(uint waypoint) => events.Add([2, id, waypoint]);
        public void FireMachineGunAtWaypoint(uint waypoint, uint count) => events.Add([3, id, waypoint, count]);
        public void SetFlag(ushort flag, short value) => events.Add([4, id, flag, value]);
        public void EnterWaitState() => events.Add([5, id]);
        public void ExitWaitState(uint first, uint second) => events.Add([6, id, first, second]);
    }

    private sealed class Special(int kind, ushort node)
    {
        public int Kind => kind;
        public ushort Node => node;
        public bool Killed { get; set; }
    }

    private sealed class Host : IMissionActorLoudDeathHost, IMissionBaddyLookup, IMissionEndLevelNodeHost
    {
        public List<long[]> Events { get; } = [];
        public List<Body> Bodies { get; } = [];
        public List<Special> Specials { get; } = [];
        public List<uint> Items { get; } = [];
        public List<int> ItemFlags { get; } = [];
        public int EndNode { get; private set; } = -234;

        public Host(JsonElement input)
        {
            if (input.ValueKind == JsonValueKind.Undefined) return;
            foreach (JsonElement body in input.GetProperty("bodies").EnumerateArray())
                Bodies.Add(new(Bodies.Count, (SourceMissionDeathList)body[0].GetInt32(), (ushort)body[1].GetInt32(), (ushort)body[2].GetInt32(),
                    (ushort)body[3].GetInt32(), Events));
            foreach (JsonElement special in input.GetProperty("specials").EnumerateArray())
                Specials.Add(new(special[0].GetInt32(), (ushort)special[1].GetInt32()));
            foreach (JsonElement item in input.GetProperty("items").EnumerateArray()) { Items.Add(item.GetUInt32()); ItemFlags.Add(0); }
        }

        public void SetEndLevelNode(ushort node) => EndNode = node;
        public object? FindBaddyOfType(ushort type) =>
            Bodies.FirstOrDefault(body => body.List == SourceMissionDeathList.Baddy && body.Type == type);
        public object? ReadHead(SourceMissionDeathList list) => Bodies.FirstOrDefault(body => body.List == list);
        public object? ReadNext(SourceMissionDeathList list, object body) =>
            Bodies.Skip(((Body)body).Id + 1).FirstOrDefault(candidate => candidate.List == list);
        public ushort ReadNode(object body) => ((Body)body).Node;
        public ushort ReadHealth(object body) => ((Body)body).Health;
        public bool IsDead(object body) => throw new InvalidOperationException("SendKill never tests IsDead.");
        public FixedVector3 ReadPosition(object body) => throw new InvalidOperationException("SendKill never reads a position.");
        public void Die(object body) => Events.Add([0, ((Body)body).Id]);
        public void Hit(object body, ActorHit hit) =>
            Events.Add([1, ((Body)body).Id, hit.Flags, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z]);
        public void DeleteSuspendedBody(object body) => throw new InvalidOperationException("SendKill never deletes suspended bodies.");
        public void HideEnvironmentItem(uint checksum)
        {
            int item = Items.IndexOf(checksum);
            if (item >= 0) ItemFlags[item] |= 1;
        }
        public void HitEnvironmentItem(uint checksum, ushort damage)
        {
            int item = Items.IndexOf(checksum);
            if (item >= 0) Events.Add([8, item, 0, damage]);
        }
        public void KillSpecialDisplay(ushort node)
        {
            foreach (Special special in Specials)
                if (special.Kind == 9 && special.Node == node) special.Killed = true;
        }
    }
}
