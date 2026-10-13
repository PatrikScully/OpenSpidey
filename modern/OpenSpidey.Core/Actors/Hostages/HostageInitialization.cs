using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Hostages;

public interface IHostageInitializationHost
{
    void AttachToSourceBaddyList();
    void EnableShadow();
    void ParseScript(ActorScriptCursor script);
}

/// <summary>CHostage constructor order; original common script interpreter remains an explicit actor host.</summary>
public static class HostageInitialization
{
    public static void Construct(HostageState state, ActorProcedureState actor, TriggerActorDefinition definition, IHostageInitializationHost host, int sourceNodeOffset = 0)
    {
        if (state.SourceNode != definition.NodeId) throw new InvalidDataException("Hostage definition and state source nodes differ.");
        actor.Body.Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        actor.Body.Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
        host.AttachToSourceBaddyList(); state.BodyFlags |= 8; host.EnableShadow();
        state.ShadowScale = 48; state.CurrentWaypoint = state.SourceNode; state.MinorRadius = 128;
        state.WaitUpdates = 0; state.GeneralParameter = 32; state.PushValue = 64; state.State = HostageBehaviorState.Initializing;
        actor.BehaviorFlags |= 1;
        state.MovementAnimations = 0x03030404; state.IdleAnimations = 4;
        state.BodyFlags &= 0xFFEF;
        host.ParseScript(new(definition.BehaviorData, checked(sourceNodeOffset + definition.ObjectDataOffset + 18)));
    }
}
