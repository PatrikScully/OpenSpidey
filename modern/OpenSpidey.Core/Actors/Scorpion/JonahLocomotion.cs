using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>PC Jonah FollowWaypoints, Arrival, next-waypoint selection, hiding, hit recovery and death.</summary>
public static class JonahLocomotion
{
    public static void FollowWaypoints(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        JonahBrainState brain = state.Brain;
        JonahState jonah = state.Jonah;
        switch (jonah.Substate)
        {
            case 0:
                brain.Neutralize();
                if (AnimationMode(jonah) != 0 && (jonah.SignalFlags & 0x80) == 0)
                {
                    jonah.SignalFlags &= ~0x40u;
                    host.RunAnimation(9, 0, -1);
                    jonah.Substate = 10;
                    return;
                }
                if (jonah.State != 7 && (jonah.SignalFlags & 2) != 0)
                {
                    jonah.SignalFlags &= ~0x80u;
                    brain.EnterState(7);
                    return;
                }
                jonah.SignalFlags &= ~1u;
                state.MovementRestart = 0;
                FixedVector3 destination;
                if (host.PathPointCount != 0)
                {
                    destination = host.PathPoint(host.PathPointCount);
                    jonah.BehaviorFlags &= ~0x10000000u;
                }
                else
                {
                    jonah.BehaviorFlags |= 0x10000000;
                    destination = host.WaypointPosition(jonah.CurrentWaypoint);
                }
                if ((jonah.SignalFlags & 0x2000) == 0)
                {
                    object? scorpion = FindScorpion(brain, host);
                    if (scorpion is not null && host.ScorpionBlocksPath(brain.Position, destination, scorpion))
                    {
                        brain.EnterState(4);
                        return;
                    }
                }
                bool pleaMode = AnimationMode(jonah) == 1;
                host.CycleAnimation(unchecked((byte)(jonah.MovementAnimations >> 8)), 1);
                host.TryMoveToPoint(destination, pleaMode ? 80 : 160, pleaMode ? 100 : 140, pleaMode ? 568 : 910, 1);
                jonah.Substate++;
                break;
            case 1:
                if ((brain.Actor.CompletionFlags & 1) != 0)
                {
                    brain.Actor.CompletionFlags &= ~1u;
                    jonah.SignalFlags &= ~0x2000u;
                    jonah.Substate = 2;
                    if (host.PathPointCount == 0)
                    {
                        if (host.PlayerHealth <= 0) { brain.EnterState(4); return; }
                        host.ExecuteWaypointScript(jonah.CurrentWaypoint);
                        if (Arrival(state, host)) return;
                    }
                    ContinuePath(state, host);
                }
                else if ((jonah.SignalFlags & 0x2000) == 0)
                {
                    object? scorpion = FindScorpion(brain, host);
                    // The PC's per-visit local destination is zero in this substate.
                    if (scorpion is not null && host.ScorpionBlocksPath(brain.Position, default, scorpion))
                    {
                        brain.Neutralize();
                        brain.EnterState(4);
                    }
                }
                break;
            case 2: ContinuePath(state, host); break;
            case 5:
                if (jonah.Animation.IsFinished) jonah.Substate = 2;
                break;
            case 10:
                if (jonah.Animation.IsFinished)
                {
                    brain.SelectAnimationSet(0x0A0A0A0A, 10);
                    jonah.Substate = 0;
                }
                break;
            default: host.SourceDiagnostic("Unknown Jonah movement substate."); break;
        }
    }

    public static bool Arrival(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        JonahBrainState brain = state.Brain;
        JonahState jonah = state.Jonah;
        switch (state.WaypointRole)
        {
            case 1:
                if (!LinkedHidingPlaceStillExists(state, host, true) || AnimationMode(jonah) == 1) return false;
                jonah.SignalFlags |= 0x80;
                host.RunAnimation(11, 0, -1);
                brain.SelectAnimationSet(0x02020202, 0x00000102);
                jonah.Substate = 5;
                return true;
            case 2:
                bool foundRoom = false;
                foreach (JonahWaypointLink link in host.ReadWaypointLinks(jonah.CurrentWaypoint, 4))
                {
                    if (link.Role != 4) continue;
                    state.HasNextRoomWaypoint = 1;
                    state.NextRoomWaypoint = link.Node;
                    foundRoom = true;
                    break;
                }
                if (!foundRoom) host.SourceDiagnostic("No path to next room.");
                brain.Neutralize();
                bool hidingPlace = LinkedHidingPlaceStillExists(state, host, false);
                if ((jonah.SignalFlags & 0x200) == 0 && hidingPlace)
                {
                    brain.EnterState(5);
                    jonah.SignalFlags &= ~0x20u;
                    return true;
                }
                if ((jonah.SignalFlags & 0x220) != 0)
                {
                    if (state.HasNextRoomWaypoint == 0) host.SourceDiagnostic("Missing next room node.");
                    jonah.SignalFlags = (jonah.SignalFlags & ~0x22u) | 2;
                    if (StartRunningToNextRoom(state, host)) return true;
                }
                jonah.SignalFlags |= 0x20;
                host.RunAnimation(20, 0, -1);
                jonah.Substate = 5;
                return true;
            case 3:
                if ((jonah.SignalFlags & 8) == 0) return false;
                goto case 4;
            case 4:
                jonah.SignalFlags &= ~0x80u;
                if (AnimationMode(jonah) == 0) return false;
                host.RunAnimation(9, 0, -1);
                brain.SelectAnimationSet(0x0A0A0A0A, 10);
                jonah.Substate = 5;
                return true;
            case 6:
                jonah.SignalFlags &= ~0x80u;
                SendBreakableTarget(state, host);
                jonah.SignalFlags = (jonah.SignalFlags & ~0x222u) | 0x400;
                host.PlaySound(0x80B5, brain.Position, 0);
                jonah.SignalFlags |= 0x2000;
                state.ObstructionUpdates = 0;
                return false;
            default: return false;
        }
    }

