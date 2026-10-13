using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;

internal static class ActorScreenFlashCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ActorScreenFlashCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-screen-flash.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] v = Read(test.GetProperty("input"));
            var bytes = new byte[64];
            for (int word = 0; word < 5; word++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan((int)v[0] + word * 2), (ushort)v[1 + word]);
            var cursor = new ActorScriptCursor(bytes, 102, (int)v[0]);
            var state = new ScreenFlashState
            {
                FadeCountdown = (int)v[6], FlashCountdown = (int)v[7], Fading = (int)v[8], Importance = (byte)v[9], Sort = (int)v[10],
                CurrentRed = (uint)v[11], CurrentGreen = (uint)v[12], CurrentBlue = (uint)v[13],
                DeltaRed = (uint)v[14], DeltaGreen = (uint)v[15], DeltaBlue = (uint)v[16]
            };
            var host = new FlashHost(cursor, state, (int)v[18]);
            bool continued = ActorScreenFlashCommand.TryExecute(0x429C, cursor, host);
            long[] transport = [continued ? 1 : 0, host.EntryCursor, cursor.Position, .. host.Arguments];
            JsonElement expected = test.GetProperty("expected");
            assert(transport.SequenceEqual(Read(expected.GetProperty("transport"))),
                $"Original429C case {index}: actual transport[{string.Join(',', transport)}], expected[{string.Join(',', Read(expected.GetProperty("transport")))}].");
            int frame = 0;
            foreach (JsonElement source in expected.GetProperty("states").EnumerateArray())
            {
                long[] actual = Snapshot(state), wanted = Read(source);
                assert(actual.SequenceEqual(wanted), $"Original429C case {index} update{frame}: actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
                state.Update(); frame++;
            }
            index++;
        }
        var untouched = new ActorScriptCursor(ReadOnlyMemory<byte>.Empty);
        assert(!ActorScreenFlashCommand.TryExecute(0x429D, untouched, null!) && untouched.Position == 0,
            "Unrelated commands do not resolve a flash service or consume operands.");
        var failed = new ActorScriptCursor(new byte[10]);
        try { ActorScreenFlashCommand.TryExecute(0x429C, failed, new FailedFlash()); throw new Exception("Required flash failure was swallowed."); }
        catch (InvalidOperationException) { assert(failed.Position == 0, "Source429C commits field24C only after the required flash producer returns."); }
    }

    private static long[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static long[] Snapshot(ScreenFlashState state) => [state.FadeCountdown, state.FlashCountdown, state.Fading, state.Importance,
        state.Sort, state.CurrentRed, state.CurrentGreen, state.CurrentBlue, state.DeltaRed, state.DeltaGreen, state.DeltaBlue,
        state.FadeFinished ? 1 : 0, state.Visible ? 1 : 0, state.Red, state.Green, state.Blue];
    private sealed class FlashHost(ActorScriptCursor cursor, ScreenFlashState state, int mutation) : IActorScreenFlashHost
    {
        public int EntryCursor { get; private set; } = -1;
        public long[] Arguments { get; private set; } = [];
        public void Screen(byte red, byte green, byte blue, int frames, byte importance, int sort)
        {
            EntryCursor = cursor.Position;
            Arguments = [red, green, blue, frames, importance, sort];
            state.Screen(red, green, blue, frames, importance, sort);
            cursor.Seek(cursor.Position + mutation);
        }
    }
    private sealed class FailedFlash : IActorScreenFlashHost
    {
        public void Screen(byte red, byte green, byte blue, int frames, byte importance, int sort) =>
            throw new InvalidOperationException("The actual source flash producer failed.");
    }
}
