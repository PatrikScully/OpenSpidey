using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Whole shipped488590 outer AI order. Its required behavior/world transports must exist before production registration.</summary>
public static class ScorpionBrain
{
    public static int Update(ScorpionBrainState brain, IScorpionBrainHost host, int elapsedTicks, Func<int, int> random)
    {
        var damage = brain.Damage;
        var body = brain.Body;
        if (damage.HitFlashUpdates != 0) damage.HitFlashUpdates = Decrease(damage.HitFlashUpdates, elapsedTicks);
        if (body.State != 0 && damage.Actor.Body.Position.Y != brain.StandingY)
            damage.Actor.Body.Position = damage.Actor.Body.Position with { Y = brain.StandingY };
        damage.PlayerTargetUpdates = Decrease(damage.PlayerTargetUpdates, elapsedTicks);
        brain.EnvironmentalTargetSuppressionUpdates = Decrease(brain.EnvironmentalTargetSuppressionUpdates, elapsedTicks);
        brain.GloatSuppressionUpdates = Decrease(brain.GloatSuppressionUpdates, elapsedTicks);
        brain.FieldC1CUpdates = Decrease(brain.FieldC1CUpdates, elapsedTicks);
        damage.FireQuipSuppressionUpdates = Decrease(damage.FireQuipSuppressionUpdates, elapsedTicks);
        brain.TailGlowEnabled = (byte)((damage.ReactionFlags & 4) != 0 ? 1 : 0);
        UpdateCamera(brain, host);
        UpdateMusic(brain, host);
        if (body.TailGeometryRegion == byte.MaxValue) host.InitializeTailGeometry();
        if (body.TailGlowRegion == byte.MaxValue) host.InitializeTailGlow();
        if ((damage.StateFlags.Check(body.State) & 4) == 0) host.DoPhysics(0);
        host.BuildTransform();
        ApplySignals(brain);
        ProcessMessages(brain, host);
        if (damage.Procedures.Count != 0) damage.Procedures.Execute(elapsedTicks);
        damage.ReactionFlags = 0;
        body.BehaviorFlags &= ~0x20u;
        if (body.Animation.ClipId == 2 && body.Animation.Frame is 2 or 17)
        {
            int sound = random(4) + 160;
            if (sound == brain.LastFootstepSound && ++sound == 164) sound = 160;
            brain.LastFootstepSound = sound;
            host.PlayPositionSound(unchecked((uint)(sound | 0x8000)), damage.Actor.Body.Position, 0);
        }
        Dispatch(brain, host, elapsedTicks);
        host.UpdateFootprint();
        if ((brain.SignalFlags & 1) != 0)
        {
            Span<FixedVector3> trail = host.TailTrail.Span;
            if (trail.Length != 6) throw new InvalidOperationException("The source tail trail owns six shared world vectors.");
            for (int index = 0; index < trail.Length; index++) trail[index] = body.TailNodes[2 + index * 4];
            brain.SignalFlags &= ~1u;
        }
        host.UpdateTailPath();
        if ((brain.SignalFlags & 0x800) != 0)
        {
            host.TryCreateTailImpact(() => body.TailNodes[22]);
            brain.SignalFlags &= ~0x800u;
        }
        int result = damage.TargetKind;
        brain.CurrentTargetIsPlayer = (byte)(result == 2 ? 1 : 0);
        return result;
    }

    private static void UpdateCamera(ScorpionBrainState brain, IScorpionBrainHost host)
    {
        var body = brain.Body;
        if (host.Player.IsCrawling && host.Player.FieldE18 == 0)
        {
            if (body.BossCameraEnabled != 0)
            {
                if (host.Camera is { } camera)
                {
                    SaveDistances();
                    camera.SetMode(3);
                    if (host.PlayerFieldAa > 3400) host.PutPlayerCameraBehind(16);
                    brain.CameraDistancesSaved = 1;
                }
                host.SetGlobalBoss(null);
                body.BossCameraEnabled = 0;
            }
        }
        else if (body.BossCameraEnabled == 0 && host.Player.FieldE18 == 0)
        {
            if (host.Camera is { } camera)
            {
                if (brain.CameraDistancesSaved == 0) { SaveDistances(); brain.CameraDistancesSaved = 1; }
                camera.SetMode(16);
                camera.SpinRate = 127;
                camera.SetHorizontalDistance(brain.SavedCameraHorizontalDistance, 0);
                camera.SetVerticalDistance(unchecked((short)brain.SavedCameraVerticalDistance), 0);
                camera.StationaryRadius = 0;
            }
            host.SetGlobalBoss(body);
            body.BossCameraEnabled = 1;
        }
        var current = host.Camera ?? throw new InvalidOperationException("The source Scorpion AI requires an actual camera for its final mode/hide pass.");
        current.Mode.ItemHidingEnabled = (byte)(current.Mode.Mode == 16 ? 1 : 0);
        if (current.Mode.Mode != 16)
        {
            for (int index = 0; index < 8; index++)
            {
                IScorpionCameraHiddenItem? item = current.GetHiddenItem(index);
                if (item is null) break;
                if (ReferenceEquals(host.PlayerHeldItemIdentity, item.Identity)) continue;
                item.ItemFlags &= 0xF7FF;
                current.ClearHiddenItem(index);
            }
        }
        return;

        void SaveDistances()
        {
            var camera = host.Camera!;
            brain.SavedCameraHorizontalDistance = camera.GetHorizontalDistance();
            brain.SavedCameraVerticalDistance = unchecked((ushort)camera.GetVerticalDistance());
        }
    }

