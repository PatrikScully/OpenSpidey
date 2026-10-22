using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>Another live type-317 baddy as WarnOtherLizMans (0x44D520) sees it.</summary>
public readonly record struct LizManWarnCandidate(object Identity, FixedVector3 Position, int Field374);

/// <summary>World and player queries behind CLizMan's fight probes.</summary>
public interface ILizManPerceptionHost : ILizManHost
{
    /// <summary>MechList 0xE2.</summary>
    short PlayerHealth { get; }
    /// <summary>MechList 0x57C.</summary>
    byte PlayerUnavailable { get; }
    /// <summary>CBody::IsDead (0x460700) on MechList.</summary>
    bool PlayerIsDead { get; }
    object AttackIdentity { get; }
    /// <summary>Utils_LineOfSight (0x4E67A0), no hit output, flags 0.</summary>
    bool LineOfSight(FixedVector3 from, FixedVector3 to);
    /// <summary>CBaddy::PathCheck (0x403310) with clearance 55; 0x2A8 bit 0x2000 sets 0x5FBEE0 for the call.</summary>
    int PathCheck(FixedVector3 from, FixedVector3 to);
    /// <summary>CBaddy::AddPointToPath (0x4048F0).</summary>
    bool AddPointToPath(FixedVector3 point, int distanceLimit);
    /// <summary>CBaddy::Baddy_SendSignal (0x403230).</summary>
    void SendSignal();
    /// <summary>BaddyList (0x56E990) members of type 317 other than this one, in list order.</summary>
    IEnumerable<LizManWarnCandidate> OtherLizMen();
    /// <summary>new CMessage(this, target, code, 0) (0x458F10).</summary>
    void SendMessage(object target, int code);
}

/// <summary>CLizMan::DoAISwitchLogic (0x451280), the fight probe (0x44FD40), WarnOtherLizMans (0x44D520) and CBaddy::PlayerIsVisible (0x404650).</summary>
public static class LizManPerception
{
    public static void DoAiSwitchLogic(LizManState state, LizManAttackOwnership ownership, ILizManPerceptionHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(host);
        if (state.State == 25)
        {
            state.Field310 = 0;
            if (!DetermineFightState(state, ownership, host))
            {
                state.State = state.Field1F0 != 0 || state.Field1F4 > 0 ? 2 : 1;
                state.Substate = 0;
            }
        }
        else if (state.State is 1 or 2)
        {
            if (((host.AttackClock + state.ActorPhase) & state.Field380) == 0)
                DetermineFightState(state, ownership, host);
        }

        if (unchecked((uint)(host.TotalTime - state.Field374)) < 0x12C &&
            (unchecked((byte)(host.AttackClock + state.ActorPhase)) & 0x1F) == 0)
        {
            WarnOtherLizMans(state, host);
            PlayerIsVisible(state, host);
        }
    }

    public static bool DetermineFightState(LizManState state, LizManAttackOwnership ownership, ILizManPerceptionHost host)
    {
        if (state.Health <= 0 || state.Field390 != 0 || host.PlayerHealth <= 0 || host.PlayerUnavailable != 0)
            return false;
        int previous = state.State;
        int path = -1;
        int distance = DistanceToPlayer(state, host, 3);
        state.Field380 = distance > 2000 ? 31 : distance > 1000 ? 15 : 3;
        FixedVector3 position = host.Body.Position;
        if (!host.LineOfSight(position, host.PlayerPosition))
        {
            if ((state.CombatFlags & 0x800) != 0)
            {
                state.CombatFlags &= ~0x800u;
                if (unchecked((uint)LizManJumpScan.ApproximateDistance(position, state.LastSeenPlayerPosition)) < 0x5DC &&
                    host.PathCheck(position, state.LastSeenPlayerPosition) == 0 && host.AddPointToPath(position, 0))
                    host.AddPointToPath(state.LastSeenPlayerPosition, 0);
            }
            return false;
        }

        int heightGap = Abs(unchecked(host.Body.Position.Y - host.PlayerPosition.Y));
        if (unchecked((uint)(host.TotalTime - state.Field374)) < 0x12C && heightGap < 204800 && distance < 1500)
        {
            path = host.PathCheck(position, host.PlayerPosition);
            if (path == 0)
            {
                Enter(state, 3);
                return Finish(state, host, previous);
            }
        }

        bool cone = CheckSightCone(state, host);
        if (cone && Abs(unchecked(host.Body.Position.Y - host.PlayerPosition.Y)) < 204800 &&
            (path != -1 ? path : host.PathCheck(position, host.PlayerPosition)) == 0)
        {
            Enter(state, 3);
            return Finish(state, host, previous);
        }

        if (host.PlayerUnavailable != 0 || ownership.Shooter is not null || DistanceToPlayer(state, host, 2) <= 500 ||
            ((state.Field39C & 1) == 0 && ownership.GlobalLizMan is not null) ||
            unchecked((uint)(host.TotalTime - state.Field374)) >= 0x12C)
        {
            if (cone) state.Field374 = host.TotalTime;
        }
        else
        {
            host.Neutralize();
            ownership.Shooter = host.AttackIdentity;
            Enter(state, 7);
        }
        return Finish(state, host, previous);
    }

