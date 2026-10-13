using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class GrenadeEffectTests
{
    public static void Run(Action<bool, string> assert)
    {
        using (JsonDocument document = Fixture("glow-flash"))
        {
            int index = 0;
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                long[] input = Values(test, "input");
                var random = new SourceRandom(Values(test, "random"), assert);
                GlowFlashParameters parameters = GlowFlashParameters.FromSourceArguments(Vector(input, 18), input.Take(18).Select(value => (int)value).ToArray());
                var state = new GlowFlashState(parameters, random.Next) { Age = (short)input[22] };
                for (int move = 0; move < input[21]; move++) state.Move();
                long[] actual = [state.CentreCodeBgr, state.Age, state.Lifetime, state.CentreFadeRate, state.SectionFadeRate, state.WidthDelta,
                    state.OddRadius, state.OddRange, state.EvenRadius, state.EvenRange, state.RadiusDecay, state.ReselectRadii,
                    state.Sections.Count, state.StepAngle, state.IsDead ? 1 : 0];
                long[] sections = state.Sections.SelectMany((section, i) => new long[] { section.Radius, section.PadBgr, state.Fringes[i].Width, state.Fringes[i].CodeBgr }).ToArray();
                Compare($"Glow flash {index++}", input, actual, Values(test, "state"), assert);
                Compare("Glow flash render sections", input, sections, Values(test, "sections"), assert);
                random.AssertComplete();
            }
        }
        using (JsonDocument document = Fixture("explosion-model"))
        {
            int index = 0;
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                long[] input = Values(test, "input");
                var random = new SourceRandom(Values(test, "random"), assert);
                var state = new ExplosionModelState(new(Vector(input, 9), "expgrnd", (int)input[0], (int)input[1], (int)input[2],
                    (int)input[3], (int)input[4], (int)input[5], (int)input[6], (int)input[7], (int)input[8]), random.Next)
                { GrowthAge = (int)input[14], Tint = (uint)input[15] };
                if (input[13] >= 0) state.Stage = (byte)input[13];
                for (int move = 0; move < input[12]; move++) state.Move();
                long[] actual = [state.Position.X, state.Position.Y, state.Position.Z, state.Scale.X, state.Scale.Y, state.Scale.Z,
                    state.VerticalVelocity, state.Tint, state.ItemFlags, state.Stage, state.Delay, state.GrowthAge, state.IsDead ? 1 : 0, state.ModelIndex];
                Compare($"Explosion model {index++}", input, actual, Values(test, "state"), assert);
                random.AssertComplete();
            }
        }
        using (JsonDocument document = Fixture("grenade-explosion"))
        {
            int index = 0;
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                long[] input = Values(test, "input");
                var random = new SourceRandom(Values(test, "random"), assert);
                var host = new Host { HandleAlive = input[3] != 0, Count = (int)input[5] };
                var state = new GrenadeExplosionState(Vector(input, 0), host, random.Next);
                for (int move = 0; move < input[4]; move++) state.Move();
                int before = host.Count;
                if (input[6] != 0) state.Dispose();
                long[] actual = [before, host.Count, state.IsDead ? 1 : 0];
                Compare($"Grenade explosion carrier {index++}", input, actual, Values(test, "state"), assert);
                Compare("Grenade explosion model/count/handle events", input, host.Events.ToArray(), Values(test, "events"), assert);
                random.AssertComplete();
                assert(host.Models.Count == 2 && host.Models[0].ModelName == "expgrnd" && host.Models[0].ModelIndex == 0 && host.Models[1].ModelIndex == 1,
                    "Grenade explosion creates its two genuine source models");
                state.Dispose();
            }
        }
        foreach (int points in new[] { 0, -1, int.MaxValue })
        {
            bool rejected = false;
            int randomCalls = 0;
            try { _ = new GlowFlashState(new(default, points, 255, 255, 255, 1, 128, 100, 0, 1, 60, 0, 1, 100, 120, 80, 100, 10, 4), _ => { randomCalls++; return 0; }); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            assert(rejected && randomCalls == 0, "Glow flash rejects undefined section allocation/angle inputs before effects or RNG");
        }
    }

    private static JsonDocument Fixture(string name)
    {
        using Stream stream = typeof(GrenadeEffectTests).Assembly.GetManifestResourceStream($"OpenSpidey.Tests.Fixtures.{name}.json")!;
        return JsonDocument.Parse(stream);
    }
    private static long[] Values(JsonElement test, string name) => test.GetProperty(name).EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static FixedVector3 Vector(long[] values, int index) => new((int)values[index], (int)values[index + 1], (int)values[index + 2]);
    private static void Compare(string label, long[] input, long[] actual, long[] expected, Action<bool, string> assert) =>
        assert(actual.SequenceEqual(expected), $"{label}: input [{string.Join(',', input)}], actual [{string.Join(',', actual)}], expected [{string.Join(',', expected)}]");

    private sealed class SourceRandom(long[] calls, Action<bool, string> assert)
    {
        private int cursor;
        public int Next(int bound)
        {
            assert(cursor + 1 < calls.Length, "Effect requested no unexpected source random calls");
            if (cursor + 1 >= calls.Length) return 0;
            assert(calls[cursor] == bound, $"Effect source random bound: got {bound}, expected {calls[cursor]}");
            int result = (int)calls[cursor + 1]; cursor += 2; return result;
        }
        public void AssertComplete() => assert(cursor == calls.Length, "Effect consumes every source random call, including Rnd(0)");
    }
    private sealed class Host : IGrenadeExplosionHost
    {
        public List<long> Events { get; } = [];
        public List<ExplosionModelState> Models { get; } = [];
        public bool HandleAlive { get; set; }
        public int Count { get; set; }
        public object CreateExplosionModel(ExplosionModelParameters parameters, Func<int, int> random)
        {
            Events.AddRange([1, parameters.Position.X, parameters.Position.Y, parameters.Position.Z, parameters.ModelIndex, parameters.Delay,
                parameters.InitialScale, parameters.ScaleStep, parameters.VerticalScale, parameters.GrowthTicks, parameters.FadeRate, parameters.VerticalSpeed, parameters.VerticalSpeedRange]);
            var model = new ExplosionModelState(parameters, random); Models.Add(model); return model;
        }
        public bool IsExplosionModelHandleAlive(object handle)
        { Events.AddRange([2, ((ExplosionModelState)handle).ModelIndex, HandleAlive ? 1 : 0]); return HandleAlive; }
        public void ChangeExplosionCount(int delta) { Count = unchecked(Count + delta); Events.AddRange([3, delta]); }
        public void MarkCarrierDead() => Events.Add(4);
    }
}
