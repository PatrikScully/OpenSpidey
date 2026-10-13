using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

public delegate bool GodotPlayerGrabUpdate(ref FixedVector3 position, ref short yaw);
public delegate bool GodotActorSessionScriptCommand(GodotScriptActor actor, ushort opcode,
    ActorScriptCursor operands, ActorScriptEnvironment environment);

/// <summary>Required gameplay leaves; cross-family actor ownership and mission routing live in the directory.</summary>
public sealed class GodotActorSessionCallbacks
{
    public required Func<int> DomeCount { get; init; }
    public required Func<bool> IgnoreSphereRadius { get; init; }
    public required Func<int> PlayerWebComboValue { get; init; }
    public required Action<GodotThugActor, ThugGrenadeRequest> SpawnGrenade { get; init; }
    public required Action<int> NotifyKill { get; init; }
    public required GodotPlayerGrabUpdate GrabUpdate { get; init; }
    public required Func<GodotThugActor, bool, IThugTrapWeb?> RecoverWeb { get; init; }
    public required Action<int, int> SetPlayerWebCombo { get; init; }
    public required Action<FixedVector3, FixedVector3> SelectHostageCamera { get; init; }
    public required GodotActorSessionScriptCommand ExecuteScriptCommand { get; init; }
}

/// <summary>One scene's genuine thug and hostage session services, with explicit required effect/action leaves.</summary>
public sealed class GodotActorSessionServices(GodotActorSessionDirectory directory, GodotActorSessionCallbacks callbacks)
    : IGodotThugInteractionHost, IGodotHostageSessionHost
{
    public int DomeCount => callbacks.DomeCount();
    public bool IgnoreSphereRadius => callbacks.IgnoreSphereRadius();
    public int PlayerWebComboValue => callbacks.PlayerWebComboValue();
    public void SpawnGrenade(GodotThugActor actor, ThugGrenadeRequest request) => callbacks.SpawnGrenade(actor, request);
    public void NotifyKill(int actorType) => callbacks.NotifyKill(actorType);
    public bool GrabUpdate(GodotThugActor actor, ref FixedVector3 position, ref short yaw) => callbacks.GrabUpdate(ref position, ref yaw);
    public IThugTrapWeb? RecoverWeb(GodotThugActor actor, bool tugWeb) => callbacks.RecoverWeb(actor, tugWeb);
    public void SetPlayerWebCombo(int duration, int amount) => callbacks.SetPlayerWebCombo(duration, amount);
    public void ApplyHitToOtherBody(IThugInjuryBody body, ActorHit hit)
    {
        object identity = body is ActorCollisionBody snapshot ? snapshot.Identity : body;
        directory.ApplyHit(identity, hit);
    }
    public ActorCollisionBody DescribeEnvironmentalContact(GodotActorWorldHit hit) => directory.DescribeEnvironmentalContact(hit);
    public ActorProcedureState? RecoverOtherActor(object handle) => directory.RecoverActor(handle);
    public ActorBodyMotion? RecoverOtherBody(ActorProcedureState actor) => directory.RecoverBody(actor);
    public object? RecoverNativeActor(ActorProcedureState actor) => directory.RecoverNativeActor(actor);
    public void SendMessageToOtherActor(GodotThugActor sender, object targetHandle, int code) => directory.SendMessage(sender, targetHandle, code);
    public void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition) => callbacks.SelectHostageCamera(actorPosition, hostagePosition);
    public void SendSignal(GodotThugActor actor) => directory.SendSignal(actor);
    public bool ExecuteScriptCommand(GodotThugActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
        callbacks.ExecuteScriptCommand(actor, opcode, operands, environment);
    public void AttachToSourceBaddyList(GodotHostageActor actor) => directory.AttachSourceBody(actor);
    public void DetachFromSourceBaddyList(GodotHostageActor actor) => directory.DetachSourceBody(actor);
    public void SendSignal(GodotHostageActor actor) => directory.SendSignal(actor);
    // CBaddy/hostage already suppress duplicate death pulses before invoking this host.
    public void SendDeathPulse(GodotHostageActor actor) => directory.SendDeathPulse(actor);
    public void SendMessage(GodotHostageActor sender, object target, int code) => directory.SendMessage(sender, target, code);
    public ActorProcedureState? RecoverActor(object? handle) => directory.RecoverActor(handle);
    public ActorBodyMotion? RecoverBody(ActorProcedureState actor) => directory.RecoverBody(actor);
    public bool ExecuteScriptCommand(GodotHostageActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
        callbacks.ExecuteScriptCommand(actor, opcode, operands, environment);
}
