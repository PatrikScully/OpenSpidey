using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.UI;

/// <summary>The actual archived Reticle sprite and source world-target colour/scale, centred in the expanded game viewport.</summary>
public partial class GodotPlayerLookaroundReticle : Node2D
{
    private ImageTexture texture = null!;
    private SpriteAnimationFrame frame;
    private PlayerFollowCamera camera = null!;
    private Func<PlayerLookaroundReticleView> view = null!;
    private byte[] brightness = Enumerable.Range(0, 256).Select(channel => (byte)Math.Min(channel * 2, 255)).ToArray();
    private bool disposed;
    public bool Enabled { get; set; } = true;
    internal SpriteAnimationFrame SourceFrame => frame;
    internal ImageTexture SourceTexture => texture;
    internal PlayerLookaroundReticleQuad? Drawn { get; private set; }
    internal bool ResourcesDisposed => disposed;
    internal uint SourceTextureFlags { get; private set; }

    public static GodotPlayerLookaroundReticle Create(byte[] originalBits, PlayerFollowCamera camera,
        Func<PlayerLookaroundReticleView> liveView)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(liveView);
        SpriteAnimation source = SpriteAnimationReader.Read(originalBits).Single(animation => animation.Name.Equals("Reticle", StringComparison.OrdinalIgnoreCase));
        if (source.Frames.Count != 1) throw new InvalidDataException("The original Reticle requires its single source frame.");
        SpriteAnimationFrame frame = source.Frames[0];
        DecodedTexture decoded = PsxTextureReader.Read(originalBits).First(texture => texture.Checksum == frame.TextureChecksum);
        using Image image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        return new() { Name = "OriginalLookaroundReticle", frame = frame, camera = camera, view = liveView,
            texture = ImageTexture.CreateFromImage(image), SourceTextureFlags = decoded.Flags, TextureFilter = TextureFilterEnum.Linear };
    }

    public void SetBrightness(ReadOnlySpan<byte> table)
    {
        if (table.Length != 256) throw new ArgumentException("Source PC brightness requires256 entries.", nameof(table));
        brightness = table.ToArray(); QueueRedraw();
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        Drawn = null;
        if (disposed || !Enabled) return;
        PlayerLookaroundReticleView state = view();
        if (state.Type == 0) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        var viewport = new HudViewportTransform(size.X, size.Y);
        FixedVector3 cameraWhole = camera.SourcePosition >> 12;
        FixedVector3 relative = PlayerLookaroundReticle.ProjectionVector(state.TargetPosition, cameraWhole);
        int depth = FixedRotation.Transform(camera.SourceViewRotation, relative).Z;
        Vector2 projected = camera.UnprojectPosition(PsxCoordinates.FixedWorldPosition((relative + cameraWhole) << 12));
        ushort x = unchecked((ushort)(int)((projected.X - viewport.ExtraWidth / 2) / viewport.ScaleX));
        ushort y = unchecked((ushort)(int)((projected.Y - viewport.ExtraHeight / 2) / viewport.ScaleY));
        Drawn = PlayerLookaroundReticle.Build(state, frame, x, y, depth);
        if (Drawn is not { } quad) return;
        HudSourceQuad q = quad.Rectangle;
        Vector2[] points = [Point(q.Left, q.Top), Point(q.Right, q.Top), Point(q.Right, q.Bottom), Point(q.Left, q.Bottom)];
        uint argb = quad.Argb;
        byte alpha = (SourceTextureFlags & 0x1000) != 0 ? (byte)128 : (byte)(argb >> 24);
        Color colour = new(brightness[(argb >> 16) & 255] / 255f, brightness[(argb >> 8) & 255] / 255f,
            brightness[argb & 255] / 255f, alpha / 255f);
        DrawPolygon(points, new Color[] { colour, colour, colour, colour },
            new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }, texture);
        Vector2 Point(short px, short py) => new(viewport.ExtraWidth / 2 + px * viewport.ScaleX, viewport.ExtraHeight / 2 + py * viewport.ScaleY);
    }

    private void Release()
    {
        if (disposed) return;
        disposed = true; Drawn = null; texture?.Dispose();
    }
    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }
}
