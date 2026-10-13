using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.UI;

/// <summary>Actual archived Compass sprite and original PC dial/needle geometry in a proportional right-bottom HUD group.</summary>
public partial class GodotCompass : Node2D, ICompassDrawHost
{
    private ImageTexture texture = null!;
    private SpriteAnimationFrame frame;
    private Func<CompassView> view = null!;
    private readonly List<CompassQuad> draws = [];
    private byte[] brightness = Enumerable.Range(0, 256).Select(channel => (byte)Math.Min(channel * 2, 255)).ToArray();
    private bool disposed;
    public CompassState State { get; private set; } = null!;
    public bool Enabled { get; set; } = true;
    internal SpriteAnimationFrame SourceFrame => frame;
    internal ImageTexture SourceTexture => texture;
    internal IReadOnlyList<CompassQuad> Draws => draws;
    internal bool ResourcesDisposed => disposed;

    public static GodotCompass Create(byte[] originalBits, CompassState state, Func<CompassView> liveView)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(liveView);
        SpriteAnimation animation = SpriteAnimationReader.Read(originalBits).Single(animation => animation.Name.Equals("Compass", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count != 1) throw new InvalidDataException("Original Compass requires its single source frame.");
        SpriteAnimationFrame frame = animation.Frames[0];
        DecodedTexture decoded = PsxTextureReader.Read(originalBits).First(texture => texture.Checksum == frame.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        return new() { Name = "OriginalCompass", State = state, view = liveView, frame = frame,
            texture = ImageTexture.CreateFromImage(image), TextureFilter = TextureFilterEnum.Linear };
    }
    public void SetBrightness(ReadOnlySpan<byte> table)
    {
        if (table.Length != 256) throw new ArgumentException("Original PC brightness table requires256 entries.", nameof(table));
        brightness = table.ToArray(); QueueRedraw();
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (disposed || !Enabled) return;
        draws.Clear();
        State.Display(view(), frame, this);
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        var viewport = new HudViewportTransform(size.X, size.Y);
        foreach (CompassQuad quad in draws)
        {
            CompassPoint[] source = [quad.First, quad.Second, quad.Fourth, quad.Third];
            Vector2[] points = source.Select(point => new Vector2(viewport.ExtraWidth + point.X * viewport.ScaleX,
                viewport.ExtraHeight + point.Y * viewport.ScaleY)).ToArray();
            Color colour = new(brightness[(quad.Argb >> 16) & 255] / 255f, brightness[(quad.Argb >> 8) & 255] / 255f, brightness[quad.Argb & 255] / 255f, (quad.Argb >> 24) / 255f);
            if (quad.Textured) DrawPolygon(points, new Color[] { colour, colour, colour, colour },
                new Vector2[] { new(quad.LeftU, 0), new(quad.RightU, 0), new(quad.RightU, 1), new(quad.LeftU, 1) }, texture);
            else DrawPolygon(points, new Color[] { colour, colour, colour, colour });
        }
    }
    public bool TryAllocateNeedle(int half) => true; // Texture/frame resources are acquired before enabling the native node.
    public void EmitQuad(CompassQuad quad) => draws.Add(quad);
    private void Release()
    {
        if (disposed) return;
        disposed = true; draws.Clear(); texture?.Dispose();
    }
    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }
}
