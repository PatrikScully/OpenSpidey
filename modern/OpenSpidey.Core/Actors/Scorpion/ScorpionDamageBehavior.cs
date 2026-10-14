using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

public interface IScorpionDamageHost
{
    int Difficulty { get; }
    uint PlayerFlags { get; }
    FixedVector3 PlayerPosition { get; }
    object? MakePlayerHandle();
    object? FindJonah();
    void TrySendJonahMessage(object jonah, int code);
    bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority);
    void AttachPlayerSpeech(int bank, int slot);
    void PlayPositionSound(uint sound, FixedVector3 position, int pitch);
    void RunAnimation(ushort clip, int from, int to);
    int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit);
    // Attach through the existing procedure owner. A rejected original heap allocation attaches nothing.
    void TryAttachPlayerLookAt(uint completionMask, int rate, int tolerance);
    void ReportUnknownRecoverySubstate(int substate);
}

/// <summary>Whole shipped Scorpion hit, path-adjusted slide and damage-recovery rules.</summary>
public static class ScorpionDamageBehavior
{
    public static bool Hit(ScorpionDamageState state, IScorpionDamageHost host, ref ActorHit hit, Func<int, int> random)
    {
        short previousHealth = state.Body.Health;
        if (previousHealth < 0) return false;
        state.ReactionContext = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        if ((host.PlayerFlags & 0x800000) == 0)
        {
            state.PlayerTargetUpdates = Difficulty(host.Difficulty, 200, 500, 600, 800);
            state.TargetKind = 2;
            state.CurrentTargetHandle = host.MakePlayerHandle();
        }
        state.HitFlashUpdates = 30;
        if (state.ReactionContext == 23)
        {
            hit = hit with { Damage = unchecked((ushort)((40 * hit.Damage * Difficulty(host.Difficulty, 200, 100, 80, 60)) >> 12)) };
            if ((host.PlayerFlags & 0x800080) == 0 && random(4) != 0 && state.FireQuipSuppressionUpdates == 0)
            {
                int slot = random(3) + 11;
                if (slot == state.LastFireQuip && ++slot >= 14) slot = 11;
                state.LastFireQuip = slot;
                if (host.PlayPositionSpeech(14, slot, host.PlayerPosition, 0)) host.AttachPlayerSpeech(14, slot);
            }
        }
        else if (state.ReactionContext == 6)
            hit = hit with { Damage = unchecked((ushort)((1000 * hit.Damage) >> 12)) };

        if ((hit.Flags & 4) != 0)
        {
            state.Body.Health = unchecked((short)(state.Body.Health - hit.Damage));
            if (state.Body.Health <= 0) { Die(state); return true; }
        }
        host.PlayPositionSound(unchecked((uint)((random(3) + 164) | 0x8000)), state.Actor.Body.Position, 0);
        if (state.Body.Health <= 0) { Die(state); return true; }

        int threshold = Difficulty(host.Difficulty, 450, 450, 450, 450);
        if (previousHealth >= threshold && state.Body.Health < threshold)
        {
            object? jonah = FindJonah(state, host);
            if (jonah is not null) host.TrySendJonahMessage(jonah, 8);
        }
        if ((state.StateFlags.Check(state.Body.State) & 8) != 0 || (state.ReactionFlags & 1) != 0) return true;
        host.RunAnimation(unchecked((byte)state.Body.IdleAnimations), 0, -1);
        state.Body.State = 9;
        state.Body.Substate = 0;
        state.Actor.Neutralize(state.Procedures);
        if ((hit.Flags & 0x18) != 0)
        {
            int distance = hit.SlideDistance;
            int frames = hit.SlideFrames;
            if (distance > 128)
            {
                distance = (2252 * distance) >> 12;
                frames = ((2252 * frames) >> 12) + 1;
            }
            SlideFromHit(state, host, distance, frames, hit.Direction with { Y = 0 });
        }
        else if (state.ReactionContext is 2 or 23)
        {
            FixedVector3 delta = (state.Actor.Body.Position - host.PlayerPosition) with { Y = 0 };
            FixedVector3 direction = FixedVectorMath.Normalize(delta >> 12) * 10;
            SlideFromHit(state, host, 10, 10, direction);
        }
        return true;
    }

    public static void SlideFromHit(ScorpionDamageState state, IScorpionDamageHost host, int distance, int frames, FixedVector3 direction)
    {
        FixedVector3 displacement = direction * distance;
        FixedVector3 position = state.Actor.Body.Position;
        int path = PathCheck(state, host, position, position + displacement, 55, out FixedVector3 contact);
        if (path == 0)
        {
            // The original divides here even for zero frames; preserve that invalid-input failure.
            int step = distance / frames;
            state.SlideUpdates = frames;
            state.Actor.Body.Velocity = (direction * step) with { Y = 0 };
            BeginSlide(state);
        }
        else if (path == 2 && (direction.X | direction.Z) != 0)
        {
            FixedVector3 delta = contact - state.Actor.Body.Position;
            state.SlideUpdates = SourceAbs(direction.X) > SourceAbs(direction.Z)
                ? unchecked(frames * delta.X) / displacement.X
                : unchecked(frames * delta.Z) / displacement.Z;
            if (state.SlideUpdates > 2)
            {
                state.Actor.Body.Velocity = (delta / state.SlideUpdates) with { Y = 0 };
                state.SlideUpdates = unchecked(state.SlideUpdates - 1);
                BeginSlide(state);
            }
        }
    }

    public static int PathCheck(ScorpionDamageState state, IScorpionDamageHost host, FixedVector3 start,
        FixedVector3 end, int clearance, out FixedVector3 hit)
    {
        int y = unchecked(state.Actor.Body.Position.Y + ((state.Body.SurfaceClearance - 20) << 12));
        start = start with { Y = y };
        end = end with { Y = y };
        FindJonah(state, host);
        return host.PathCheckWithHit(start, end, clearance, out hit);
    }

    public static void TakeHit(ScorpionDamageState state, IScorpionDamageHost host, int elapsedTicks)
    {
        switch (state.Body.Substate)
        {
            case 0:
                state.Field310 = 0;
                host.TryAttachPlayerLookAt(2, 80, 200);
                state.Body.WaitUpdates = Difficulty(host.Difficulty, 40, 30, 21, 21);
                state.Body.Substate = 4;
                if (state.Body.Animation.ClipId != 10) host.RunAnimation(10, 0, -1);
                break;
            case 4:
                state.Body.WaitUpdates = Math.Max(0, unchecked(state.Body.WaitUpdates - elapsedTicks));
                if (state.Body.WaitUpdates == 0)
                {
                    state.Body.State = 2;
                    state.Body.Substate = 0;
                }
                break;
            default:
                host.ReportUnknownRecoverySubstate(state.Body.Substate);
                break;
        }
    }

    private static object? FindJonah(ScorpionDamageState state, IScorpionDamageHost host) =>
        state.CachedJonah ??= host.FindJonah();
    private static void Die(ScorpionDamageState state) { state.Body.State = 21; state.Body.Substate = 0; }
    private static void BeginSlide(ScorpionDamageState state) { state.Body.State = 19; state.Body.Substate = 0; }
    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
    private static int Difficulty(int level, int easy, int normal, int hard, int alternative) =>
        level switch { 0 => easy, 1 => normal, 2 => hard, _ => alternative };
}
