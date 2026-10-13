using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;
using OpenSpidey.Core.Cameras;

namespace OpenSpidey.Game.World;

internal sealed class ControlActorImportChecks
{
    private readonly GodotActorRegistry actors;
    private readonly MissionRuntime mission;
    private GodotControlActor actor = null!;
    private bool observedDeath;
    private bool observedPendingDeletion;
    private readonly List<ushort> queued = [];
    private readonly PlayerFollowCamera camera;
    public bool Finished { get; private set; }

    public ControlActorImportChecks(GodotActorRegistry actors, MissionRuntime mission, PlayerFollowCamera camera)
    {
        this.actors = actors;
        this.mission = mission;
        this.camera = camera;
        mission.Trace += trace => { if (trace.Kind == MissionTraceKind.Queued) queued.Add(trace.NodeId); };
        actors.Spawned += spawned => { if (spawned.Definition.NodeId == 36) actor = (GodotControlActor)spawned; };
        mission.ReceivePulse(36);
        if (actor is null || actor.ScriptState.WaitTicks != 128 || actors.LiveCount != 1)
            throw new InvalidDataException("Original control actor did not spawn through the mission factory.");
    }

    public void Verify()
    {
        if (Finished) return;
        if (actors.SourceUpdates == 66)
        {
            if (actor.SourceUpdates != 66 || actor.Lifecycle.Phase != ActorLifePhase.Dead || actors.LiveCount != 1 ||
                !actor.ScriptState.DeathPulseSent || mission.PendingCount != 0 || !queued.SequenceEqual(new ushort[] { 35, 49 }) ||
                mission.CommandPoints[35].PulsesReceived != 1 || mission.CommandPoints[49].PulsesReceived != 1)
                throw new InvalidDataException("Original actor death pulses or retirement timing were lost.");
            observedDeath = true;
        }
        if (actors.SourceUpdates == 67)
        {
            if (actor.Lifecycle.Phase != ActorLifePhase.PendingDeletion || actors.LiveCount != 1)
                throw new InvalidDataException("Dead actor skipped its deferred cleanup update.");
            observedPendingDeletion = true;
        }
        if (actors.SourceUpdates < 69) return;
        if (!observedDeath || !observedPendingDeletion || actors.LiveCount != 0 || actors.DeletedCount != 1 ||
            GodotObject.IsInstanceValid(actor) || mission.PendingCount != 0 || !camera.ScriptRig.Enabled || !camera.ScriptRig.AngleLocked ||
            camera.ScriptRig.Profiles.Get(CameraSurface.Floor, CameraParameter.HorizontalDistance) != 528 ||
            camera.ScriptRig.Profiles.Get(CameraSurface.Floor, CameraParameter.VerticalDistance) != -150 ||
            camera.KeepAspect != Camera3D.KeepAspectEnum.Height || Mathf.Abs(camera.GlobalTransform.Basis.Determinant() - 1) > 0.01f ||
            !float.IsFinite(camera.GlobalPosition.X) || !float.IsFinite(camera.GlobalPosition.Y) || !float.IsFinite(camera.GlobalPosition.Z))
            throw new InvalidDataException("Control actor node leaked, deleted early or repeated its death events.");
        Finished = true;
        GD.Print("Verified original Godot control actor: 128-tick wait, two ordered death pulses, camera event execution, dead/deletion phases and native node cleanup.");
    }
}
