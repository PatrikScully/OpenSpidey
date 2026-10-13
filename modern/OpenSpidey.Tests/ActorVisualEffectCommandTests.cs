using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ActorVisualEffectCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorVisualEffectCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-visual-effects.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ushort[] words = test.GetProperty("words").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            byte[] bytes = new byte[words.Length * 2];
            for (int word = 0; word < words.Length; word++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(word * 2), words[word]);
            var cursor = new ActorScriptCursor(bytes);
            var host = new Host(cursor, test.GetProperty("accepted").GetInt32() != 0);
            int[] seed = test.GetProperty("random").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            host.Stream.Restore(new(seed[0], seed[1], seed[2]));
            int[] position = test.GetProperty("position").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            bool result = ActorVisualEffectCommands.TryExecute(test.GetProperty("opcode").GetUInt16(), cursor,
                new(position[0], position[1], position[2]), host);
            assert(result && expected.GetProperty("result").GetInt32() == 1 && cursor.Position == expected.GetProperty("consumed").GetInt32() &&
                host.Allocations == expected.GetProperty("allocs").GetInt32() && host.ModelEntry == expected.GetProperty("entry").GetInt32() &&
                host.Request.SequenceEqual(Numbers(expected.GetProperty("request"))),
                $"Whole PC visual-effect case{index} matches original raw-word/signed operands, actor-position callback/cursor, allocation request and exact source no-effect subcases.");
            assert(host.RandomEvents.SequenceEqual(Numbers(expected.GetProperty("random"))) &&
                new[] { host.Stream.State.Value, host.Stream.State.Multiplier, host.Stream.State.Increment }
                    .SequenceEqual(expected.GetProperty("randomState").EnumerateArray().Select(value => value.GetInt32())),
                $"Whole PC effect case{index} retains original per-section laser RNG then angle, or three positional wibbling calls before allocation and constructor RNG only after success.");
            List<long> glow = [];
            if (host.Glow is { } state)
            {
                glow.AddRange([state.Position.X, state.Position.Y, state.Position.Z, state.Sections.Count, state.FringeCount,
                    state.CentreCodeBgr, state.StepAngle, state.SkipTriangles ? 1 : 0, state.Angle, state.Mask, state.Age, state.Lifetime,
                    state.CentreFadeRate, state.SectionFadeRate, state.WidthDelta, state.OddRadius, state.OddRange, state.EvenRadius,
                    state.EvenRange, state.RadiusDecay, state.ReselectRadii]);
                foreach (GlowSection section in state.Sections) glow.AddRange([section.Radius, section.PadBgr]);
                foreach (GlowFringe fringe in state.Fringes) glow.AddRange([fringe.Width, fringe.CodeBgr]);
            }
            assert(glow.SequenceEqual(Numbers(expected.GetProperty("glow"))),
                $"Whole actual Effects_LaserFlash/CGlowFlash case{index} preserves all16 sections/fringes, unsigned scale/mask inputs, low-byte RGB/lifetime, short radius wrap, final angle/mask and packed colors.");
            List<long> model = [];
            if (host.Model is { Model: { } stateModel }) model.AddRange([stateModel.Position.X, stateModel.Position.Y, stateModel.Position.Z,
                stateModel.Scale.X, stateModel.Scale.Y, stateModel.Scale.Z, stateModel.VerticalVelocity, stateModel.Tint,
                stateModel.ItemFlags, stateModel.Stage, stateModel.Delay, stateModel.GrowthAge, stateModel.ModelIndex]);
            assert(model.SequenceEqual(Numbers(expected.GetProperty("model"))) && host.Count == expected.GetProperty("count").GetInt32(),
                $"Whole original CBody/C3D/CWibbling constructor case{index} matches native position/scale/velocity/tint/flags/stage/model and exact derived count, including failed allocations.");
            host.Model?.Dispose(); host.Model?.Dispose();
            assert(host.Count == 7, "Actual successful wibbling construction releases its own count exactly once at real destruction, and failed allocation contributes no lease.");
            index++;
        }
    }
    private static IEnumerable<long> Numbers(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt64());
    private sealed class Host(ActorScriptCursor cursor, bool accepted) : IActorVisualEffectHost, IWibblingExplosionCounter
    {
        public GameRandom Stream { get; } = new(0);
        public List<long> RandomEvents { get; } = [];
        public List<long> Request { get; } = [];
        public int Allocations { get; private set; }
        public int ModelEntry { get; private set; } = -1;
        public int Count { get; private set; } = 7;
        public GlowFlashState? Glow { get; private set; }
        public WibblingExplosionState? Model { get; private set; }
        public int Random(int bound) { int result = Stream.Next(bound); RandomEvents.AddRange([bound, result]); return result; }
        public void CreateLaserFlash(LaserFlashParameters parameters) { Allocations++; Glow = LaserFlash.Create(parameters, Random); }
        public void TryCreateWibbling(ExplosionModelParameters parameters)
        {
            Allocations++;
            if (!accepted) return;
            ModelEntry = cursor.Position;
            Request.AddRange([parameters.Position.X, parameters.Position.Y, parameters.Position.Z, parameters.ModelIndex,
                parameters.Delay, parameters.InitialScale, parameters.ScaleStep, parameters.VerticalScale, parameters.GrowthTicks,
                parameters.FadeRate, parameters.VerticalSpeed, parameters.VerticalSpeedRange]);
            Model = new(parameters, this, Random);
        }
        public void ChangeCount(int delta) => Count = unchecked(Count + delta);
    }
}
