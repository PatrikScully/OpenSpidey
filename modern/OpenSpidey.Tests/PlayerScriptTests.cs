using System.Reflection;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Core.Missions;

internal static class PlayerScriptTests
{
    public static void Run(Action<bool, string> assert)
    {
        using (Stream headingStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.heading-table.json")!)
        using (JsonDocument headingDocument = JsonDocument.Parse(headingStream))
        {
            int[] headings = Values(headingDocument.RootElement);
            for (int angle = 0; angle < 4096; angle++)
                assert(GameAngle.Heading(angle) == ((short)headings[angle * 2], (short)headings[angle * 2 + 1]),
                    $"Heading {angle} matches the recovered table initializer.");
        }
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-scripts.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int sequence = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            int[] words = Values(fixture.GetProperty("words"));
            PlayerScriptProgram program = Program(words);
            var host = new Host();
            var runtime = new PlayerScriptRuntime(program, host, fixture.GetProperty("initialElapsed").GetInt32());
            var buttons = new PlayerButtonState[20];
            int frameIndex = 0;
            foreach (JsonElement encodedFrame in fixture.GetProperty("frames").EnumerateArray())
            {
                int[] frame = Values(encodedFrame);
                host.Position = new(frame[1], frame[2], frame[3]);
                host.AnimationFinished = frame[4] != 0;
                host.AnimationSignal = frame[5] != 0;
                host.IsCrawling = frame[6] != 0;
                host.CameraHeading = (short)frame[7];
                buttons[14] = new((byte)frame[15], 0, 0);
                buttons[15] = new((byte)frame[16], 0, 0);
                PlayerAxes axes = default;
                if (runtime.Active)
                {
                    var environment = new PlayerScriptEnvironment(frame[8] != 0, frame[9] != 0, frame[10],
                        frame[11] != 0 ? new FixedVector3(frame[12], frame[13], frame[14]) : null);
                    PlayerScriptInput input = runtime.Update(frame[0], environment, buttons);
                    axes = input.Axes;
                    buttons = input.Buttons.ToArray();
                }
                int wordCursor = runtime.InstructionIndex < program.Instructions.Count
                    ? program.Instructions[runtime.InstructionIndex].SourceOffset / 2 : words.Length;
                var actual = new List<int> { runtime.Active ? 1 : 0, runtime.Decoding ? 1 : 0, runtime.ElapsedTicks,
                    wordCursor, runtime.CheckpointNode, runtime.PendingCommands,
                    host.Position.X, host.Position.Y, host.Position.Z, host.Velocity.X, host.Velocity.Y, host.Velocity.Z,
                    host.AnimationId, host.AnimationFinished ? 1 : 0, host.AnimationSignal ? 1 : 0, host.ActionState,
                    host.ScriptedActionValue, host.ScriptFlag ? 1 : 0, host.LevelStatus, axes.Forward, axes.Sideways,
                    host.IdleTimer, host.RawPlays, host.PolicyPlays, host.Teleports, host.Stands, host.VoiceCalls };
                foreach (PlayerButtonState button in buttons) actual.AddRange([button.Pressed, button.Triggered, button.Auxiliary]);
                int[] expected = Values(fixture.GetProperty("results")[frameIndex]);
                int mismatch = Enumerable.Range(0, actual.Count).Where(index => actual[index] != expected[index]).DefaultIfEmpty(-1).First();
                assert(actual.SequenceEqual(expected), $"Player-script sequence {sequence}, update {frameIndex} matches recovered VM; mismatch at {mismatch}.");
                frameIndex++;
            }
            sequence++;
        }
        CheckContracts(assert);
    }

