using System.Buffers.Binary;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;

internal static class RenderEnvironmentCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        foreach (int sourceOffset in new[] { 100, 102 })
        {
            byte[] bytes = new byte[32];
            int cursor = 0;
            WriteWord(4);
            WriteWord(104); WriteWord(10); WriteWord(8000); WriteWord(4096);
            WriteWord(171);
            while (((sourceOffset + cursor) & 3) != 0) cursor++;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), 0xDE834642); cursor += 4;
            WriteWord(0x8000); WriteWord(90); WriteWord(0x7FFF);
            WriteWord(202); WriteWord(0x00FF); WriteWord(0xFD9C);
            WriteWord(65535);
            var node = new TriggerNode(0, 4, sourceOffset, bytes.AsMemory(0, cursor));
            var host = new Host();
            var runtime = new MissionRuntime(new([node]), new MissionRuntimeTests.ActorFactory(), RenderEnvironmentCommandHandler.CreateHandlers(host));
            runtime.CommandListCompleted += _ => host.EndCommandList();
            runtime.QueueAutoexec();
            assert(runtime.ExecutePending() == 1 && host.FlushCount == 1 && host.Fog == (10, 8000, 4096),
                "Fog is flushed once after the complete original command list.");
            assert(host.Backgrounds.Single() == (0xDE834642U, new ActorAngles(short.MinValue, 90, short.MaxValue)) && host.Sky == 0x00FFFD9C,
                "Background operands retain file-relative four-byte alignment, signed axes and source high/low colour words.");
            void WriteWord(ushort value) { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor), value); cursor += 2; }
        }

        byte[] blockedProgram = [4, 0, 104, 0, 10, 0, 64, 31, 0, 16, 160, 0, 0, 0, 200, 0, 255, 2, 255, 255, 255, 255];
        var blockedHost = new Host();
        var blocker = new BlockingHandler();
        var blocked = new MissionRuntime(new([new(0, 4, 0, blockedProgram)]), new MissionRuntimeTests.ActorFactory(),
            RenderEnvironmentCommandHandler.CreateHandlers(blockedHost).Append(blocker));
        blocked.CommandListCompleted += _ => blockedHost.EndCommandList();
        blocked.QueueAutoexec();
        assert(blocked.ExecutePending() == 0 && blockedHost.FlushCount == 0 && blocked.IsBlocked,
            "An asynchronous command preserves pending fog until its original command list resumes and completes.");
        blocker.IsBlocking = false;
        assert(blocked.ExecutePending() == 1 && blockedHost.FlushCount == 1 && blockedHost.FlushedFade == 0x02FFFFFF,
            "The final fade colour is available when deferred fog commits after the blocking command.");
        var bad = new TriggerInstruction(104, 22, new byte[4]);
        try
        {
            new RenderEnvironmentCommandHandler(MissionOpcode.SetFoggingParameters, new Host()).Execute(new(new(0, 4, 0, Array.Empty<byte>()), 0), bad);
            assert(false, "Truncated render arguments must fail before a world callback.");
        }
        catch (InvalidDataException) { assert(true, "Malformed render commands report their source coordinates."); }
    }

    public static void RunOriginalAssets(PkrArchive archive, Action<bool, string> assert)
    {
        if (!archive.Contains("data/L1A1_T.trg")) return;
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
        var host = new Host();
        var handlers = RenderEnvironmentCommandHandler.CreateHandlers(host).ToDictionary(handler => handler.Opcode);
        foreach (TriggerInstruction command in TriggerCommandReader.Read(triggers.Nodes[14]))
            if (handlers.TryGetValue(command.Opcode, out IMissionCommandHandler? handler)) handler.Execute(new(triggers.Nodes[14], 0), command);
        host.EndCommandList();
        assert(host.Backgrounds.Select(background => background.Checksum).SequenceEqual(new uint[] { 0xDE834642, 0x160FC566 }) &&
            host.Backgrounds.All(background => background.Angles == new ActorAngles(0, 90, 0)),
            "Original L1A1 restart14 creates the exact two authored OBJ backdrops with their untouched constructor arguments.");
        assert(host.Fog == (10, 8000, 4096) && host.Sky == 0x00FFFD9C && host.FlushedFade == 0x02FFFFFF,
            "Original restart14 provides its genuine sky, white fade and deferred fog parameters.");
    }

    private sealed class BlockingHandler : IMissionBlockingCommandHandler
    {
        public ushort Opcode => 160;
        public bool IsBlocking { get; set; } = true;
        public void Execute(MissionCommandContext context, TriggerInstruction instruction) { }
    }
    private sealed class Host : IRenderEnvironmentHost
    {
        public (int Duration, int Near, int Range) Fog;
        public uint Sky, Fade, FlushedFade;
        public int FlushCount;
        public List<(uint Checksum, ActorAngles Angles)> Backgrounds { get; } = [];
        public void SetFog(ushort duration, ushort near, ushort range) => Fog = (duration, near, range);
        public void SetSkyColour(uint packed) => Sky = packed;
        public void SetFadeColour(uint packed) => Fade = packed;
        public void CreateBackground(uint checksum, ActorAngles angularVelocity) => Backgrounds.Add((checksum, angularVelocity));
        public void SetBackgroundVisible(ushort index, bool visible) => throw new InvalidOperationException("This fixture contains no visibility command.");
        public void EndCommandList() { FlushCount++; FlushedFade = Fade; }
    }
}
