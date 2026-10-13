using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class WebSnapTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebSnapTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-snap.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var target = input[0] < 0 ? null : new Target((ushort)input[0]);
            var host = new Host(target, input[1] != 0, input[4]);
            using var owner = new WebStrandOwner(host);
            owner.Fire(new FixedVector3(123456, -54321, 76543) + new FixedVector3(input[2] << 12, input[2] * 123 - 321, input[2] * 321 + 987),
                new(123456, -54321, 76543), target, false, default, new(500, 300, -700), host.Random);
            owner.SwitchToSnap(new(12345, -23456, 34567), input[3] != 0 ? new FixedVector3[] { new(98765, 87654, 76543) } : null,
                host, new(500, 300, -700), host.Random);
            JsonElement[] states = test.GetProperty("expected").EnumerateArray().ToArray();
            bool deleted = false;
            for (int frame = 0; frame < states.Length; frame++)
            {
                if (frame != 0)
                {
                    if (input[6] != 0 && frame >= input[6]) host.Expired = true;
                    if (!deleted) deleted = owner.AdvanceActor((uint)(500 + frame * input[5]), new(500, 300, -700)) == ActorUpdateAction.Delete;
                }
                JsonElement state = states[frame];
                long[] header = [owner.Mode, owner.Age, owner.BodyFlags, owner.LastUpdateTick, owner.ElapsedTicks, owner.AccumulatedTicks];
                assert(header.SequenceEqual(state.EnumerateArray().Take(6).Select(value => value.GetInt64())),
                    "Actual PC snap mode2 preserves own EveryFrame clock, strict age64 retirement and direct player-list deletion.");
                var line = new List<long>();
                if (owner.Strand is { } strand)
                {
                    line.AddRange([strand.Start.X, strand.Start.Y, strand.Start.Z, strand.TriggerPulse, strand.Pulse]);
                    foreach (FixedVector3 end in strand.Ends) line.AddRange([end.X, end.Y, end.Z]);
                }
                assert(line.SequenceEqual(state[6].EnumerateArray().Select(value => value.GetInt64())),
                    "Actual PC snap updates its line from loose-chain points and clears both yank pulse bytes before target acceptance.");
                var chain = new List<long>();
                if (owner.Chain is { } loose)
                {
                    chain.AddRange([loose.Points.Count, loose.RestLength, loose.RestLengthSquared, loose.Gravity,
                        loose.GroundY, loose.GroundEnabled ? 1 : 0, loose.Friction, loose.Recoil]);
                    foreach (WebChainPoint point in loose.Points)
                        chain.AddRange([point.Position.X, point.Position.Y, point.Position.Z, point.Velocity.X, point.Velocity.Y,
                            point.Velocity.Z, point.PreviousPosition.X, point.PreviousPosition.Y, point.PreviousPosition.Z, point.Anchored ? 1 : 0]);
                }
                assert(chain.SequenceEqual(state[7].EnumerateArray().Select(value => value.GetInt64())),
                    "Actual PC snap constructor, target401 attachment, floor gate122880, source chain motion and expired-handle freezing match.");
                assert(host.Events.Select(value => (long)value).SequenceEqual(state[8].EnumerateArray().Select(value => value.GetInt64())),
                    "Actual PC snap routes player50 rejection, manip401 two-argument tug, baddy path tug and first-contact callback exactly.");
                host.Events.Clear();
            }
        }
    }

    private sealed class Target(ushort type) : IWebStrandTarget
    {
        public object Identity => this;
        public ushort Type => type;
        public uint BehaviorFlags { get; set; }
        public IReadOnlyList<FixedVector3>? ActiveTrapStrandEndpoints => null;
    }
    private sealed class Host(Target? target, bool accepted, int vertical) : IWebStrandOwnerHost, IWebStrandSnapHost
    {
        private uint cursor;
        private bool broke;
        public bool Expired { get; set; }
        public List<int> Events { get; } = [];
        public object PlayerIdentity { get; } = new();
        public int PlayerWebShot => 1;
        public FixedVector3 PlayerPosition => new(123456, -54321 + vertical, 76543);
        public int Random(int bound) => bound == 0 ? 0 : (int)(unchecked(cursor++ * 7 + 1) % (uint)bound);
        public PlayerKickHandle StoreTarget(IWebStrandTarget? actor) => actor is null ? default : new(actor.Identity, 999);
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle)
        {
            if (!Expired) return target;
            handle = handle with { Target = null }; return null;
        }
        public void AttachOwner(WebStrandOwner owner) { }
        public void DetachOwner(WebStrandOwner owner) { }
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) =>
            throw new InvalidOperationException("Snap fixture excludes splat creation.");
        public void ReplaceYankContact(IWebStrandTarget actor, int strandCount) =>
            throw new InvalidOperationException("Snap fixture starts from the ordinary existing strand.");
        public FixedVector3 SourcePosition(IWebStrandTarget actor) => new(123456, -54321, 76543);
        public FixedVector3 TargetAttachment(IWebStrandTarget actor) => SourcePosition(actor) +
            (actor.Type == 401 ? new FixedVector3(1024, 8192, -2048) : default);
        public bool TugImpulse(IWebStrandTarget actor, FixedVector3 contact, FixedVector3 direction, IReadOnlyList<FixedVector3>? path)
        {
            Events.AddRange([1, actor.Type, contact.X, contact.Y, contact.Z, direction.X, direction.Y, direction.Z,
                actor.Type != 401 && path is not null ? 1 : 0]); return accepted;
        }
        public void SetFirstContactDetails() => Events.Add(2);
        // The machine oracle treats broken particle constructors as an explicit leaf; their states have separate proofs.
        public void EmitBrokenStrand(KnottedWebFragmentRequest request, Func<int, int> random)
        { if (!broke) { broke = true; Events.Add(3); } }
        public GlowFlashState EmitSnapGlow(GlowFlashParameters parameters, Func<int, int> random) => new(parameters, random);
    }
}
