using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered CPlayer::Hit eligibility, damage, interrupted effects and reaction physics.</summary>
public static class PlayerHitRules
{
    /// <remarks>A rejected hit can still stop its looping sound and adjust the camera before the late action-state gate.</remarks>
    public static bool Apply(PlayerCombatState state, IPlayerHitHost host, ActorHit hit, Func<int, int> random)
    {
        if (host.CurrentSuit == 4 || host.SymbioteRelated != 0 || state.FieldE18 != 0 ||
            state.SynthesizedInputActive || state.Field36C != 0) return false;
        uint action = state.ActionState;
        if ((action & 0x20000080) != 0 || ((action & 0x800000) != 0 && state.AnimationId != 285)) return false;
        bool directed = (hit.Flags & 2) != 0;
        if (directed && hit.AttackKind == 10 && action == 2048)
        {
            host.PlayPositionSound(17, state.Position, 0);
            return false;
        }
        if (state.LoopingSoundHandle != 0)
        {
            host.StopSound(state.LoopingSoundHandle);
            state.LoopingSoundHandle = 0;
        }
        if (host.CameraFlag180 && !state.IsCrawling) host.PutCameraBehind(0);
        action = state.ActionState;
        if ((action & 0x10000000) != 0 && (!directed || hit.AttackKind != 17)) return false;
        state.Field534 = 240;
        state.Field52C = unchecked((state.Field528 + 11) << 10);
        if (state.FirstSmokeTrailActive)
        {
            host.FadeSmokeTrail(0);
            state.FirstSmokeTrailActive = false;
        }
        if (state.SecondSmokeTrailActive)
        {
            host.FadeSmokeTrail(1);
            state.SecondSmokeTrailActive = false;
        }
        state.LastHitAction = action;
        state.LastHitClock = host.Timer;
        if (state.HeldObjectPresent)
        {
            state.HeldObjectPresent = false;
            host.SmashHeldObject();
        }
        if ((hit.Flags & 4) != 0)
        {
            if (state.ArmorActive)
            {
                int left = unchecked(state.ArmorAmount - hit.Damage);
                state.ArmorAmount = left;
                if (left < 0)
                {
                    state.Health = unchecked((short)(state.Health + (short)left));
                    state.ArmorAnimationVariant = 0;
                    if (state.ArmorTexturesActive)
                    {
                        host.SetArmorTextures(false);
                        state.ArmorActive = false;
                        state.ArmorTexturesActive = false;
                    }
                    state.ArmorAmount = 0;
                    state.SavedArmorAmount = 0;
                    state.SavedArmorActive = 0;
                    host.SaveArmor(0, 0);
                }
            }
            else state.Health = unchecked((short)(state.Health - hit.Damage));
            if (directed && hit.AttackKind == 16) return true;
            if (host.Timer > unchecked(state.PainSoundClock + 30))
            {
                host.PlaySound(unchecked((ushort)(random(3) + 18)), 0x2000, 0);
                state.PainSoundClock = host.Timer;
            }
            if (state.Health <= 0)
            {
                host.StopMyXa();
                if (state.LookaroundActive)
                {
                    host.ExitLookaroundMode();
                    state.LookaroundActive = false;
                }
                ReleaseWeb(state, host);
                PlayerDeathRules.Enter(state, host);
                return true;
            }
        }
        if (directed && hit.AttackKind == 26)
        {
            state.Electrified = 1;
            state.ElectrificationTicks = 120;
            host.Electrify();
        }
        action = state.ActionState;
        if (((action & 0x1000000) != 0 && state.Field8DC == 0) || (action & 0x10043606) != 0) return true;
        Play(state, host, state.IsCrawling ? (ushort)0xB5 : (ushort)0xAA);
        if (directed)
        {
            int type = hit.AttackKind;
            if (!state.IsCrawling && type == 10) Play(state, host, 0xBB);
            else if (type == 8 && (hit.Flags & 8) != 0)
            {
                FixedVector3 velocity = new(unchecked(48 * hit.Direction.X), state.Velocity.Y, unchecked(48 * hit.Direction.Z));
                FixedVector3 destination = state.Position + new FixedVector3(unchecked(velocity.X * state.ElapsedTicks), 0,
                    unchecked(velocity.Z * state.ElapsedTicks));
                if (host.LineOfSight(state.Position, destination, 0, 0)) state.Velocity = velocity;
            }
            else if (type is 9 or 14 or 11 or 15 or 26)
            {
                if (state.Field8E8 != 0 || state.Field8E9 != 0)
                {
                    DetachFromCrawl(state);
                    state.ForwardDirection = FixedVectorMath.Normalize(state.ForwardDirection with { Y = 0 });
                    host.OrientToNormal(true, state.ForwardDirection);
                    Play(state, host, 0xB0);
                }
                else
                {
                    FixedVector3 normal = FixedVectorMath.Normalize(new(hit.Direction.X >> 12, 0, hit.Direction.Z >> 12));
                    host.OrientToNormal(true, normal);
                    Play(state, host, 0xAC);
                    state.HeadingTurnTicksRemaining = 0;
                    if (type == 26) state.Velocity = state.Velocity with
                    { X = unchecked(48 * hit.Direction.X), Z = unchecked(48 * hit.Direction.Z) };
                }
            }
            else if (type == 12)
            {
                if (state.Field8E8 != 0 || state.Field8E9 != 0)
                {
                    DetachFromCrawl(state);
                    host.OrientToNormal(false, default);
                }
                state.Velocity = state.Velocity with { X = 0, Z = 0 };
                Play(state, host, 0xB0);
                state.Velocity = state.Velocity with { Y = -524288 };
            }
        }
        state.ActionState = 0x800000;
        ReleaseWeb(state, host);
        return true;
    }

    internal static void DetachFromCrawl(PlayerCombatState state)
    {
        state.Field8E8 = 0;
        state.Field8E9 = 0;
        state.IsCrawling = false;
        state.CrawlSurfaceNormal = new(0, -4096, 0);
    }
    internal static void Play(PlayerCombatState state, IPlayerHitHost host, ushort clip)
    {
        host.PlaySingleAnimation(clip, 0, -1);
        state.AnimationId = clip;
        state.AnimationFrame = 0;
    }
    internal static void ReleaseWeb(PlayerCombatState state, IPlayerHitHost host)
    {
        if (!state.WebPresent) return;
        host.SwitchWebToBlob();
        state.WebPresent = false;
    }
}
