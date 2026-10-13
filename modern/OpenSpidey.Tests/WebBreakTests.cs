using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class WebBreakTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebBreakTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-break.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            FixedVector3 Vector(int index) => new(input[index], input[index + 1], input[index + 2]);
            var host = new Host(input[9]);
            using var owner = new WebStrandOwner(host);
            owner.Fire(Vector(3), Vector(0), null, false, default, Vector(6), host.Random);
            host.Reset(input[9]);
            owner.BreakIntoBits(host, Vector(6), host.Random);
            JsonElement expected = test.GetProperty("expected");
            assert(owner.BodyFlags == expected[0].GetUInt32() && owner.Phase == ActorLifePhase.Dead,
                "Actual PC rejected tug marks the body dead immediately while source strands remain owned until the player-list destructor.");
            assert(host.Events.SequenceEqual(expected[1].EnumerateArray().Select(value => value.GetInt32())),
                "Actual PC BreakIntoBits preserves fragment count thresholds, camera-side impulses, flip/gravity RNG, fragment constructors and odd-index glow interleaving.");
            assert(host.Glows.Select(glow => (int)glow.Angle).SequenceEqual(expected[2].EnumerateArray().Select(value => value.GetInt32())),
                "Actual PC rejected tug chooses each glow angle after its constructor radii have consumed the shared random stream.");
            foreach (KnottedWebFragment fragment in host.Fragments) fragment.Dispose();
        }
    }
    private sealed class Host(int seed) : IWebStrandOwnerHost, IWebStrandSnapHost
    {
        private int cursor = seed;
        public List<int> Events { get; } = [];
        public List<KnottedWebFragment> Fragments { get; } = [];
        public List<GlowFlashState> Glows { get; } = [];
        public void Reset(int initial) { cursor = initial; Events.Clear(); }
        public int Random(int bound) { int value = (cursor++ * 7 + 1) % bound; Events.AddRange([1, bound, value]); return value; }
        public object PlayerIdentity => this;
        public int PlayerWebShot => 0;
        public FixedVector3 PlayerPosition => default;
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => default;
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => null;
        public void AttachOwner(WebStrandOwner owner) { }
        public void DetachOwner(WebStrandOwner owner) { }
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) => throw new InvalidOperationException();
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) => throw new InvalidOperationException();
        public FixedVector3 SourcePosition(IWebStrandTarget target) => throw new InvalidOperationException();
        public FixedVector3 TargetAttachment(IWebStrandTarget target) => throw new InvalidOperationException();
        public bool TugImpulse(IWebStrandTarget target, FixedVector3 contact, FixedVector3 direction, IReadOnlyList<FixedVector3>? path) => throw new InvalidOperationException();
        public void SetFirstContactDetails() => throw new InvalidOperationException();
        public void EmitBrokenStrand(KnottedWebFragmentRequest request, Func<int, int> random)
        {
            Events.AddRange([4, request.Start.X, request.Start.Y, request.Start.Z, request.End.X, request.End.Y, request.End.Z,
                request.StartVelocity.X, request.StartVelocity.Y, request.StartVelocity.Z, request.EndVelocity.X, request.EndVelocity.Y, request.EndVelocity.Z]);
            Fragments.Add(new(request, random, position => { Events.AddRange([3, position.X, position.Y, position.Z]); return unchecked(position.Y + 0x1388000); }));
        }
        public GlowFlashState EmitSnapGlow(GlowFlashParameters value, Func<int, int> random)
        {
            Events.AddRange([5, value.Position.X, value.Position.Y, value.Position.Z, value.Points, value.CentreRed, value.CentreGreen,
                value.CentreBlue, value.CentreFadeRate, value.SectionRed, value.SectionGreen, value.SectionBlue, value.SectionFadeRate,
                value.FringeWidth, value.WidthDelta, value.ReselectRadii, value.OddRadius, value.OddRange, value.EvenRadius,
                value.EvenRange, value.RadiusDecay, value.Lifetime]);
            var glow = new GlowFlashState(value, random); Glows.Add(glow); return glow;
        }
    }
}
