using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Production CJonah::AI host: the actor's own body, procedure, message, path and height owners plus the live scene.</summary>
internal sealed class GodotJonahAi : IJonahLocomotionHost
{
    private readonly GodotScorpionEncounterActor actor;
    private readonly JonahLocomotionState locomotion;
    private readonly JonahScorpionContactState contact;
    private readonly JonahEncounterBehavior behavior;
    private readonly ActorPathController path;
    private readonly ActorHeightController height;
    private GodotJonahFootprintShadow? shadow;

    public GodotJonahAi(GodotScorpionEncounterActor actor, JonahState jonah)
    {
        this.actor = actor;
        var brain = new JonahBrainState(jonah, actor.Motion, actor.Procedures, actor.Messages);
        brain.BindInputSignals(actor.ScriptState);
        locomotion = new(brain);
        contact = new(brain);
        path = new(actor.Motion, World) { Radius = jonah.MinorRadius, Clearance = jonah.SurfaceClearance };
        height = new(actor.Motion, World) { Clearance = jonah.SurfaceClearance };
        behavior = new(locomotion, this);
    }

    private GodotScorpionEncounterRuntime Encounter => actor.Encounter;
    private GodotActorWorldQuery World => Encounter.SceneOwnership.World;
    private GodotPlayerMeleeDirectory Handles => Encounter.SceneOwnership.Directory;
    private JonahBrainState Brain => locomotion.Brain;

    public PlayerKickHandle HidingPlace => locomotion.HidingPlace;
    public void Update(int elapsedTicks) => behavior.Update(elapsedTicks);
    public bool Hit(ActorHit hit) => JonahDamage.Hit(locomotion, this, hit);

