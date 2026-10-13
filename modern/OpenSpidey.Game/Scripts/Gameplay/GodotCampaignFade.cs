using Godot;
using OpenSpidey.Core.Effects;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Actual viewport snapshot, source sepia RGB5 conversion and 120-vblank completion barrier.</summary>
public partial class GodotCampaignFade : CanvasLayer
{
    private readonly TextureRect overlay = new()
    {
        Name = "CampaignSepia",
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Visible = false
    };
    private ImageTexture? texture;
    private TaskCompletionSource? completion;
    public SourceGameFadeState State { get; } = new();
    public int VblanksAdvanced { get; private set; }

    public GodotCampaignFade() { Layer = 200; ProcessMode = ProcessModeEnum.Always; }

    public override void _Ready()
    {
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        SetPhysicsProcess(false);
    }

    public Task BeginSepiaAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Campaign fade requires an actual viewport.");
        if (completion is not null) throw new InvalidOperationException("A campaign fade is already running.");
        cancellationToken.ThrowIfCancellationRequested();
        using Image captured = GetViewport().GetTexture().GetImage();
        captured.Convert(Image.Format.Rgba8);
        byte[] pixels = captured.GetData();
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            ushort source = (ushort)((pixels[offset] >> 3) | (pixels[offset + 1] >> 3) << 5 | (pixels[offset + 2] >> 3) << 10);
            ushort sepia = SourceSepiaPixel.Convert(source);
            pixels[offset] = Expand(sepia);
            pixels[offset + 1] = Expand(sepia >> 5);
            pixels[offset + 2] = Expand(sepia >> 10);
        }
        using Image image = Image.CreateFromData(captured.GetWidth(), captured.GetHeight(), false, Image.Format.Rgba8, pixels);
        texture?.Dispose();
        texture = ImageTexture.CreateFromImage(image);
        overlay.Texture = texture;
        overlay.Visible = true;
        State.BeginSepia();
        VblanksAdvanced = 0;
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        completion = pending;
        SetPhysicsProcess(true);
        return WaitForFadeAsync(pending, cancellationToken);
    }

    private async Task WaitForFadeAsync(TaskCompletionSource pending, CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
        try { await pending.Task; }
        finally
        {
            completion = null;
            if (GodotObject.IsInstanceValid(this)) SetPhysicsProcess(false);
        }
    }

    public void HideSnapshot()
    {
        if (GodotObject.IsInstanceValid(overlay)) overlay.Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        State.AdvanceVblank();
        VblanksAdvanced++;
        if (!State.Active)
        {
            SetPhysicsProcess(false);
            completion?.TrySetResult();
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        completion?.TrySetCanceled();
        completion = null;
        texture?.Dispose();
        texture = null;
    }

    private static byte Expand(int value) { value &= 31; return (byte)((value << 3) | (value >> 2)); }
}
