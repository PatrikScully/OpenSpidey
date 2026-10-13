using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Core.UI;

public readonly record struct PlayerLookaroundReticleView(byte Type, FixedVector3 TargetPosition, uint Colour);
public readonly record struct PlayerLookaroundReticleQuad(HudSourceQuad Rectangle, uint Argb, uint Scale);

/// <summary>RenderLookaroundReticle/DrawReticle's original whole-world projection input and unsigned-scale sprite geometry.</summary>
public static class PlayerLookaroundReticle
{
    public static FixedVector3 ProjectionVector(FixedVector3 target, FixedVector3 cameraWhole) => (target >> 12) - cameraWhole;

    public static PlayerLookaroundReticleQuad? Build(PlayerLookaroundReticleView view, SpriteAnimationFrame frame,
        ushort screenX, ushort screenY, int depth, bool quadAvailable = true)
    {
        if (view.Type == 0 || !quadAvailable) return null;
        int distanceScale = unchecked(3072 - depth);
        uint scale = unchecked((uint)Math.Max(distanceScale, 768));
        int left = unchecked((int)((scale * (uint)((frame.OffsetX << 9) / 320)) >> 12) + screenX);
        int top = unchecked((int)((scale * (uint)frame.OffsetY) >> 12) + screenY);
        int right = unchecked((int)((scale * (uint)((frame.Width << 9) / 320)) >> 12) + left);
        int bottom = unchecked((int)((scale * frame.Height) >> 12) + top);
        uint argb = 0xB0000000 | ((view.Colour & 255) << 16) | (view.Colour & 0xFF00) | ((view.Colour >> 16) & 255);
        return new(new(unchecked((short)left), unchecked((short)top), unchecked((short)right), unchecked((short)bottom)), argb, scale);
    }
}
