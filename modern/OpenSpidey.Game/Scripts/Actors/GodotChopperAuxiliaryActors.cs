using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Choppers;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>CSearchlight type322 from Chopper module user1; rendering AI is not ported yet.</summary>
public partial class GodotChopperSearchlightActor : GodotScriptActor, IGodotSourceBaddyBody, IActorScriptHost
{
    private GodotActorRegistry registry = null!;
    private bool begun;
    public ActorBodyMotion Body { get; private set; } = null!;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public ImportedAnimatedModel Model { get; } = null!;
    public ushort MinorRadius => 0;
    public uint BehaviorFlags => ScriptState.BehaviorFlags;
    public ushort BodyFlags { get; private set; }
    public short Health { get; private set; } = 100;

    public static GodotChopperSearchlightActor Create(TriggerNode node, GodotActorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != ChopperState.SearchlightType)
            throw new NotSupportedException("Chopper module user1 requires type322.");
        var actor = new GodotChopperSearchlightActor { Name = $"Searchlight{node.Id}", registry = registry, Definition = definition };
        actor.Body = new() { Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
            Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z) };
        actor.ScriptState = new() { ActorType = definition.ActorType, NodeId = node.Id };
        actor.Variables = new(actor.ScriptState, registry) { Position = actor.Body.Position, Health = actor.Health, ItemFlags = 2, AnimationSpeed = 0x10000 };
        actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
        actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
        return actor;
    }

    internal override void Begin() { Runtime.Start(registry.Environment); begun = true; }
    internal override void Advance() => SourceUpdates++;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) { }
    public bool Hit(ActorHit hit) { Health = (short)Math.Max(0, Health - hit.Damage); return true; }
    public void MarkSourceBodyDead() => BodyFlags |= 0x40;
    public void MarkDead() => Lifecycle.Kill();
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
        throw new NotSupportedException($"Searchlight {Definition.NodeId}: script command 0x{opcode:X4} is not connected.");
    public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
    public void WriteVariable(ushort variable, ActorScriptCursor operands) => Variables.Write(variable, operands);
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() { SendDeathPulse(); MarkSourceBodyDead(); ItemFlags |= 1; MarkDead(); }
}

/// <summary>CSniperTarget type323 from Chopper module user2; strike AI is not ported yet.</summary>
public partial class GodotChopperSniperActor : GodotScriptActor, IGodotSourceBaddyBody, IActorScriptHost
{
    private GodotActorRegistry registry = null!;
    public ActorBodyMotion Body { get; private set; } = null!;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public ImportedAnimatedModel Model { get; } = null!;
    public ushort MinorRadius => 0;
    public uint BehaviorFlags => ScriptState.BehaviorFlags;
    public ushort BodyFlags { get; private set; }
    public short Health { get; private set; } = 100;

    public static GodotChopperSniperActor Create(TriggerNode node, GodotActorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != ChopperState.SniperType)
            throw new NotSupportedException("Chopper module user2 requires type323.");
        var actor = new GodotChopperSniperActor { Name = $"Sniper{node.Id}", registry = registry, Definition = definition };
        actor.Body = new() { Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
            Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z) };
        actor.ScriptState = new() { ActorType = definition.ActorType, NodeId = node.Id };
        actor.Variables = new(actor.ScriptState, registry) { Position = actor.Body.Position, Health = actor.Health, ItemFlags = 2, AnimationSpeed = 0x10000 };
        actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
        actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
        return actor;
    }

    internal override void Begin() { Runtime.Start(registry.Environment); }
    internal override void Advance() => SourceUpdates++;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) { }
    public bool Hit(ActorHit hit) { Health = (short)Math.Max(0, Health - hit.Damage); return true; }
    public void MarkSourceBodyDead() => BodyFlags |= 0x40;
    public void MarkDead() => Lifecycle.Kill();
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
        throw new NotSupportedException($"Sniper {Definition.NodeId}: script command 0x{opcode:X4} is not connected.");
    public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
    public void WriteVariable(ushort variable, ActorScriptCursor operands) => Variables.Write(variable, operands);
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() { SendDeathPulse(); MarkSourceBodyDead(); ItemFlags |= 1; MarkDead(); }
}