    public static bool GetNextWaypoint(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        IReadOnlyList<JonahWaypointLink> links = host.ReadWaypointLinks(state.Jonah.CurrentWaypoint, 4);
        if (links.Count == 0) return false;
        if ((state.Jonah.SignalFlags & 1) != 0)
        {
            int count = links.Count(link => link.Role == 4);
            if (count == 0) return false;
            int selection = host.Random(count);
            foreach (JonahWaypointLink link in links)
            {
                if (link.Role != 4 || selection-- != 0) continue;
                state.SelectWaypoint(link.Node, link.Role);
                return true;
            }
            return false;
        }
        if (links.Count == 1)
        {
            state.SelectWaypoint(links[0].Node, links[0].Role);
            return true;
        }
        int eligible = links.Count(IsOrdinaryWaypoint);
        // The original also calls Rnd(0), advancing the source random generator even when selection fails.
        int ordinarySelection = host.Random(eligible);
        foreach (JonahWaypointLink link in links)
        {
            if (!IsOrdinaryWaypoint(link) || ordinarySelection-- != 0) continue;
            state.SelectWaypoint(link.Node, link.Role);
            return true;
        }
        host.SourceDiagnostic("No selectable Jonah waypoint.");
        return false;
    }

    public static bool LinkedHidingPlaceStillExists(JonahLocomotionState state, IJonahLocomotionHost host, bool successor)
    {
        int node = state.Jonah.CurrentWaypoint;
        state.HidingPlace = state.HidingPlace with { Target = null };
        if (successor)
        {
            IReadOnlyList<JonahWaypointLink> links = host.ReadWaypointLinks(node, 4);
            if (links.Count == 0) return false;
            foreach (JonahWaypointLink link in links)
                if (link.Role == 2) { node = link.Node; break; }
        }
        foreach (JonahWaypointLink link in host.ReadWaypointLinks(node, 4))
        {
            if (link.Type is not (1 or 2)) continue;
            object? body = host.FindEnvironmentBody(link.Node);
            if (body is null) continue;
            state.HidingPlace = host.MakeEnvironmentHandle(body);
            return true;
        }
        return false;
    }

    public static bool StartRunningToNextRoom(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        if (state.HasNextRoomWaypoint == 0 || ((state.Jonah.SignalFlags & 0x200) == 0 &&
            (state.ObstructionUpdates < 80 || LinkedHidingPlaceStillExists(state, host, false)))) return false;
        state.SelectWaypoint(state.NextRoomWaypoint, 4);
        state.Brain.EnterState(7);
        state.Jonah.SignalFlags |= 0x102;
        return true;
    }

    public static void TakeHit(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        switch (state.Jonah.Substate)
        {
            case 0:
                object? scorpion = FindScorpion(state.Brain, host);
                if (scorpion is not null) host.TryLookAtScorpion(scorpion, 2, 80, 200);
                state.Jonah.Substate++;
                break;
            case 1:
                if (state.Jonah.Animation.IsFinished)
                {
                    state.HitReactionContext = 0;
                    if (!StartRunningToNextRoom(state, host)) state.Brain.EnterState(1);
                }
                break;
            default: host.SourceDiagnostic("Unknown Jonah hit recovery substate."); break;
        }
    }

