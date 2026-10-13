using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class BigExplosionTests
{
    private sealed class Host : IBigExplosionHost, IWibblingExplosionCounter
    {
        public int AllocationMask { get; init; }
        public int Count { get; set; }
        private int allocationIndex;
        public List<WibblingExplosionState> Models { get; } = [];
        public List<long> Events { get; } = [];
        public GameRandom Random { get; } = new(0);
        public int Next(int bound) { int value = Random.Next(bound); Events.AddRange([1, bound, value]); return value; }
        public void ChangeCount(int delta) => Count = unchecked(Count + delta);
        public void TryCreateWibbling(ExplosionModelParameters p, Func<int, int> random)
        {
            bool accepted = (AllocationMask & (1 << allocationIndex)) != 0;
            Events.AddRange([2, allocationIndex++, accepted ? 1 : 0]);
            if (!accepted) return;
            Events.AddRange([3, p.Position.X, p.Position.Y, p.Position.Z, p.ModelIndex, p.Delay, p.InitialScale,
                p.ScaleStep, p.VerticalScale, p.GrowthTicks, p.FadeRate, p.VerticalSpeed, p.VerticalSpeedRange]);
            if (p.ModelName != "expgrnd") throw new InvalidDataException("Original big explosion must keep its actual model library.");
            Models.Insert(0, new(p, this, random));
        }
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(BigExplosionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.big-explosion.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var host = new Host { AllocationMask = input[3], Count = input[7] };
            host.Random.Restore(new(input[4], input[5], input[6]));
            BigExplosion.Create(new(input[0], input[1], input[2]), host, host.Next);
            assert(host.Events.SequenceEqual(expected[0].EnumerateArray().Select(value => value.GetInt64())),
                $"Big explosion case{caseIndex} matches original PC nine emission positions/tuples, source allocation order and constructor RNG only on success.");
            int[] randomState = [host.Random.State.Value, host.Random.State.Multiplier, host.Random.State.Increment];
            assert(randomState.SequenceEqual(expected[1].EnumerateArray().Select(value => value.GetInt32())),
                $"Big explosion case{caseIndex} retains exact source random state through partial/all allocation failure.");
            int frame = 0;
            foreach (JsonElement update in expected[2].EnumerateArray())
            {
                if (frame == input[8] && frame > 0)
                {
                    foreach (WibblingExplosionState model in host.Models) { model.Dispose(); model.Dispose(); }
                    host.Models.Clear();
                }
                else if (frame > 0)
                    foreach (WibblingExplosionState model in host.Models.ToArray())
                        if (model.Advance() == ActorUpdateAction.Delete) host.Models.Remove(model);
                assert(host.Count == update[0].GetInt32(), $"CWibbling case{caseIndex} frame{frame} preserves wrapping global count and decrements only at actual destructor cleanup.");
                JsonElement[] states = update[1].EnumerateArray().ToArray();
                assert(host.Models.Count == states.Length, $"Big explosion case{caseIndex} frame{frame} preserves source BulletList head order and delayed death removal.");
                for (int index = 0; index < states.Length; index++)
                {
                    WibblingExplosionState owned = host.Models[index]; ExplosionModelState model = owned.Model;
                    long[] actual = [model.Position.X, model.Position.Y, model.Position.Z, model.Scale.X, model.Scale.Y, model.Scale.Z,
                        model.VerticalVelocity, model.Tint, model.ItemFlags, model.Stage, model.Delay, model.GrowthAge, model.IsDead ? 1 : 0,
                        model.ModelIndex, (int)owned.Phase];
                    assert(actual.SequenceEqual(states[index].EnumerateArray().Select(value => value.GetInt64())),
                        $"Big explosion case{caseIndex} frame{frame} model{index} matches source position, wrapped scale, growth/fade stage and dead/pending lifetime.");
                }
                frame++;
            }
            foreach (WibblingExplosionState model in host.Models) model.Dispose();
            assert(host.Count == input[7], "Source unload/destructors restore the original wibbling count without grenade-carrier accounting.");
            caseIndex++;
        }
    }
}
