using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Live Godot adapter for the recovered invisible control actor.</summary>
public partial class GodotControlActor : GodotScriptActor, IActorScriptHost
{
    private GodotActorRegistry registry = null!;
    private ControlActorEffects? effects;
    private bool destroyed;
    private ushort bodyFlags = 0x16;
    public ushort BodyFlags => (ushort)(bodyFlags | (Lifecycle?.Phase == ActorLifePhase.Suspended ? 1 : 0) |
        (Lifecycle?.Phase == ActorLifePhase.PendingDeletion ? 0x80 : 0));
    public ActorBodyMotion Body { get; } = new() { LinearDamping = new(1, 1, 1), AngularDamping = new(1, 1, 1) };
    public ControlActorEffects Effects => effects ??= new(ScriptState, registry.RequireControlServices());
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Control;
    internal static GodotControlActor Create(TriggerNode node, GodotActorRegistry registry)
    {
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != 203) throw new NotSupportedException($"Actor type {definition.ActorType}, node {node.Id} has not been ported.");
        var actor = new GodotControlActor { Name = $"ControlActor{node.Id}", registry = registry, Definition = definition, Visible = false };
        actor.ScriptState = new() { ActorType = definition.ActorType, NodeId = node.Id };
        actor.Lifecycle = new(!definition.ClearsBodyFlag2, startsSuspended: false);
        if (definition.ClearsBodyFlag2) actor.bodyFlags &= 0xFFFD;
        actor.Variables = new(actor.ScriptState, registry)
        {
            Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
            CollisionRadius = 0, ItemFlags = 1, AnimationSpeed = 0x10000
        };
        actor.Runtime = new(new(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
        actor.Body.Position = actor.Variables.Position;
        actor.Body.Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
        actor.AddChild(new GodotNativeDestructionObserver { Name = "SourceControlDestructor", Destroyed = actor.DestroyEffects });
        registry.ActorRemoved += actor.OnSourceRemoved;
        return actor;
    }

    internal override void Begin()
    {
        Runtime.Start(registry.Environment);
        ApplyTransform();
    }

    internal override void Advance()
    {
        SourceUpdates++;
        Body.ScriptWaitTicks = ScriptState.WaitTicks;
        Runtime.Advance(2, registry.Environment);
        Body.ScriptWaitTicks = ScriptState.WaitTicks;
        if (effects is not null) effects.Advance(registry.RequireControlServices().Clock, Variables.Position);
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        GlobalPosition = PsxCoordinates.FixedWorldPosition(Variables.Position);
    }

    public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    { Variables.Write(variable, operands); Body.Position = Variables.Position; }
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, SourceNodeId, opcode, operands)) return true;
        if (GodotActorLinkEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorDirectEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotEnvironmentChunkCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorScreenFlashCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Body.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if (GodotActorSpeechCommands.TryExecute(registry, opcode, operands, ScriptState, environment, SourcePosition)) return true;
        if (GodotActorPlayerScriptCommands.TryExecute(registry, opcode, operands)) return true;
        if (opcode == 0x4280)
        {
            Body.Position = Variables.Position;
            bool result = ActorTimedMotionCommands.Execute(ScriptState, Body, operands, ReadVariable, environment, ticks => Body.Integrate(ticks));
            Variables.Position = Body.Position;
            return result;
        }
        if (opcode == 0x4506)
        {
            if (registry.PlayerPosition is { } position) { Variables.Position = position; Body.Position = position; }
            return true;
        }
        if (opcode is 0x450C or 0x428D or 0x450B or 0x4507 or 0x4508 or 0x4509 or 0x428E or 0x4290 or 0x428F or 0x4291)
            return Effects.Execute(opcode, operands, Variables.Position, environment);
        throw new NotSupportedException($"Control actor effect 0x{opcode:X4} has not been ported.");
    }

    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    private void OnSourceRemoved(GodotScriptActor actor)
    { if (ReferenceEquals(actor, this)) DestroyEffects(); }
    private void DestroyEffects()
    {
        if (destroyed) return;
        destroyed = true;
        registry.ActorRemoved -= OnSourceRemoved;
        effects?.Destroy();
    }
    public void SendDeathPulse()
    {
        foreach (ushort link in Definition.Links) registry.ReceivePulse(link);
    }
    public void Retire()
    {
        if (Lifecycle.Phase is ActorLifePhase.Dead or ActorLifePhase.PendingDeletion) return;
        if ((ScriptState.BehaviorFlags & 0x4000) == 0 && ScriptState.TryMarkDeathPulseSent()) SendDeathPulse();
        bodyFlags |= 0x40;
        Lifecycle.Kill();
    }
    /// <summary>Inherited CBody::Die sets zombie40; it does not send the script-completion pulse.</summary>
    public void DieSourceBody()
    { if ((bodyFlags & 0x40) == 0) { bodyFlags |= 0x40; Lifecycle.Kill(); } }
}
