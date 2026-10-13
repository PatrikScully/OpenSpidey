using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class WebDomeShockTests
{
    private sealed class Target(int index, FixedVector3 position, ushort type, ushort flags) : IWebDomeShockTarget
    {
        public int Index => index;
        public object Identity => this;
        public FixedVector3 Position => position;
        public ushort Type => type;
        public ushort BodyFlags { get; set; } = flags;
        public IWebDomeShockTarget? Next { get; set; }
    }
    private sealed class Host(int[] input) : IWebDomeShockHost
    {
        private int cursor = input[5];
        public List<long> Events { get; } = [];
        public Target[] Targets { get; } = Enumerable.Range(0, 7).Select(index => new Target(index,
            new(unchecked(input[0] + (input[6 + index * 5] << 12)),
                unchecked(input[1] + ((input[7 + index * 5] + 50) << 12)), unchecked(input[2] + (input[8 + index * 5] << 12))),
            (ushort)input[9 + index * 5], (ushort)input[10 + index * 5])).ToArray();
        public IWebDomeShockTarget? BaddyHead => Targets[0];
        public IWebDomeShockTarget? EnvironmentalHead => Targets[4];
        public bool LowMemory => input[4] != 0;
        public int Random(int bound) { int value = (cursor++ * 7 + 1) % bound; Events.AddRange([1, bound, value]); return value; }
        public void ShakeCamera(FixedVector3 position, int magnitude) => Events.AddRange([2, position.X, position.Y, position.Z, magnitude]);
        public void SetFlaming(IWebDomeShockTarget target, int frames, byte red, byte green, byte blue) =>
            Events.AddRange([4, ((Target)target).Index, frames, red, green, blue]);
        public void EmitGlow(FixedVector3 position, int size, byte red, byte green, byte blue, int growth, int first, int second)
        {
            Target target = Targets.First(target => target.Position == position);
            Events.AddRange([3, target.Index, size, red, green, blue, growth, first, second]);
        }
        public void Hit(IWebDomeShockTarget target, ActorHit hit) => Events.AddRange([5, ((Target)target).Index,
            hit.Flags, hit.PartIndex, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]);
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebDomeShockTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-dome-shocks.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(input);
            for (int target = 0; target < host.Targets.Length; target++)
                if (target is not (3 or 6)) host.Targets[target].Next = host.Targets[target + 1];
            var wave = new WebDomeShockWave(new(input[0], input[1], input[2]), input[3], host, host.Random);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (frame != 0) { host.Events.Clear(); wave.AdvanceBits(); }
                long[] actual = [wave.Position.X, wave.Position.Y, wave.Position.Z, wave.ColourAmplitude.X,
                    wave.ColourAmplitude.Y, wave.ColourAmplitude.Z, wave.FireVariant, wave.Radius, wave.Height, wave.Alive ? 0 : 1, 8];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(11).Select(value => value.GetInt64())),
                    $"CDomeShockWave actualPC case {index} frame {frame}: source140 range,RGB16 decay, height threshold and finaldeath.");
                assert(wave.Phases.SequenceEqual(expected[11].EnumerateArray().Select(value => value.GetInt32())), "Dome shock retains the source sixteen constructor phases.");
                assert(host.Targets.Select(target => (int)target.BodyFlags).SequenceEqual(expected[12].EnumerateArray().Select(value => value.GetInt32())),
                    $"CDomeShockWave actualPC case {index} frame {frame}: clear/set flag100 on the actual source ordered target identities.");
                assert(host.Events.SequenceEqual(expected[13].EnumerateArray().Select(value => value.GetInt64())),
                    $"CDomeShockWave actualPC case {index} frame {frame}: strict vertical range, signed approximate distance, both hitwalks, flame/glow branches and source falloff/slide.");
                frame++;
            }
            index++;
        }
    }
}
