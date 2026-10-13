using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.UI;

public readonly record struct ScreenTargetPoint(short X, short Y);
public readonly record struct ScreenTargetTriangle(ScreenTargetPoint First, ScreenTargetPoint Second, ScreenTargetPoint Third, uint Argb);

/// <summary>Actual Screen_TargetOn/SetTarget and four-triangle Screen_DrawTarget geometry, before viewport scaling.</summary>
public sealed class ScreenTargetState
{
    public bool Visible { get; set; }
    public FixedVector3 Position { get; private set; }
    public ushort Radius { get; private set; }
    public short Angle { get; private set; }
    public void SetTarget(FixedVector3 position, ushort radius, short angle)
    { Position = position; Radius = radius; Angle = angle; }

    public IReadOnlyList<ScreenTargetTriangle> Build(short screenX, short screenY)
    {
        if (!Visible) return [];
        ScreenTargetTriangle[] triangles = new ScreenTargetTriangle[4];
        int firstAngle = Angle, secondAngle = Angle + 128;
        for (int index = 0; index < 4; index++, firstAngle += 1024, secondAngle += 1024)
            // PCGfx_DrawQuad sends the backward outer corner before the inner tip.
            triangles[index] = new(Point(Radius, secondAngle - 256), Point(Radius - 12, firstAngle), Point(Radius, secondAngle), 0xFFA0A000);
        return triangles;

        ScreenTargetPoint Point(int radius, int angle)
        {
            (short sine, short cosine) = GameAngle.Heading(angle);
            return new(unchecked((short)(screenX + (unchecked(radius * sine) >> 12))),
                unchecked((short)(screenY + unchecked(320 * (unchecked(radius * cosine) >> 12)) / 512)));
        }
    }
}
