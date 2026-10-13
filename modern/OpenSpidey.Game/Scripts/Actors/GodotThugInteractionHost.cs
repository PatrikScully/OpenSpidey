using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Required session/game interactions beyond the source actor's native model, world, audio and effect ownership.</summary>
public interface IGodotThugInteractionHost
{
    int DomeCount { get; }
    bool IgnoreSphereRadius { get; }
    int PlayerWebComboValue { get; }
    void SpawnGrenade(GodotThugActor actor, ThugGrenadeRequest request);
    void NotifyKill(int actorType);
    bool GrabUpdate(GodotThugActor actor, ref FixedVector3 position, ref short yaw);
    IThugTrapWeb? RecoverWeb(GodotThugActor actor, bool tugWeb);
    void SetPlayerWebCombo(int duration, int amount);
    void ApplyHitToOtherBody(IThugInjuryBody body, ActorHit hit);
    ActorCollisionBody DescribeEnvironmentalContact(GodotActorWorldHit hit);
    ActorProcedureState? RecoverOtherActor(object handle);
    ActorBodyMotion? RecoverOtherBody(ActorProcedureState actor);
    object? RecoverNativeActor(ActorProcedureState actor);
    void SendMessageToOtherActor(GodotThugActor sender, object targetHandle, int code);
    void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition);
    void SendSignal(GodotThugActor actor);
    bool ExecuteScriptCommand(GodotThugActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment);
}
