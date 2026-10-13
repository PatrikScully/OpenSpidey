using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.UI;

/// <summary>Actual four original flat target triangles, projecting their genuine world target through the live native camera.</summary>
public partial class GodotScreenTarget : Node2D
{
    public ScreenTargetState State { get; } = new();
    public required PlayerFollowCamera Camera { get; init; }
    internal IReadOnlyList<ScreenTargetTriangle> Draws { get; private set; } = [];
    public void SetTargetVisible(bool shown) { State.Visible = shown; QueueRedraw(); }
    public void SetTarget(FixedVector3 position, ushort radius, short angle) { State.SetTarget(position, radius, angle); QueueRedraw(); }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        Draws = [];
        if (!State.Visible) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        HudViewportTransform viewport = new(size.X, size.Y);
        Vector2 projected = Camera.UnprojectPosition(PsxCoordinates.FixedWorldPosition(State.Position));
        // Use the recovered proportional logical pixel size while keeping the target centre in the expanded game view.
        short centerX = unchecked((short)(int)((projected.X - viewport.ExtraWidth / 2) / viewport.ScaleX));
        short centerY = unchecked((short)(int)((projected.Y - viewport.ExtraHeight / 2) / viewport.ScaleY));
        Draws = State.Build(centerX, centerY);
        foreach (ScreenTargetTriangle triangle in Draws)
        {
            Color colour = new(((triangle.Argb >> 16) & 255) / 255f, ((triangle.Argb >> 8) & 255) / 255f, (triangle.Argb & 255) / 255f, 1);
            Vector2[] points = [Project(triangle.First), Project(triangle.Second), Project(triangle.Third)];
            DrawPolygon(points, [colour, colour, colour]);
        }
        Vector2 Project(ScreenTargetPoint point) => new(viewport.ExtraWidth / 2 + point.X * viewport.ScaleX, viewport.ExtraHeight / 2 + point.Y * viewport.ScaleY);
    }
}
