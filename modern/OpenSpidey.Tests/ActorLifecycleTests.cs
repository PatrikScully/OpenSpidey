using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Assets.Scripting;

internal static class ActorLifecycleTests
{
    public static void Run(Action<bool, string> assert)
    {
        var actor = new ActorLifecycle(true, false);
        assert(actor.PrepareUpdate(100, 100) == ActorUpdateAction.RunBehavior && actor.PrepareUpdate(101, 100) == ActorUpdateAction.Skip &&
            actor.Phase == ActorLifePhase.Suspended, "Distance suspension has the original inclusive resume/exclusive suspend boundary.");
        assert(!actor.ResumeNearby(101, 100) && actor.ResumeNearby(100, 100), "Nearby actors resume without running behavior in the resume pass.");
        actor.Kill();
        assert(actor.PrepareUpdate(0, 100) == ActorUpdateAction.Skip && actor.Phase == ActorLifePhase.PendingDeletion &&
            actor.PrepareUpdate(0, 100) == ActorUpdateAction.Delete, "Actor death preserves its separate pending-deletion update.");
        actor = new(true, false);
        actor.Kill(); actor.Suspend();
        assert(actor.ResumeNearby(0, 100) && actor.Phase == ActorLifePhase.Dead,
            "Suspension and death remain independent when a dead actor resumes.");
        actor = new(false, true);
        assert(!actor.ResumeNearby(0, 100), "Actors without the radial flag do not automatically resume.");
        assert(ActorLifecycle.Distance(default, new(4 << 12, 8 << 12, 12 << 12)) == 17,
            "Actor distance uses largest plus half-middle plus quarter-smallest source axes.");
        assert(ActorLifecycle.Distance(default, new(int.MinValue, 0, 0)) == 4294836224u,
            "Actor distance retains wrapping absolute values and the source signed shift before unsigned conversion.");
        assert(MissionLevelId.FromAssetName("data/L1A1_G.psx") == 257 && MissionLevelId.FromAssetName("data/lBa3_t.trg") == 4355 &&
            MissionLevelId.FromAssetName("data/D1A2_T.trg") == 0x9902, "Mission IDs preserve numeric, extended-letter and demo level codes.");

        TriggerNode source = BuildPoint();
        TriggerNode[] nodes = [source, new(1, 1, 0, default), new(2, 7, 0, default), new(3, 5, 0, default)];
        var signals = new Signals();
        foreach (MissionOpcode operation in new[] { MissionOpcode.SendSignal, MissionOpcode.SendSuspend, MissionOpcode.SendActivate })
            new ActorSignalCommandHandler(operation, new(nodes), signals).Execute(new(source, 0), new((ushort)operation, 0, default));
        assert(signals.Calls.SequenceEqual(new[] { "signal:2", "signal:1", "signal:2", "suspend:2", "suspend:1", "suspend:2",
            "activate:2", "activate:1", "activate:2" }), "Actor messages preserve duplicate/source link order and ignore power-up targets.");
    }

    private static TriggerNode BuildPoint()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)6); writer.Write((ushort)4);
        foreach (ushort id in new ushort[] { 2, 3, 1, 2 }) writer.Write(id);
        writer.Write(0u); writer.Write(ushort.MaxValue);
        return new(0, 6, 0, stream.ToArray());
    }
    private sealed class Signals : IMissionActorSignals
    {
        public List<string> Calls { get; } = [];
        public void SignalActiveActors(ushort nodeId) => Calls.Add("signal:" + nodeId);
        public void SuspendActiveActors(ushort nodeId) => Calls.Add("suspend:" + nodeId);
        public void ActivateActiveActors(ushort nodeId) => Calls.Add("activate:" + nodeId);
    }
}
