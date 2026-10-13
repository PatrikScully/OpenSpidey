using System.Text.Json;
using OpenSpidey.Core.Actors;

internal static class ActorListTests
{
    private sealed class Body(int id, int list, int kind, int target, ActorLifecycle lifecycle)
    {
        public int Id { get; } = id;
        public int List { get; } = list;
        public int Kind { get; } = kind;
        public int Target { get; } = target;
        public ActorLifecycle Lifecycle { get; } = lifecycle;
        public Body? Next { get; set; }
        public bool Fired { get; set; }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorListTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-list.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            SourceActorList[] lists = Enumerable.Range(0, 7).Select(_ => new SourceActorList()).ToArray();
            List<int> events = [];
            int nextId = 0, tick = 0;
            // Synthetic PC callbacks read mNextItem themselves. Keep their native views in sync
            // with the list's actual observable order; detached bodies retain their previous view.
            void RefreshLinks()
            {
                foreach (SourceActorList list in lists)
                {
                    Body[] bodies = list.SourceOrder.Cast<Body>().ToArray();
                    for (int index = 0; index < bodies.Length; index++)
                        bodies[index].Next = index + 1 < bodies.Length ? bodies[index + 1] : null;
                }
            }
            void Detach(Body body) { lists[body.List].Detach(body); RefreshLinks(); }
            void Create(int list, int kind, int target, int dead)
            {
                ActorLifecycle lifecycle = new(false, false);
                if (dead != 0) lifecycle.Kill();
                if (dead == 2) lifecycle.PrepareUpdate(0, uint.MaxValue);
                Body body = new(nextId++, list, kind, target, lifecycle);
                lists[list].Attach(body, () => Update(body));
                RefreshLinks();
            }
            void Update(Body body)
            {
                switch (body.Lifecycle.PrepareUpdate(0, uint.MaxValue))
                {
                    case ActorUpdateAction.Delete: events.AddRange([-1, body.Id]); Detach(body); return;
                    case ActorUpdateAction.Skip: return;
                }
                events.AddRange([0, body.Id, tick, body.Id]);
                if (body.Fired) return;
                body.Fired = true;
                switch (body.Kind)
                {
                    case 1: Create(body.Target, 0, 0, 0); break;
                    case 2: body.Lifecycle.Kill(); break;
                    case 3: Detach(body); break;
                    case 4: if (body.Next is { } detached) Detach(detached); break;
                    case 5: body.Next?.Lifecycle.Kill(); break;
                }
            }
            foreach (JsonElement body in test.GetProperty("actors").EnumerateArray())
                Create(body[0].GetInt32(), body[1].GetInt32(), body[2].GetInt32(), body[3].GetInt32());
            for (tick = 1; tick <= 3; tick++) foreach (SourceActorList list in lists) list.Update();
            JsonElement expected = test.GetProperty("expected");
            assert(events.SequenceEqual(expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt32())),
                "Captured-next source actor traversal and death staging match original Ob_AI through live mutations.");
            for (int list = 0; list < lists.Length; list++)
                assert(lists[list].SourceOrder.Cast<Body>().Select(body => body.Id).SequenceEqual(
                    expected.GetProperty("heads")[list].EnumerateArray().Select(value => value.GetInt32())),
                    "World actor head ownership matches original Ob_AI across same-family and later-family births.");
        }
    }
}
