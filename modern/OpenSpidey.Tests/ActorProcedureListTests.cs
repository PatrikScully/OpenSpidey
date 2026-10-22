using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class ActorProcedureListTests
{
    public static void Run(Action<bool, string> assert)
    {
        var calls = new List<int>();
        var list = new ActorProcedureList();
        var old = new Probe(258, calls);
        var movement = new Probe(1794, calls);
        var latest = new Probe(259, calls);
        list.Attach(old);
        list.Attach(movement);
        list.Attach(latest);
        assert(old.Flags == 1 && movement.Flags == 0 && latest.Flags == 0,
            "Attaching a new facing procedure marks only previous facing procedures.");
        list.Execute(2);
        assert(calls.SequenceEqual([259, 1794, 258]) && list.Count == 2 && old.ExecuteNext is null,
            "Source execution visits the head first and retires marked entries after the full chain.");
        calls.Clear();
        list.Mark(stopCategory: 0x700);
        assert(movement.Flags == 4 && latest.Flags == 0, "Category stop remains distinct from immediate retirement.");
        list.Execute(2);
        assert(calls.SequenceEqual([259, 1794]) && list.Count == 2,
            "Stop flags alone persist until the procedure's Wait handles them.");
        list.Mark(category: 0x700);
        list.CleanUp();
        calls.Clear();
        list.Execute(2);
        assert(calls.SequenceEqual([259]) && list.Count == 1,
            "Unlinking a retired tail updates the surviving continuation.");

        var protectedProcedure = new Probe(0x60601, calls);
        list.Attach(protectedProcedure);
        var body = new ActorBodyMotion
        {
            Position = new(1, 2, 3), Acceleration = new(4, 5, 6), Velocity = new(7, 8, 9),
            LocalVelocity = new(10, 11, 12), AngularVelocity = new(13, 14, 15), AngularAcceleration = new(16, 17, 18)
        };
        var state = new ActorProcedureState(body) { BehaviorFlags = uint.MaxValue };
        state.Neutralize(list);
        assert(latest.Flags == 1 && protectedProcedure.Flags == 0 && state.BehaviorFlags == 0xB7FFFFFB,
            "Neutralize retires ordinary AI while preserving protected state-switch procedures.");
        assert(body.Position == new FixedVector3(1, 2, 3) && body.Acceleration == default && body.Velocity == default &&
            body.LocalVelocity == default && body.AngularVelocity == default && body.AngularAcceleration == default,
            "Neutralize clears all body motion while retaining position.");
        list.CleanUp(all: true);
        assert(list.Count == 0, "Actor destruction removes protected and ordinary procedures alike.");

        var traveler = new ActorProcedureState(new ActorBodyMotion());
        var move = new ActorMoveToProcedure(traveler, new(409600, 0, 0), 48, 70, 1024, 1);
        var route = new ActorProcedureList();
        route.AttachMove(move);
        route.Execute(2);
        assert(traveler.Body.Angles.Y == -280 && move.Started && traveler.Body.Position.X > 0,
            "Facing executes before movement and supplies the alignment signal within the same update.");
        int updates = 1;
        while ((traveler.CompletionFlags & 1) == 0 && updates++ < 100) route.Execute(2);
        assert(traveler.Body.Position == new FixedVector3(409600, 0, 0) && route.Count == 1,
            "Waypoint travel reaches the exact destination and retires only its completed movement procedure.");
        route.Mark(ordinary: true);
        route.CleanUp();
        assert(route.Count == 0, "Neutralizing after arrival retires the persistent facing procedure.");

        var mutation = new ActorProcedureList();
        calls.Clear();
        var added = new Probe(1025, calls);
        var spawning = new Probe(1794, calls) { BeforeNext = () => mutation.Attach(added) };
        mutation.Attach(spawning);
        mutation.Execute(2);
        assert(calls.SequenceEqual([1794]) && mutation.Count == 2,
            "An AI procedure attached while executing starts on the following update.");
        spawning.BeforeNext = null;
        calls.Clear();
        mutation.Execute(2);
        assert(calls.SequenceEqual([1025, 1794]), "The newly attached procedure is the next update's head.");
    }

    private sealed class Probe(int sourceType, List<int> calls) : IActorProcedure
    {
        public int SourceType => sourceType;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public Action? BeforeNext { get; set; }
        public void Execute(int elapsedTicks)
        {
            calls.Add(SourceType);
            BeforeNext?.Invoke();
            ExecuteNext?.Invoke();
        }
    }
}
