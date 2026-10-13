using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

internal static class ActorPartCollisionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorPartCollisionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-part-collision.json")
            ?? throw new InvalidDataException("Posed actor collision reference fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.GetProperty("boxes").EnumerateArray())
        {
            FixedVector3 end = Vector(test.GetProperty("end"));
            bool hit = ActorPartCollision.TryClipBox(Box(test.GetProperty("box")), Vector(test.GetProperty("start")), ref end);
            int[] actual = [hit ? 1 : 0, end.X, end.Y, end.Z];
            assert(actual.SequenceEqual(Values(test.GetProperty("expected"))),
                "Strict source face crossing preserves inside/endpoint/tangent misses, face order and signed integer clipping.");
        }
        foreach (JsonElement test in document.RootElement.GetProperty("posed").EnumerateArray())
        {
            JsonElement[] parts = test.GetProperty("parts").EnumerateArray().ToArray();
            FixedJointTransform[] poses = parts.Select(part => Matrix(part.GetProperty("pose"))).ToArray();
            ActorPartBox[] boxes = parts.Select(part => Box(part.GetProperty("box"))).ToArray();
            bool hit = ActorPartCollision.TryCollide(Vector(test.GetProperty("position")), Matrix(test.GetProperty("rotation")),
                poses, boxes, test.GetProperty("hidden").GetUInt32(), Vector(test.GetProperty("start")),
                Vector(test.GetProperty("end")), test.GetProperty("scale").GetInt32(), out ActorHook hook);
            int[] actual = [hit ? 1 : 0, hook.X, hook.Y, hook.Z, hook.PartIndex];
            assert(actual.SequenceEqual(Values(test.GetProperty("expected"))),
                "Posed part sweeps match original actor/part transposes, short wraps, scaling, hidden masks, closest-part selection and hook coordinates.");
        }
        VerifyBoundedBoxReader(assert);
        Reject(() => ActorPartCollision.TryCollide(default, default, Array.Empty<FixedJointTransform>(),
            new ActorPartBox[1], 0, default, default, 8192, out _), assert);
    }

    private static void VerifyBoundedBoxReader(Action<bool, string> assert)
    {
        const int parts = 3, models = 6;
        int table = 12 + parts * 36;
        int firstModel = table + 4 + models * 4;
        byte[] bytes = new byte[firstModel + models * 28];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x20004);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), parts);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(table), models);
        // The first three original player header boxes; lower LOD records deliberately differ.
        ActorPartBox[] expected = [new(15, -14, 13, -11, 17, -8), new(21, -20, 3, -34, 8, -22), new(16, -15, 2, -15, 7, -18)];
        for (int model = 0; model < models; model++)
        {
            int offset = firstModel + model * 28;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(table + 4 + model * 4), offset);
            ActorPartBox box = model < parts ? expected[model] : new(100, -100, 200, -200, 300, -300);
            short[] coordinates = [box.UpperX, box.LowerX, box.UpperY, box.LowerY, box.UpperZ, box.LowerZ];
            for (int coordinate = 0; coordinate < coordinates.Length; coordinate++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 12 + coordinate * 2), coordinates[coordinate]);
        }
        assert(PsxActorCollisionBoxes.Read(bytes).SequenceEqual(expected),
            "Collision boxes retain original upper/lower header order and select pose parts rather than duplicate LOD models.");
        int required = firstModel + parts * 28;
        for (int length = 0; length < required; length++)
        {
            int truncated = length;
            Reject(() => PsxActorCollisionBoxes.Read(bytes.AsSpan(0, truncated)), assert);
        }
        byte[] invalid = bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(table), 2);
        Reject(() => PsxActorCollisionBoxes.Read(invalid), assert);
        invalid = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(invalid.AsSpan(table + 4), uint.MaxValue);
        Reject(() => PsxActorCollisionBoxes.Read(invalid), assert);
        invalid = bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(firstModel), 8);
        assert(PsxActorCollisionBoxes.Read(invalid).SequenceEqual(expected),
            "Original model flag8 does not change the direct SModel+12 collision box lookup.");
    }

    public static void VerifyOriginalPlayer(ReadOnlySpan<byte> bytes, Action<bool, string> assert)
    {
        // Independently extracted original spidey.psx SModel headers, before renderer part swaps.
        ActorPartBox[] expected = [new(15,-14,13,-11,17,-8), new(21,-20,3,-34,8,-22), new(16,-15,2,-15,7,-18),
            new(5,-6,28,1,5,-6), new(10,-6,42,-5,8,-10), new(6,-8,15,-1,4,-4), new(7,-9,22,-1,4,-3),
            new(9,-8,10,-23,8,-17), new(7,-4,28,1,5,-6), new(7,-9,42,-5,8,-10), new(9,-5,15,-1,4,-4),
            new(10,-6,22,-1,4,-3), new(9,-9,52,-2,14,-3), new(8,-11,68,-9,17,-11), new(9,-7,9,-2,8,-26),
            new(10,-8,52,-2,14,-3), new(12,-8,68,-9,17,-11), new(8,-8,9,-2,8,-26)];
        assert(PsxActorCollisionBoxes.Read(bytes).SequenceEqual(expected),
            "All eighteen original player collision boxes match independent original model-header extraction.");
        int table = 12 + 18 * 36;
        for (int part = 0; part < 18; part++)
        {
            int model = BinaryPrimitives.ReadInt32LittleEndian(bytes[(table + 4 + part * 4)..]);
            assert((BinaryPrimitives.ReadUInt32LittleEndian(bytes[model..]) & 8) != 0,
                "Every original player model uses flag8, which the source collision routine accepts.");
        }
    }

    private static int[] Values(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(JsonElement element)
    {
        int[] values = Values(element);
        return new(values[0], values[1], values[2]);
    }
    private static ActorPartBox Box(JsonElement element)
    {
        int[] values = Values(element);
        return new((short)values[0], (short)values[1], (short)values[2], (short)values[3], (short)values[4], (short)values[5]);
    }
    private static FixedJointTransform Matrix(JsonElement element)
    {
        int[] values = Values(element);
        return new((short)values[0], (short)values[1], (short)values[2], (short)values[3], (short)values[4], (short)values[5],
            (short)values[6], (short)values[7], (short)values[8], values.Length > 9 ? (short)values[9] : (short)0,
            values.Length > 9 ? (short)values[10] : (short)0, values.Length > 9 ? (short)values[11] : (short)0);
    }
    private static void Reject(Action operation, Action<bool, string> assert)
    {
        try { operation(); }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or NotSupportedException)
        {
            assert(true, "Actor part collision rejects missing poses, truncated box records and unsupported source formats.");
            return;
        }
        assert(false, "Actor part collision accepted an invalid pose or box source.");
    }
}