    private static void UpdateMusic(ScorpionBrainState brain, IScorpionBrainHost host)
    {
        var music = host.Music;
        if (brain.Body.State is 0 or 13) music.Phase = 2;
        else if (brain.Damage.TargetKind == 2 && music.Phase != 0)
        {
            if (music.TrackEnding != 0 || music.Phase == 2) { music.Phase = 0; music.TrackClock = 6666; }
        }
        else if (music.Phase != 1 && (music.TrackEnding != 0 || music.Phase == 2))
        { music.Phase = 1; music.TrackClock = 6666; }
    }

    private static void ApplySignals(ScorpionBrainState brain)
    {
        var damage = brain.Damage;
        var body = brain.Body;
        uint signals = brain.SignalFlags;
        if ((signals & 0xC) != 0) damage.PlayerTargetUpdates = 0;
        if ((signals & 4) != 0 && ((damage.StateFlags.Check(body.State) & 0x20) != 0 || body.State == 3 && body.Substate < 666) && damage.TargetKind != 2)
        { damage.Actor.Neutralize(damage.Procedures); Enter(brain, 14); }
        else if ((brain.SignalFlags & 8) != 0 && (body.State == 2 || body.State == 3 && body.Substate < 666) && damage.TargetKind != 2)
        { damage.Actor.Neutralize(damage.Procedures); Enter(brain, 15); }
        else if ((brain.SignalFlags & 0x40) != 0 && ((damage.StateFlags.Check(body.State) & 0x10) != 0 || body.State == 3 && body.Substate < 666))
        { damage.Actor.Neutralize(damage.Procedures); Enter(brain, 20); brain.SignalFlags &= ~0x40u; }
    }

    private static void ProcessMessages(ScorpionBrainState brain, IScorpionBrainHost host)
    {
        var damage = brain.Damage;
        var body = brain.Body;
        for (ThugMessage? message = brain.Messages.Head; message is not null; message = message.Next)
        {
            if (body.State != 21 || message.Code >= 11)
            {
                switch (message.Code)
                {
                    case 5:
                        if (body.State != 10) { damage.Actor.Neutralize(damage.Procedures); Enter(brain, 10); }
                        else if ((damage.ReactionFlags & 2) != 0) { damage.SlideUpdates = 0; body.Substate = 1; }
                        else continue; // This is the one unconsumed persistent trapped-web message.
                        break;
                    case 6:
                        if (body.State == 12) damage.SlideUpdates = 0;
                        else
                        {
                            damage.Actor.Neutralize(damage.Procedures);
                            host.PlayPositionSound(0x80A4, damage.Actor.Body.Position, 0);
                            Enter(brain, 12);
                        }
                        break;
                    case 8:
                        brain.SignalFlags |= 4;
                        body.CurrentWaypoint = message.Parameter32;
                        break;
                    case 9:
                        if (body.State is not (20 or 1 or 16 or 17))
                        {
                            brain.SignalFlags |= 0x40;
                            brain.ObstaclePosition = message.Vector0;
                            brain.ObstacleWaypoint = message.Vector1;
                        }
                        break;
                    case 14: Burst(brain.TrapWeb); brain.TrapWeb = brain.TrapWeb with { Target = null }; break;
                    case 15: Burst(brain.TugWeb); brain.TugWeb = brain.TugWeb with { Target = null }; break;
                }
            }
            message.Flags |= 1;
        }
        brain.Messages.Cleanup();
        return;

        void Burst(OpenSpidey.Core.Player.PlayerKickHandle handle)
        {
            if (handle.Target is null) return;
            object? web = host.RecoverWeb(handle);
            if (web is null) return;
            host.BurstWeb(web);
            host.PlayPositionSound(0x80C6, damage.Actor.Body.Position, 0);
        }
    }