    /// <summary>CBaddy::PlayerIsVisible (0x404650), CLizMan vtable slot 5.</summary>
    public static bool PlayerIsVisible(LizManState state, ILizManPerceptionHost host)
    {
        if (host.PlayerIsDead || !host.LineOfSight(host.Body.Position, host.PlayerPosition)) return false;
        if (host.PathCheck(host.Body.Position, host.PlayerPosition) == 0)
        {
            state.LastSeenPlayerPosition = host.PlayerPosition;
            state.CombatFlags |= 0x800;
        }
        return true;
    }

    public static void WarnOtherLizMans(LizManState state, ILizManPerceptionHost host)
    {
        FixedVector3 self = host.Body.Position;
        foreach (LizManWarnCandidate other in host.OtherLizMen())
        {
            if (unchecked((uint)(host.TotalTime - other.Field374)) < 0x12C) continue;
            if (unchecked((uint)ThugPerception.ApproximateHorizontalDistance(self, other.Position)) >= 0x800) continue;
            if ((Abs(unchecked(self.Y - other.Position.Y)) & unchecked((int)0xFFFFF000)) >= 0x100000) continue;
            if (!host.LineOfSight(other.Position, self)) continue;
            host.SendMessage(other.Identity, 7);
        }
    }

    /// <summary>CBaddy::DistanceToPlayer (0x404170) with its 0x204/0x208 gAttackRelated cache.</summary>
    public static int DistanceToPlayer(LizManState state, ILizManPerceptionHost host, int tolerance)
    {
        if (state.DistanceSampleClock != 0 && unchecked(host.AttackClock - state.DistanceSampleClock) <= tolerance)
            return state.CachedPlayerDistance;
        state.DistanceSampleClock = host.AttackClock;
        state.CachedPlayerDistance = ThugPerception.ApproximateHorizontalDistance(host.Body.Position, host.PlayerPosition);
        return state.CachedPlayerDistance;
    }

    /// <summary>CBaddy::CheckSightCone (0x404510) called with cone 2047, vertical 5000, far 1500, near 100.</summary>
    private static bool CheckSightCone(LizManState state, ILizManPerceptionHost host)
    {
        if (Abs(unchecked(host.PlayerPosition.Y - host.Body.Position.Y)) > 5000 << 12) return false;
        int distance = DistanceToPlayer(state, host, 2);
        if (distance < 100) return true;
        if (distance > 1500) return false;
        int delta = FixedAim.Calculate(host.Body.Position, host.PlayerPosition).Yaw - host.Body.Angles.Y;
        if (delta < -2048) delta += 4096;
        else if (delta > 2048) delta -= 4096;
        return Abs(delta) <= 2047 >> 1;
    }

    private static bool Finish(LizManState state, ILizManPerceptionHost host, int previous)
    {
        if (previous == state.State) return false;
        host.SendSignal();
        return true;
    }

    private static void Enter(LizManState state, int next)
    {
        state.State = next;
        state.Substate = 0;
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
