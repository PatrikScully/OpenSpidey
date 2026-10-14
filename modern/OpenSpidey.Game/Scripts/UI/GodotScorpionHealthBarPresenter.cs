using Godot;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Game.UI;

/// <summary>Draw-phase adapter for the source Scorpion/Jonah panel; health and hit-flash remain owned by the encounter actors.</summary>
internal partial class GodotScorpionHealthBarPresenter : Node
{
    private BossHealthBars<ScorpionEncounterBodyState, GodotBossHealthBarTexture> bars = null!;
    private Func<ScorpionEncounterBodyState, int> readHitFlashUpdates = null!;
    private GodotScorpionHealthBarResources resources = null!;
    private bool disposed;
    internal IReadOnlyList<BossHealthBarPrimitive<GodotBossHealthBarTexture>> LastFrame { get; private set; } = [];
    internal GodotScorpionHealthBarResources Resources => resources;
    internal bool ResourcesDisposed => resources.IsDisposed;

    public static GodotScorpionHealthBarPresenter Create(GodotScorpionHealthBarResources resources,
        BossHealthBars<ScorpionEncounterBodyState, GodotBossHealthBarTexture> bars, Func<ScorpionEncounterBodyState, int> readHitFlashUpdates)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(readHitFlashUpdates);
        resources.AttachPresenter();
        return new() { Name = "OriginalScorpionHealthBar", resources = resources, bars = bars, readHitFlashUpdates = readHitFlashUpdates };
    }

    public void DrawOn(CanvasItem canvas, HudViewportTransform viewport, int screenOffsetY)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        resources.RequireActive();
        LastFrame = ScorpionHealthBarFrame.Build(bars, body => new(body.BodyFlags, body.Health), readHitFlashUpdates,
            resources.PipTexture, resources.PipFrame, screenOffsetY);
        canvas.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        foreach (BossHealthBarPrimitive<GodotBossHealthBarTexture> primitive in LastFrame.OrderByDescending(value => value.Depth))
        {
            switch (primitive)
            {
                case BossHealthBarSprite<GodotBossHealthBarTexture> sprite:
                    canvas.DrawTextureRect(sprite.Texture.Native, Rectangle(viewport.Project(sprite.Quad, HudHorizontalAnchor.Right)), false);
                    break;
                case BossHealthBarQuad<GodotBossHealthBarTexture> quad:
                    ScreenRectangle rectangle = viewport.Project(quad.Rectangle, HudHorizontalAnchor.Right);
                    Vector2[] points = [new(rectangle.X, rectangle.Y), new(rectangle.X + rectangle.Width, rectangle.Y),
                        new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), new(rectangle.X, rectangle.Y + rectangle.Height)];
                    Color left = Colour(quad.LeftColour), right = Colour(quad.RightColour);
                    canvas.DrawPolygon(points, [left, right, right, left]);
                    break;
            }
        }
    }

    private static Rect2 Rectangle(ScreenRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    private static Color Colour(uint bgr) => new(Shade((byte)bgr), Shade((byte)(bgr >> 8)), Shade((byte)(bgr >> 16)));
    private static float Shade(byte channel) => Math.Min(channel * 2, 255) / 255f;

    private void Release()
    {
        if (disposed) return;
        disposed = true;
        LastFrame = [];
        resources?.Dispose();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