    private static void CheckContracts(Action<bool, string> assert)
    {
        var host = new Host();
        var runtime = new PlayerScriptRuntime(Program([0, 3, 4, 0, -1, 6, 17, 0, 0, 255]), host);
        PlayerScriptInput first = runtime.Update(2, default);
        assert(first.Buttons[4] == new PlayerButtonState(1, 1, 0) && host.AnimationId == 17 && runtime.Active,
            "Zero-duration button and animation commands run together on their creation update.");
        PlayerScriptInput second = runtime.Update(2, default);
        assert(second.Buttons[4] == default && !runtime.Active && runtime.PendingCommands == 0,
            "Commands remain active at zero and expire only after their timer becomes negative.");

        host = new Host();
        runtime = new(Program([0, 9, 0, 0, 4, 1, 2, 3, 0, 255]), host);
        runtime.Update(2, default);
        runtime.Update(2, default);
        assert(runtime.Decoding && host.VoiceCalls == 0, "A completed blocking wait resumes decoding on the following update.");
        runtime.Update(2, default);
        assert(host.VoiceCalls == 1 && !runtime.Active, "Resumed decoding executes the following voice command once.");

        host = new Host { IsCrawling = true };
        runtime = new(Program([0, 17, 0, 255]), host);
        first = runtime.Update(6, default);
        second = runtime.Update(6, default);
        assert(first.Buttons[16].Pressed == 1 && second.Buttons[16] == default,
            "Crawling interaction synthesizes register 16 for one update.");

        host = new Host();
        runtime = new(Program([0, 16, 3, 0, 9, 100, 0, 255]), host);
        runtime.Update(2, default);
        runtime.Update(2, new(true, true, 1));
        assert(!runtime.Active && host.Position == host.TriggerPosition(3) && host.Teleports == 0 && host.LevelStatus == 7,
            "Aborting clears commands, requests the pending menu exit and restores checkpoint position without teleport effects.");

        host = new Host();
        runtime = new(Program([0, 255]), host);
        runtime.Update(2, new(false, false, 1795, new(-31900664, 0, 34009330)));
        assert(runtime.Active && !runtime.Decoding, "Idle input remains synthesized inside the recovered level-1795 boss area.");
        runtime.Update(2, new(false, false, 1795, new(-31900664 + 4915200, 0, 34009330)));
        assert(!runtime.Active, "The exact boss-area boundary releases synthesized input.");
        assert(GameWord.Absolute(int.MinValue) == int.MinValue, "Signed absolute value preserves the original minimum-word wrap.");

        assert(Rejects(() => Program([0, 3, 1])), "Truncated script operands cannot consume adjacent mission commands.");
        assert(Rejects(() => Program([0, 254, 0, 255])), "Unknown script layouts report a source error.");
        assert(Rejects(() => Program([0, 255, 0])), "Player script ownership ends exactly after Stop.");
        assert(Rejects(() => PlayerScriptProgram.Read([0])), "Partial signed words are rejected.");
        runtime = new(Program([0, 3, 14, 2, 0, 255]), new Host());
        assert(Rejects(() => runtime.Update(2, default)), "Scripts cannot synthesize unsupported Start/Select registers.");
        bool faulted = false;
        try { runtime.Update(2, default); }
        catch (InvalidOperationException) { faulted = true; }
        assert(faulted, "A failed script cannot replay its partially executed update.");

        PlayerScriptProgram? started = null;
        var scriptHandler = new PlayerScriptCommandHandler(program => started = program);
        using var missionData = new MemoryStream();
        using (var writer = new BinaryWriter(missionData, System.Text.Encoding.UTF8, leaveOpen: true))
            foreach (ushort word in new ushort[] { 4, 199, 0, 3, 10, 2, 0, 255, ushort.MaxValue }) writer.Write(word);
        var node = new TriggerNode(0, 4, 100, missionData.ToArray());
        var mission = new MissionRuntime(new TriggerFile([node]), new MissionRuntimeTests.ActorFactory(), [scriptHandler]);
        mission.QueueAutoexec();
        assert(mission.ExecutePending() == 1 && started?.Instructions[0].SourceOffset == 104,
            "Mission command 199 transfers a bounded script with absolute source coordinates to the player controller.");
        runtime = new(started!, new Host());
        assert(runtime.Update(2, default).Axes == new PlayerAxes(-127, 0),
            "A mission-started player script publishes synthesized movement through the managed VM.");
        runtime = new(Program([0, 3, 10, 8, 0, 255]), new Host());
        runtime.Update(2, default);
        runtime.ReplaceProgram(Program([0, 3, 8, 4, 0, 255]));
        assert(runtime.Update(2, default).Axes == new PlayerAxes(-127, -127) && runtime.PendingCommands == 2,
            "Replacing the source bytecode retains its existing concurrent input commands.");
        runtime = new(Program([0, 9, 0, 100, 255]), new Host());
        runtime.Update(2, default);
        runtime.Update(2, default);
        int previousElapsed = runtime.ElapsedTicks;
        runtime.ReplaceProgram(Program([0, 255]));
        assert(runtime.ElapsedTicks == previousElapsed && runtime.CheckpointNode == 0 && runtime.InstructionIndex == 0,
            "A new player script preserves the source elapsed clock while resetting its cursor and checkpoint.");
        PlayerButtonState[] pad = new PlayerButtonState[20];
        pad[6] = new(1, 1, 0); pad[7] = new(1, 0, 0); pad[16] = new(1, 1, 0);
        PlayerControlFrame mapped = PlayerScriptControlMapper.Map(new(new(-127, 50), pad));
        assert(mapped.Movement.Scripted == new PlayerAxes(-127, 50) &&
            mapped.Buttons.Held == (PlayerButton.Zip | PlayerButton.Swing | PlayerButton.Jump) &&
            mapped.Buttons.Triggered == (PlayerButton.Zip | PlayerButton.Jump),
            "The source's straight-zip, swing and jump pad slots feed the corresponding modern controls.");
    }

