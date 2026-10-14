using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>PC CJonah::AI, Init, Plead, Idle and Fret decisions. Unported behavior leaves require real hosts.</summary>
public static class JonahBrain
{
    public static void Update(JonahBrainState brain, IJonahBrainHost host, int elapsedTicks)
    {
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(host);
        JonahState jonah = brain.Jonah;
        object? scorpion = FindScorpion(brain, host);
        brain.SpeechSuppressionTicks = Decrease(brain.SpeechSuppressionTicks, elapsedTicks);
        brain.PanicSoundTicks = Decrease(brain.PanicSoundTicks, elapsedTicks);
        jonah.SignalFlags = jonah.State == 5 ? jonah.SignalFlags | 0x800 : jonah.SignalFlags & ~0x800u;
        brain.Position = brain.Position with
        {
            X = unchecked(brain.Position.X - brain.PreviousDisplacementX),
            Z = unchecked(brain.Position.Z - brain.PreviousDisplacementZ)
        };
        if ((brain.CheckStateFlags() & 2) == 0) host.DoPhysics(0);
        host.BuildTransform();
        ProcessMessages(brain, host, scorpion);
        if (brain.Procedures.Count != 0) brain.Procedures.Execute(elapsedTicks);
        PlayFrameSound(brain, host);
        switch (jonah.State)
        {
            case 0:
                if (host.PlayerSynthesizingInput)
                {
                    Initialize(brain, host, elapsedTicks);
                    host.SetMovementHeight(0, 100, 600);
                }
                else { brain.WaitTicks = 120; brain.EnterState(3); }
                break;
            case 1:
            case 7:
                host.FollowWaypoints();
                host.SetMovementHeight(0, 100, 600);
                break;
            case 2: Idle(brain, host); break;
            case 3: Plead(brain, host, elapsedTicks); break;
            case 4: Fret(brain, host); break;
            case 5: host.Hide(); break;
            case 6: host.TakeHit(); break;
            case 8: host.Die(); break;
            default: host.SourceDiagnostic("Unknown Jonah state."); break;
        }
        // The original retains the identity found before all messages and AI procedures.
        if (scorpion is not null) host.GoAroundScorpion(scorpion);
        host.DrawShadow();
    }

    public static void Initialize(JonahBrainState brain, IJonahBrainHost host, int elapsedTicks)
    {
        JonahState jonah = brain.Jonah;
        switch (jonah.Substate)
        {
            case 0:
                int ground = host.GroundHeight(brain.Position, 300, 300, 0);
                if (ground != -1)
                {
                    brain.Position = brain.Position with { Y = unchecked(ground - (jonah.SurfaceClearance << 12)) };
                    brain.StandingY = brain.Position.Y;
                    brain.GroundY = ground;
                    jonah.Substate++;
                    brain.SpeechSuppressionTicks = 700;
                    host.CycleAnimation(6, 1);
                    brain.WaitTicks = 550;
                    jonah.SignalFlags |= 0x40;
                }
                break;
            case 1:
                brain.WaitTicks = Decrease(brain.WaitTicks, elapsedTicks);
                if ((brain.InputSignals & 1) != 0)
                {
                    brain.InputSignals &= 0xFFFE;
                    host.RunAnimation(25, 0, -1);
                    jonah.Substate++;
                }
                break;
            case 2:
                brain.WaitTicks = Decrease(brain.WaitTicks, elapsedTicks);
                if (jonah.Animation.IsFinished) host.CycleAnimation(6, 1);
                if (brain.WaitTicks == 0) { brain.WaitTicks = 120; brain.EnterState(3); }
                break;
            default: host.SourceDiagnostic("Unknown Jonah initialization substate."); break;
        }
    }

    public static void Idle(JonahBrainState brain, IJonahBrainHost host)
    {
        if (brain.Jonah.Substate == 0)
        {
            brain.Neutralize();
            host.CycleAnimation(unchecked((byte)brain.Jonah.IdleAnimations), 1);
            brain.Jonah.Substate++;
        }
        else if (brain.Jonah.Substate != 1) host.SourceDiagnostic("Unknown Jonah idle substate.");
    }

