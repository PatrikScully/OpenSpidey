using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Complete shipped488140 target decision,487FC0 intro and487880 environmental candidate selection.</summary>
public static class ScorpionTargetSelection
{
    public static void DetermineTarget(ScorpionTargetState state, IScorpionTargetHost host, Func<int, int> random)
    {
        var brain = state.Brain;
        var damage = brain.Damage;
        if (damage.PlayerTargetUpdates != 0)
        {
            damage.TargetKind = 2;
            state.CurrentTarget = host.MakeHandle(host.PlayerIdentity);
            return;
        }
        bool invalid = state.CurrentTarget.Target is null || host.RecoverHandle(ref state.CurrentTarget) is null;
        if (invalid)
        {
            damage.TargetKind = 2;
            state.CurrentTarget = host.MakeHandle(host.PlayerIdentity);
        }
        object? jonah = FindJonah(state, host);
        uint decision = jonah is null ? 0 : host.CallJonahDecisionModule(jonah);
        switch (decision)
        {
            case 1:
                if (SetJonahHandle(state, host))
                {
                    damage.TargetKind = 1;
                    Enter(state, 2);
                }
                break;
            case 2:
                if (brain.EnvironmentalTargetSuppressionUpdates != 0 || !SelectEnvironment(state, host, random))
                {
                    if (brain.Body.State != 3 || damage.TargetKind != 2)
                    {
                        state.CurrentTarget = host.MakeHandle(host.PlayerIdentity);
                        damage.TargetKind = 2;
                        Enter(state, 3);
                    }
                }
                else Enter(state, 16);
                break;
            default:
                TargetPlayer(state, host, Math.Max(180, damage.PlayerTargetUpdates));
                if (brain.Body.State != 3) Enter(state, 3);
                break;
        }
    }

    public static void Intro(ScorpionTargetState state, IScorpionTargetHost host, int elapsedTicks)
    {
        var brain = state.Brain;
        switch (brain.Body.Substate)
        {
            case 0:
                if (SetJonahHandle(state, host))
                {
                    host.TryAttachTargetLookAt(state.CurrentTarget.Target, 2, 50, 170);
                    brain.Body.Substate = unchecked(brain.Body.Substate + 1);
                }
                else Enter(state, 2);
                break;
            case 1:
                if ((brain.Damage.Actor.CompletionFlags & 2) != 0)
                {
                    brain.Damage.SlideUpdates = 600;
                    brain.Damage.Actor.CompletionFlags &= ~2u;
                    brain.Body.Substate = 2;
                }
                break;
            case 2:
                brain.Damage.SlideUpdates = unchecked(brain.Damage.SlideUpdates - elapsedTicks);
                if (brain.Damage.SlideUpdates <= 0) Enter(state, 2);
                break;
            default: host.SourceDiagnostic("Unknown substate!"); break;
        }
    }

    public static bool SelectEnvironment(ScorpionTargetState state, IScorpionTargetHost host, Func<int, int> random)
    {
        object? jonah = FindJonah(state, host);
        if (jonah is null) return false;
        // The PC copies this pair to its stack. Stale recovery clears only that copy, never Jonah's field.
        PlayerKickHandle hiding = host.JonahHidingPlace(jonah);
        if (hiding.Target is null || host.RecoverHandle(ref hiding) is null) return false;
        var brain = state.Brain;
        brain.Damage.TargetKind = 3;
        object? primary = host.RecoverHandle(ref hiding);
        if (primary is null) throw new InvalidOperationException("The source primary environmental handle became invalid between its two recoveries.");
        bool keepPrimary = false;
        if ((brain.SignalFlags & 0x10) != 0 || random(2) != 0) keepPrimary = HasPath(primary);
        if (!keepPrimary)
        {
            List<object> candidates = [];
            for (object? current = host.FirstEnvironment; current is not null && candidates.Count < 4; current = host.NextEnvironment(current))
            {
                if (!ReferenceEquals(current, primary) && host.BodyType(current) == 401 && HasPath(current)) candidates.Add(current);
            }
            if (candidates.Count != 0)
            {
                object selected = candidates[random(candidates.Count)];
                state.CurrentTarget = host.MakeHandle(selected);
                // The original reads both live positions after Mem_MakeHandle; keep callback-visible order.
                state.EnvironmentAimPosition = (host.BodyPosition(selected) + host.BodyPosition(primary)) >> 1;
                brain.SignalFlags = (brain.SignalFlags & ~0x30u) | 0x10;
                return true;
            }
        }
        state.EnvironmentAimPosition = host.BodyPosition(primary);
        state.CurrentTarget = hiding;
        brain.SignalFlags |= 0x20;
        return true;

        bool HasPath(object target)
        {
            FixedVector3 position = brain.Damage.Actor.Body.Position;
            FixedVector3 targetPosition = host.BodyPosition(target);
            int dx = SourceAbs(unchecked(position.X - targetPosition.X)), dz = SourceAbs(unchecked(position.Z - targetPosition.Z));
            if ((dx < 0xBE000 && dz < 0xBE000) || !host.LineOfSight(position, targetPosition, 0, 1)) return false;
            int path = ScorpionDamageBehavior.PathCheck(brain.Damage, host, brain.Damage.Actor.Body.Position,
                host.BodyPosition(target), 20, out FixedVector3 contact);
            return path == 0 || path == 2 &&
                unchecked((uint)ThugPerception.ApproximateHorizontalDistance(contact, host.BodyPosition(target))) < 0x226;
        }
    }

    private static object? FindJonah(ScorpionTargetState state, IScorpionTargetHost host) =>
        state.Brain.Damage.CachedJonah ??= host.FindJonah();
    private static bool SetJonahHandle(ScorpionTargetState state, IScorpionTargetHost host)
    {
        object? jonah = FindJonah(state, host);
        if (jonah is null) return false;
        state.CurrentTarget = host.MakeHandle(jonah);
        return true;
    }
    private static void TargetPlayer(ScorpionTargetState state, IScorpionTargetHost host, int updates)
    {
        state.Brain.Damage.PlayerTargetUpdates = updates;
        state.Brain.Damage.TargetKind = 2;
        state.CurrentTarget = host.MakeHandle(host.PlayerIdentity);
    }
    private static void Enter(ScorpionTargetState state, int behavior) { state.Brain.Body.State = behavior; state.Brain.Body.Substate = 0; }
    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