    private static void Dispatch(ScorpionBrainState brain, IScorpionBrainHost host, int elapsedTicks)
    {
        var damage = brain.Damage;
        var body = brain.Body;
        switch (body.State)
        {
            case 0:
                if (body.Substate == 0)
                {
                    damage.FireQuipSuppressionUpdates = 800;
                    int ground = host.GroundHeight(damage.Actor.Body.Position, 300, 300, 0);
                    if (ground != -1)
                    {
                        int substate = body.Substate;
                        brain.GroundY = ground;
                        damage.Actor.Body.Position = damage.Actor.Body.Position with { Y = unchecked(ground - (body.SurfaceClearance << 12)) };
                        brain.StandingY = damage.Actor.Body.Position.Y;
                        body.Substate = unchecked(substate + 1);
                        host.CycleAnimation(unchecked((byte)body.IdleAnimations), 1);
                    }
                }
                else if (body.Substate == 1)
                { host.DetermineTarget(); Enter(brain, 13); brain.GloatSuppressionUpdates = 500; }
                else host.SourceDiagnostic("Unknown sub-state!");
                break;
            case 1:
            case 15: host.StepBehavior(ScorpionBehaviorRoutine.FollowWaypoints); break;
            case 2: host.StepBehavior(ScorpionBehaviorRoutine.Idle); break;
            case 3: TargetBehavior(ScorpionBehaviorRoutine.PursueTarget); break;
            case 5: TargetBehavior(ScorpionBehaviorRoutine.AttackEnvironmentalObject); break;
            case 6: host.StepBehavior(ScorpionBehaviorRoutine.FireTailLaser); break;
            case 9: ScorpionDamageBehavior.TakeHit(damage, host, elapsedTicks); break;
            case 10:
            case 11: host.StepBehavior(ScorpionBehaviorRoutine.GetTrapped); break;
            case 12: host.StepBehavior(ScorpionBehaviorRoutine.GetTugged); break;
            case 13:
                if (host.Player.SynthesizedInputActive) host.StepBehavior(ScorpionBehaviorRoutine.DoIntroSequence);
                else Enter(brain, 2);
                break;
            case 14: host.StepBehavior(ScorpionBehaviorRoutine.ReactToJonah); break;
            case 16:
            case 17: host.StepBehavior(ScorpionBehaviorRoutine.WindUpEnvironmentalAttack); break;
            case 18: host.StepBehavior(ScorpionBehaviorRoutine.Gloat); break;
            case 19: Slide(); break;
            case 20: host.StepBehavior(ScorpionBehaviorRoutine.AvoidObstacle); break;
            case 21: host.StepBehavior(ScorpionBehaviorRoutine.Die); break;
            default: host.SourceDiagnostic("Unknown state!"); break;
        }
        return;

        void TargetBehavior(ScorpionBehaviorRoutine routine)
        {
            body.BehaviorFlags |= 0x20000000;
            object? target = host.RecoverHandle(damage.CurrentTargetHandle);
            if (target is null)
            { damage.Actor.Neutralize(damage.Procedures); Enter(brain, 2); }
            else host.StepBehavior(routine, target);
        }
        void Slide()
        {
            if (body.Substate == 0) { host.RunAnimation(10, 0, -1); body.Substate = unchecked(body.Substate + 1); }
            else if (body.Substate != 1) { host.SourceDiagnostic("Unknown substate!"); return; }
            brain.PreviousPosition = damage.Actor.Body.Position;
            host.DoPhysics(1);
            object? jonah = damage.CachedJonah ??= host.FindJonah();
            if (jonah is not null && FixedVectorMath.OriginalDistance(damage.Actor.Body.Position, host.JonahPosition(jonah)) < body.MinorRadius + (uint)host.JonahRadius(jonah))
            { damage.Actor.Body.Position = brain.PreviousPosition; damage.SlideUpdates = 0; }
            if (damage.SlideUpdates == 0)
            {
                damage.Actor.Body.Velocity = default;
                Enter(brain, 9);
            }
        }
    }

    private static void Enter(ScorpionBrainState brain, int state) { brain.Body.State = state; brain.Body.Substate = 0; }
    private static int Decrease(int value, int elapsedTicks) => Math.Max(0, unchecked(value - elapsedTicks));
}
