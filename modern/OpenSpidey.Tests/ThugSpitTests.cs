using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugSpitTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugSpitTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-spit.json")
            ?? throw new InvalidDataException("Thug spit source fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] angles = Values(test.GetProperty("angles"));
            int[] random = Values(test.GetProperty("random"));
            var host = new Host
            {
                AnimationFrame = test.GetProperty("frame").GetInt32(), ItemFlags = test.GetProperty("flags").GetUInt16(),
                Position = Vector(test.GetProperty("position")), PlayerPosition = Vector(test.GetProperty("player")),
                Angles = new((short)angles[0], (short)angles[1], (short)angles[2]), Mouth = Vector(test.GetProperty("mouth"))
            };
            int randomCalls = 0;
            bool result = ThugSpitBehavior.Monitor(host, bound =>
            {
                int value = random[randomCalls++];
                host.Events.AddRange([3, bound, value]);
                return value;
            });
            JsonElement expected = test.GetProperty("expected");
            assert(result == (expected.GetProperty("result").GetInt32() != 0),
                "Spit waits through frame 29 and completes from frame 30 even when hidden or distant.");
            assert(host.Events.SequenceEqual(Values(expected.GetProperty("events"))),
                "Source spit sound, mouth hook, random bounds/X-Z order and particle construction remain ordered.");
            JsonElement particles = expected.GetProperty("particles");
            assert(host.Particles.Count == particles.GetArrayLength() && randomCalls == host.Particles.Count * 2,
                "Visible near actors emit exactly six particles and twelve independent random calls.");
            for (int index = 0; index < host.Particles.Count; index++)
            {
                ThugSpitParticle particle = host.Particles[index];
                long[] actual = [particle.Position.X, particle.Position.Y, particle.Position.Z,
                    particle.Velocity.X, particle.Velocity.Y, particle.Velocity.Z, particle.Lifetime,
                    particle.VerticalAcceleration, particle.StartRgb, particle.EndRgb, particle.AdditionalCodeFlags];
                assert(actual.SequenceEqual(particles[index].EnumerateArray().Select(value => value.GetInt64())),
                    "Spit preserves fixed-table direction, mouth placement, lifetime, gravity, colors and source code flag.");
            }
        }
    }

    private static int[] Values(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(JsonElement element)
    {
        int[] values = Values(element);
        return new(values[0], values[1], values[2]);
    }

    private sealed class Host : IThugSpitHost
    {
        public int AnimationFrame { get; init; }
        public ushort ItemFlags { get; init; }
        public FixedVector3 Position { get; init; }
        public FixedVector3 PlayerPosition { get; init; }
        public ActorAngles Angles { get; init; }
        public FixedVector3 Mouth { get; init; }
        public List<int> Events { get; } = [];
        public List<ThugSpitParticle> Particles { get; } = [];
        public FixedVector3 GetDynamicHookPosition(ActorHook hook)
        {
            Events.AddRange([2, hook.X, hook.Y, hook.Z, hook.PartIndex]);
            return Mouth;
        }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) =>
            Events.AddRange([1, sound, position.X, position.Y, position.Z, volume]);
        public void EmitSpitParticle(ThugSpitParticle particle)
        {
            Particles.Add(particle);
            Events.AddRange([4, particle.Position.X, particle.Position.Y, particle.Position.Z,
                particle.Velocity.X, particle.Velocity.Y, particle.Velocity.Z, particle.Lifetime, particle.VerticalAcceleration]);
        }
    }
}