    private static bool Rejects(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return true; }
        return false;
    }

    private static int[] Values(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static PlayerScriptProgram Program(int[] words)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            foreach (int word in words) writer.Write((short)word);
        return PlayerScriptProgram.Read(stream.ToArray());
    }

    private sealed class Host : IPlayerScriptHost
    {
        public FixedVector3 Position { get; set; }
        public FixedVector3 Velocity { get; set; }
        public short CameraHeading { get; set; }
        public int AnimationId { get; private set; }
        public bool AnimationFinished { get; set; }
        public bool AnimationSignal { get; set; }
        public bool IsCrawling { get; set; }
        public int ScriptedActionValue { get; set; }
        public int ActionState { get; private set; }
        public bool ScriptFlag { get; private set; }
        public int LevelStatus { get; private set; }
        public int IdleTimer { get; private set; } = 99;
        public int RawPlays { get; private set; }
        public int PolicyPlays { get; private set; }
        public int Teleports { get; private set; }
        public int Stands { get; private set; }
        public int VoiceCalls { get; private set; }
        public FixedVector3 TriggerPosition(int nodeId) => new(unchecked(nodeId * 409600), (nodeId % 3) * 409600, unchecked(-nodeId * 819200));
        public void ResetIdleTimer() => IdleTimer = 0;
        public void TeleportToNode(int nodeId) { Position = TriggerPosition(nodeId); Teleports++; }
        public void PlayVoice(short bank, short track, short duration) => VoiceCalls++;
        public void PlayAnimation(int animationId, ScriptAnimationPolicy policy)
        {
            AnimationId = animationId;
            AnimationFinished = false;
            if (policy == ScriptAnimationPolicy.PreserveSoundScript) RawPlays++; else PolicyPlays++;
        }
        public void Stand() { ActionState = 1; Stands++; }
        public void EnterDelayedStandState() => ActionState = 0x40000000;
        public void SetScriptFlag(bool enabled) => ScriptFlag = enabled;
        public void RequestMenuExit() => LevelStatus = 7;
    }
}
