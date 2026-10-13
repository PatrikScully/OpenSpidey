using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ManipulationChunkTests
{
    private sealed class Host : IManipulationImpactHost
    {
        public List<long> Events { get; } = [];
        public uint Seed { get; set; }
        public int Ground { get; set; }
        public bool AllocateDebris { get; set; }
        public IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder { get; set; } = [];
        public FixedVector3 PlayerPosition { get; set; }
        public FixedVector3 CameraPositionWholeUnits { get; set; }
        public int Random(int range)
        { Events.AddRange([1, range]); Seed = unchecked(Seed * 1664525u + 1013904223u); return range > 0 ? (int)((Seed >> 1) % (uint)range) : 0; }
        public int GroundHeight(FixedVector3 p, int above, int below, int flags)
        { Events.AddRange([11, p.X, p.Y, p.Z, above, below, flags]); return Ground; }
        public void PlayPositionSound(ushort sound, FixedVector3 p, int volume) => Events.AddRange([12, sound, p.X, p.Y, p.Z, volume]);
        public void SendExplosionMessage(object target, FixedVector3 p) => Events.AddRange([10, (int)target, 10, 0, 1, p.X, p.Y, p.Z]);
        public void CreateBigExplosion(FixedVector3 p) => Events.AddRange([13, p.X, p.Y, p.Z]);
        public void HitPlayer(ActorHit hit) => RecordHit(0, hit);
        public void HitBaddy(object target, ActorHit hit) => RecordHit((int)target, hit);
        private void RecordHit(int identity, ActorHit hit)
        {
            Events.AddRange([5, identity, hit.Flags, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z]);
            if ((hit.Flags & 16) != 0) Events.AddRange([hit.SlideDistance, hit.SlideFrames]);
        }
        public void ReactSymbioteToExplosion(object target) => Events.AddRange([6, (int)target]);
        public void TrySpawnDebris(Func<ManipulationDebrisRequest> factory)
        {
            if (!AllocateDebris) return;
            ManipulationDebrisRequest request = factory();
            Events.Add(15);
            RecordDebris(ManipulationDebrisState.Create(request.ModelChecksum, request.Position, request.Velocity, Random));
        }
        private void RecordDebris(ManipulationDebrisState debris)
        {
            Events.AddRange([7, debris.ModelChecksum, debris.Body.Position.X, debris.Body.Position.Y, debris.Body.Position.Z,
                debris.Body.Velocity.X, debris.Body.Velocity.Y, debris.Body.Velocity.Z, debris.Body.Angles.X, debris.Body.Angles.Y,
                debris.Body.AngularVelocity.X, debris.Body.AngularVelocity.Y, debris.RemainingTicks]);
        }
        public void SpawnChunkSmoke(ManipulationSmokeRequest request)
        { var smoke = new ManipulationChunkSmoke(request, Random); RecordSmoke(request, smoke); }
        public void RecordSmoke(ManipulationSmokeRequest request, ManipulationChunkSmoke smoke) => Events.AddRange([8,
            smoke.Position.X, smoke.Position.Y, smoke.Position.Z, smoke.Origin.X, smoke.Origin.Y, smoke.Origin.Z, request.CameraSide,
            smoke.TargetScale, smoke.Angle, smoke.AngularVelocity, smoke.ColorDecay, smoke.FadeDelay, smoke.VerticalVelocity]);
        public void SendAttachedPulse(ushort node) => Events.AddRange([14, node]);
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ManipulationChunkTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.manipulation-chunk.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            if (input[0] == 0)
            {
                var host = new Host { Ground = (int)input[10], AllocateDebris = input[11] != 0, Seed = (uint)input[12], CameraPositionWholeUnits = Vector(input, 16) };
                var state = new ManipulationState(9, new() { Position = Vector(input, 1), Velocity = Vector(input, 4) })
                { Flags = (int)input[7], ImpactSound = (short)input[8], PulseSent = (byte)input[9],
                    ChunkModels = input.Skip(20).Take((int)input[19]).Select(value => unchecked((uint)value)).ToArray() };
                int bodyStart = 20 + (int)input[19]; host.PlayerPosition = Vector(input, bodyStart + 2);
                host.SourceBaddyOrder = Enumerable.Range(1, 3).Select(index => new ManipulationTargetBody(new(index,
                    Vector(input, bodyStart + index * 5 + 2), 0, (uint)input[bodyStart + index * 5 + 1]), (ushort)input[bodyStart + index * 5])).ToArray();
                ManipulationChunk.Create(state, Vector(input, 13), host, host.Random);
                assert(state.PulseSent == expected[0].GetInt32(), $"Original Chunk case{caseIndex} preserves one-shot attached pulse ownership.");
                assert(host.Events.SequenceEqual(expected[1].EnumerateArray().Select(value => value.GetInt64())),
                    $"Original Chunk case{caseIndex} preserves ground/message/audio/damage order, strict explosion ranges, normal-dependent debris, allocation failure and smoke constructor RNG.");
            }
            else
            {
                var host = new Host { Seed = (uint)input[8] };
                var request = new ManipulationSmokeRequest(Vector(input, 1), Vector(input, 4), (int)input[7]);
                var smoke = new ManipulationChunkSmoke(request, host.Random); host.RecordSmoke(request, smoke);
                assert(host.Events.SequenceEqual(expected[0].EnumerateArray().Select(value => value.GetInt64())), $"Original smoke case{caseIndex} preserves constructor random order including Rnd0, position, origin, side, scale, fade timing and rise velocity.");
                int update = 0;
                foreach (JsonElement frame in expected[1].EnumerateArray())
                {
                    smoke.Advance();
                    long[] actual = [smoke.Position.X, smoke.Position.Y, smoke.Position.Z, smoke.Angle, smoke.AngularVelocity, smoke.Scale,
                        smoke.Age, smoke.CodeRgb, smoke.Alive ? 0 : 1];
                    assert(actual.SequenceEqual(frame.EnumerateArray().Select(value => value.GetInt64())),
                        $"Original smoke case{caseIndex} update{update} preserves short wrapping, angular damping, contraction, rise, fade and lifetime."); update++;
                }
                assert(smoke.PostScale == 0x10001000 && smoke.SemiTransparencyRate == 32 && ManipulationChunkSmoke.AnimationIndex == 14 && ManipulationChunkSmoke.Frame == 0,
                    "Original CFlatBit and CChunkSmoke retain exact post-scale, transparency and fixed animation frame.");
            }
            caseIndex++;
        }
    }
    private static FixedVector3 Vector(long[] input, int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
}
