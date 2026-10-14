using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

partial class GodotScorpionAi
{
    void IScorpionBrainHost.StepBehavior(ScorpionBehaviorRoutine routine, object? target) => StepBehavior(routine, target);
    private ActorWaypointRoute? route;
    public int BaddyCollisionCheck { get; set; } = 1;
    public int AttackClock => Encounter.AttackClock;

    public void StepBehavior(ScorpionBehaviorRoutine routine, object? target = null)
    {
        switch (routine)
        {
            case ScorpionBehaviorRoutine.GetTugged: ScorpionBehavior.GetTugged(targets, this); break;
            case ScorpionBehaviorRoutine.WindUpEnvironmentalAttack: ScorpionBehavior.WindUpEnvironmentalAttack(targets, this, Encounter.Registry.RandomSource); break;
            case ScorpionBehaviorRoutine.ReactToJonah: ScorpionBehavior.ReactToJonah(targets, this); break;
            case ScorpionBehaviorRoutine.AvoidObstacle: ScorpionBehavior.AvoidObstacle(targets, this); break;
            case ScorpionBehaviorRoutine.Die: ScorpionBehavior.Die(targets, this, actor.FrameTiming.ElapsedTicks); break;
            case ScorpionBehaviorRoutine.PursueTarget: ScorpionBehavior.PursueTarget(targets, this, target ?? throw new InvalidOperationException("PursueTarget requires a recovered target.")); break;
            case ScorpionBehaviorRoutine.FollowWaypoints: ScorpionBehavior.FollowWaypoints(targets, this); break;
            case ScorpionBehaviorRoutine.AttackEnvironmentalObject:
            case ScorpionBehaviorRoutine.FireTailLaser:
            case ScorpionBehaviorRoutine.GetTrapped:
            case ScorpionBehaviorRoutine.Gloat:
                throw new NotSupportedException($"CScorpion behavior leaf {routine} (0x{(int)routine:X6}) is not ported.");
            default:
                throw new NotSupportedException($"CScorpion behavior leaf {routine} (0x{(int)routine:X6}) is not dispatched.");
        }
    }

    public FixedVector3 WaypointPosition(int node) => World.WaypointPosition(node);
    public void RunAppropriateAnimation()
    {
        ScorpionState body = Body;
        ushort first = (byte)body.MovementAnimations, second = (byte)(body.MovementAnimations >> 8);
        if (body.Animation.ClipId == first && body.Animation.Finished == 0) return;
        if (body.Animation.ClipId == second && body.Animation.Finished == 0) return;
        if (body.Animation.ClipId != first) { actor.RunAnimation(first, 0, -1); return; }
        actor.RunAnimation(second, 0, -1);
    }

