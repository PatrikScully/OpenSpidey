using System.Diagnostics;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.World;

/// <summary>Real L1A3 CHostage354 factory plus untouched point359's source10 distant-rescue signal.</summary>
internal static class HostageMissionSignalImportChecks
{
    public static async Task VerifyAsync(StoryLevelSession story, Action<bool, string> assert)
    {
        var player = story.Player ?? throw new InvalidDataException("Distant rescue needs the actual L1A3 player.");
        GodotEnemyRuntime enemies = Descendants(story).OfType<GodotEnemyRuntime>().Single();
        TriggerNode hostageNode = story.Registry.Triggers.Nodes[354], signalNode = story.Registry.Triggers.Nodes[359];
        IReadOnlyList<TriggerInstruction> commands = TriggerCommandReader.Read(signalNode);
        assert(story.Application.MissionName.Equals("L1A3_T", StringComparison.OrdinalIgnoreCase) && enemies.IsPlayerBound &&
            enemies.Modules.Modules.IsLoaded("hostage") &&
            TriggerActorDefinition.Read(hostageNode).ActorType == 305 && commands.Count == 1 && commands[0].Opcode == 10 &&
            MissionRuntime.ReadLinks(signalNode).SequenceEqual(new ushort[] { 354 }) &&
            !story.Registry.SourceActors(ActorUpdateGroup.Enemy).Any(actor => actor.SourceNodeId == 354),
            "The focused rescue uses untouched354/305 and original359/10→354 under the actual module/player/difficulty owners; it installs no replacement actor or script.");
        int bodies = story.Registry.LiveCount, leases = story.Registry.BaddyConstruction.State.Count;
        story.Registry.Spawn(hostageNode);
        GodotHostageActor hostage = story.Registry.SourceActors(ActorUpdateGroup.Enemy).OfType<GodotHostageActor>()
            .Single(actor => actor.SourceNodeId == 354);
        assert(story.Registry.LiveCount == bodies + 1 && story.Registry.BaddyConstruction.State.Count == leases + 1 &&
            hostage.State.State == HostageBehaviorState.Initializing && hostage.SourceUpdates == 0 &&
            hostage.State.InputFlags == 0 && hostage.ScriptState.PendingSignals == 0 && Distance() >= 200 &&
            ReferenceEquals(enemies.SceneOwnership.Actors.RecoverNative(hostage), hostage),
            "The real354 constructor owns its genuine model/shadow/native identity and one CBaddy lease while the actual player remains outside the original200-unit proximity gate.");
        int signalVisits = 0;
        uint signalClock = 0;
        HostageBehaviorState? stateAtSignal = null;
        story.Mission.Trace += OnTrace;
        try
        {
            story.Mission.Queue(signalNode.Id);
            var deadline = Stopwatch.StartNew();
            while (signalVisits == 0 || hostage.State.State != HostageBehaviorState.GettingUp)
            {
                if (story.Failure is { } failure) throw new InvalidDataException("Actual distant-rescue mission/AI failed.", failure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Genuine359 signal did not free distant354. " + Diagnostics());
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            assert(signalVisits == 1 && stateAtSignal == HostageBehaviorState.WaitingForPlayer &&
                hostage.SourceUpdates >= 2 && hostage.State.GroundHeight != -1 && Distance() >= 200 &&
                ((hostage.State.RescueFlags & 1) != 0) == (story.Registry.Difficulty is 0 or 1) &&
                hostage.ScriptState.PendingSignals == 1 && hostage.State.InputFlags == 1 &&
                hostage.ScriptState.PulsesReceived == 0 && ReferenceEquals(enemies.Hostages.SourcePlayer, player),
                "Only the genuine retained mInputFlags bit from source359 frees distant354 on its next real AI; rescue bit1 follows the original difficulty0/1 gate while GetUp preserves the signal without control-style consumption or pulse-byte increments. " + Diagnostics());
            int updates = hostage.SourceUpdates;
            while (hostage.SourceUpdates == updates)
            {
                if (story.Failure is { } failure) throw new InvalidDataException("Actual hostage GetUp failed.", failure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Distant354 did not advance its genuine GetUp animation.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            assert(hostage.State.State == HostageBehaviorState.GettingUp && hostage.Model.ClipId == 2 &&
                hostage.State.InputFlags == 1 && hostage.ScriptState.PendingSignals == 1 && hostage.ScriptState.PulsesReceived == 0 && Distance() >= 200,
                "Actual CHostage GetUp plays original clip2 and retains the same source signal field through another source update, with no nearby-player substitute.");
            hostage.Model.VerifyCurrentPose();
        }
        finally { story.Mission.Trace -= OnTrace; }

        void OnTrace(MissionTrace trace)
        {
            if (trace.Kind != MissionTraceKind.CommandCompleted || trace.NodeId != 359 || trace.Opcode != 10) return;
            signalVisits++; signalClock = story.SourceClock; stateAtSignal = hostage.State.State;
            assert(signalVisits == 1 && hostage.ScriptState.PendingSignals == 1 && hostage.State.InputFlags == 1 && Distance() >= 200 &&
                hostage.State.State == HostageBehaviorState.WaitingForPlayer && story.Player == player,
                "The actual MissionEventPump executes untouched359/10 after world AI and writes the same retained ushort seen by waiting354, before its next CheckIfFreed. " + Diagnostics());
        }
        uint Distance() => ActorLifecycle.Distance(player.Controller.Position, hostage.SourcePosition);
        string Diagnostics() => $"clock={story.SourceClock}, signalClock={signalClock}, visits={signalVisits}, stateAtSignal={stateAtSignal}, " +
            $"state={hostage.State.State}/{hostage.State.Substate}, updates={hostage.SourceUpdates}, inputFlags={hostage.State.InputFlags:X4}/{hostage.ScriptState.PendingSignals:X4}, " +
            $"pulses={hostage.ScriptState.PulsesReceived}, difficulty={story.Registry.Difficulty}, rescueFlags={hostage.State.RescueFlags:X4}, " +
            $"distance={Distance()}, player={player.Controller.Position}, hostage={hostage.SourcePosition}, ground={hostage.State.GroundHeight}.";
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
