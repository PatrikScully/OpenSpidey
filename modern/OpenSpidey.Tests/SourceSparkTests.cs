using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class SourceSparkTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(SourceSparkTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.source-sparks.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] v = Read(test.GetProperty("input"));
            JsonElement expected = test.GetProperty("expected");
            int family = test.GetProperty("family").GetInt32();
            if (family == 0) VerifyConstructor(v, expected, index);
            else if (family == 1) VerifyMoves(v, expected, index);
            else VerifyCommand(v, expected, index);
            index++;
        }
        var untouched = new ActorScriptCursor(ReadOnlyMemory<byte>.Empty);
        assert(!ActorSparkCommand.TryExecute(0x429C, untouched, default, default, null!) && untouched.Position == 0,
            "Other inherited actor commands do not consume raw spark counts or resolve a particle service.");

        void VerifyConstructor(long[] v, JsonElement expected, int caseIndex)
        {
            SparkSettings settings = Settings(v, 3); settings.AllocationUsage = (int)v[23];
            int cursor = (int)v[20], registered = 0;
            var events = new List<int>();
            int Random(int bound)
            {
                assert(registered == 1, "Original CBit/CPixel registration precedes every CSpark cone and lifetime random call.");
                return RandomValue(bound, ref cursor, events);
            }
            var spark = new SparkParticle(settings, new(Vector(v, 0), (int)v[17], (int)v[18], (int)v[19]), Random,
                created => { registered++; assert(created.Position == default && created.Age == 0 && created.Lifetime == 0,
                    "Source Spark publication occurs at its actual base constructor before spark position/age/lifetime initialization."); });
            assert(events.SequenceEqual(Read(expected.GetProperty("random")).Select(value => (int)value)),
                $"Whole source Spark ctor{caseIndex} retains X/Y/Z nonzero-cone RNG, half-cone16-bit angle wrapping and final lifetime draw.");
            int frame = 0;
            foreach (JsonElement source in expected.GetProperty("states").EnumerateArray())
            {
                if (frame != 0 && spark.Alive) spark.AdvanceBits();
                Verify(spark, source, $"ctor{caseIndex}/frame{frame++}");
            }
            assert(settings.AllocationUsage == (int)v[23] && registered == 1,
                "A direct actual CSpark captures process settings without modifying shared allocation mode or duplicating its source publication.");
        }
        void VerifyMoves(long[] v, JsonElement expected, int caseIndex)
        {
            var spark = new SparkParticle(new(), new(default, 0, 0, 1), _ => 0)
            {
                Position = Vector(v, 0), Velocity = Vector(v, 3), Acceleration = Vector(v, 6), Age = (short)v[9], Lifetime = (ushort)v[10],
                Code = 0, Tag = 0, WidthHeight = 0, Red = (byte)v[11], Green = (byte)v[12], Blue = (byte)v[13],
                FadeRed = (byte)v[14], FadeGreen = (byte)v[15], FadeBlue = (byte)v[16], Friction = new(3, 3, 3)
            };
            int frame = 0;
            foreach (JsonElement source in expected.EnumerateArray())
            { spark.AdvanceBits(); Verify(spark, source, $"directMove{caseIndex}/frame{frame++}"); }
        }
        void VerifyCommand(long[] v, JsonElement expected, int caseIndex)
        {
            byte[] bytes = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)v[0]);
            var cursor = new ActorScriptCursor(bytes, 102);
            var emitter = new Emitter(Settings(v, 9), (int)v[1], (int)v[2], (int)v[23], (int)v[24], cursor);
            bool continued = ActorSparkCommand.TryExecute(0x4292, cursor, Vector(v, 3), new((short)v[6], (short)v[7], (short)v[8]), emitter);
            long[] actual = [continued ? 1 : 0, cursor.Position, emitter.LiveBitCount, emitter.Settings.AllocationUsage];
            assert(actual.SequenceEqual(Read(expected.GetProperty("return"))),
                $"Whole4292 case{caseIndex} raw count/cursor, signed one-time BitCount gate and usage0→1: actual[{string.Join(',', actual)}], expected[{string.Join(',', Read(expected.GetProperty("return")))}].");
            long[] globals = SettingsSnapshot(emitter.Settings);
            assert(globals.SequenceEqual(Read(expected.GetProperty("settings"))),
                $"Whole4292 case{caseIndex} retains exact process trajectory/cone, packed spark size, unchanged semi-transparency and RGB/fade globals: actual[{string.Join(',', globals)}], expected[{string.Join(',', Read(expected.GetProperty("settings")))}].");
            assert(emitter.Allocations.Select(value => (long)value).SequenceEqual(Read(expected.GetProperty("allocations"))) &&
                emitter.RandomEvents.Select(value => (long)value).SequenceEqual(Read(expected.GetProperty("random"))),
                $"Whole4292 case{caseIndex} allocation boundary commits raw count first, keeps usage0 for entire batch, publishes before RNG and skips failed allocations without extra draws.");
            JsonElement[] sparks = expected.GetProperty("sparks").EnumerateArray().ToArray();
            assert(sparks.Length == emitter.Created.Count, $"Whole4292 case{caseIndex} creates each successful original request exactly once.");
            for (int i = 0; i < sparks.Length; i++) Verify(emitter.Created[i].Spark, sparks[i], $"whole4292/{caseIndex}/spark{i}");
            assert(emitter.HeadOrder.Select(entry => (long)entry.Index).SequenceEqual(Read(expected.GetProperty("headOrder"))),
                $"Whole4292 case{caseIndex} original Pixel head insertion reverses successful allocation order while preserving failed-allocation holes.");
        }
        void Verify(SparkParticle spark, JsonElement expected, string context)
        {
            long[] actual = Snapshot(spark), wanted = Read(expected);
            assert(actual.SequenceEqual(wanted), $"Whole original Spark{context}: actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
        }
    }

    private static long[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static FixedVector3 Vector(long[] v, int start) => new((int)v[start], (int)v[start + 1], (int)v[start + 2]);
    private static SparkSettings Settings(long[] v, int start) => new()
    {
        Trajectory = new((short)v[start], (short)v[start + 1], (short)v[start + 2]),
        Cone = new((short)v[start + 3], (short)v[start + 4], (short)v[start + 5]), Size = (uint)v[start + 6], SemiTransparent = v[start + 7] != 0,
        Red = (byte)v[start + 8], Green = (byte)v[start + 9], Blue = (byte)v[start + 10],
        FadeRed = (byte)v[start + 11], FadeGreen = (byte)v[start + 12], FadeBlue = (byte)v[start + 13]
    };
    private static long[] SettingsSnapshot(SparkSettings settings) => [settings.Trajectory.X, settings.Trajectory.Y, settings.Trajectory.Z,
        settings.Cone.X, settings.Cone.Y, settings.Cone.Z, settings.Size, settings.SemiTransparent ? 1 : 0,
        settings.Red, settings.Green, settings.Blue, settings.FadeRed, settings.FadeGreen, settings.FadeBlue];
    private static long[] Snapshot(SparkParticle spark) => [spark.Position.X, spark.Position.Y, spark.Position.Z, spark.Velocity.X,
        spark.Velocity.Y, spark.Velocity.Z, spark.Acceleration.X, spark.Acceleration.Y, spark.Acceleration.Z,
        spark.Age, spark.Lifetime, spark.Tag, spark.Code, spark.WidthHeight, spark.Red, spark.Green, spark.Blue,
        spark.FadeRed, spark.FadeGreen, spark.FadeBlue, spark.Friction.X, spark.Friction.Y, spark.Friction.Z, spark.Alive ? 0 : 1];
    private static int RandomValue(int bound, ref int cursor, List<int> events)
    {
        int divisor = bound < 0 ? -bound : bound;
        int value = divisor == 0 ? 0 : (cursor * 7 + 1) % divisor;
        cursor++; events.AddRange([bound, value]); return value;
    }
    private sealed class Emitter : IActorSparkEmitter
    {
        private readonly int failedIndex;
        private readonly ActorScriptCursor cursor;
        private int randomCursor;
        private int allocationIndex;
        public SparkSettings Settings { get; }
        public int LiveBitCount { get; private set; }
        public List<int> Allocations { get; } = [];
        public List<int> RandomEvents { get; } = [];
        public List<(int Index, SparkParticle Spark)> Created { get; } = [];
        public List<(int Index, SparkParticle Spark)> HeadOrder { get; } = [];
        public Emitter(SparkSettings settings, int count, int usage, int seed, int failedIndex, ActorScriptCursor cursor)
        { Settings = settings; Settings.AllocationUsage = usage; LiveBitCount = count; randomCursor = seed; this.failedIndex = failedIndex; this.cursor = cursor; }
        public void TrySpawnSpark(SparkSpawnRequest request)
        {
            int index = allocationIndex++;
            Allocations.AddRange([76, Settings.AllocationUsage, LiveBitCount, cursor.Position, index == failedIndex ? 1 : 0]);
            if (index == failedIndex) return;
            var spark = new SparkParticle(Settings, request, bound => RandomValue(bound, ref randomCursor, RandomEvents),
                allocated => { LiveBitCount = unchecked(LiveBitCount + 1); HeadOrder.Insert(0, (index, allocated)); });
            Created.Add((index, spark));
        }
    }
}
