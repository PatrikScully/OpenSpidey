using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>World, procedure, speech and camera services consumed by the shipped Scorpion behavior leaves.</summary>
public interface IScorpionBehaviorHost : IScorpionIdleHost
{
    FixedVector3 WaypointPosition(int node); // Trig_GetPosition4E3940.
    void RunAppropriateAnimation(); // CBaddy404B60.
    // CClass_new followed by the named CAIProc constructor; a rejected allocation attaches nothing.
    void TryAttachPointLookAt(FixedVector3 point, uint completionMask, int rate, int tolerance); // 401180 without a body.
    void TryAttachStateSwitchMessage(int code); // 401E60.
    void AttachSpeech(int bank, int slot); // CBody::AttachXA4603D0 on the Scorpion itself.
    void StopSpeech(); // CBody::StopMyXA460400.
    object? RecoverWeb(PlayerKickHandle handle);
    void BurstWeb(object web); // CTrapWebEffect::Burst4F8600.
    void SuppressPlayerInput(int ticks); // CPlayer::SetIgnoreInputTimer4C30A0.
    void SendPulse(ushort node); // Trig_GetLinksPointer4E3880 then Trig_SendPulse4DFD30.
    bool HasCamera { get; } // Global camera0x56F3B8.
    short CameraAngle { get; } // Camera0x236.
    void SetCameraMode(int mode);
    void TrackScorpionPositionAndOrientation(ScorpionState scorpion); // Camera0x100/0xFC then0x140/0x13C.
    void SetCameraAngle(short angle, ushort ticks);
    void SetCameraXOffset(short offset, ushort ticks);
    void SetCameraYOffset(short offset, ushort ticks);
    void SetCameraZOffset(short offset, ushort ticks);
    void SetCameraHorizontalDistance(ushort distance, ushort ticks);
    void SetCameraVerticalDistance(short distance, ushort ticks);
    int AttackClock { get; } // gAttackRelated0x5FCCF4.
    int BaddyCollisionCheck { get; set; } // 0x5FBEE0, read by the zone line query.
    uint BodySignalFlags(object body); // CBaddy0x218 of the pursued body.
    void DoPhysics(int limitElapsed); // CBaddy404C50.
    // InitLineInfo4524C0 runs on the initial line; the probe then flattens both Y values before LineToItem4549A0 mode1.
    ScorpionLineHit LineToItem(FixedVector3 initialStart, FixedVector3 initialEnd, FixedVector3 start, FixedVector3 end);
    void TryAttachYawRotation(int angle, int rate, uint completionMask); // CAIProc_RotY401060.
    void TryAttachAcceleration(int acceleration, int start, int completionMask); // CAIProc_AccZ4016B0.
    void TryMoveToPoint(FixedVector3 destination, int speed, int turnRate, int facingTolerance, uint completionMask); // CAIProc_MoveTo4018D0.
    int PathPointCount { get; set; } // CBaddy0x1F0.
    FixedVector3 PathPoint(int index); // CBaddy0x1A8 + 12*index.
    void RunCurrentScript(); // CBaddy402E10.
    bool GetNextWaypoint(); // CBaddy402F60.
    IReadOnlyList<JonahWaypointLink> ReadWaypointLinks(int node, int maximumCount); // Trig_GetLinkInfoList4DFE70.
    // gTrigNodes type1002/5 check, then Trig_GetPosition and SquirtAngles with the angles restored; null for other nodes.
    ScorpionBreakWall? ReadBreakWall(int node);
    void ChunkItemByChecksum(uint checksum); // Chunk_ChunkItemByChecksum4273D0.
}

/// <summary>SLineInfo pItem0x68 and pFace0x80 after the zone query.</summary>
public readonly record struct ScorpionLineHit(object? Item, bool Face);

/// <summary>The word at the SquirtAngles result and the aligned dword the PC reads as the chunk checksum.</summary>
public readonly record struct ScorpionBreakWall(ushort Tag, uint Checksum);