    public uint Clock => Encounter.Clock;
    public bool PlayerSynthesizingInput => Encounter.Player.Combat.State.SynthesizedInputActive;
    public object? RecoverScorpion(PlayerKickHandle handle) => Handles.Recover(ref handle) is GodotScorpionEncounterActor { State: ScorpionState } scorpion ? scorpion : null;
    /// <summary>The first BaddyList body of type310.</summary>
    public object? FindFirstScorpion()
    {
        foreach (object identity in Encounter.Registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder)
            if (identity is GodotScriptActor { SourceActorType: ScorpionState.ActorType } scorpion && Encounter.Registry.Owns(scorpion)) return scorpion;
        return null;
    }
    public PlayerKickHandle MakeScorpionHandle(object scorpion) => Handles.MakeHandle(RequireScorpion(scorpion));
    public JonahState? ScorpionCurrentTarget(object scorpion) =>
        Encounter.Modules.CallTypedUserFunction<GodotScorpionEncounterActor, object?>("scorpion", 1, RequireScorpion(scorpion)) is
            GodotScorpionEncounterActor { State: JonahState target } ? target : null;
    /// <summary>CScorpion vtable slot18 (488580): target kind0xBF8 equals1.</summary>
    public bool ScorpionVictorious(object scorpion) => RequireScorpion(scorpion).Damage!.TargetKind == 1;
    public bool ScorpionContactSignal(object scorpion) => RequireScorpion(scorpion).RequireScorpionAi().CurrentTargetIsPlayer != 0;
    public int Random(int exclusiveMaximum) => Encounter.Registry.RandomSource(exclusiveMaximum);
    public void RunAnimation(ushort clip, int startFrame, int endFrame) => actor.RunAnimation(clip, startFrame, endFrame);
    public void CycleAnimation(ushort clip, sbyte direction) => actor.CycleAnimation(clip, direction);
    public void TryLookAtScorpion(object scorpion, uint completionMask, int rate, int tolerance)
    {
        GodotScorpionEncounterActor target = RequireScorpion(scorpion);
        actor.Procedures.Attach(ActorLookAtProcedure.ForBody(actor.Motion, () => target.Motion.Body, completionMask, rate, tolerance));
    }
    public void TrySendWaypointMessage(object scorpion, int code, int positionMode, FixedVector3 position, FixedVector3 waypoint)
    {
        if (positionMode != 2) throw new NotSupportedException($"Jonah's CMessage vector mode{positionMode} is not established.");
        RequireScorpion(scorpion).ReceiveMessage(new ThugMessage(code, actor) { Vector0 = position, Vector1 = waypoint });
    }
    public void PlayVoice(int bank, int index, FixedVector3 position, int flags) => Encounter.Registry.PositionVoicePlayback(bank, index, position, flags);
    public void PlaySound(uint sound, FixedVector3 position, int pitchOffset)
    {
        if (pitchOffset != 0) throw new NotSupportedException("Jonah source SFX uses pitch offset0.");
        (Encounter.Registry.Sounds ?? throw new InvalidOperationException("Jonah source SFX requires the scene sound channels."))
            .PlayAt(sound, position, Encounter.Registry.SoundListener());
    }
    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("Utils_GetGroundHeight with a body out-pointer is not used by Jonah.");
        return World.GroundHeight(position, above, below);
    }
    public FixedVector3 WaypointPosition(int waypoint) => World.WaypointPosition(waypoint);
    /// <summary>CBaddy::PathCheck403310 with a hit out-pointer.</summary>
    public JonahPathTrace TracePath(FixedVector3 start, FixedVector3 end, int clearance)
    {
        ActorPathCheckResult result = path.PathCheck(start, end, true, clearance);
        return new(result.Code, result.HitPoint);
    }
    public bool ScorpionBlocksPath(FixedVector3 start, FixedVector3 end, object scorpion) =>
        throw new NotSupportedException("Jonah's Utils_CheckObjectCollision4E6470 against the Scorpion is not ported.");
    public bool AddPointToPath(FixedVector3 position, int flags) => path.AddPointToPath(position, flags);
    public void DoPhysics(int limitElapsed) => actor.DoPhysics(limitElapsed);
    public void BuildTransform() => actor.BuildTransform();
    /// <summary>CBaddy::SetHeight4041C0.</summary>
    public void SetMovementHeight(int flags, int clearance, int distance)
    {
        height.Clearance = locomotion.Jonah.SurfaceClearance;
        height.SetHeight(flags != 0, clearance, distance, actor.FrameTiming.ElapsedTicks);
    }
    public void FollowWaypoints() => JonahLocomotion.FollowWaypoints(locomotion, this);
    public void Hide() => JonahLocomotion.Hide(locomotion, this);
    public void TakeHit() => JonahLocomotion.TakeHit(locomotion, this);
    public void Die() => JonahLocomotion.Die(locomotion, this, actor.FrameTiming.ElapsedTicks);
    public void GoAroundScorpion(object scorpion) => JonahScorpionContact.Resolve(contact, RequireScorpion(scorpion).Motion.Body.Position);
    /// <summary>Jonah DoMGSShadow446980 over the genuine pose and protected Shadow2 CQuadBit.</summary>
    public void DrawShadow()
    {
        if (shadow is null) { shadow = GodotJonahFootprintShadow.Create(Encounter.ShadowTexture); actor.AddChild(shadow); }
        shadow.UpdateFromPose(actor.Model.Model, actor.Motion.Body.Position, actor.SourceTransformTranslation, actor.SourceTransformBasis, actor.Variables);
    }
    public void SourceDiagnostic(string message) => throw new InvalidOperationException("Original Jonah print_if_false: " + message);

    /// <summary>Trig_GetLinkInfoList4DFE70: node, linked node type, its waypoint word for type1002, zero parameter.</summary>
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
    /// <summary>CBody::FindBodyByNode4602F0 over EnvironmentList.</summary>
    public object? FindEnvironmentBody(int node)
    {
        foreach (object identity in Encounter.Registry.SourceList(ActorUpdateGroup.Environment).SourceOrder)
        {
            if (identity is not GodotScriptActor body)
                throw new NotSupportedException($"EnvironmentList body {identity.GetType().Name} has no source node adapter.");
            if (body.SourceNodeId == node) return body;
        }
        return null;
    }
    public PlayerKickHandle MakeEnvironmentHandle(object body) => Handles.MakeHandle(body);
    public object? RecoverEnvironmentBody(PlayerKickHandle handle) => Handles.Recover(ref handle);
    public object? PlayerHeldObject => Encounter.Player.Melee?.Kick.HeldObject?.Identity;
    public short PlayerHealth => Encounter.Player.Combat.State.Health;
    public FixedVector3 ScorpionPosition(object scorpion) => RequireScorpion(scorpion).Motion.Body.Position;
    public int PathPointCount { get => path.PathCount; set => path.PathCount = value; }
    public FixedVector3 PathPoint(int index) => path.GetPoint(index);
    public void ExecuteWaypointScript(int node) =>
        throw new NotSupportedException("CBaddy_RunCurrentScript402E10 at a Jonah waypoint needs the encounter script command set.");
    public void TryMoveToPoint(FixedVector3 destination, int speed, int turnRate, int facingTolerance, uint completionMask) =>
        actor.Procedures.AttachMove(new(actor.Motion, destination, speed, turnRate, facingTolerance, completionMask));
    public void TrySendScorpionParameterMessage(object scorpion, int code, int parameter) =>
        RequireScorpion(scorpion).ReceiveMessage(new ThugMessage(code, actor) { Parameter32 = parameter });
    public void SuppressPlayerInput(int ticks) =>
        throw new NotSupportedException("Jonah's player-input suppression transport is not ported.");
    public bool HasCamera => Encounter.BoundCamera is not null;
    public short CameraAngle => Encounter.EncounterCamera.Angle;
    public void SetCameraMode(int mode) => Encounter.EncounterCamera.SetMode(unchecked((uint)mode));
    public void TrackJonahPositionAndOrientation(JonahState jonah) => Encounter.EncounterCamera.TrackJonahPositionAndOrientation(jonah);
    public void SetCameraAngle(short angle, ushort ticks) => Encounter.EncounterCamera.SetAngle(angle, ticks);
    public void SetCameraXOffset(short offset, ushort ticks) => Encounter.EncounterCamera.SetXOffset(offset, ticks);
    public void SetCameraYOffset(short offset, ushort ticks) => Encounter.EncounterCamera.SetYOffset(offset, ticks);
    public void SetCameraZOffset(short offset, ushort ticks) => Encounter.EncounterCamera.SetZOffset(offset, ticks);
    public void SetCameraHorizontalDistance(ushort distance, ushort ticks) => Encounter.EncounterCamera.SetHorizontalDistance(distance, ticks);
    public void SetCameraVerticalDistance(short distance, ushort ticks) => Encounter.EncounterCamera.SetVerticalDistance(distance, ticks);
    public void SetLevelStatus(int status) =>
        throw new NotSupportedException("Jonah's level-status write is not connected to the mission lifecycle.");

    private static GodotScorpionEncounterActor RequireScorpion(object scorpion) => scorpion as GodotScorpionEncounterActor is { State: ScorpionState } actor ? actor :
        throw new ArgumentException("Jonah's Scorpion identity must be a live source CScorpion.", nameof(scorpion));
}
