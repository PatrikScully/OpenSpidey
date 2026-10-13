using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Actors.Switches;

public enum SourceSwitchMode { Inactive, Off, On, Reward, Penalty, Fading }

/// <summary>The original static ENV CItem, with shared visibility/collision flags and RGB.</summary>
public sealed class SwitchEnvironmentItem(object identity, uint checksum, FixedVector3 position)
{
    public object Identity { get; } = identity;
    public uint ModelChecksum { get; } = checksum;
    public FixedVector3 Position { get; set; } = position;
    public ActorAngles Angles { get; set; }
    public ushort ItemFlags { get; set; }
    public uint Color { get; set; }
}
public sealed class SwitchState(ushort node)
{
    public ushort Node { get; } = node;
    public const ushort Type = 407;
    public FixedVector3 Position { get; set; }
    public ActorAngles Angles { get; set; }
    public ushort BodyFlags { get; set; } = 0x16;
    public ushort InputFlags { get; set; }
    public uint ElapsedOnTicks { get; set; }
    public ushort Timeout { get; set; }
    public SourceSwitchMode Mode { get; set; }
    public SwitchEnvironmentItem? OffItem { get; set; }
    public SwitchEnvironmentItem? OnItem { get; set; }
    public FixedVector3 OffAimPosition { get; set; }
    public FixedVector3 OnAimPosition { get; set; }
    public object OffAimIdentity { get; } = new();
    public object OnAimIdentity { get; } = new();
    public byte SubtractPlayerCounter { get; set; }
    public PlayerSwitchAimPoint? GetAutoAimPoint() => PlayerTargetSelection.GetSwitchAutoAimPoint((int)Mode,
        new(OffAimIdentity, OffAimPosition), new(OnAimIdentity, OnAimPosition));
}
public readonly record struct SwitchPulseNode(ushort Id, ushort Type, FixedVector3 Position);
public interface ISwitchHost
{
    void AttachToControlList(SwitchState state);
    SwitchEnvironmentItem? FindEnvironmentItem(uint checksum);
    void MarkModelSwitchFaces(SwitchEnvironmentItem item);
    IEnumerable<SwitchPulseNode> SourceTriggerOrder { get; }
    void SendPulseToNode(ushort node);
    void SendAttachedPulse(ushort sourceNode);
    bool HasPlayer { get; }
    int PlayerCounter { get; set; }
    void ReportInvalidState(int mode);
    void ReportMissingPulseNode(int selector);
}

/// <summary>Original CSwitch type407; it controls two ENV items and owns no separate drawable model or script.</summary>
public static class SwitchBehavior
{
    public static void Construct(SwitchState state, TriggerActorDefinition definition, int sourceNodeOffset, ISwitchHost host)
    {
        if (definition.ActorType != SwitchState.Type || definition.NodeId != state.Node)
            throw new InvalidDataException("Switch construction needs its original type407 trigger definition.");
        state.BodyFlags &= unchecked((ushort)~0x10);
        host.AttachToControlList(state);
        state.Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        state.Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
        var cursor = new ActorScriptCursor(definition.BehaviorData, checked(sourceNodeOffset + definition.ObjectDataOffset + 18));
        state.OffItem = host.FindEnvironmentItem(cursor.ReadAlignedDword());
        state.OnItem = host.FindEnvironmentItem(cursor.ReadAlignedDword());
        if (state.OffItem is { } off) { state.OffAimPosition = off.Position; host.MarkModelSwitchFaces(off); }
        if (state.OnItem is { } on) { state.OnAimPosition = on.Position; host.MarkModelSwitchFaces(on); }
        SourceSwitchMode mode = (SourceSwitchMode)unchecked((short)cursor.ReadWord());
        switch (mode)
        {
            case SourceSwitchMode.Inactive: state.Mode = mode; break;
            case SourceSwitchMode.Off: Off(state); break;
            case SourceSwitchMode.On: On(state); break;
            case SourceSwitchMode.Reward:
            case SourceSwitchMode.Penalty:
                state.Mode = mode;
                state.SubtractPlayerCounter = (byte)(mode == SourceSwitchMode.Penalty ? 1 : 0);
                SwitchEnvironmentItem item = Required(state.OnItem);
                item.ItemFlags |= 0x400;
                item.Color = 0;
                Visible(item, true);
                break;
            default: host.ReportInvalidState((int)mode); break;
        }
        state.Timeout = cursor.ReadWord();
    }