/// <summary>Whole shipped Scorpion behavior leaves dispatched by488590.</summary>
public static class ScorpionBehavior
{
    /// <summary>PC486C00: web tug reaction and the deferred source tug-web burst.</summary>
    public static void GetTugged(ScorpionTargetState state, IScorpionBehaviorHost host)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        switch (body.Substate)
        {
            case 0:
                damage.Actor.Neutralize(damage.Procedures);
                host.TryAttachStateSwitchMessage(15);
                host.CycleAnimation(12, 1);
                body.Substate = unchecked(body.Substate + 1);
                break;
            case 1:
                if ((body.BehaviorFlags & 8) == 0 || damage.SlideUpdates++ > 10)
                {
                    host.RunAnimation(13, 0, -1);
                    body.Substate = unchecked(body.Substate + 1);
                }
                break;
            case 2:
                if (body.Animation.Frame >= 10)
                {
                    body.Substate = 3;
                    if (brain.TugWeb.Target is not null)
                    {
                        if (host.RecoverWeb(brain.TugWeb) is { } web)
                        {
                            host.BurstWeb(web);
                            host.PlayPositionSound(0x80C6, damage.Actor.Body.Position, 0);
                        }
                        brain.TugWeb = brain.TugWeb with { Target = null };
                    }
                }
                break;
            case 3:
                if (body.Animation.Finished != 0) Enter(body, 2);
                break;
            default:
                host.SourceDiagnostic("Unknown substate.");
                break;
        }
    }

    /// <summary>PC484A90: turn to the environmental aim point0xC00, then the wind-up clip and quip.</summary>
    public static void WindUpEnvironmentalAttack(ScorpionTargetState state, IScorpionBehaviorHost host, Func<int, int> random)
    {
        ScorpionDamageState damage = state.Brain.Damage;
        ScorpionState body = damage.Body;
        switch (body.Substate)
        {
            case 0:
                damage.Actor.Neutralize(damage.Procedures);
                if (body.State == 17) body.Substate = 2;
                else
                {
                    host.TryAttachPointLookAt(state.EnvironmentAimPosition, 2, 50, 56);
                    body.Substate = unchecked(body.Substate + 1);
                }
                break;
            case 1:
                host.RunAppropriateAnimation();
                if ((damage.Actor.CompletionFlags & 2) != 0)
                {
                    damage.Actor.CompletionFlags &= ~2u;
                    body.Substate = unchecked(body.Substate + 1);
                }
                break;
            case 2:
                host.RunAnimation(15, 0, -1);
                if (random(4) != 0 && damage.FireQuipSuppressionUpdates == 0)
                {
                    int slot = random(3) + 10;
                    if (slot == state.Brain.LastWindUpQuip && ++slot >= 13) slot = 10;
                    state.Brain.LastWindUpQuip = slot;
                    if (host.PlayPositionSpeech(13, slot, damage.Actor.Body.Position, 0)) host.AttachSpeech(13, slot);
                }
                body.Substate = unchecked(body.Substate + 1);
                break;
            case 3:
                if (body.Animation.Finished != 0) Enter(body, 2);
                break;
            default:
                host.SourceDiagnostic("Unknown substate!");
                break;
        }
    }

    /// <summary>PC487CD0: head for the current waypoint, or chase around Jonah for up to65 updates.</summary>
    public static void ReactToJonah(ScorpionTargetState state, IScorpionBehaviorHost host)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        switch (body.Substate)
        {
            case 0:
            {
                damage.Actor.Neutralize(damage.Procedures);
                FixedVector3 waypoint = host.WaypointPosition(body.CurrentWaypoint) with { Y = brain.StandingY };
                int path = ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, waypoint, 20, out FixedVector3 hit);
                if (path == 0 || path == 2 && unchecked((uint)ThugPerception.ApproximateHorizontalDistance(hit, waypoint)) < 200)
                {
                    Enter(body, 15);
                    return;
                }
                host.CycleAnimation(unchecked((byte)body.IdleAnimations), 1);
                damage.SlideUpdates = 0;
                body.Substate = unchecked(body.Substate + 1);
                break;
            }
            case 1:
                if (damage.SlideUpdates++ <= 64)
                {
                    FixedVector3 waypoint = host.WaypointPosition(body.CurrentWaypoint) with { Y = damage.Actor.Body.Position.Y };
                    if (!ScorpionIdle.ApproachAround(state, host, waypoint, method: 1)) return;
                }
                else host.StorePathDestination(damage.Actor.Body.Position);
                Enter(body, 15);
                break;
            default:
                host.SourceDiagnostic("Unknown substate.");
                break;
        }
    }

    /// <summary>PC484C60: side-step along the message9 obstacle line, alternating sides, then MoveAnywhere486800.</summary>
    public static void AvoidObstacle(ScorpionTargetState state, IScorpionBehaviorHost host)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        if (body.Animation.Finished != 0) host.CycleAnimation(unchecked((byte)body.IdleAnimations), 1);
        switch (body.Substate)
        {
            case 0:
            {
                damage.SlideUpdates = 0;
                body.Substate = 1;
                FixedVector3 axis = FixedVectorMath.Normalize((brain.ObstaclePosition - brain.ObstacleWaypoint) >> 10);
                brain.ObstacleAxis = axis with { Y = 0 };
                brain.ObstacleSide = new FixedVector3(axis.Z, 0, unchecked(-axis.X)) * (body.MinorRadius + 40);
                brain.ObstacleAxis *= 100;
                break;
            }
            case 1:
                break;
            case 2:
                ScorpionIdle.CircleWhenStuck(state, host);
                if (damage.SlideUpdates >= 666) host.SourceDiagnostic("Stuck again. Emergency fix required.");
                return;
            default:
                host.SourceDiagnostic("Unknown substate!");
                return;
        }
        brain.ObstacleSide = new(unchecked(-brain.ObstacleSide.X), brain.ObstacleSide.Y, unchecked(-brain.ObstacleSide.Z));
        FixedVector3 candidate = brain.ObstaclePosition - brain.ObstacleAxis * 4;
        candidate += brain.ObstacleAxis * unchecked((damage.SlideUpdates >> 1) - 4);
        candidate += brain.ObstacleSide;
        if (ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, candidate, 20, out _) != 0)
        {
            if (++damage.SlideUpdates >= 24)
            {
                damage.SlideUpdates = 0;
                body.Substate = unchecked(body.Substate + 1);
            }
            return;
        }
        body.Substate = 0;
        host.StorePathDestination(candidate with { Y = damage.Actor.Body.Position.Y });
        body.State = 1;
        brain.SignalFlags |= 0x80;
    }

    /// <summary>PC487B80: death clip, Scorpion death camera, then the CBaddy4048B0 one-shot death pulse.</summary>
    public static void Die(ScorpionTargetState state, IScorpionBehaviorHost host, int elapsedTicks)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        switch (body.Substate)
        {
            case 0:
                host.RunAnimation(26, 0, -1);
                damage.Field310 = 0;
                damage.Actor.Neutralize(damage.Procedures);
                host.StopSpeech();
                host.PlayPositionSpeech(15, 1, damage.Actor.Body.Position, 0);
                body.BodyFlags &= 0xFFEF;
                body.Substate = unchecked(body.Substate + 1);
                host.SuppressPlayerInput(0x8000);
                if (!host.HasCamera) return;
                host.SetCameraMode(3);
                host.TrackScorpionPositionAndOrientation(body);
                host.SetCameraXOffset(0, 0);
                host.SetCameraYOffset(0, 0);
                host.SetCameraZOffset(0, 0);
                host.SetCameraHorizontalDistance(450, 0);
                host.SetCameraVerticalDistance(50, 0);
                break;
            case 1:
                if (host.HasCamera) host.SetCameraAngle(unchecked((short)(host.CameraAngle + (elapsedTicks << 5))), 16);
                if (body.Animation.Finished == 0) return;
                if ((body.BehaviorFlags & 0x4000) == 0 && brain.DeathPulseSent == 0 && body.SourceNode != ushort.MaxValue)
                {
                    brain.DeathPulseSent = 1;
                    host.SendPulse(body.SourceNode);
                }
                body.Substate = unchecked(body.Substate + 1);
                break;
            case 2:
                break;
            default:
                host.SourceDiagnostic("Unknown substate!");
                break;
        }
    }

    /// <summary>PC485120: approach the recovered target, probe walls, jump when clear and hand over to the tail attack.</summary>
    public static void PursueTarget(ScorpionTargetState state, IScorpionBehaviorHost host, object target)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        if (damage.TargetKind == 1 && (host.BodySignalFlags(target) & 0x800) != 0 && body.Substate < 666)
        {
            host.DetermineTarget();
            if (damage.TargetKind != 1) return;
        }
        else if (damage.TargetKind == 2 && host.PlayerIsCrawling && host.LineOfSight(damage.Actor.Body.Position, host.PlayerPosition, 0, 0))
        {
            Enter(body, 6);
            return;
        }
        int substate = body.Substate;
        if (substate > 3)
        {
            if (substate < 666 || substate > 668) host.SourceDiagnostic("Unknown substate!");
            else if (substate <= 667) ExecuteCharge(state, host);
            else if (ExecuteCharge(state, host)) body.Substate = 0;
            return;
        }
        int wall;
        switch (substate)
        {
            case 3:
                wall = ProbeWall(state, host);
                if (wall != 0)
                {
                    if (wall != 2) { damage.Actor.Neutralize(damage.Procedures); Enter(body, 2); }
                    return;
                }
                if ((damage.Actor.CompletionFlags & 8) == 0) return;
                damage.Actor.CompletionFlags &= ~8u;
                Enter(body, 5);
                return;
            case 0:
                damage.Actor.Neutralize(damage.Procedures);
                host.TryAttachTargetLookAt(target, 2, 50, 170);
                damage.SlideUpdates = 0;
                body.Substate = unchecked(body.Substate + 1);
                return;
            case 1:
                host.DoPhysics(0);
                if (unchecked((uint)ThugPerception.ApproximateHorizontalDistance(host.BodyPosition(target), damage.Actor.Body.Position)) < 0x226)
                {
                    damage.Actor.Neutralize(damage.Procedures);
                    Enter(body, 5);
                    return;
                }
                host.RunAppropriateAnimation();
                if ((damage.Actor.CompletionFlags & 2) != 0)
                {
                    damage.Actor.CompletionFlags &= ~2u;
                    brain.SignalFlags &= ~2u;
                    host.TryAttachAcceleration(15, -100, 0);
                    host.CycleAnimation(unchecked((byte)(body.MovementAnimations >> 8)), 1);
                    body.Substate = unchecked(body.Substate + 1);
                }
                return;
            case 2:
                if ((host.AttackClock & 3) == 0 && ShouldJumpToTarget(state, host, host.BodyPosition(target))) return;
                wall = ProbeWall(state, host);
                if (wall == 0)
                {
                    if (ThugPerception.ApproximateHorizontalDistance(damage.Actor.Body.Position, host.BodyPosition(target)) < 550)
                    {
                        host.TryAttachAcceleration(160, 0, 8);
                        body.Substate = unchecked(body.Substate + 1);
                    }
                    if (ReferenceEquals(target, host.PlayerIdentity) && damage.PlayerTargetUpdates == 0) host.DetermineTarget();
                    return;
                }
                if (wall != 2) { damage.Actor.Neutralize(damage.Procedures); Enter(body, 2); }
                return;
            default:
                host.SourceDiagnostic("Unknown substate!");
                return;
        }

        static int ProbeWall(ScorpionTargetState state, IScorpionBehaviorHost host)
        {
            if ((state.Brain.Body.BehaviorFlags & 0x2000) != 0) host.BaddyCollisionCheck = 1;
            int result = GonnaHitWall(state, host, 0);
            host.BaddyCollisionCheck = 0;
            return result;
        }
    }

    /// <summary>PC484240: path points or waypoints, charges, script/next-waypoint selection and the ID_BREAK_WALL burst.</summary>
    public static void FollowWaypoints(ScorpionTargetState state, IScorpionBehaviorHost host)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        int substate = body.Substate;
        if (substate > 667)
        {
            if (substate != 668) host.SourceDiagnostic("Unknown substate!");
            else if (ExecuteCharge(state, host)) body.Substate = unchecked((int)((~brain.SignalFlags >> 7) & 2));
            return;
        }
        if (substate >= 666) { ExecuteCharge(state, host); return; }
        switch (substate)
        {
            case 0:
            {
                damage.Field310 = 0;
                FixedVector3 destination;
                int count = host.PathPointCount;
                if (count != 0)
                {
                    body.BehaviorFlags &= ~0x10000000u;
                    destination = host.PathPoint(count);
                    brain.ChargeTarget = destination with { Y = brain.StandingY };
                }
                else
                {
                    body.BehaviorFlags |= 0x10000000;
                    destination = host.WaypointPosition(body.CurrentWaypoint);
                    brain.ChargeTarget = destination;
                }
                if (brain.ChargeAttempts < 2 && PrepareCharge(state, host)) brain.ChargeAttempts++;
                else
                {
                    host.TryMoveToPoint(destination, 100, 55, 500, 1);
                    body.Substate = unchecked(body.Substate + 1);
                }
                return;
            }
            case 1:
                host.RunAppropriateAnimation();
                if (damage.TargetKind == 1 && (brain.SignalFlags & 0x80) == 0 && body.State != 15) host.DetermineTarget();
                if ((damage.Actor.CompletionFlags & 1) == 0) return;
                damage.Actor.CompletionFlags &= ~1u;
                brain.ChargeAttempts = 0;
                break;
            case 2:
                break;
            case 5:
            {
                IReadOnlyList<JonahWaypointLink> links = host.ReadWaypointLinks(body.CurrentWaypoint, 1);
                if (links.Count != 0)
                {
                    host.TryAttachPointLookAt(host.WaypointPosition(links[0].Node), 2, 50, 56);
                    body.Substate = unchecked(body.Substate + 1);
                }
                else host.SourceDiagnostic("No link through wall to next room!");
                return;
            }
            case 6:
                host.RunAppropriateAnimation();
                if ((damage.Actor.CompletionFlags & 2) == 0) return;
                damage.Actor.CompletionFlags &= ~2u;
                host.RunCurrentScript();
                host.RunAnimation(25, 0, -1);
                if (!host.GetNextWaypoint()) host.SourceDiagnostic("No link through wall into next room!");
                body.Substate = unchecked(body.Substate + 1);
                return;
            case 7:
                if (body.Animation.Frame >= 28)
                {
                    host.ChunkItemByChecksum(brain.BreakWallChecksum);
                    body.Substate = unchecked(body.Substate + 1);
                }
                return;
            case 8:
                if (body.Animation.Finished != 0)
                {
                    body.Substate = 0;
                    brain.SignalFlags = (brain.SignalFlags & ~0xCu) | 8;
                }
                return;
            default:
                host.SourceDiagnostic("Unknown substate!");
                return;
        }
        if (body.State == 15 && host.PathPointCount == 0 && host.ReadBreakWall(body.CurrentWaypoint) is { } wall)
        {
            if (wall.Tag != 8504) host.SourceDiagnostic("No checksum in ID_BREAK_WALL type scriptpoint!");
            brain.BreakWallChecksum = wall.Checksum;
            body.Substate = 5;
            return;
        }
        if (host.PathPointCount == 0) host.RunCurrentScript();
        if (host.PathPointCount != 0)
        {
            host.PathPointCount = unchecked(host.PathPointCount - 1);
            if (body.State == 15) { body.Substate = 0; return; }
        }
        if (host.PathPointCount == 0)
        {
            if (body.State == 15)
            {
                if (!host.GetNextWaypoint())
                {
                    brain.SignalFlags &= ~0x18u;
                    host.DetermineTarget();
                    body.State = 17;
                }
                body.Substate = 0;
                return;
            }
            host.DetermineTarget();
            body.State = 2;
        }
        body.Substate = 0;
        if ((brain.SignalFlags & 0x80) != 0)
        {
            brain.SignalFlags &= ~0x80u;
            state.EnvironmentAimPosition = host.PlayerPosition;
            Enter(body, 16);
        }
    }

    /// <summary>PC483E80: pick the nearest of the three0x552108 charge headings, clamp the charge and turn toward it.</summary>
    public static bool PrepareCharge(ScorpionTargetState state, IScorpionBehaviorHost host)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        FixedVector3 position = damage.Actor.Body.Position;
        int distance = ThugPerception.ApproximateHorizontalDistance(brain.ChargeTarget, position);
        int yaw = unchecked(FixedAim.Calculate(position, brain.ChargeTarget).Yaw - damage.Actor.Body.Angles.Y);
        if (yaw < -2048) yaw += 4096;
        else if (yaw > 2048) yaw -= 4096;
        int best = 4096;
        for (int index = 0; index < ChargeRows.Length; index++)
        {
            int difference = unchecked(yaw - ChargeRows[index].Heading);
            if (SourceAbs(difference) >= SourceAbs(best)) continue;
            best = difference;
            brain.ChargeIndex = index;
        }
        ChargeRow row = ChargeRows[brain.ChargeIndex];
        if (distance <= row.MinimumDistance) return false;
        if (distance <= row.MaximumDistance) brain.SignalFlags &= ~0x100u;
        else
        {
            FixedVector3 delta = brain.ChargeTarget - position;
            int scale = (row.MaximumDistance << 12) / FixedVectorMath.OriginalDistance(delta, default);
            delta = delta with { X = unchecked(scale * (delta.X >> 12)), Z = unchecked(scale * (delta.Z >> 12)) };
            brain.ChargeTarget = (position + delta) with { Y = brain.StandingY };
            brain.SignalFlags |= 0x100;
        }
        damage.Actor.Neutralize(damage.Procedures);
        int magnitude = SourceAbs(best);
        host.TryAttachYawRotation(best, magnitude > 100 ? magnitude / 100 : 1, 2);
        damage.Body.Substate = 666;
        return true;
    }

    /// <summary>PC4840D0: substates666-668 of a prepared charge; true once the landing clip finishes.</summary>
    public static bool ExecuteCharge(ScorpionTargetState state, IScorpionBehaviorHost host)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        if (body.Substate == 666)
        {
            host.RunAppropriateAnimation();
            if ((damage.Actor.CompletionFlags & 2) != 0)
            {
                damage.Actor.CompletionFlags &= ~2u;
                host.RunAnimation(ChargeRows[brain.ChargeIndex].Clip, 0, -1);
                brain.ChargeStart = damage.Actor.Body.Position;
                body.Substate = unchecked(body.Substate + 1);
                host.PlayPositionSound(0x80BF, damage.Actor.Body.Position, 0);
            }
            return false;
        }
        if (body.Substate != 667) return body.Substate == 668 && body.Animation.Finished != 0;
        int frame = body.Animation.Frame;
        ChargeRow row = ChargeRows[brain.ChargeIndex];
        if (frame <= row.StartFrame) return false;
        if (frame < row.EndFrame)
        {
            FixedVector3 start = brain.ChargeStart, target = brain.ChargeTarget;
            int step = unchecked(row.Scale * (frame - row.StartFrame));
            damage.Actor.Body.Position = damage.Actor.Body.Position with
            {
                X = unchecked(start.X + step * ((target.X - start.X) >> 12)),
                Z = unchecked(start.Z + step * ((target.Z - start.Z) >> 12))
            };
            return false;
        }
        host.PlayPositionSound(0x80C0, damage.Actor.Body.Position, 0);
        damage.Actor.Body.Position = brain.ChargeTarget;
        body.Substate = unchecked(body.Substate + 1);
        return false;
    }

    /// <summary>PC484800: DoPhysics, then a half-radius forward zone probe; 2 retargets an environmental item, 1 blocks.</summary>
    public static int GonnaHitWall(ScorpionTargetState state, IScorpionBehaviorHost host, int limitElapsed)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        ScorpionState body = damage.Body;
        brain.PreviousPosition = damage.Actor.Body.Position;
        host.DoPhysics(limitElapsed);
        FixedVector3 position = damage.Actor.Body.Position;
        FixedVector3 ahead = position + FixedVectorMath.Normalize((position - brain.PreviousPosition) >> 10) * (body.MinorRadius >> 1);
        if (host.BaddyCollisionCheck == 0) host.SourceDiagnostic("Not detecting baddy polys!");
        int y = unchecked(position.Y + ((body.SurfaceClearance - 10) << 12));
        ScorpionLineHit hit = host.LineToItem(brain.PreviousPosition, ahead, brain.PreviousPosition with { Y = y }, ahead with { Y = y });
        if (hit.Item is null && !hit.Face) return 0;
        damage.Actor.Body.Position = brain.PreviousPosition;
        if (hit.Item is not null && host.BodyType(hit.Item) == 401)
        {
            damage.Actor.Neutralize(damage.Procedures);
            state.CurrentTarget = host.MakeHandle(hit.Item);
            body.Substate = 0;
            damage.TargetKind = 3;
            body.State = 5;
            return 2;
        }
        if (damage.TargetKind == 2) damage.PlayerTargetUpdates = 0;
        return 1;
    }

    /// <summary>PC484EF0: from1100 units away with a clear path, charge up to2000 units and stop550 short of the target.</summary>
    public static bool ShouldJumpToTarget(ScorpionTargetState state, IScorpionBehaviorHost host, FixedVector3 target)
    {
        ScorpionBrainState brain = state.Brain;
        ScorpionDamageState damage = brain.Damage;
        int distance = ThugPerception.ApproximateHorizontalDistance(damage.Actor.Body.Position, target);
        if (unchecked(distance - 550) < 550) return false;
        int path = ScorpionDamageBehavior.PathCheck(damage, host, damage.Actor.Body.Position, target, 20, out FixedVector3 hit);
        if (path != 0 && (path != 2 || unchecked((uint)ThugPerception.ApproximateHorizontalDistance(target, hit)) >= 0x226)) return false;
        FixedVector3 delta = target - damage.Actor.Body.Position;
        int length = FixedVectorMath.OriginalDistance(delta, default);
        FixedVector3 direction = FixedVectorMath.Normalize((delta >> 10) with { Y = 0 });
        if (length > 2000) length = 2000;
        FixedVector3 travel = direction * length;
        brain.ChargeTarget = damage.Actor.Body.Position + travel;
        int remaining = ThugPerception.ApproximateHorizontalDistance(target, brain.ChargeTarget);
        if (remaining < 550) brain.ChargeTarget -= direction * (550 - remaining);
        damage.Body.Substate = 666;
        brain.ChargeStart = damage.Actor.Body.Position;
        damage.Actor.Neutralize(damage.Procedures);
        host.TryAttachPointLookAt(brain.ChargeTarget + travel, 2, 50, 56);
        brain.ChargeIndex = 2;
        return true;
    }

    private readonly record struct ChargeRow(ushort Clip, int Scale, int StartFrame, int EndFrame, int Heading, int MinimumDistance, int MaximumDistance);
    private static readonly ChargeRow[] ChargeRows = [new(23, 512, 4, 12, -1024, 150, 400), new(24, 512, 4, 12, 1023, 150, 400), new(4, 227, 10, 28, 0, 500, 2000)];

    private static void Enter(ScorpionState body, int state) { body.State = state; body.Substate = 0; }
    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
