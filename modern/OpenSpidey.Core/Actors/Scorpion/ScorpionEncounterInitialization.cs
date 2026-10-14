using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

public interface IScorpionEncounterInitializationHost
{
    void InitializeModel(string name);
    void AssignSourceLight(short actorType);
    void AttachToSourceBaddyList();
    ushort ReadAnimationFrameCount(ushort clip);
    void CycleAnimation(ushort clip, sbyte direction);
    void ParseScript(ActorScriptCursor script);
    void CreateHealthBar(short actorType);
}

/// <summary>The shipped Scorpion/Jonah authored constructors, retaining observable model/list/animation/script/panel order.</summary>
public static class ScorpionEncounterInitialization
{
    public static void ConstructDefaultScorpion(ScorpionState state, ActorProcedureState actor, IScorpionEncounterInitializationHost host)
    {
        state.BindProcedures(actor);
        InitializeBody(actor);
        InitializeModel(state, "scorpion", ScorpionState.ActorType, host);
        state.TailGeometryRegion = state.TailGlowRegion = byte.MaxValue;
        state.Type = ScorpionState.ActorType;
        state.CollisionPartMask = 0x00FE0000;
    }

    public static void ConstructDefaultJonah(JonahState state, ActorProcedureState actor, IScorpionEncounterInitializationHost host, bool joelJewettCheat = false)
    {
        state.BindProcedures(actor);
        InitializeBody(actor);
        InitializeModel(state, joelJewettCheat ? "jjjj" : "jameson", JonahState.ActorType, host);
        state.Type = JonahState.ActorType;
    }

    public static void ConstructScorpion(ScorpionState state, ActorProcedureState actor,
        TriggerActorDefinition definition, IScorpionEncounterInitializationHost host, int sourceNodeOffset = 0)
    {
        Validate(definition, ScorpionState.ActorType, host);
        state.BindProcedures(actor);
        InitializeBody(actor);
        Place(actor, definition);
        InitializeModel(state, "scorpion", ScorpionState.ActorType, host);
        host.AttachToSourceBaddyList();
        SelectAuthoredBody(state, ScorpionState.ActorType, definition.NodeId);
        host.CycleAnimation(2, 1);
        state.Animation = ActorAnimationPlayback.Cycle(state.Animation, 2, host.ReadAnimationFrameCount(2));
        state.TailGeometryRegion = state.TailGlowRegion = byte.MaxValue;
        state.BehaviorFlags |= 0x02022001;
        state.CollisionPartMask = 0x00FE0000;
        state.SurfaceClearance = 100;
        state.MinorRadius = 175;
        state.WaitUpdates = 0;
        state.GeneralParameter = 32;
        state.PushValue = 64;
        state.State = 0;
        state.Health = 900;
        state.MovementAnimations = 0x16150201;
        state.IdleAnimations = 0;
        ParseAuthoredScript(definition, sourceNodeOffset, host);
        host.CreateHealthBar(ScorpionState.ActorType);
        state.BossCameraEnabled = 0;
    }

    public static void ConstructJonah(JonahState state, ActorProcedureState actor,
        TriggerActorDefinition definition, IScorpionEncounterInitializationHost host, bool joelJewettCheat = false, int sourceNodeOffset = 0)
    {
        Validate(definition, JonahState.ActorType, host);
        state.BindProcedures(actor);
        InitializeBody(actor);
        Place(actor, definition);
        InitializeModel(state, joelJewettCheat ? "jjjj" : "jameson", JonahState.ActorType, host);
        host.AttachToSourceBaddyList();
        SelectAuthoredBody(state, JonahState.ActorType, definition.NodeId);
        state.SurfaceClearance = 100;
        state.MinorRadius = 100;
        state.WaitUpdates = 0;
        state.GeneralParameter = 32;
        state.PushValue = 64;
        state.State = 0;
        state.BehaviorFlags |= 0x02002001;
        state.Health = 120;
        state.MovementAnimations = 0x02020202;
        state.IdleAnimations = 0x00000102;
        state.BodyFlags &= 0xFFEF;
        ParseAuthoredScript(definition, sourceNodeOffset, host);
        host.CreateHealthBar(JonahState.ActorType);
    }

    private static void Validate(TriggerActorDefinition definition, short type, IScorpionEncounterInitializationHost host)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(host);
        if (definition.ActorType != type) throw new InvalidDataException($"Source encounter constructor requires actor type{type}.");
    }
    private static void Place(ActorProcedureState actor, TriggerActorDefinition definition)
    {
        actor.Body.Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        actor.Body.Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
    }
    private static void InitializeBody(ActorProcedureState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ActorBodyMotion body = actor.Body;
        body.Position = body.Velocity = body.Acceleration = body.LocalVelocity = default;
        body.Angles = body.AngularVelocity = body.AngularAcceleration = default;
        body.LinearDamping = new(1, 1, 1);
        body.AngularDamping = new(1, 1, 1);
    }
    private static void InitializeModel(ScorpionEncounterBodyState state, string name, short type, IScorpionEncounterInitializationHost host)
    {
        host.InitializeModel(name);
        state.ModelName = name;
        state.ItemFlags |= 0x480;
        host.AssignSourceLight(type);
    }
    private static void SelectAuthoredBody(ScorpionEncounterBodyState state, short type, ushort node)
    { state.CurrentWaypoint = node; state.SourceNode = node; state.Type = type; }
    private static void ParseAuthoredScript(TriggerActorDefinition definition, int sourceNodeOffset, IScorpionEncounterInitializationHost host) =>
        host.ParseScript(new(definition.BehaviorData, checked(sourceNodeOffset + definition.ObjectDataOffset + 18)));
}
