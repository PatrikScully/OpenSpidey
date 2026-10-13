using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotSwitchConfiguration
{
    public required GodotSwitchEnvironment Environment { get; init; }
    public required Action<GodotSwitchActor> AttachToControlList { get; init; }
    public required Action<GodotSwitchActor> DetachFromControlList { get; init; }
    public required Func<bool> HasPlayer { get; init; }
    public required Func<int> ReadPlayerCounter { get; init; }
    public required Action<int> WritePlayerCounter { get; init; }
}

/// <summary>Actual control-list CSwitch. Its visuals/collision remain shared original ENV items.</summary>
public partial class GodotSwitchActor : GodotScriptActor, ISwitchHost, IActorScriptVariableWorld
{
    private GodotActorRegistry registry = null!;
    private GodotSwitchConfiguration configuration = null!;
    private bool attached;
    public SwitchState State { get; private set; } = null!;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Control;
    public PlayerSwitchCandidate TargetCandidate => new(this, SwitchState.Type);
    public PlayerSwitchAimPoint? AutoAimPoint => State.GetAutoAimPoint();
    public static GodotSwitchActor Create(TriggerNode node, GodotActorRegistry registry, GodotSwitchConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != SwitchState.Type) throw new InvalidDataException("Native switch factory requires source407.");
        var actor = new GodotSwitchActor { Name = $"Switch{node.Id}", registry = registry, configuration = configuration,
            Definition = definition, State = new(node.Id), ScriptState = new() { ActorType = 407, NodeId = node.Id },
            Lifecycle = new(!definition.ClearsBodyFlag2, false) };
        try
        {
            actor.Variables = new(actor.ScriptState, actor) { CollisionRadius = 0 };
            SwitchBehavior.Construct(actor.State, definition, node.SourceOffset, actor);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= unchecked((ushort)~2);
            actor.Synchronize();
            return actor;
        }
        catch
        {
            if (actor.attached) { configuration.DetachFromControlList(actor); actor.attached = false; }
            actor.Free(); throw;
        }
    }
    internal override void Begin() { }
    internal override void Advance()
    {
        SourceUpdates++;
        State.InputFlags |= unchecked((ushort)ScriptState.PendingSignals);
        ScriptState.PendingSignals = 0;
        SwitchBehavior.Advance(State, 2, this);
        Synchronize();
    }
    public void AdvanceSource(int elapsedTicks)
    {
        SourceUpdates++;
        SwitchBehavior.Advance(State, elapsedTicks, this);
        Synchronize();
    }
    public void Flick() { SwitchBehavior.Flick(State, this); Synchronize(); }
    public bool Hit() => SwitchBehavior.Hit();
    private void Synchronize()
    {
        Variables.Position = State.Position;
        base.Position = PsxCoordinates.FixedWorldPosition(State.Position);
        configuration.Environment.Synchronize();
        if ((State.BodyFlags & 0x40) != 0) Lifecycle.Kill();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || !attached) return;
        configuration.DetachFromControlList(this); attached = false;
    }
    public void AttachToControlList(SwitchState state)
    {
        if (!ReferenceEquals(state, State)) throw new InvalidDataException("Switch attachment references another source state.");
        configuration.AttachToControlList(this); attached = true;
    }
    public SwitchEnvironmentItem? FindEnvironmentItem(uint checksum) => configuration.Environment.Resolve(checksum);
    public void MarkModelSwitchFaces(SwitchEnvironmentItem item) => configuration.Environment.MarkSwitchFaces(item);
    public IEnumerable<SwitchPulseNode> SourceTriggerOrder => registry.Triggers.Nodes.Where(node => node.Id != 0 && node.TypeCode == 1).Select(node =>
    {
        TriggerPosition position = TriggerPlacement.Read(node).Position;
        return new SwitchPulseNode(node.Id, node.TypeCode, new FixedVector3(position.X, position.Y, position.Z) << 12);
    });
    public void SendPulseToNode(ushort node) => registry.ReceivePulse(node);
    public void SendAttachedPulse(ushort sourceNode)
    {
        if (sourceNode != Definition.NodeId) throw new InvalidDataException("Switch pulse lost its source link owner.");
        foreach (ushort linked in Definition.Links) registry.ReceivePulse(linked);
    }
    public bool HasPlayer => configuration.HasPlayer();
    public int PlayerCounter { get => configuration.ReadPlayerCounter(); set => configuration.WritePlayerCounter(value); }
    public void ReportInvalidState(int mode) => throw new InvalidDataException($"Switch{State.Node}: source input/state command is invalid for mode{mode}.");
    public void ReportMissingPulseNode(int selector) => throw new InvalidDataException($"Switch{State.Node}: original LFA1 pulse node selector{selector} is missing.");
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    public FixedVector3? PlayerPosition => registry.PlayerPosition;
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
    public int Random(int range) => registry.RandomSource(range);
    public ushort FirstLink(ushort nodeId) => registry.FirstLink(nodeId);
    public void SelectModel(uint checksum) => throw new NotSupportedException("Original CSwitch has no model-selection script command; ENV checksums belong to its constructor.");
}
