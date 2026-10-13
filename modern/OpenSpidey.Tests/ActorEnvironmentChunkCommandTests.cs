using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;

internal static class ActorEnvironmentChunkCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ActorEnvironmentChunkCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-environment-chunk.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = Read(test.GetProperty("input"));
            int sourceOffset = (int)input[0], position = (int)input[1];
            var bytes = new byte[32];
            int aligned = ((sourceOffset + position + 3) & ~3) - sourceOffset;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(aligned), (uint)input[2]);
            var cursor = new ActorScriptCursor(bytes, sourceOffset, position);
            var host = new ChunkHost(cursor);
            bool continued = ActorEnvironmentChunkCommand.TryExecute(0x42B5, cursor, host);
            long[] actual = [continued ? 1 : 0, host.Calls, host.Checksum, host.EntryCursor, cursor.Position];
            assert(actual.SequenceEqual(Read(test.GetProperty("expected"))), $"Original whole CBaddy42B5 [{string.Join(',', input)}] retains absolute4-byte alignment, direct unsigned checksum and callback-before-field24C commit.");
        }
        var untouched = new ActorScriptCursor(new byte[4], 2);
        var unused = new ChunkHost(untouched);
        assert(!ActorEnvironmentChunkCommand.TryExecute(0x42B4, untouched, unused) && untouched.Position == 0 && unused.Calls == 0,
            "Unrelated common commands must leave the chunk callback and operands untouched.");
        var failed = new ActorScriptCursor(new byte[8], 2);
        try { ActorEnvironmentChunkCommand.TryExecute(0x42B5, failed, new FailingHost()); throw new Exception("Required chunk failure was swallowed."); }
        catch (InvalidOperationException)
        { assert(failed.Position == 0, "Actual source field24C commits only after successful whole chunk destruction; a failing required native producer cannot consume the script."); }
    }
    private static long[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class ChunkHost(ActorScriptCursor cursor) : IActorEnvironmentChunkHost
    {
        public int Calls { get; private set; }
        public uint Checksum { get; private set; }
        public int EntryCursor { get; private set; } = -1;
        public void ChunkEnvironment(uint checksum) { Calls++; Checksum = checksum; EntryCursor = cursor.Position; }
    }
    private sealed class FailingHost : IActorEnvironmentChunkHost
    {
        public void ChunkEnvironment(uint checksum) => throw new InvalidOperationException("Required native source chunk producer failed.");
    }
}
