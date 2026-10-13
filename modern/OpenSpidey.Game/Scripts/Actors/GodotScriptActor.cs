using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Game.Actors;

/// <summary>Engine-owned actor identity and lifetime, shared by source actor adapters.</summary>
public abstract partial class GodotScriptActor : Node3D
{
    public TriggerActorDefinition Definition { get; protected set; } = null!;
    public ActorScriptState ScriptState { get; protected set; } = null!;
    public virtual ushort SourceNodeId => ScriptState.NodeId;
    public short SourceActorType => ScriptState.ActorType;
    public ActorScriptVariables Variables { get; protected set; } = null!;
    public ActorScriptRuntime Runtime { get; protected set; } = null!;
    public ActorLifecycle Lifecycle { get; protected set; } = null!;
    public virtual FixedVector3 SourcePosition => Variables.Position;
    public int SourceUpdates { get; protected set; }
    public SourceSuperItemDepth SourceDepth { get; } = new();
    public abstract ActorUpdateGroup UpdateGroup { get; }
    internal abstract void Begin();
    internal abstract void Advance();
}
