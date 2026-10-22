namespace OpenSpidey.Core.Player;

/// <summary>CPlayer::KnockSpideyFromCrawlPosition (0x4BB710), used by CRhino::StompGround.</summary>
public static class PlayerCrawlKnockoff
{
    public static bool Apply(PlayerCombatState state, IPlayerHitHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (!state.IsCrawling || (state.Field8E8 == 0 && state.Field8E9 == 0)) return false;
        if (state.LookaroundActive)
        {
            host.ExitLookaroundMode();
            state.LookaroundActive = false;
        }
        state.IsCrawling = false;
        state.ActionState = 0x800000;
        state.CrawlSurfaceNormal = new(0, -4096, 0);
        // field_350 = the anim-175 frame-sound list (dword_6A8574), then a plain RunAnim(175).
        host.ResetDeathAnimationSounds(175);
        host.RunAnimation(175, 0, -1);
        state.AnimationId = 175;
        state.AnimationFrame = 0;
        state.FieldAE5 = 1;
        if (state.Field8E8 != 0)
        {
            host.OrientToNormal(true, state.UpDirection);
            state.Field8E8 = 0;
        }
        else
        {
            host.OrientToNormal(true, state.ForwardDirection);
            state.Field8E9 = 0;
        }
        return true;
    }
}
