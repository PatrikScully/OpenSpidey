using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class WebOwnerTests
{
    private sealed class Host(int shot) : IWebStrandOwnerHost
    {
        public object PlayerIdentity { get; } = new();
        public int PlayerWebShot => shot;
        public int AttachCount, DetachCount;
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => target is null ? default : new(target.Identity, 123);
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle)
        {
            if (handle.Target is not null) throw new InvalidDataException("No-target owner fixture received an unexpected live handle.");
            return null;
        }
        public void AttachOwner(WebStrandOwner owner) => AttachCount++;
        public void DetachOwner(WebStrandOwner owner) => DetachCount++;
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) =>
            throw new InvalidOperationException("No-target fixture excludes actual splat allocation.");
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) =>
            throw new InvalidOperationException("No-target fixture excludes actual trap actors.");
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebOwnerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-owner.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            FixedVector3 Vector(int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
            var host = new Host(input[12]); var owner = new WebStrandOwner(host); int cursor = 0;
            int Random(int bound) => (cursor++ * 7 + 1) % bound;
            owner.Fire(Vector(0), Vector(3), null, false, default, Vector(9), Random);
            Compare(expected[0], 0);
            owner.SetFirePosition(Vector(6), Vector(9), Random); Compare(expected[1], 1);
            owner.SwitchToBlob(); Compare(expected[2], 2);
            bool deleted = false;
            for (int frame = 3; frame < expected.Length; frame++)
            {
                uint sourceClock = unchecked((uint)(input[14] + (frame - 2) * input[13]));
                if (!deleted) deleted = owner.AdvanceActor(sourceClock, Vector(9)) == ActorUpdateAction.Delete;
                Compare(expected[frame], frame);
            }
            if (owner.Mode == 4)
                assert(deleted && host.DetachCount == 1, "Direct CPlayer WebList walk deletes Body40 on its next visit, without generic Ob_AI pending-deletion stage.");
            owner.Dispose(); owner.Dispose();
            assert(host.AttachCount == 1 && host.DetachCount == 1, "CWeb owns exactly one attach and destructor detach, including explicit unload after normal retirement.");
            caseIndex++;

            void Compare(JsonElement state, int frame)
            {
                var actual = new List<long> { owner.Mode, owner.Age, owner.BodyFlags, owner.Strand is null ? 0 : 1,
                    owner.LastUpdateTick, owner.ElapsedTicks, owner.AccumulatedTicks };
                if (owner.Strand is { } strand)
                {
                    actual.AddRange([strand.SegmentCount, strand.Start.X, strand.Start.Y, strand.Start.Z, strand.Side.X, strand.Side.Y, strand.Side.Z,
                        strand.FlickerKnots, strand.TriggerPulse]);
                    for (int i = 0; i < strand.SegmentCount; i++) actual.AddRange([strand.Ends[i].X, strand.Ends[i].Y, strand.Ends[i].Z,
                        strand.Knots[i].RestPosition.X, strand.Knots[i].RestPosition.Y, strand.Knots[i].RestPosition.Z]);
                }
                assert(actual.SequenceEqual(state.EnumerateArray().Select(v => v.GetInt64())),
                    $"Web owner case {caseIndex} frame {frame}: extracted original Fire/SetFirePos/Blob/AI line creation, fixed camera-facing endpoints, ushort timer, strict >16 contraction and mode4 body flag.");
            }
        }
    }
}