    public void TryAttachPointLookAt(FixedVector3 point, uint completionMask, int rate, int tolerance) =>
        actor.Procedures.Attach(ActorLookAtProcedure.ForPoint(actor.Motion, point, completionMask, rate, tolerance));
    public void TryAttachStateSwitchMessage(int code) =>
        actor.Procedures.Attach(new ScorpionStateSwitchMessageProcedure(Body, code, message => actor.ReceiveMessage(new ThugMessage(message, actor))));
    public void AttachSpeech(int bank, int slot) => actor.SpeechAttachment.Attach(Encounter.Clock, bank, slot);
    public void StopSpeech() { }
    public void SuppressPlayerInput(int ticks) => Encounter.Player.Controller.SetIgnoreInputTimer(ticks);
    public void SendPulse(ushort node) => Encounter.Registry.ReceivePulse(node);
    public bool HasCamera => Encounter.BossCamera is not null;
    public short CameraAngle => Encounter.EncounterCamera.Angle;
    public void SetCameraMode(int mode) => Encounter.EncounterCamera.SetMode(unchecked((uint)mode));
    public void TrackScorpionPositionAndOrientation(ScorpionState scorpion)
    {
        if (!ReferenceEquals(scorpion, Body)) throw new InvalidOperationException("The death camera must track the live CScorpion body.");
        Encounter.BoundCamera?.TrackScorpionDeathView(actor.Motion.Body.Position, actor.Motion.Body.Angles);
    }
    public void SetCameraAngle(short angle, ushort ticks) => Encounter.EncounterCamera.SetAngle(angle, ticks);
    public void SetCameraXOffset(short offset, ushort ticks) => Encounter.EncounterCamera.SetXOffset(offset, ticks);
    public void SetCameraYOffset(short offset, ushort ticks) => Encounter.EncounterCamera.SetYOffset(offset, ticks);
    public void SetCameraZOffset(short offset, ushort ticks) => Encounter.EncounterCamera.SetZOffset(offset, ticks);
    public void SetCameraHorizontalDistance(ushort distance, ushort ticks) => Encounter.EncounterCamera.SetHorizontalDistance(distance, ticks);
    public void SetCameraVerticalDistance(short distance, ushort ticks) => Encounter.EncounterCamera.SetVerticalDistance(distance, ticks);
    public uint BodySignalFlags(object identity) => identity switch
    {
        GodotScorpionEncounterActor { State: JonahState jonah } => jonah.SignalFlags,
        GodotScriptActor script => script.ScriptState.BehaviorFlags,
        _ => 0
    };
    public ScorpionLineHit LineToItem(FixedVector3 initialStart, FixedVector3 initialEnd, FixedVector3 start, FixedVector3 end)
    {
        if (BaddyCollisionCheck == 0) SourceDiagnostic("Not detecting baddy polys!");
        int saved = World.CollisionCheckMode;
        try
        {
            World.CollisionCheckMode = 1;
            GodotActorWorldHit? hit = World.Trace(start, end);
            if (hit is null) return new(null, false);
            object? item = ResolveLineItem(hit);
            bool face = item is null;
            return new(item, face);
        }
        finally { World.CollisionCheckMode = saved; }
    }
    public void TryAttachYawRotation(int angle, int rate, uint completionMask) =>
        actor.Procedures.Attach(new ActorYawRotationProcedure(actor.Motion, angle, rate, completionMask));
    public void TryAttachAcceleration(int acceleration, int start, int completionMask) =>
        actor.Procedures.Attach(new ActorAccelerationProcedure(actor.Motion, acceleration, start, unchecked((uint)completionMask)));
    public void TryMoveToPoint(FixedVector3 destination, int speed, int turnRate, int facingTolerance, uint completionMask) =>
        actor.Procedures.AttachMove(new(actor.Motion, destination, speed, turnRate, facingTolerance, completionMask));
    public int PathPointCount { get => path.PathCount; set => path.PathCount = value; }
    public FixedVector3 PathPoint(int index) => path.GetPoint(index);
    public void RunCurrentScript() => actor.RunWaypointScript();
    public bool GetNextWaypoint()
    {
        bool advanced = RequireRoute().Advance();
        Body.CurrentWaypoint = RequireRoute().CurrentNode;
        return advanced;
    }
    public IReadOnlyList<JonahWaypointLink> ReadWaypointLinks(int node, int maximumCount)
    {
        TriggerFile triggers = Encounter.Registry.Triggers;
        IReadOnlyList<ushort> links = MissionRuntime.ReadLinks(triggers.Nodes[node]);
        var result = new List<JonahWaypointLink>(Math.Min(links.Count, maximumCount));
        foreach (ushort link in links.Take(maximumCount))
        {
            TriggerNode linked = triggers.Nodes[link];
            short type = unchecked((short)linked.TypeCode);
            int role = type == 1002 ? System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(linked.Data.Span[2..]) : 0;
            result.Add(new(link, type, role, 0));
        }
        return result;
    }
    public ScorpionBreakWall? ReadBreakWall(int node)
    {
        TriggerFile triggers = Encounter.Registry.Triggers;
        if ((uint)node >= triggers.Nodes.Count) return null;
        TriggerNode trigger = triggers.Nodes[node];
        if (trigger.TypeCode is not (1002 or 5)) return null;
        TriggerScriptFragment fragment = TriggerPlacement.Read(trigger).ReadScriptAfterAngles(trigger);
        ReadOnlySpan<byte> data = fragment.Data.Span;
        if (data.Length < 8) return null;
        int wordOffset = 0;
        ushort tag = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data[wordOffset..]);
        int aligned = (wordOffset + 2 + 1) & ~1;
        if (aligned + 4 > data.Length) return null;
        uint checksum = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data[aligned..]);
        return new(tag, checksum);
    }
    public void ChunkItemByChecksum(uint checksum) => (Encounter.Registry.EnvironmentChunks ?? throw new NotSupportedException(
        "Chunk_ChunkItemByChecksum requires the scene environment-chunk host.")).ChunkEnvironment(checksum);

    private ActorWaypointRoute RequireRoute() => route ??= new(Encounter.Registry.Triggers, Body.CurrentWaypoint, actor.ParseWaypointScript);
    private object? ResolveLineItem(GodotActorWorldHit hit)
    {
        if (hit.Collider is not Node collider) return null;
        foreach (object identity in Encounter.Registry.SourceList(ActorUpdateGroup.Environment).SourceOrder)
        {
            if (identity is not Node3D body) continue;
            if (body == collider || body.IsAncestorOf(collider) || collider.IsAncestorOf(body)) return identity;
        }
        return null;
    }
}
