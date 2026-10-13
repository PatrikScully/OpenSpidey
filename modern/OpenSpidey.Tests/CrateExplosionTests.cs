using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class CrateExplosionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(CrateExplosionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.crate-explosion.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(input);
            int Random(int range)
            {
                host.Events.AddRange([1, range]);
                host.MemoryRestricted = input[3] != 0;
                return input[2];
            }
            CrateExplosion.Create(new(100, 200, 300), input[0] != 0, host, Random);
            assert(host.Events.SequenceEqual(test.GetProperty("events").EnumerateArray().Select(value => value.GetInt32())),
                "Actual source Small/Big explosion helper preserves fragment count/parameters, GlowFlash radius70/200 and low-memory read stages.");
        }
    }

    private sealed class Host(int[] input) : ICrateExplosionHost
    {
        public List<int> Events { get; } = [];
        public bool MemoryRestricted { get; set; } = input[1] != 0;
        public bool LowMemory => MemoryRestricted;
        public void CreateFragment(ExplosionFragmentParameters parameters)
        {
            Events.AddRange([2, parameters.Position.X, parameters.Position.Y, parameters.Position.Z, parameters.Red,
                parameters.Green, parameters.Blue, parameters.Animation, parameters.Scale, parameters.SemiTransparent ? 1 : 0,
                parameters.Spread, parameters.Gravity, parameters.MaximumLifetime]);
            MemoryRestricted = input[4] != 0;
        }
        public void CreateGlowFlash(GlowFlashParameters parameters) =>
            Events.AddRange([3, parameters.Position.X, parameters.Position.Y, parameters.Position.Z, parameters.Points,
                parameters.CentreRed, parameters.CentreGreen, parameters.CentreBlue, parameters.CentreFadeRate,
                parameters.SectionRed, parameters.SectionGreen, parameters.SectionBlue, parameters.SectionFadeRate,
                parameters.FringeWidth, parameters.WidthDelta, parameters.ReselectRadii, parameters.OddRadius,
                parameters.OddRange, parameters.EvenRadius, parameters.EvenRange, parameters.RadiusDecay, parameters.Lifetime]);
    }
}
