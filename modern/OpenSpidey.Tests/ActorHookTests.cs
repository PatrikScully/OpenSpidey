using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

internal static class ActorHookTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorHookTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-hooks.json")
            ?? throw new InvalidDataException("Actor hook reference fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement packetCase in document.RootElement.GetProperty("packets").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(packetCase.GetProperty("bytes").GetString()!);
            int parts = packetCase.GetProperty("partCount").GetInt32();
            ActorHookPacket packet = ActorHookPacket.Read(bytes, parts);
            ActorHook[] expected = packetCase.GetProperty("hooks").EnumerateArray().Select(ReadHook).ToArray();
            assert(packet.HeaderMetadata == 0 && packet.ByteLength == bytes.Length && packet.Hooks.SequenceEqual(expected),
                "Original thug/henchman hook headers and signed source SHook records decode exactly.");
            for (int length = 0; length < bytes.Length; length++)
            {
                int truncatedLength = length;
                Reject(() => ActorHookPacket.Read(bytes.AsSpan(0, truncatedLength), parts), assert);
            }
            byte[] invalidPart = bytes.ToArray();
            invalidPart[10] = (byte)parts;
            invalidPart[11] = 0;
            Reject(() => ActorHookPacket.Read(invalidPart, parts), assert);
            byte[] metadata = bytes.ToArray();
            metadata[0] = 0x34;
            metadata[1] = 0x12;
            assert(ActorHookPacket.Read(metadata, parts).HeaderMetadata == 0x1234,
                "The unconsumed low hook-header halfword remains opaque metadata.");
            assert(ActorHookPacket.Read(bytes.Concat(new byte[] { 0xFF }).ToArray(), parts).ByteLength == bytes.Length,
                "Hook packet decoding is bounded by its declared records rather than trailing executable data.");
        }
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] position = ReadArray(test.GetProperty("position"));
            int[] translation = ReadArray(test.GetProperty("translation"));
            int[] rotation = ReadArray(test.GetProperty("rotation"));
            int[] part = ReadArray(test.GetProperty("part"));
            ActorHook hook = ReadHook(test.GetProperty("hook"));
            var pose = new FixedJointTransform[16];
            pose[hook.PartIndex] = Matrix(part);
            FixedVector3 actual = ActorHookPosition.Evaluate(Vector(position), Vector(translation), Matrix(rotation), pose, hook);
            assert(actual == Vector(ReadArray(test.GetProperty("expected"))),
                "Dynamic actor hooks match native recovered affine transforms, halfword wrap and world/model scale rounding.");
        }
        assert(ActorHookPacket.Read(new byte[4], 1).Hooks.Count == 0, "Empty source hook tables decode without inventing a hook.");
        Reject(() => ActorHookPosition.Evaluate(default, default, default, Array.Empty<FixedJointTransform>(), new(0, 20, -100, 13)), assert);
    }

    private static int[] ReadArray(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(int[] values) => new(values[0], values[1], values[2]);
    private static ActorHook ReadHook(JsonElement element)
    {
        int[] values = ReadArray(element);
        return new((short)values[0], (short)values[1], (short)values[2], (ushort)values[3]);
    }
    private static FixedJointTransform Matrix(int[] values) => new((short)values[0], (short)values[1], (short)values[2],
        (short)values[3], (short)values[4], (short)values[5], (short)values[6], (short)values[7], (short)values[8],
        values.Length > 9 ? (short)values[9] : (short)0,
        values.Length > 9 ? (short)values[10] : (short)0,
        values.Length > 9 ? (short)values[11] : (short)0);
    private static void Reject(Action operation, Action<bool, string> assert)
    {
        try { operation(); }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException)
        {
            assert(true, "Actor hook parsing/evaluation rejects truncated records and invalid pose indices.");
            return;
        }
        assert(false, "Actor hook parsing/evaluation accepted an invalid source packet or pose index.");
    }
}