    public static void Advance(SwitchState state, int elapsedTicks, ISwitchHost host)
    {
        if ((state.InputFlags & 1) != 0)
        {
            state.InputFlags &= 0xFFFE;
            switch (state.Mode)
            {
                case SourceSwitchMode.Inactive: Off(state); break;
                case SourceSwitchMode.Off: On(state); host.SendAttachedPulse(state.Node); break;
                case SourceSwitchMode.On: state.Mode = SourceSwitchMode.Inactive; break;
                default: host.ReportInvalidState((int)state.Mode); break;
            }
        }
        if (state.Mode is SourceSwitchMode.On or SourceSwitchMode.Reward or SourceSwitchMode.Penalty && state.Timeout != 0)
        {
            state.ElapsedOnTicks = unchecked(state.ElapsedOnTicks + (uint)elapsedTicks);
            if (state.ElapsedOnTicks > state.Timeout)
            {
                state.ElapsedOnTicks = 0;
                if (state.Mode == SourceSwitchMode.On) Off(state);
                else { PulseNode(state.SubtractPlayerCounter == 0 ? 3 : 2, host); state.Mode = SourceSwitchMode.Fading; }
            }
        }
        if (state.Mode is SourceSwitchMode.Reward or SourceSwitchMode.Penalty)
        {
            SwitchEnvironmentItem item = Required(state.OnItem);
            item.ItemFlags &= 0xFFFE;
            uint intensity = item.Color & 255;
            // Original PC0x4D1C06 writes ENV108 RGB; recovered C++ mistakenly writes the invisible switch body.
            item.Color = intensity < 128 ? Gray(unchecked(intensity + (uint)elapsedTicks)) : 0x808080;
        }
        if (state.Mode == SourceSwitchMode.Fading)
        {
            SwitchEnvironmentItem item = Required(state.OnItem);
            uint amount = unchecked((uint)elapsedTicks * 4), intensity = item.Color & 255;
            if (intensity > amount) item.Color = Gray(unchecked(intensity - amount));
            else
            {
                item.ItemFlags |= 0x21;
                // PC dispatches this switch's CBody::Die, not the ENV item's Die.
                state.BodyFlags |= 0x40;
                item.Color = 0;
            }
        }
    }

    public static void Flick(SwitchState state, ISwitchHost host)
    {
        switch (state.Mode)
        {
            case SourceSwitchMode.Off: On(state); host.SendAttachedPulse(state.Node); break;
            case SourceSwitchMode.On: Off(state); if (state.Timeout == 0) host.SendAttachedPulse(state.Node); break;
            case SourceSwitchMode.Reward:
            case SourceSwitchMode.Penalty:
                ChangeCounter(state, host);
                PulseNode(state.SubtractPlayerCounter == 0 ? 1 : 0, host);
                state.Mode = SourceSwitchMode.Fading;
                break;
            case SourceSwitchMode.Fading: ChangeCounter(state, host); break;
        }
    }

    public static SwitchState? FindOwner(object itemIdentity, IEnumerable<SwitchState> sourceControlOrder) =>
        sourceControlOrder.FirstOrDefault(state => ReferenceEquals(state.OffItem?.Identity, itemIdentity) || ReferenceEquals(state.OnItem?.Identity, itemIdentity));
    // CSwitch inherits original CBody::Hit, which accepts the hit without changing state.
    public static bool Hit() => true;
    public static void Visible(SwitchEnvironmentItem item, bool visible)
    {
        if (visible) item.ItemFlags &= 0xFFDE;
        else item.ItemFlags |= 0x21;
    }
    private static void On(SwitchState state)
    {
        state.ElapsedOnTicks = 0; state.Mode = SourceSwitchMode.On;
        Visible(Required(state.OnItem), true); Visible(Required(state.OffItem), false);
    }
    private static void Off(SwitchState state)
    {
        state.Mode = SourceSwitchMode.Off;
        Visible(Required(state.OnItem), false); Visible(Required(state.OffItem), true);
    }
    private static void ChangeCounter(SwitchState state, ISwitchHost host)
    {
        if (host.HasPlayer) host.PlayerCounter = unchecked(host.PlayerCounter + (state.SubtractPlayerCounter != 0 ? -1 : 1));
    }
    private static uint Gray(uint intensity) => unchecked((((intensity << 8) | intensity) << 8) | intensity);
    private static SwitchEnvironmentItem Required(SwitchEnvironmentItem? item) => item ?? throw new InvalidDataException("Original switch state needs a resolved ENV item.");
    private static void PulseNode(int selector, ISwitchHost host)
    {
        foreach (SwitchPulseNode node in host.SourceTriggerOrder)
        {
            if (node.Id == 0 || node.Type != 1) continue;
            FixedVector3 position = node.Position;
            bool matches = selector switch
            {
                0 => (position.X & unchecked((int)0xFFFFF000)) == 409600 && position.Y == 0 && position.Z == 0,
                1 => position == default,
                2 => (position.X & unchecked((int)0xFFFFF000)) == 409600 && position.Y == 0 && (position.Z & unchecked((int)0xFFFFF000)) == 409600,
                3 => position.X == 0 && position.Y == 0 && (position.Z & unchecked((int)0xFFFFF000)) == 409600,
                _ => false,
            };
            if (!matches) continue;
            host.SendPulseToNode(node.Id); return;
        }
        host.ReportMissingPulseNode(selector);
    }
}
