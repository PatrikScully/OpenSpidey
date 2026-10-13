using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ActorSpriteRingTests
{
    private sealed class Host(int ground) : IActorSpriteRingHost
    {
        public List<int> GroundEvents { get; } = [];
        public List<ActorSpriteRingParticle> Particles { get; } = [];
        public int GroundHeight(FixedVector3 position, int above, int below)
        {
            GroundEvents.AddRange([position.X, position.Y, position.Z, above, below]);
            return ground;
        }
        public void EmitSprite(ActorSpriteRingParticle particle) => Particles.Add(particle);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorSpriteRingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-sprite-ring.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var body = new ActorBodyMotion
            {
                Position = Vector(4), Angles = new((short)input[7], (short)input[8], (short)input[9])
            };
            var host = new Host((int)input[2]);
            int result = 0;
            if (input[0] == 0) result = ActorSpriteRing.Create(body, input[1] != 0 ? Vector(10) : null, host);
            else host.Particles.Add(new(body.Position, Vector(10), 1, (short)input[13], (ushort)input[14], (short)input[15], (ushort)input[16]));
            JsonElement expected = test.GetProperty("expected");
            assert(result == expected[0].GetInt32() && host.GroundEvents.SequenceEqual(expected[1].EnumerateArray().Select(value => value.GetInt32())) &&
                host.Particles.Count == expected[2].GetArrayLength(),
                $"Sprite ring case {caseIndex} preserves exact source actor-center ground query,24 emissions and return value.");
            for (int i = 0; i < host.Particles.Count; i++)
            {
                ActorSpriteRingParticle particle = host.Particles[i];
                var motion = new SpriteRingParticleMotion(particle, (byte)input[3]);
                JsonElement reference = expected[2][i];
                Check(reference[0], "birth");
                long[] actualParameters = [particle.Velocity.X, particle.Velocity.Y, particle.Velocity.Z, particle.ScaleDecay,
                    particle.ColorDecay, particle.FrigDeltaZ, motion.PostScale, motion.SemiTransparencyRate];
                assert(actualParameters.SequenceEqual(reference[1].EnumerateArray().Select(value => value.GetInt64())) &&
                    particle.AnimationIndex == 1 && motion.Angle == 0,
                    "Ring sprites use exact original Q12 heading velocities and SMOKE animation/scale/color/depth/billboard constructor data.");
                int update = 0;
                foreach (JsonElement frame in reference[2].EnumerateArray())
                {
                    motion.Advance();
                    Check(frame, $"update {update++}");
                }
                void Check(JsonElement frame, string stage)
                {
                    long[] actual = [motion.Position.X, motion.Position.Y, motion.Position.Z, motion.Scale, motion.CodeRgb,
                        motion.Age, motion.Frame, motion.Alive ? 0 : 1];
                    assert(actual.SequenceEqual(frame.EnumerateArray().Select(value => value.GetInt64())),
                        $"Ring case {caseIndex} sprite {i} matches machine-confirmed CMotionBlur position/scale/RGB/age/frame/death {stage}.");
                }
            }
            caseIndex++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }
}
