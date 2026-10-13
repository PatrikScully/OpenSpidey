using System.Buffers.Binary;
using System.Diagnostics;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L1A4 control239's actual sixty-tick wait and4298 medium shake through the live world/camera phases.</summary>
internal static class ActorCameraShakeImportChecks
{
    public static async Task VerifyAsync(StoryLevelSession story, GodotActorCameraShakeHost host)
    {
        GodotActorRegistry registry = story.Registry;
        PlayerFollowCamera camera = Descendants(story).OfType<PlayerFollowCamera>().Single();
        CameraShakeState state = camera.ShakeState;
        var saved = (state.Amplitude, state.Decay, state.Speed);
        GodotControlActor? actor = null; int applied = 0;
        int count = registry.BaddyConstruction.State.Count;
        Assert(story.Failure is null && story.Startup.Phase == MissionStartupPhase.Playing && host.CameraPresent &&
            story.Application.MissionName.Equals("L1A4_T", StringComparison.OrdinalIgnoreCase),
            "The genuine L1A4 source world and actual camera are bound before the inherited4298 proof.");
        TriggerActorDefinition definition = TriggerActorDefinition.Read(registry.Triggers.Nodes[239]);
        ushort[] words = Enumerable.Range(0, definition.BehaviorData.Length / 2)
            .Select(index => BinaryPrimitives.ReadUInt16LittleEndian(definition.BehaviorData.Span[(index * 2)..])).ToArray();
        Assert(definition.ActorType == 203 && words.SequenceEqual(new ushort[] { 0x4280, 60, 0x4298, 1, 0x4100 }) &&
            definition.Links.Count == 0 && !registry.SourceActors(ActorUpdateGroup.Control).Any(body => body.SourceNodeId == 239),
            "Untouched239 retains exact source wait60,4298 operand1 and Stop without replacement script or extra mission links.");
        host.SourceShakeApplied += OnShake;
        try
        {
            registry.Spawn(registry.Triggers.Nodes[239]);
            actor = registry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single(body => body.SourceNodeId == 239);
            Assert(actor.SourceUpdates == 0 && actor.ScriptState.WaitTicks == 60 && applied == 0 && registry.BaddyConstruction.State.Count == count + 1,
                "Actual239 constructor enters its real sixty-tick wait before camera effects and owns one genuine CBaddy lease.");
            var deadline = Stopwatch.StartNew();
            while (applied == 0 || registry.Owns(actor))
            {
                if (story.Failure is { } failure) throw new InvalidOperationException("Actual inherited4298 source phase failed.", failure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(12)) throw new TimeoutException("Untouched239 did not shake and retire through its real source phases.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Assert(applied == 1 && !GodotObject.IsInstanceValid(actor) && registry.BaddyConstruction.State.Count == count &&
                !registry.SourceList(ActorUpdateGroup.Control).SourceOrder.Contains(actor),
                "Actual source completion retires239 once, destroys its native body and releases its single CBaddy lease independently of camera shake decay.");
            GD.Print("Verified native inherited4298: untouched L1A4 control239, original wait60/update31, real camera medium profile and genuine source-body retirement.");
        }
        finally
        {
            host.SourceShakeApplied -= OnShake;
            if (actor is not null && registry.Owns(actor)) registry.DestroyImmediately(actor);
            (state.Amplitude, state.Decay, state.Speed) = saved;
        }

        void OnShake(FixedVector3 position, int mode)
        {
            GodotControlActor current = actor ?? throw new InvalidDataException("Source239 shook during its initial constructor wait.");
            if (position != current.SourcePosition) return;
            Assert(current.SourceUpdates == 31 && current.ScriptState.WaitTicks == 0 && mode == 1 &&
                state.Amplitude == new CameraShakeAxes(0, 0, 30) && state.Decay == new CameraShakeDecay(3, 0, 3) &&
                state.Speed == new CameraShakeAxes(600, 0, 600),
                "Source239's actual thirty-first AI visit consumes its real operand1 then sets the same native camera medium amplitude/decay/speed before the render-camera decay phase.");
            applied++;
        }
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren()) { yield return child; foreach (Node descendant in Descendants(child)) yield return descendant; }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException("Native actor camera shake: " + evidence); }
}