    public static void Hide(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        JonahBrainState brain = state.Brain;
        JonahState jonah = state.Jonah;
        bool hidingPlace = LinkedHidingPlaceStillExists(state, host, false);
        switch (jonah.Substate)
        {
            case 0:
                brain.Neutralize();
                brain.WaitTicks = 0;
                jonah.SignalFlags &= ~0x1Cu;
                if (AnimationMode(jonah) == 0)
                { host.RunAnimation(11, 0, -1); jonah.Substate = 5; break; }
                jonah.Substate++;
                goto case 1;
            case 1:
                host.CycleAnimation(8, 1);
                jonah.Substate++;
                break;
            case 2:
                if (hidingPlace && (jonah.SignalFlags & 0x200) == 0)
                {
                    object? body = host.RecoverEnvironmentBody(state.HidingPlace);
                    if (body is null || !ReferenceEquals(body, host.PlayerHeldObject)) break;
                }
                jonah.SignalFlags = (jonah.SignalFlags & 0x200) != 0 ? (jonah.SignalFlags & ~5u) | 1 : jonah.SignalFlags & ~1u;
                bool plead = (jonah.SignalFlags & 4) != 0;
                if (!plead)
                {
                    object? scorpion = FindScorpion(brain, host);
                    plead = scorpion is not null && unchecked((int)ActorLifecycle.Distance(host.ScorpionPosition(scorpion), brain.Position)) < 500;
                }
                if (plead)
                {
                    GetNextWaypoint(state, host);
                    brain.WaitTicks = 100;
                    brain.EnterState(3);
                }
                else if (GetNextWaypoint(state, host)) brain.EnterState(1);
                else jonah.Substate++;
                break;
            case 3: break;
            case 5:
                if (jonah.Animation.IsFinished)
                { brain.SelectAnimationSet(0x02020202, 0x00000102); jonah.Substate = 1; }
                break;
            default: host.SourceDiagnostic("Unknown Jonah hiding substate."); break;
        }
    }

    public static void Die(JonahLocomotionState state, IJonahLocomotionHost host, int elapsedTicks)
    {
        JonahState jonah = state.Jonah;
        if (jonah.Substate == 2)
        {
            if (host.HasCamera) host.SetCameraAngle(unchecked((short)(host.CameraAngle + 32 * elapsedTicks)), 16);
            state.Brain.WaitTicks = unchecked(state.Brain.WaitTicks + elapsedTicks);
            if (state.Brain.WaitTicks >= 180) host.SetLevelStatus(2);
            return;
        }
        if (jonah.Substate == 0) { jonah.MinorRadius = 0; jonah.Substate = 1; }
        else if (jonah.Substate != 1) { host.SourceDiagnostic("Unknown Jonah death substate."); return; }
        if (!jonah.Animation.IsFinished) return;
        host.RunAnimation(15, 0, -1);
        state.Brain.WaitTicks = 0;
        jonah.Substate++;
        host.SuppressPlayerInput(0x8000);
        if (!host.HasCamera) return;
        host.SetCameraMode(3);
        host.TrackJonahPositionAndOrientation(jonah);
        host.SetCameraXOffset(0, 0);
        host.SetCameraYOffset(0, 0);
        host.SetCameraZOffset(0, 0);
        host.SetCameraHorizontalDistance(75, 0);
        host.SetCameraVerticalDistance(-300, 0);
    }

    private static void ContinuePath(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        if (host.PathPointCount != 0)
        {
            state.Jonah.Substate = 0;
            host.PathPointCount--;
        }
        else if (GetNextWaypoint(state, host)) state.Jonah.Substate = 0;
        else
        {
            host.CycleAnimation(unchecked((byte)state.Jonah.IdleAnimations), 1);
            state.Brain.EnterState(2);
        }
    }

    private static void SendBreakableTarget(JonahLocomotionState state, IJonahLocomotionHost host)
    {
        IReadOnlyList<JonahWaypointLink> links = host.ReadWaypointLinks(state.Jonah.CurrentWaypoint, 4);
        if (links.Count == 0) { host.SourceDiagnostic("No linked nodes for Jonah's breakable target."); return; }
        foreach (JonahWaypointLink link in links)
        {
            if (link.Role != 5) continue;
            object? scorpion = FindScorpion(state.Brain, host);
            if (scorpion is not null) host.TrySendScorpionParameterMessage(scorpion, 8, link.Node);
            return;
        }
        host.SourceDiagnostic("No linked breakable target.");
    }

    private static object? FindScorpion(JonahBrainState brain, IJonahBrainHost host)
    {
        object? scorpion = host.RecoverScorpion(brain.ScorpionHandle);
        if (scorpion is not null) return scorpion;
        scorpion = host.FindFirstScorpion();
        if (scorpion is not null) brain.ScorpionHandle = host.MakeScorpionHandle(scorpion);
        return scorpion;
    }
    private static bool IsOrdinaryWaypoint(JonahWaypointLink link) => link.Role is not (4 or 5) && link.Type is 1000 or 1002;
    private static byte AnimationMode(JonahState jonah) => unchecked((byte)(jonah.IdleAnimations >> 8));
}
