using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class ParticleSchedulerTests
{
    private sealed class Bit(int id, SourceParticleList list, int kind, int target, bool alive)
    {
        public int Id { get; } = id;
        public SourceParticleList List { get; } = list;
        public int Kind { get; } = kind;
        public int Target { get; } = target;
        public bool Alive { get; set; } = alive;
        public bool Fired { get; set; }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ParticleSchedulerTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.particle-scheduler.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            SourceParticleScheduler scheduler = new();
            List<int> events = [];
            int nextId = 0, tick = 0;
            void Create(SourceParticleList list, int kind, int target, bool alive)
            {
                Bit bit = new(nextId++, list, kind, target, alive);
                scheduler.Attach(list, new(bit, _ => Move(bit), () => bit.Alive,
                    () => { events.AddRange([-1, bit.Id]); scheduler.Detach(bit); }));
            }
            void Move(Bit bit)
            {
                events.AddRange([tick, bit.Id]);
                if (bit.Fired) return;
                bit.Fired = true;
                IReadOnlyList<object> list = scheduler.Identities(bit.List);
                int index = list.ToList().IndexOf(bit);
                Bit? next = index >= 0 && index + 1 < list.Count ? (Bit)list[index + 1] : null;
                switch (bit.Kind)
                {
                    case 1: Create((SourceParticleList)bit.Target, 0, 0, true); break;
                    case 2: bit.Alive = false; break;
                    case 3: scheduler.Detach(bit); break;
                    case 4: if (next is not null) scheduler.Detach(next); break;
                    case 5: if (next is not null) next.Alive = false; break;
                }
            }
            foreach (JsonElement bit in test.GetProperty("bits").EnumerateArray())
                Create((SourceParticleList)bit[0].GetInt32(), bit[1].GetInt32(),
                    bit[2].GetInt32(), bit[3].GetInt32() == 0);
            for (tick = 1; tick <= 2; tick++)
            {
                scheduler.Move((uint)tick);
                scheduler.RemoveDead();
            }
            JsonElement expected = test.GetProperty("expected");
            assert(events.SequenceEqual(expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt32())),
                "Mixed bit phases and live mutations match actual PC Move/RemoveDeadBits.");
            int count = 0;
            for (int list = 0; list < 14; list++)
            {
                IReadOnlyList<object> actual = scheduler.Identities((SourceParticleList)list);
                count += actual.Count;
                assert(actual.Cast<Bit>().Select(bit => bit.Id).SequenceEqual(
                    expected.GetProperty("heads")[list].EnumerateArray().Select(value => value.GetInt32())),
                    "Source bit list ownership and head insertion match actual PC after cleanup.");
            }
            assert(scheduler.Count == count, "Detached source effects release their scheduler registration.");
        }
        VerifyDeleteAll(assert);
    }

    private static void VerifyDeleteAll(Action<bool, string> assert)
    {
        // Independent original408310 order, including the four DeleteBitList calls after the inlined heads.
        SourceParticleList[] order = [SourceParticleList.NonRendered, SourceParticleList.TextBox, SourceParticleList.Flat,
            SourceParticleList.Linked2EndedLeftover, SourceParticleList.Pixel, SourceParticleList.PolyLine,
            SourceParticleList.GPolyLine, SourceParticleList.Quad, SourceParticleList.GenPoly, SourceParticleList.Chunk,
            SourceParticleList.Glow, SourceParticleList.Glass, SourceParticleList.GLine, SourceParticleList.Special];
        var scheduler = new SourceParticleScheduler();
        List<string> events = [];
        int aliveReads = 0;
        foreach (SourceParticleList list in order.Reverse())
        {
            foreach (bool alive in new[] { true, false })
            {
                object identity = new();
                string name = $"{list}:{(alive ? "alive" : "dead")}";
                scheduler.Attach(list, new(identity, _ => throw new InvalidOperationException("DeleteAll must never move a bit."),
                    () => { aliveReads++; return alive; },
                    () => { events.Add(name); scheduler.Detach(identity); }));
            }
        }
        scheduler.DeleteAll();
        assert(aliveReads == 0 && events.SequenceEqual(order.SelectMany(list => new[] { $"{list}:dead", $"{list}:alive" })) && scheduler.Count == 0 &&
            order.All(list => scheduler.Identities(list).Count == 0),
            "Original Bit_DeleteAll destroys both live/dead unprotected bits in all14 source heads, preserving reverse constructor order within each list.");

        events.Clear();
        Dictionary<SourceParticleList, object> protectedBits = [];
        foreach (SourceParticleList list in order)
        {
            object ordinary = new(), protectedIdentity = new();
            protectedBits.Add(list, protectedIdentity);
            scheduler.Attach(list, new(ordinary, _ => { }, () => false,
                () => { events.Add(list.ToString()); scheduler.Detach(ordinary); }, () => false));
            scheduler.Attach(list, new(protectedIdentity, _ => { }, () => false,
                () => throw new InvalidOperationException("A protected source bit must retain its actual destructor owner."), () => true));
        }
        string? failure = null;
        try { scheduler.DeleteAll(); } catch (InvalidOperationException error) { failure = error.Message; }
        assert(failure is not null && failure.Contains("retained 14 source bits", StringComparison.Ordinal) &&
            events.SequenceEqual(order.Select(list => list.ToString())) && scheduler.Count == 14 &&
            protectedBits.All(entry => scheduler.Identities(entry.Key).SequenceEqual(new[] { entry.Value })),
            "Protected bits survive even when dead; DeleteAll still visits every later head before asserting that actual protected registrations remain.");
        foreach (object identity in protectedBits.Values) scheduler.Detach(identity);
        scheduler.DeleteAll();
        assert(scheduler.Count == 0, "Once real companion owners detach protected registrations, the complete source empty-head assertion succeeds.");

        events.Clear();
        object owner = new(), companion = new(), tail = new(), sameListNewborn = new(), earlierListNewborn = new(), laterListNewborn = new();
        int companionDestructions = 0;
        void DestroyCompanion() { companionDestructions++; events.Add("companion"); scheduler.Detach(companion); }
        void AttachSimple(SourceParticleList list, object identity, string name, bool protect = false) =>
            scheduler.Attach(list, new(identity, _ => { }, () => true,
                () => { events.Add(name); scheduler.Detach(identity); }, () => protect));
        AttachSimple(SourceParticleList.GPolyLine, tail, "tail");
        scheduler.Attach(SourceParticleList.GPolyLine, new(companion, _ => { }, () => true,
            DestroyCompanion, () => true));
        scheduler.Attach(SourceParticleList.GPolyLine, new(owner, _ => { }, () => false, () =>
        {
            events.Add("owner");
            // The source owner deletes its protected child, which is the next entry captured by DeleteAll before this destructor.
            DestroyCompanion(); scheduler.Detach(owner);
            AttachSimple(SourceParticleList.GPolyLine, sameListNewborn, "same-list newborn");
            AttachSimple(SourceParticleList.Pixel, earlierListNewborn, "earlier-list newborn");
            AttachSimple(SourceParticleList.Quad, laterListNewborn, "later-list newborn");
        }));
        failure = null;
        try { scheduler.DeleteAll(); } catch (InvalidOperationException error) { failure = error.Message; }
        assert(companionDestructions == 1 && events.SequenceEqual(new[] { "owner", "companion", "tail", "later-list newborn" }) &&
            scheduler.Count == 2 && scheduler.Identities(SourceParticleList.GPolyLine).SequenceEqual(new[] { sameListNewborn }) &&
            scheduler.Identities(SourceParticleList.Pixel).SequenceEqual(new[] { earlierListNewborn }) && failure is not null,
            "Captured-next destruction skips an already destroyed companion, retains its source next tail and visits new later-head bits; same/earlier-head newborns correctly remain for the final assertion.");
        scheduler.DeleteAll();
        assert(events.TakeLast(2).SequenceEqual(new[] { "earlier-list newborn", "same-list newborn" }) && scheduler.Count == 0,
            "A subsequent source DeleteAll starts from the actual remaining heads and releases earlier/same-list newborns in the original phase order.");

        events.Clear();
        object protectedEarlier = new(), laterOwner = new();
        AttachSimple(SourceParticleList.Flat, protectedEarlier, "protected flat companion", protect: true);
        scheduler.Attach(SourceParticleList.GPolyLine, new(laterOwner, _ => { }, () => true, () =>
        {
            events.Add("later owner"); events.Add("protected flat companion");
            scheduler.Detach(protectedEarlier); scheduler.Detach(laterOwner);
        }));
        scheduler.DeleteAll();
        assert(events.SequenceEqual(new[] { "later owner", "protected flat companion" }) && scheduler.Count == 0,
            "A later-phase owner may delete its already visited protected Flat companion before Bit_DeleteAll performs its final global empty-head assertions.");
    }
}
