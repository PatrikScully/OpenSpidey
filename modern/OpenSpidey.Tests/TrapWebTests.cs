using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class TrapWebTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(TrapWebTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.trap-web.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.True ? 1 :
                value.ValueKind == JsonValueKind.False ? 0 : value.GetInt32()).ToArray();
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            var target = new Target(new(input[11], input[12], input[13]), (byte)input[5], (byte)input[6])
            { ItemFlags = (ushort)input[7], TintColor = (uint)input[8] };
            var host = new Host(target, input[4], input[3]);
            using var effect = new TrapWebEffect(target, (byte)input[0], host, host.Random) { FireVariant = (byte)input[1] };
            Compare(expected[0], 0);
            for (int index = 0; index < input[2]; index++) effect.AddAnotherStrand();
            Compare(expected[1], 1);
            for (int frame = 0; frame < input[9]; frame++) { effect.AdvanceBits(); Compare(expected[frame + 2], frame + 2); }
            host.Expired = input[10] != 0; effect.Burst(); Compare(expected[^1], expected.Length - 1);
            effect.Dispose(); effect.Dispose();
            assert(host.Attached == 1 && host.Detached == 1,
                "Trap-web destruction owns its one nonrendered bit and all line/quad children exactly once.");
            caseIndex++;

            void Compare(JsonElement state, int stage)
            {
                long[] header = [effect.Type, effect.FireVariant, effect.HookCount, effect.StrandCount,
                    effect.ProjectorHeight, effect.ProjectorRadius, effect.ProjectorAngle, effect.ProjectorCountdown,
                    effect.ProjectorSpeed, effect.BurnAngle, effect.BurnRamp, effect.Alive ? 0 : 1, target.ItemFlags,
                    target.TintColor, effect.Start.X, effect.Start.Y, effect.Start.Z, effect.StartColour];
                assert(header.SequenceEqual(state.EnumerateArray().Take(18).Select(value => value.GetInt64())),
                    $"Actual PC trap-web case{caseIndex} stage{stage} preserves projector fields, cached hook count, burning actor tint and strict life state.");
                long[] actualHooks = Enumerable.Range(0, effect.HookCount + 1).SelectMany(index => new long[]
                    { effect.Hooks[index].X, effect.Hooks[index].Y, effect.Hooks[index].Z, effect.Hooks[index].PartIndex, effect.Heights[index] }).ToArray();
                assert(actualHooks.SequenceEqual(Flatten(state[18])), "Original trap hook collision and per-hook signed-short projector height match PC.");
                long[] actualPoints = Enumerable.Range(0, effect.HookCount).SelectMany(index => new long[]
                    { effect.Points[index + 1].X, effect.Points[index + 1].Y, effect.Points[index + 1].Z, effect.Colours[index] }).ToArray();
                assert(actualPoints.SequenceEqual(Flatten(state[19])), "Actual PC animated hook endpoints and independent per-line RNG colours match.");
                long[] actualTriangles = effect.Triangles.SelectMany(strand => new long[]
                    { strand.Newest, strand.Previous, strand.Widest, strand.A.X, strand.A.Y, strand.A.Z,
                        strand.B.X, strand.B.Y, strand.B.Z, strand.C.X, strand.C.Y, strand.C.Z,
                        strand.D.X, strand.D.Y, strand.D.Z, strand.Tint }).ToArray();
                assert(actualTriangles.SequenceEqual(Flatten(state[20])), "Actual PC trap triangles preserve widest-height selection, cap20 and coincident C/D corners.");
                assert(host.Events.Select(value => (long)value).SequenceEqual(state[21].EnumerateArray().Select(value => value.GetInt64())),
                    "Actual PC trap projector/collision/dynamic-hook RNG, burning relocation and ordered Burst fragment leaves match.");
                host.Events.Clear();
            }
        }
        static IEnumerable<long> Flatten(JsonElement rows) => rows.EnumerateArray().SelectMany(row => row.EnumerateArray().Select(value => value.GetInt64()));
    }

    private sealed class Target(FixedVector3 position, byte upper, byte lower) : ITrapWebEffectTarget
    {
        public Host Host { get; set; } = null!;
        public object Identity => this;
        public FixedVector3 Position => position;
        public ushort ItemFlags { get; set; }
        public uint TintColor { get; set; }
        public byte UpperExtent => upper;
        public byte LowerExtent => lower;
        public bool Collide(FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook)
        {
            Host.CollisionIndex++;
            bool hit = Host.MissPeriod == 0 || Host.CollisionIndex % Host.MissPeriod != 0;
            hook = new(unchecked((short)((start.X - Position.X) >> 8)), unchecked((short)((end.Y - Position.Y) >> 8)),
                unchecked((short)((start.Z - Position.Z) >> 8)), hit ? (ushort)(Host.CollisionIndex % 8 + 1) : ushort.MaxValue);
            Host.Events.AddRange([2, start.X, start.Y, start.Z, end.X, end.Y, end.Z, scale, hit ? 1 : 0]);
            return hit;
        }
        public FixedVector3 HookPosition(ActorHook hook)
        {
            Host.Events.AddRange([4, hook.X, hook.Y, hook.Z, hook.PartIndex]);
            return Position + new FixedVector3((hook.X << 8) + 123 * hook.PartIndex, hook.Y << 8, hook.Z << 8);
        }
        public void BuildTransform() => Host.Events.Add(3);
    }
    private sealed class Host : ITrapWebEffectHost
    {
        private readonly Target target;
        private uint cursor;
        public int MissPeriod { get; }
        public int CollisionIndex { get; set; }
        public int Attached, Detached;
        public bool Expired { get; set; }
        public List<int> Events { get; } = [];
        public Host(Target target, int seed, int missPeriod)
        { this.target = target; target.Host = this; cursor = (uint)seed; MissPeriod = missPeriod; }
        public int Random(int bound)
        {
            int value = bound == 0 ? 0 : (int)(unchecked(cursor++ * 7 + 1) % (uint)bound);
            Events.AddRange([1, bound, value]); return value;
        }
        public PlayerKickHandle StoreTarget(ITrapWebEffectTarget target) => new(target.Identity, 999);
        public ITrapWebEffectTarget? RecoverTarget(ref PlayerKickHandle handle)
        {
            if (!Expired) return target;
            handle = handle with { Target = null }; return null;
        }
        public bool ContactExists(ITrapWebEffectTarget target, byte type) => false;
        public void BindContact(ITrapWebEffectTarget target, byte type, TrapWebEffect effect) { }
        public void ClearContact(ITrapWebEffectTarget target, byte type) { }
        public void AttachEffect(TrapWebEffect effect) => Attached++;
        public void DetachEffect(TrapWebEffect effect) => Detached++;
        public void CreateLine(TrapWebLine line) { }
        public void DeleteLine(TrapWebLine line) { }
        public void CreateStrand(TrapWebEffect effect, TrapWebTriangle strand) { }
        public void DeleteStrand(TrapWebTriangle strand) { }
        public int GroundY(FixedVector3 position)
        { Events.AddRange([5, position.X, position.Y, position.Z]); return unchecked(position.Y + 0x1388000); }
        public void CreateFragment(ImpactWebFragmentRequest request, Func<int, int> random) =>
            Events.AddRange([6, request.GroundY, request.Previous.X, request.Previous.Y, request.Previous.Z,
                request.FirstHook.X, request.FirstHook.Y, request.FirstHook.Z, request.SecondHook.X, request.SecondHook.Y,
                request.SecondHook.Z, request.OwnerPosition.X, request.OwnerPosition.Y, request.OwnerPosition.Z, request.Speed, request.Jitter]);
        public void EmitFirePulse(FixedVector3 position, int scale) => Events.AddRange([7, position.X, position.Y, position.Z, scale]);
    }
}
