using System.Buffers.Binary;
using System.Text;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class SceneLibraryTests
{
    public static void Run(Action<bool, string> assert)
    {
        byte[] bytes = new byte[108];
        Write(0, 0x20006); Write(4, 80); Write(12, 2); Write(16, 24); Write(20, 52);
        Write(80, uint.MaxValue); Write(84, 0x12345678); Write(88, 0x12345678);
        PsxAssetLibrary library = PsxAssetLibrary.Read("data/example.psx", bytes);
        assert(library.Scene.Models.All(model => model.Checksum == 0x12345678) && library.FindModel(0x12345678) == 0,
            "Model checksum tables retain duplicate names and resolve the original first match.");
        try { library.FindModel(0x87654321); throw new Exception("Unknown model checksum resolved."); }
        catch (KeyNotFoundException) { assert(true, "Missing models report their checksum and source library."); }
        try { PsxSceneReader.Read(bytes.AsSpan(0, 88)); throw new Exception("Truncated model checksum table accepted."); }
        catch (InvalidDataException) { assert(true, "Model checksum tables are bounded before reading texture names."); }
        assert(PsxLibraryRepository.AssetPath("DATA\\L1A1_O.PSX") == "data/l1a1_o.psx" &&
            PsxLibraryRepository.AssetPath("L1A1_O") == "data/l1a1_o.psx", "Source library requests use one canonical archive identity.");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)4);
        Command(MissionOpcode.LoadSceneLibrary, "L1A1_O");
        Command(MissionOpcode.SetObjectLibrary, "L1A1_O");
        Command(MissionOpcode.LoadEnvironment, "L1A1_G");
        writer.Write((ushort)MissionOpcode.SynchronizeLibraries);
        Command(MissionOpcode.UnloadSceneLibrary, "L1A1_O");
        writer.Write(ushort.MaxValue);
        var host = new Host();
        var runtime = new MissionRuntime(new([new(0, 4, 0, stream.ToArray())]), new MissionRuntimeTests.ActorFactory(),
            SceneLibraryCommandHandler.CreateHandlers(host));
        runtime.QueueAutoexec();
        assert(runtime.ExecutePending() == 1 && host.Calls.SequenceEqual(new[] {
            "load:L1A1_O", "objects:L1A1_O", "world:L1A1_G", "unload:L1A1_O" }),
            "Library mission commands execute in source order and synchronization follows completed synchronous loads.");

        void Write(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void Command(MissionOpcode opcode, string name)
        {
            writer.Write((ushort)opcode); writer.Write(Encoding.Latin1.GetBytes(name)); writer.Write((byte)0);
            while (stream.Position % 2 != 0) writer.Write((byte)0);
        }
    }

    private sealed class Host : ISceneLibraryHost
    {
        public List<string> Calls { get; } = [];
        public void LoadLibrary(string name) => Calls.Add("load:" + name);
        public void SelectObjectLibrary(string name) => Calls.Add("objects:" + name);
        public void LoadEnvironment(string name) => Calls.Add("world:" + name);
        public void UnloadLibrary(string name) => Calls.Add("unload:" + name);
    }
}
