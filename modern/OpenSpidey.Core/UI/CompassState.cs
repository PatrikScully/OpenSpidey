using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Core.UI;

public readonly record struct CompassView(FixedVector3 CameraPosition, FixedJointTransform ViewRotation, uint Clock, int PanelScreenY);
public readonly record struct CompassPoint(int X, int Y);
public readonly record struct CompassQuad(bool Textured, CompassPoint First, CompassPoint Second, CompassPoint Third,
    CompassPoint Fourth, uint Argb, float Depth, float LeftU, float RightU);
public interface ICompassDrawHost
{
    bool TryAllocateNeedle(int half);
    void EmitQuad(CompassQuad quad);
}

/// <summary>Panel_Create/Destroy/DisplayCompass, preserving original PC primitive order and allocation-sensitive flash decay.</summary>
public sealed class CompassState
{
    public bool Active { get; private set; }
    public FixedVector3 TargetPosition { get; private set; }
    public int FlashTimer { get; set; }
    public void Create(FixedVector3 fixedTarget) { TargetPosition = fixedTarget >> 12; Active = true; }
    public void Destroy() => Active = false;

    public void Display(CompassView view, SpriteAnimationFrame frame, ICompassDrawHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!Active) return;
        FixedVector3 direction = (TargetPosition - view.CameraPosition) >> 6;
        uint distance = unchecked((uint)(FixedVectorMath.OriginalSquareRoot(unchecked(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z)) << 6));
        direction = FixedRotation.Transform(view.ViewRotation, direction) with { Y = 0 };
        direction = FixedVectorMath.Normalize(direction);
        short angle = unchecked((short)GameAngle.Atan2(direction.Z, direction.X));
        (short sinA, short cosA) = GameAngle.Heading(angle - 1024);
        (short sinB, short cosB) = GameAngle.Heading(angle + 1024);
        int tipX = unchecked(25 * direction.X) >> 12;
        int tailX = unchecked(6 * direction.X) >> 12;
        int sideA = (9 * cosA) >> 12, sideB = (9 * cosB) >> 12;
        int tipY = unchecked(view.PanelScreenY + unchecked(3604 * (unchecked(320 * (unchecked(25 * direction.Z) >> 12)) / 512)) / 4096);
        int tailA = unchecked(view.PanelScreenY + 320 * ((9 * sinA) >> 12) / 512);
        int tailB = unchecked(view.PanelScreenY + 320 * ((9 * sinB) >> 12) / 512);
        int tailY = unchecked(view.PanelScreenY + unchecked(320 * (unchecked(6 * direction.Z) >> 12)) / 512);
        int pulseWidth = distance <= 0x4000 ? (int)(((0x4000 - distance) >> 8) + 8) : 8;
        (short pulse, _) = GameAngle.Heading(unchecked(pulseWidth * (short)view.Clock));
        int brightness = (255 * Math.Abs((int)pulse)) >> 12;
        // Original PC quad call order differs from the preceding unused PSX triangle buffer.
        host.EmitQuad(new(false, Point(sideA + 432, 199 - tailA), Point(tailX + 432, 199 - tailY),
            Point(tipX + 432, 199 - tipY), Point(sideB + 432, 199 - tailB), unchecked(((uint)brightness | 0xFFFFFF00) << 8), .2f, 0, 0));
        if (!host.TryAllocateNeedle(0)) return;
        uint tint = 0x323280;
        if (FlashTimer != 0)
        {
            (short wave, _) = GameAngle.Heading(unchecked((int)view.Clock << 6));
            int sign = unchecked(205 * wave) >> 31;
            tint = unchecked((uint)((Math.Abs((127 * wave) >> 12) + 128) |
                (((sign ^ ((205 * wave) >> 12)) + 0xFFFFFF * sign + 50) << 8)));
            FlashTimer = unchecked(FlashTimer - 2);
            if (FlashTimer < 0) FlashTimer = 0;
        }
        uint argb = 0xFF000000 | ((tint & 255) << 16) | (tint & 0xFF00) | ((tint >> 16) & 255);
        HudSourceQuad rectangle = HudSpriteLayout.Stretch(frame, 458, unchecked(215 - view.PanelScreenY), 32, 40);
        short halfWidth = unchecked((short)((rectangle.Right - rectangle.Left) / 2));
        DrawHalf(halfWidth, .05f, .95f);
        if (host.TryAllocateNeedle(1)) DrawHalf(-halfWidth, .95f, .05f);
        void DrawHalf(int offset, float left, float right) => host.EmitQuad(new(true,
            Point(unchecked((short)(rectangle.Left + offset)), rectangle.Top), Point(unchecked((short)(rectangle.Right + offset)), rectangle.Top),
            Point(unchecked((short)(rectangle.Left + offset)), rectangle.Bottom), Point(unchecked((short)(rectangle.Right + offset)), rectangle.Bottom), argb, 0, left, right));
        static CompassPoint Point(int x, int y) => new(x, y);
    }
}
