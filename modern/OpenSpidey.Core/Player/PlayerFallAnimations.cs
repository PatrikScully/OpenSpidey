namespace OpenSpidey.Core.Player;

public readonly record struct PlayerFallAnimationPlan(bool ResetReference, ushort? Next, bool ClearWebRelease);

/// <summary>Recovered fall, flail and web-release animation selection.</summary>
public static class PlayerFallAnimations
{
    public static PlayerFallAnimationPlan Resolve(ushort clip, int previousVelocity, int velocity, bool pendingWebRelease)
    {
        bool changedDirection = ((uint)(previousVelocity ^ velocity) & 0x80000000) != 0;
        if (!changedDirection && MaintainsFall(clip) && !(pendingWebRelease && velocity > 0)) return default;
        if (clip is 175 or 176) return new(true, null, false);
        if (pendingWebRelease) return new(true, (ushort)(clip == 280 ? 278 : 276), true);
        ushort? next = clip switch { 231 => 232, 224 => 225, 218 => 219, 221 => 222, 215 => 216,
            232 or 216 => null, _ => 212 };
        return new(true, next, false);
    }

    private static bool MaintainsFall(ushort clip) => clip is 211 or 212 or 224 or 225 or 231 or 232 or 226 or 228 or
        233 or 235 or 175 or 176 or 127 or 238 or 241 or 240 or 239 or 215 or 216 or 221 or 218 or 222 or 219 or 278 or 276;
}
