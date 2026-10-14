using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Source310 CScorpion and316 CJonah, built by their module slot0 factories with the genuine126-loaded banks.</summary>
public partial class GodotScorpionEncounterActor : GodotScriptActor, IScorpionEncounterInitializationHost, IActorScriptHost, IGodotSourceBaddyBody
{
    private GodotScorpionEncounterRuntime runtime = null!;
    private bool begun;
    private GodotScorpionAi? scorpionAi;
    private GodotJonahAi? jonahAi;
    public ScorpionEncounterBodyState State { get; private set; } = null!;
    public ActorProcedureState Motion { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ScorpionDamageState? Damage { get; private set; } // Scorpion only.
    public GodotScorpionEncounterModel Model { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new(); // The shared CMessage list layout.
    public ActorSpeechAttachment SpeechAttachment { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public FixedVector3 SourceTransformTranslation { get; private set; }
    internal GodotScorpionEncounterRuntime Encounter => runtime;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Motion.Body.Position;
    ActorBodyMotion IGodotSourceBaddyBody.Body => Motion.Body;
    ImportedAnimatedModel IGodotSourceBaddyBody.Model => Model.Model;
    ushort IGodotSourceBaddyBody.ItemFlags { get => State.ItemFlags; set => State.ItemFlags = value; }
    ushort IGodotSourceBaddyBody.MinorRadius => State.MinorRadius;
    ushort IGodotSourceBaddyBody.BodyFlags => State.BodyFlags;
    uint IGodotSourceBaddyBody.BehaviorFlags => State.BehaviorFlags;
    short IGodotSourceBaddyBody.Health => State.Health;
    void IGodotSourceBaddyBody.MarkSourceBodyDead() => State.BodyFlags |= 0x40;

    internal static GodotScorpionEncounterActor Create(TriggerNode node, GodotScorpionEncounterRuntime runtime, short actorType)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        int ordinal = runtime.Registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotScorpionEncounterActor { Name = (actorType == ScorpionState.ActorType ? "Scorpion" : "Jonah") + node.Id, runtime = runtime, Definition = definition };
        try
        {
            actor.Motion = new(new());
            actor.ScriptState = new() { ActorType = actorType, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, runtime.Registry);
            if (actorType == ScorpionState.ActorType)
            {
                var scorpion = new ScorpionState(ordinal);
                actor.State = scorpion;
                ScorpionEncounterInitialization.ConstructScorpion(scorpion, actor.Motion, definition, actor, node.SourceOffset);
                actor.Damage = new(scorpion, actor.Motion, actor.Procedures);
            }
            else if (actorType == JonahState.ActorType)
            {
                var jonah = new JonahState(ordinal);
                actor.State = jonah;
                ScorpionEncounterInitialization.ConstructJonah(jonah, actor.Motion, definition, actor, runtime.JoelJewettCheat, node.SourceOffset);
            }
            else throw new NotSupportedException($"Source type {actorType} has no Scorpion encounter factory.");
            actor.Lifecycle = new((actor.State.BodyFlags & 2) != 0, false);
            actor.Variables.Position = actor.Motion.Body.Position;
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin()
    {
        if (Damage is not null) scorpionAi = new(this, Damage);
        else jonahAi = new(this, (JonahState)State);
        begun = true;
        BuildTransform();
    }

    /// <summary>CBody::EveryFrame timing and CSuper::UpdateFrame, then the source CScorpion/CJonah AI.</summary>
    internal override void Advance()
    {
        if (!begun) throw new InvalidOperationException("Source encounter AI cannot run before the initial actor census.");
        SourceUpdates++;
        var playback = Model.Model.Playback;
        State.BodyFlags = FrameTiming.Prepare(runtime.Clock, State.BodyFlags, State.ItemFlags, new(playback.Frame, playback.ClipId, playback.Direction));
        if ((State.ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        State.Animation = Model.Model.Playback;
        if (scorpionAi is not null) scorpionAi.Update(FrameTiming.ElapsedTicks);
        else jonahAi!.Update(FrameTiming.ElapsedTicks);
        Variables.Position = Motion.Body.Position;
        Variables.Health = State.Health;
        SynchronizeSourceLighting();
    }

    internal GodotScorpionAi RequireScorpionAi() => scorpionAi ??
        throw new InvalidOperationException("Source module scorpion user functions require a running CScorpion.");
    internal GodotJonahAi RequireJonahAi() => jonahAi ??
        throw new InvalidOperationException("The source Scorpion reads a running CJonah's hiding place.");

    public bool Hit(ActorHit hit)
    {
        bool accepted = scorpionAi is not null ? scorpionAi.Hit(ref hit)
            : (jonahAi ?? throw new InvalidOperationException("A source boss cannot be hit before its AI is composed.")).Hit(hit);
        Variables.Health = State.Health;
        return accepted;
    }

    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) => Messages.Enqueue(new(code, sender) { Vector0 = vector });
    internal void ReceiveMessage(ThugMessage message) => Messages.Enqueue(message);

    public void DoPhysics(int mode)
    {
        Motion.Body.UseLocalVelocity = (Motion.BehaviorFlags & 1) != 0;
        Motion.Body.Integrate(FrameTiming.ElapsedTicks, limitElapsed: mode != 0);
    }

    /// <summary>M3d_BuildTransform475F50 caches the rotation and whole-unit translation read by dynamic hooks.</summary>
    public void BuildTransform()
    {
        ActorAngles angles = Motion.Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        SourceTransformTranslation = Motion.Body.Position >> 12;
        SynchronizeSourcePose();
    }

    public FixedVector3 DynamicHookPosition(ActorHook hook)
    {
        var pose = new FixedJointTransform[Model.Model.SourcePartCount];
        for (int part = 0; part < pose.Length; part++) pose[part] = Model.Model.GetSourcePartPose(part);
        return ActorHookPosition.Evaluate(Motion.Body.Position, SourceTransformTranslation, SourceTransformBasis, pose, hook);
    }

    public void RunAnimation(ushort clip, int from, int to)
    {
        Model.Model.RunClip(clip, from, to);
        State.Animation = Model.Model.Playback;
    }

    public void SynchronizeSourcePose() => Model.SynchronizeSourcePose(Motion.Body.Position, Motion.Body.Angles, State.ItemFlags);
    public void SynchronizeSourceLighting() => Model.SynchronizeSourceLighting(State.ItemFlags);

    public void InitializeModel(string name)
    {
        Model = GodotScorpionEncounterModel.Create(runtime.Resources, name, runtime.LightingState);
        AddChild(Model);
    }
    public void AssignSourceLight(short actorType) => Model.AssignSourceLight(actorType);
    public void AttachToSourceBaddyList() { } // The registry performs the BaddyList attach when it adopts the factory result.
    public ushort ReadAnimationFrameCount(ushort clip) => checked((ushort)Model.Model.ReadFrameCount(clip));
    public void CycleAnimation(ushort clip, sbyte direction)
    {
        Model.Model.CycleClip(clip, direction);
        State.Animation = Model.Model.Playback;
    }
    public void ParseScript(ActorScriptCursor script)
    {
        Variables.Damage = State.Damage;
        Runtime = new(script, this);
        Runtime.Start();
    }
    public void CreateHealthBar(short actorType)
    {
        runtime.CreateHealthBar(State, actorType, body => body is ScorpionState && Damage is { } damage && ReferenceEquals(damage.Body, body) ? damage.HitFlashUpdates : 0);
        Model.Model.RestoreSourcePlayback(State.Animation);
    }

    public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    { Variables.Write(variable, operands); State.Damage = Variables.Damage; }
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
        throw new NotSupportedException($"The source encounter actor script requires an unported opcode{opcode:X4}.");
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => throw new NotSupportedException("The source encounter script has no proven script-switch dependency.");
    public bool PlayerCeilingCheck(short x, short z) => throw new NotSupportedException("The source encounter script has no proven player-ceiling dependency.");
    public void FlashCompass() => throw new NotSupportedException("The source encounter script has no proven compass dependency.");
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) runtime.Registry.ReceivePulse(link); }
    public void Retire() => throw new NotSupportedException("The source encounter actor must retain its native body resources.");
    internal void ParseWaypointScript(ActorScriptCursor script)
    {
        Variables.Damage = State.Damage;
        Runtime.ParseAt(script, runtime.Registry.Environment);
    }
    internal void RunWaypointScript()
    {
        Variables.Damage = State.Damage;
        TriggerNode node = runtime.Registry.Triggers.Nodes[State.CurrentWaypoint];
        TriggerScriptFragment fragment = TriggerPlacement.Read(node).ReadScriptAfterAngles(node);
        Runtime.ParseAt(new(fragment.Data, fragment.SourceOffset), runtime.Registry.Environment);
    }
}
