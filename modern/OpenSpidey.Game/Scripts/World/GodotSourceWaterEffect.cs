using Godot;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Game.World;

/// <summary>Actual untextured source-slot6 water tint quad, including direct PC brightness/ARGB alpha and full gameplay viewport proportions.</summary>
public partial class GodotSourceWaterEffect : Node2D
{
    private Func<uint> clock = null!;
    private Func<int> pause = null!;
    private Func<(uint Width, uint Height)> resolution = null!;
    private byte[] brightness = [];
    public SourceWaterPostState State { get; private set; } = null!;
    public uint UploadedArgb { get; private set; }
    public Rect2 UploadedRectangle { get; private set; }
    public bool Submitted { get; private set; }

    public static GodotSourceWaterEffect Create(SourceWaterPostState state, Func<uint> clock, Func<int> pause,
        Func<(uint Width, uint Height)> resolution, IReadOnlyList<byte> brightness)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(pause);
        ArgumentNullException.ThrowIfNull(resolution); ArgumentNullException.ThrowIfNull(brightness);
        if (brightness.Count != 256) throw new ArgumentException("Water post renderer requires the real PC brightness table.", nameof(brightness));
        return new() { Name = "OriginalWaterTintSlot6", State = state, clock = clock, pause = pause, resolution = resolution,
            brightness = brightness.ToArray(), ZIndex = SourceWaterQuad.SourceSlot, ZAsRelative = false };
    }
    public override void _Process(double delta) => SynchronizeSourceRender();
    public void SynchronizeSourceRender()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        (uint width, uint height) = resolution();
        State.PauseValue = pause();
        SourceWaterPostEffects.AdvanceRender(State, clock(), (int)size.X, (int)size.Y, width, height);
        QueueRedraw();
    }
    public override void _Draw()
    {
        Submitted = State.Quad is not null;
        if (State.Quad is not { } quad) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        UploadedArgb = SourceWaterPostEffects.ApplyBrightness(quad.Argb, brightness);
        UploadedRectangle = new(Vector2.Zero, new Vector2(quad.Width * size.X / 512, quad.Height * size.Y / 240));
        DrawRect(UploadedRectangle, new Color((byte)(UploadedArgb >> 16) / 255f, (byte)(UploadedArgb >> 8) / 255f,
            (byte)UploadedArgb / 255f, (byte)(UploadedArgb >> 24) / 255f));
    }
}
