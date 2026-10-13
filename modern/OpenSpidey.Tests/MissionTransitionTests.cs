using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Missions;

internal static class MissionTransitionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionTransitionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.mission-transition.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ushort opcode = test.GetProperty("opcode").GetUInt16();
            ushort operand = test.GetProperty("operand").GetUInt16();
            var host = new TransitionHost(test.GetProperty("mission").GetString()!,
                (LevelExitReason)test.GetProperty("status").GetInt32(), test.GetProperty("player").GetInt32() != 0);
            byte[] arguments = opcode == (ushort)MissionOpcode.LoadMission
                ? Encoding.Latin1.GetBytes(test.GetProperty("next").GetString()! + '\0')
                : opcode == (ushort)MissionOpcode.EndLevel ? [] : new byte[2];
            if (arguments.Length == 2) BinaryPrimitives.WriteUInt16LittleEndian(arguments, operand);
            var node = new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty);
            var instruction = new TriggerInstruction(opcode, 0, arguments);
            JsonElement expected = test.GetProperty("expected");
            if (opcode is 105 or 106)
                VerifySound(opcode, instruction, expected, node, assert);
            else if (opcode == (ushort)MissionOpcode.SetActorSuspensionDistance)
                new ActorSuspensionDistanceCommandHandler(value => host.Distance = value).Execute(new(node, 0), instruction);
            else
                new LevelTransitionCommandHandler((MissionOpcode)opcode, host).Execute(new(node, 0), instruction);
            string mission = Encoding.Latin1.GetString(expected.GetProperty("missionBytes").EnumerateArray().Select(value => value.GetByte()).ToArray());
            assert((int)host.ExitReason == expected.GetProperty("status").GetInt32() && host.MissionName == mission &&
                host.EndNode == expected.GetProperty("endNode").GetInt32() && host.Distance == expected.GetProperty("distance").GetInt32(),
                "Mission status, ASCII-only name comparison, nine-byte copy, unsigned completion node and suspension distance match actual PC command execution.");
            assert(host.Saved.SequenceEqual(expected.GetProperty("saved").EnumerateArray().Select(value => value.GetInt32())) &&
                host.Killed == expected.GetProperty("killed").GetInt32(),
                "Same-mission/no-player carry restoration and scene teardown follow the original LoadNewTrg branch gates.");
            if (host.Killed != 0)
                assert(host.MissionAtKill == host.MissionName && host.ExitAtKill == host.ExitReason && host.SavedAtKill!.SequenceEqual(host.Saved),
                    "Original scene kill observes the already selected mission, new exit status and preserved carried-player save fields.");
        }
        var earlyKill = new TransitionHost("L1A1_T", LevelExitReason.Running, true);
        var context = new MissionCommandContext(new TriggerNode(224, 6, 0x27D2, ReadOnlyMemory<byte>.Empty), 1);
        new LevelTransitionCommandHandler(MissionOpcode.KillSceneObjects, earlyKill).Execute(context, new(305, 0x27DC, ReadOnlyMemory<byte>.Empty));
        assert(earlyKill.Killed == 1 && earlyKill.ExitAtKill == LevelExitReason.Running &&
            earlyKill.MissionAtKill == "L1A1_T" && earlyKill.SavedAtKill!.SequenceEqual(new[] { 100, 200, 300, 4, 5 }),
            "Actual first-level305 destroys the current source bodies before selecting another status, mission or carried state.");
        var cleanup = new CleanupHost();
        foreach (IMissionCommandHandler handler in MissionResourceCleanupCommandHandler.CreateHandlers(cleanup))
            handler.Execute(context, new(handler.Opcode, 0, ReadOnlyMemory<byte>.Empty));
        assert(cleanup.Calls.SequenceEqual(new[] { 110, 162 }),
            "Authored first-level110/162 release scene banks and code modules through their separate real owners in source order.");
    }

    private static void VerifySound(ushort opcode, TriggerInstruction instruction, JsonElement expected,
        TriggerNode node, Action<bool, string> assert)
    {
        SoundDefinitionBank bank = new(Enumerable.Range(0, 8).Select(index =>
            new SoundDefinition(1u | (uint)index << 16, 60u | 4096u << 16, 1024, 80)).ToArray());
        var actual = new SoundChannelController(new Playback()) { PlayerBank = bank, LevelBank = bank };
        var reference = new SoundChannelController(new Playback()) { PlayerBank = bank, LevelBank = bank };
        actual.Play(1); reference.Play(1);
        new SoundCommandHandler((MissionOpcode)opcode, actual).Execute(new(node, 0), instruction);
        int[] source = expected.GetProperty("audio").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        assert(source[0] == opcode, "Sound mission dispatch selects the same SFX leaf as actual PC execution.");
        if (source[0] == 105)
        {
            assert(source[3] == 0, "Original trigger PlaySound supplies pitch offset zero.");
            reference.Play((uint)source[1], (short)source[2]);
        }
        else reference.Stop((uint)source[1]);
        assert(actual.AllocatedCount == reference.AllocatedCount &&
            Enumerable.Range(0, 32).All(channel => actual.ReadChannel(channel) == reference.ReadChannel(channel)),
            "Mission sound-word parsing, global volume0x2000 and unsigned stop handles compose the verified shared SFX controller with actual PC leaf arguments.");
    }

    private sealed class TransitionHost(string mission, LevelExitReason exit, bool hasPlayer) : ILevelTransitionHost
    {
        public string MissionName { get; private set; } = mission;
        public LevelExitReason ExitReason { get; private set; } = exit;
        public bool HasPlayer => hasPlayer;
        public int EndNode { get; private set; } = 0x12345678;
        public int Distance { get; set; } = 0x23456789;
        public int[] Saved { get; private set; } = [100, 200, 300, 4, 5];
        public int Killed { get; private set; }
        public string? MissionAtKill { get; private set; }
        public LevelExitReason ExitAtKill { get; private set; }
        public int[]? SavedAtKill { get; private set; }
        public void PreserveCarriedPlayerState() => Saved = [17, 123, -456, 170, 187];
        public void SetExitReason(LevelExitReason reason) => ExitReason = reason;
        public void SetMissionName(string name) => MissionName = name;
        public void SetEndLevelNode(ushort node) => EndNode = node;
        public void KillAllSceneObjects()
        {
            Killed++; MissionAtKill = MissionName; ExitAtKill = ExitReason; SavedAtKill = Saved.ToArray();
        }
    }

    private sealed class Playback : ISoundChannelPlayback
    {
        private readonly bool[] playing = new bool[32];
        public bool IsPlaying(int channel) => playing[channel];
        public void Play(int channel, SoundDefinition definition, bool levelSpecific, SoundMix mix) => playing[channel] = true;
        public void UpdateMix(int channel, SoundMix mix) { }
        public void Stop(int channel) => playing[channel] = false;
    }
    private sealed class CleanupHost : IMissionResourceCleanupHost
    {
        public List<int> Calls { get; } = [];
        public void ClearSceneLibraries() => Calls.Add(110);
        public void ClearCodeModules() => Calls.Add(162);
    }
}
