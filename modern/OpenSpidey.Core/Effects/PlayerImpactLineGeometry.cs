namespace OpenSpidey.Core.Effects;

public readonly record struct PlayerImpactLineCorner(float X, float Y, bool Head);

/// <summary>PCGfx_DrawLine's actual two-pixel square-ended quad, including its zero-length fallback.</summary>
public static class PlayerImpactLineGeometry
{
    public static PlayerImpactLineCorner[] Build(float headX, float headY, float tailX, float tailY)
    {
        float dx = tailX - headX, dy = tailY - headY;
        float length = (float)Math.Sqrt(dx * dx + dy * dy);
        float offX, offY;
        if (length != 0) { float scale = 1 / length; offX = scale * dx; offY = scale * dy; }
        else { offX = 0; offY = 1; }
        float firstX = headX - offX, firstY = headY - offY;
        float secondX = tailX + offX, secondY = tailY + offY;
        return [new(firstX + offY, firstY - offX, true), new(secondX + offY, secondY - offX, false),
            new(secondX - offY, secondY + offX, false), new(firstX - offY, firstY + offX, true)];
    }
}
