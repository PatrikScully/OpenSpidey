using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Services consumed by the shipped Scorpion idle decision and its two movement helpers.</summary>
public interface IScorpionIdleHost : IScorpionTargetHost
{
    bool PlayerIsCrawling { get; } // CPlayer0xAD4.
    void DetermineTarget();
    void CycleAnimation(ushort clip, sbyte direction);
    FixedVector3 JonahPosition(object jonah);
    ushort JonahRadius(object jonah);
    void StorePathDestination(FixedVector3 point); // CBaddy0x1B4 point1 with0x1F0 count1.
}

/// <summary>Whole PC486980 idle decision, with487E20 approach-around and486800 stuck-circle movement.</summary>
public static class ScorpionIdle
{
    private const int TargetRange = 0x226;

    public static void Step(ScorpionTargetState state, IScorpionIdleHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ScorpionDamageState damage = state.Brain.Damage;
        ScorpionState body = damage.Body;
        object? target = damage.TargetKind == 0 ? null : host.RecoverHandle(ref state.CurrentTarget);
        if (target is null) { host.DetermineTarget(); return; }
        FixedVector3 position = damage.Actor.Body.Position;
        if (damage.TargetKind == 2 && host.PlayerIsCrawling && host.LineOfSight(position, host.PlayerPosition, 0, 0))
        { Enter(body, 6); return; }
        switch (body.Substate)
        {
            case 0:
            {
                damage.Actor.Neutralize(damage.Procedures);
                FixedVector3 targetPosition = host.BodyPosition(target);
                if (unchecked((uint)ThugPerception.ApproximateHorizontalDistance(damage.Actor.Body.Position, targetPosition)) < TargetRange)
                { Enter(body, 5); return; }
                position = damage.Actor.Body.Position;
                if ((SourceAbs(unchecked(position.X - targetPosition.X)) >= 0xBE000 || SourceAbs(unchecked(position.Z - targetPosition.Z)) >= 0xBE000) &&
                    host.LineOfSight(position, targetPosition, 0, 1))
                {
                    int path = ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, targetPosition, 20, out FixedVector3 hit);
                    if (path == 0 || path == 2 && unchecked((uint)ThugPerception.ApproximateHorizontalDistance(hit, targetPosition)) < TargetRange)
                    { Enter(body, 3); return; }
                }
                host.CycleAnimation(0, 1);
                damage.SlideUpdates = 0;
                body.Substate = unchecked(body.Substate + 1);
                break;
            }
            case 1:
            {
                FixedVector3 targetPosition = host.BodyPosition(target);
                if (damage.TargetKind == 2 &&
                    ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, targetPosition, 20, out _) == 0)
                { Enter(body, 3); return; }
                int previous = damage.SlideUpdates;
                damage.SlideUpdates = unchecked(previous + 1);
                if (previous > 16) body.Substate = unchecked(body.Substate + 1);
                else ApproachAround(state, host, host.BodyPosition(target), method: 0);
                break;
            }
            case 2:
                ApproachAround(state, host, host.BodyPosition(target), method: 1);
                damage.SlideUpdates = unchecked(damage.SlideUpdates + 1);
                if (damage.SlideUpdates > 40) body.Substate = unchecked(body.Substate + 1);
                break;
            case 3:
                CircleWhenStuck(state, host);
                if (damage.SlideUpdates >= 666) host.SourceDiagnostic("Stuck goddammit.");
                break;
            default:
                host.SourceDiagnostic("Unknown substate.");
                break;
        }
    }

    /// <summary>PC487E20: method0 tries the phased waypoint near the target, method1 a525-unit circle point.</summary>
    public static bool ApproachAround(ScorpionTargetState state, IScorpionIdleHost host, FixedVector3 target, int method)
    {
        ScorpionDamageState damage = state.Brain.Damage;
        ScorpionState body = damage.Body;
        FixedVector3 candidate = default;
        if (method == 0)
        {
            candidate = ActorPathController.GetWaypointNearTarget(target, 368640, body.ActorPhase);
            body.ActorPhase = unchecked((byte)(body.ActorPhase + 1));
        }
        else if (method == 1)
        {
            FixedVector3 position = damage.Actor.Body.Position;
            candidate = CirclePoint(position, damage.SlideUpdates);
        }
        else host.SourceDiagnostic("Unknown method.");
        int path = ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, candidate, 20, out FixedVector3 hit);
        if (path == 2) candidate = hit;
        else if (path != 0) return false;
        path = ScorpionDamageBehavior.PathCheck(damage, host, candidate, target, 20, out hit);
        if (path != 0 && (path != 2 || unchecked((uint)CrapDistance(target, hit)) >= TargetRange)) return false;
        host.StorePathDestination(candidate with { Y = state.Brain.StandingY });
        Enter(body, 1);
        return true;
    }

    /// <summary>PC486800: advances the shared0x1F8 counter and picks a clear point on a525-unit circle.</summary>
    public static void CircleWhenStuck(ScorpionTargetState state, IScorpionIdleHost host)
    {
        ScorpionDamageState damage = state.Brain.Damage;
        damage.SlideUpdates = unchecked(damage.SlideUpdates + 1);
        damage.ReactionFlags = 8;
        FixedVector3 position = damage.Actor.Body.Position;
        FixedVector3 candidate = CirclePoint(position, damage.SlideUpdates);
        object? jonah = damage.CachedJonah ??= host.FindJonah();
        if (jonah is not null && unchecked((uint)ThugPerception.ApproximateHorizontalDistance(candidate, host.JonahPosition(jonah))) <
            unchecked((uint)(damage.Body.MinorRadius + host.JonahRadius(jonah)))) return;
        int path = ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, candidate, 55, out FixedVector3 hit);
        if (path == 0)
        {
            host.StorePathDestination(candidate);
            Enter(damage.Body, 1);
        }
        else if (path == 2 && unchecked((uint)FixedVectorMath.OriginalDistance(damage.Actor.Body.Position, hit)) > 100)
        {
            host.StorePathDestination(hit with { Y = state.Brain.StandingY });
            Enter(damage.Body, 1);
        }
    }

    private static FixedVector3 CirclePoint(FixedVector3 position, int counter)
    {
        var heading = GameAngle.Heading(200 * unchecked((ushort)counter));
        return new(unchecked(position.X + 525 * heading.Cosine), position.Y, unchecked(position.Z + 525 * heading.Sine));
    }

    /// <summary>Utils_CrapDist4E6220: three-axis largest + middle/2 + smallest/4.</summary>
    private static int CrapDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = SourceAbs(unchecked(first.X - second.X));
        int middle = SourceAbs(unchecked(first.Y - second.Y));
        int smallest = SourceAbs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
    }

    private static void Enter(ScorpionState body, int state) { body.State = state; body.Substate = 0; }
    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