    public static void Plead(JonahBrainState brain, IJonahBrainHost host, int elapsedTicks)
    {
        JonahState jonah = brain.Jonah;
        object? scorpion = FindScorpion(brain, host);
        if (scorpion is not null && !ReferenceEquals(host.ScorpionCurrentTarget(scorpion), jonah))
        {
            brain.EnterState(1);
            return;
        }
        switch (jonah.Substate)
        {
            case 0:
                brain.Neutralize();
                if (scorpion is not null) host.TryLookAtScorpion(scorpion, 0, 100, 10);
                if (AnimationMode(jonah) == 0) { host.RunAnimation(11, 0, -1); jonah.Substate = 5; }
                else
                {
                    switch (jonah.Animation.ClipId)
                    {
                        case 2: host.RunAnimation(3, 0, -1); jonah.Substate = 5; break;
                        case 6: host.RunAnimation(6, jonah.Animation.Frame, -1); jonah.Substate++; break;
                        case 8: host.RunAnimation(4, 0, -1); jonah.Substate = 6; break;
                        default: SpeakAndAnimate(brain, host); jonah.Substate++; break;
                    }
                }
                break;
            case 1:
                if (jonah.Animation.IsFinished)
                {
                    scorpion = FindScorpion(brain, host);
                    if (brain.SpeechSuppressionTicks == 0 && scorpion is not null && host.ScorpionVictorious(scorpion))
                        SpeakAndAnimate(brain, host);
                    else host.RunAnimation(6, 0, -1);
                }
                brain.WaitTicks = unchecked(brain.WaitTicks - elapsedTicks);
                if (brain.WaitTicks <= 0 && jonah.Animation.ClipId == 6)
                {
                    RestorePleaAnimations(jonah);
                    host.RunAnimation(7, 0, -1);
                    jonah.Substate++;
                }
                break;
            case 2:
                if (jonah.Animation.IsFinished) brain.EnterState(1);
                break;
            case 5:
                if (jonah.Animation.IsFinished)
                {
                    RestorePleaAnimations(jonah);
                    host.RunAnimation(4, 0, -1);
                    jonah.Substate++;
                }
                break;
            case 6:
                if (jonah.Animation.IsFinished) { SpeakAndAnimate(brain, host); jonah.Substate = 1; }
                break;
            default: host.SourceDiagnostic("Unknown Jonah pleading substate."); break;
        }
    }

    public static void Fret(JonahBrainState brain, IJonahBrainHost host)
    {
        JonahState jonah = brain.Jonah;
        object? scorpion = FindScorpion(brain, host);
        switch (jonah.Substate)
        {
            case 0:
                brain.Neutralize();
                if (scorpion is not null)
                {
                    host.TryLookAtScorpion(scorpion, 0, 100, 10);
                    SendWaypointMessage(brain, host, scorpion);
                }
                if (brain.PanicSoundTicks == 0)
                {
                    host.PlaySound(0x80B1, brain.Position, 0);
                    brain.PanicSoundTicks = 120;
                }
                jonah.SignalFlags &= ~0x1000u;
                goto case 1;
            case 1:
                if (AnimationMode(jonah) == 0) { host.RunAnimation(19, 0, -1); jonah.Substate = 5; }
                else if (AnimationMode(jonah) == 1) { host.RunAnimation(9, 0, -1); jonah.Substate = 7; }
                else host.SourceDiagnostic("Unspecified Jonah animation mode.");
                break;
            case 2:
                brain.WaitTicks = unchecked(brain.WaitTicks + 1);
                if ((jonah.SignalFlags & 0x1000) == 0)
                {
                    var heading = GameAngle.Heading(host.Random(4096));
                    FixedVector3 candidate = brain.Position + new FixedVector3(400 * heading.Sine, 0, 400 * heading.Cosine);
                    JonahPathTrace trace = host.TracePath(brain.Position, candidate, 55);
                    if (trace.Result == 0) { brain.PanicTarget = candidate; jonah.SignalFlags |= 0x1000; }
                    else if (trace.Result == 2 && FixedVectorMath.OriginalDistance(brain.Position, trace.HitPosition) > 100)
                    { brain.PanicTarget = trace.HitPosition; jonah.SignalFlags |= 0x1000; }
                }
                if (brain.WaitTicks <= 10) break;
                brain.WaitTicks = 0;
                FixedVector3 waypoint = host.WaypointPosition(jonah.CurrentWaypoint);
                if ((jonah.SignalFlags & 0x2000) == 0)
                {
                    object? currentScorpion = FindScorpion(brain, host);
                    if (currentScorpion is not null && host.ScorpionBlocksPath(brain.Position, waypoint, currentScorpion))
                    {
                        if (scorpion is not null) SendWaypointMessage(brain, host, scorpion);
                        if ((jonah.SignalFlags & 0x1000) != 0 && host.AddPointToPath(brain.Position, 0) &&
                            host.AddPointToPath(brain.PanicTarget, 0)) brain.EnterState(1);
                        break;
                    }
                }
                host.RunAnimation(20, jonah.Animation.Frame, -1);
                jonah.Substate = 10;
                break;
            case 5:
                if (jonah.Animation.IsFinished) { host.CycleAnimation(20, 1); jonah.Substate = 2; }
                break;
            case 7:
                if (jonah.Animation.IsFinished)
                { jonah.MovementAnimations = 0x0A0A0A0A; jonah.IdleAnimations = 10; jonah.Substate = 1; }
                break;
            case 10:
                if (jonah.Animation.IsFinished) { host.RunAnimation(21, 0, -1); jonah.Substate++; }
                break;
            case 11:
                if (jonah.Animation.IsFinished) brain.EnterState(1);
                break;
        }
    }

