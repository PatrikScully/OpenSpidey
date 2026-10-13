using Godot;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.UI;

/// <summary>Original TextBoxList renderer and219 owner, sharing the actual simple-message measurements and scene bit scheduler.</summary>
public partial class GodotMissionTextBoxes : Node2D, IMissionTextBoxHost
{
    private MissionMessages messages = null!;
    private SourceParticleScheduler particles = null!;
    private Func<int> timer = null!;
    private readonly HashSet<GodotMissionTextBox> owned = [];
    private Vector2 viewport;
    private bool disposed;
    public uint LastSimpleMessageX => messages.LastSimpleMessageX;
    public int LastSimpleMessageWidth => messages.LastSimpleMessageWidth;
    public IReadOnlyList<GodotMissionTextBox> SourceOrder => particles.Identities(SourceParticleList.TextBox).Cast<GodotMissionTextBox>().ToArray();
    public bool ResourcesDisposed => disposed;

    public static GodotMissionTextBoxes Create(MissionMessages messages, SourceParticleScheduler particles, Func<int> timer)
    {
        ArgumentNullException.ThrowIfNull(messages); ArgumentNullException.ThrowIfNull(particles); ArgumentNullException.ThrowIfNull(timer);
        return new() { Name = "OriginalMissionTextBoxes", messages = messages, particles = particles, timer = timer, ZIndex = -1 };
    }

    public void CreateTextBox(MissionTextBoxDefinition definition)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var box = new GodotMissionTextBox { Name = "OriginalTextBox", State = new(definition, timer()), OwnerLayer = this };
        owned.Add(box);
        try
        {
            AddChild(box);
            particles.Attach(SourceParticleList.TextBox, new(box, clock =>
                { box.State.Move(unchecked((int)clock)); QueueRedraw(); }, () => !box.Disposed && !box.State.Dead, box.Free,
                () => box.State.Protected));
            QueueRedraw();
        }
        catch { box.Free(); throw; }
    }

    public void ClearTextBoxes()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Bit_ClearTextBoxes reads each next before deleting, and ignores mProtected.
        foreach (GodotMissionTextBox box in SourceOrder) box.Free();
        QueueRedraw();
    }

    internal void Forget(GodotMissionTextBox box)
    {
        particles.Detach(box); owned.Remove(box);
        if (!disposed) QueueRedraw();
    }

    public override void _Process(double delta)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size != viewport) { viewport = size; QueueRedraw(); }
    }

    public override void _Draw()
    {
        if (disposed) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        foreach (GodotMissionTextBox box in SourceOrder)
        {
            ScreenRectangle quad = new HudViewportTransform(size.X, size.Y).Project(box.State.DisplayQuad(), HudHorizontalAnchor.Center);
            MissionTextBoxDefinition definition = box.State.Definition;
            DrawRect(new(quad.X, quad.Y, quad.Width, quad.Height), new Color(definition.Red / 255f, definition.Green / 255f,
                definition.Blue / 255f, MissionTextBoxState.DisplayAlpha / 255f));
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (GodotMissionTextBox box in owned.ToArray()) if (GodotObject.IsInstanceValid(box)) box.Free();
        owned.Clear();
    }
}

/// <summary>A real CTextBox identity; leaving/reparenting a native tree never deletes its source bit registration.</summary>
public partial class GodotMissionTextBox : Node
{
    public MissionTextBoxState State { get; internal set; } = null!;
    internal GodotMissionTextBoxes OwnerLayer { get; set; } = null!;
    public bool Disposed { get; private set; }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || Disposed) return;
        Disposed = true; OwnerLayer.Forget(this);
    }
}
