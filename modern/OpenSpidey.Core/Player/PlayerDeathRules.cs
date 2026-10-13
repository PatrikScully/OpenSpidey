namespace OpenSpidey.Core.Player;

/// <summary>Recovered SwitchToDeathMode(false), including crawl knockoff and source death animation selection.</summary>
public static class PlayerDeathRules
{
    public static void Enter(PlayerCombatState state, IPlayerHitHost host)
    {
        bool reset = state.Field54C != 0;
        state.Health = 0;
        if (reset)
        {
            state.Field54C = 0;
            state.ActionState = 0x800000;
            Run(state, host, 0xB0);
            if (state.DeathEffectPresent) host.DeleteDeathEffect();
            state.DeathEffectPresent = false;
            host.SetCameraField12C(-1);
            return;
        }
        if (state.IsCrawling && (state.Field8E8 != 0 || state.Field8E9 != 0))
        {
            if (state.LookaroundActive)
            {
                host.ExitLookaroundMode();
                state.LookaroundActive = false;
            }
            state.IsCrawling = false;
            state.ActionState = 0x800000;
            state.CrawlSurfaceNormal = new(0, -4096, 0);
            PlayerHitRules.Play(state, host, 175);
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
            Run(state, host, 0xB0);
            return;
        }
        uint action = state.ActionState;
        if (action is 2 or 4)
        {
            if (state.AnimationId == 0xB0) return;
            Run(state, host, 0xB0);
            state.ActionState = 4;
            return;
        }
        if (action == 0x80) return;
        state.Velocity = default;
        state.ActionState = 0x80;
        if (action is 1 or 8 or 16 or 64 or 0x800000)
        {
            if (state.AnimationId is 0xB0 or 0xB2)
            {
                Run(state, host, 0xB6);
                host.PlayPositionSound(9, state.Position, 0);
                host.PlayPositionSound(0x24, state.Position, 0);
                return;
            }
        }
        Run(state, host, 0xAB);
        host.PlayPositionSound(0x24, state.Position, 0);
    }

    private static void Run(PlayerCombatState state, IPlayerHitHost host, ushort clip)
    {
        host.ResetDeathAnimationSounds(clip);
        host.RunAnimation(clip, 0, -1);
        state.AnimationId = clip;
        state.AnimationFrame = 0;
    }
}