    private static object? FindScorpion(JonahBrainState brain, IJonahBrainHost host)
    {
        object? scorpion = host.RecoverScorpion(brain.ScorpionHandle);
        if (scorpion is not null) return scorpion;
        scorpion = host.FindFirstScorpion();
        if (scorpion is not null) brain.ScorpionHandle = host.MakeScorpionHandle(scorpion);
        return scorpion;
    }

    private static void ProcessMessages(JonahBrainState brain, IJonahBrainHost host, object? scorpion)
    {
        if (brain.Messages.Head is null) return;
        JonahState jonah = brain.Jonah;
        for (ThugMessage? message = brain.Messages.Head; message is not null; message = message.Next)
        {
            if ((jonah.State != 8 || message.Code >= 11) && message.Code == 8)
            {
                if (jonah.State != 6 && (scorpion is null || !host.ScorpionContactSignal(scorpion))) continue;
                if ((jonah.SignalFlags & 0x400) == 0) jonah.SignalFlags |= 0x202;
            }
            message.Flags |= 1;
        }
        brain.Messages.Cleanup();
    }

    private static void PlayFrameSound(JonahBrainState brain, IJonahBrainHost host)
    {
        if (brain.Jonah.Animation.ClipId != 10 || brain.Jonah.Animation.Frame is not (4 or 14)) return;
        int sound = host.Random(4) + 173;
        if (sound == brain.LastFootstepSound && ++sound == 177) sound = 173;
        brain.LastFootstepSound = sound;
        host.PlaySound(unchecked((uint)sound) | 0x8000, brain.Position, 0);
    }

    private static void SendWaypointMessage(JonahBrainState brain, IJonahBrainHost host, object scorpion)
    {
        FixedVector3 waypoint = host.WaypointPosition(brain.Jonah.CurrentWaypoint);
        host.TrySendWaypointMessage(scorpion, 9, 2, brain.Position, waypoint);
    }

    private static void SpeakAndAnimate(JonahBrainState brain, IJonahBrainHost host)
    {
        bool alternate = (brain.Jonah.SignalFlags & 0x100) != 0;
        if (unchecked(host.Clock - brain.PleaClockOrAlternateVoice) > 300)
        {
            int first = alternate ? 7 : 4;
            int count = alternate ? 4 : 3;
            int voice = host.Random(count) + first;
            uint previous = alternate ? brain.PleaClockOrAlternateVoice : unchecked((uint)brain.LastRegularPleaVoice);
            if (unchecked((uint)voice) == previous && ++voice >= first + count) voice = first;
            if (alternate) brain.PleaClockOrAlternateVoice = unchecked((uint)voice);
            else brain.LastRegularPleaVoice = voice;
            if (brain.SpeechSuppressionTicks == 0) host.PlayVoice(14, voice, brain.Position, 0);
            brain.PleaClockOrAlternateVoice = host.Clock;
        }
        host.RunAnimation(alternate ? (ushort)5 : (ushort)25, 0, -1);
    }

    private static byte AnimationMode(JonahState jonah) => unchecked((byte)(jonah.IdleAnimations >> 8));
    private static int Decrease(int value, int elapsedTicks) => Math.Max(0, unchecked(value - elapsedTicks));
    private static void RestorePleaAnimations(JonahState jonah)
    { jonah.MovementAnimations = 0x02020202; jonah.IdleAnimations = 0x00000102; }
}
