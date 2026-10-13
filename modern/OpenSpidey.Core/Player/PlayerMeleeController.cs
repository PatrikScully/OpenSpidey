using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>CPlayer::InitiateCombo, UpdateAndTrackCombo and the PunchCombo action dispatcher.</summary>
public sealed class PlayerMeleeController(PlayerMeleeState state, PlayerFightData data, IPlayerMeleeHost host, Func<int, int> random)
{
    private static readonly int[] AttackButtons = [16, 17, 18, 19];
    private static readonly int[] InitialButtons = [0, 1, 2, 3, 16, 17, 18, 19];
    private static readonly int[] EndButtons = [0, 2, 1, 3, 16, 17, 18, 19, 10, 9, 11, 8];
    private static readonly int[] SlideHooks = [6, 5, 1, 0];
    private PlayerCombatState Combat => state.Combat;

    public void Initiate(ushort move, int headStart)
    {
        PlayerComboMove definition = data.Moves[move];
        int half = headStart / 2;
        if (half < 0 || half >= definition.Frames.Count)
            throw new ArgumentOutOfRangeException(nameof(headStart), "Combo head start must index its original frame stream.");
        Array.Clear(state.HitTargets);
        state.HitTargetCount = 0;
        state.FirstContactFrame = true;
        IPlayerMeleeTarget? target = host.SelectTargetBaddy(1024, -4096, 4096, 0);
        if (target is not null)
        {
            FixedVector3 delta = target.Position - Combat.Position;
            short heading = (short)((1024 - GameAngle.Atan2(-(delta.Z >> 12), -(delta.X >> 12))) & 4095);
            state.HeadingDelta = (heading - state.EffectiveHeading) & 4095;
            host.OrientToNormal(false, default);
            state.HeadingDelta = 0;
        }
        state.Move = move;
        foreach (int button in InitialButtons) { state.Input.Triggered[button] = 0; state.Input.Mirrors[button] = 0; }
        state.TransitionAnimation = 0;
        state.DistanceCursor = 0;
        state.TrackingActive = 1;
        state.ComboStartTicks = unchecked(state.AccumulatedTicks - headStart);
        state.LastButtonClock = host.Timer;
        state.FollowOn = null;
        state.CollisionStreamActive = true;
        state.PartsEnabled = definition.Branches.Count != 0;
        state.PartCount = Math.Min(definition.Branches.Count, 16);
        for (int index = 0; index < state.PartCount; index++)
        {
            PlayerComboBranch branch = definition.Branches[index];
            PlayerComboPart part = state.Parts[index];
            part.Active = true;
            part.WaitingFirst = true;
            part.UsesLateWindow = data.Moves[branch.Move].UsesLateWindow;
            part.Started = false;
            part.Flags = branch.Flags != 0 ? 768 : 0;
            part.Input = data.Moves[branch.Move].Input;
            part.InputCursor = 0;
            // The original deliberately does not reset the old last-press clock here.
        }
        RunAnimation(definition.Animation, definition.Frames[half]);
        host.PlaySound(unchecked((ushort)(random(4) + 10)), 0x2000, 0);
    }

    public int Track()
    {
        PlayerComboMove move = data.Moves[state.Move];
        int elapsed = unchecked(state.AccumulatedTicks - state.ComboStartTicks), half = elapsed / 2;
        bool poseApplied = false;
        bool sameAnimation = state.TransitionAnimation != 0 && Combat.AnimationId == state.TransitionAnimation;
        if (!sameAnimation)
        {
            for (int i = 0; i <= half; i++)
            {
                if (move.Frames[i] != 0xFF) continue;
                PlayerComboBranch? followOn = state.FollowOn;
                foreach (int button in EndButtons) state.Input.Triggered[button] = 0;
                foreach (int button in InitialButtons) state.Input.Mirrors[button] = 0;
                foreach (int button in new[] { 10, 9, 11, 8 }) state.Input.Mirrors[button] = 0;
                state.CollisionStreamActive = false;
                state.TrackingActive = 0;
                if (followOn is null) return 0;
                Initiate(followOn.Move, followOn.HeadStart);
                return followOn.Move;
            }
            Combat.AnimationFrame = move.Frames[half];
        }

        ApplyDistance(sameAnimation ? Combat.AnimationFrame : half, ref poseApplied);
        LatchDirection(state.Input.Sideways, state.Input.PreviousSideways, 11, 10);
        LatchDirection(state.Input.Forward, state.Input.PreviousForward, 9, 8);
        int buttons = state.Input.Triggered[17] | (2 * (state.Input.Triggered[19] |
            (2 * (state.Input.Triggered[18] | (2 * state.Input.Triggered[16])))));
        if (buttons != 0) state.LastButtonClock = host.Timer;
        bool hasParts = state.PartsEnabled;
        ClearAttackButtons();
        if (hasParts && buttons != 0)
        {
            int matched = 0;
            bool won = false;
            for (int index = 0; index < state.PartCount; index++)
            {
                PlayerComboPart part = state.Parts[index];
                if (!part.Active) continue;
                int low = part.UsesLateWindow ? move.LateInputStart : move.InputStart;
                int high = part.UsesLateWindow ? move.LateInputEnd : move.InputEnd;
                if (low > elapsed || high < elapsed) continue;
                int bit = 1 << part.Input[part.InputCursor];
                matched |= bit;
                if ((buttons & bit) == 0) continue;
                if (bit == 1) part.Started = true;
                else if (part.Started && state.Input.Pressed[17] == 0) { part.Active = false; continue; }
                if (part.WaitingFirst)
                {
                    part.WaitingFirst = false;
                    part.LastPressTicks = state.AccumulatedTicks;
                    part.InputCursor++;
                }
                else if (unchecked((uint)(state.AccumulatedTicks - part.LastPressTicks)) > part.Input[part.InputCursor + 1])
                    part.Active = false;
                else { part.LastPressTicks = state.AccumulatedTicks; part.InputCursor += 2; }
                if (part.Input[part.InputCursor] != 0xFF) continue;
                state.FollowOn = move.Branches[index];
                state.TransitionAnimation = state.FollowOn.TransitionAnimation;
                state.PartsEnabled = false;
                won = true;
                break;
            }
            if (!won && (matched | buttons) != matched) state.PartsEnabled = false;
        }
        else if (!hasParts)
        {
            if (move.InputStart > elapsed) ClearAttackButtons();
            else if (state.FollowOn is { } followOn && elapsed >= followOn.ExitTick)
            {
                bool startFollowOn = true;
                if (state.TransitionAnimation != 0)
                {
                    if (Combat.AnimationId != state.TransitionAnimation)
                    {
                        state.DistanceCursor = 0;
                        RunAnimation(state.TransitionAnimation, 0);
                        poseApplied = false;
                        startFollowOn = false;
                    }
                    else if (!state.AnimationFinished) startFollowOn = false;
                }
                if (startFollowOn) { Initiate(followOn.Move, followOn.HeadStart); return followOn.Move; }
            }
        }
        if (move.CollisionStart > elapsed || move.CollisionEnd < elapsed) return 1;
        if (!poseApplied) host.ApplyPlayerPose();
        if (!state.CollisionStreamActive) throw new InvalidOperationException("Combo collision stream has already been released.");
        for (int part = 0; move.CollisionHooks[part] != 0xFF; part++)
        {
            if (!state.FirstContactFrame) state.PreviousContactHooks[part] = state.CurrentContactHooks[part];
            state.CurrentContactHooks[part] = host.GetPlayerDynamicHookPosition(new(0, 0, 0, move.CollisionHooks[part]));
        }
        if (state.FirstContactFrame) { state.FirstContactFrame = false; return 1; }
        for (IPlayerMeleeTarget? target = host.BaddyHead; target is not null; target = target.Next)
        {
            if ((target.ItemFlags & 2) == 0 || target.Type == 316) continue;
            bool hitBefore = state.HitTargets.Take(state.HitTargetCount).Any(identity => ReferenceEquals(identity, target.Identity));
            if (hitBefore || unchecked((int)ActorLifecycle.Distance(Combat.Position, target.Position)) >= 700) continue;
            host.BuildTargetTransform(target);
            for (int part = 0; move.CollisionHooks[part] != 0xFF; part++)
            {
                FixedVector3 start = state.PreviousContactHooks[part];
                FixedVector3 delta = state.CurrentContactHooks[part] - start;
                FixedVector3 end = start + delta + (delta >> 1);
                if (!host.CollideWithTarget(target, start, end, 0x1800, out ActorHook hook)) continue;
                FixedVector3 impact = host.GetTargetDynamicHookPosition(target, hook);
                ushort amount = unchecked((ushort)GetDamage(move.Damage));
                FixedVector3 direction = FixedVectorMath.Normalize(new(unchecked(end.X - start.X) >> 12, 0,
                    unchecked(end.Z - start.Z) >> 12));
                if (Combat.Field5AC != 0 && Combat.AnimationId is 100 or 102 or 104 or 106)
                { Combat.Field5AC = unchecked(Combat.Field5AC - 1); amount = unchecked((ushort)(2 * amount)); }
                byte flags = (byte)((move.HitFlags != 0 ? 0x40 : 0) | 0x0F);
                if (move.SlideDistance != 0) flags |= 0x10;
                var hit = new ActorHit(flags, unchecked((byte)hook.PartIndex), 0, amount, direction,
                    move.SlideDistance != 0 ? move.SlideDistance : (short)0, move.SlideDistance != 0 ? move.SlideFrames : (ushort)0);
                if (host.CameraFlag180 && !Combat.IsCrawling) host.PutCameraBehind(0);
                if (host.HitTarget(target, hit))
                {
                    if (state.HitSessionStarted == 0)
                    { state.HitSessionStarted = 1; state.HitSessionHealth = Combat.Health; state.HitSessionClock = host.Timer; }
                    Combat.Field534 = 360;
                    Combat.Field52C = unchecked((Combat.Field528 + 11) << 10);
                    (ushort sound, int kind) = Combat.AnimationId switch
                    {
                        106 or 113 => ((ushort)17, 2),
                        104 or 111 => ((ushort)16, 1),
                        _ => (unchecked((ushort)(random(2) + 14)), 0)
                    };
                    host.PlayPositionSound(sound, impact, 0);
                    host.CreateCombatImpactEffect(impact, kind);
                }
                // The original remembers a contact even if the target refused its Hit.
                if (state.HitTargetCount < 4) state.HitTargets[state.HitTargetCount++] = target.Identity;
                return 1;
            }
        }
        return 1;
    }

    public void UpdatePunchCombo()
    {
        Combat.FieldEA6 = 0;
        if (host.CheckGroundGone() || host.CheckJump()) return;
        if (state.Move is 0 or 1 && state.Input.Pressed[17] != 0 &&
            (state.Input.Pressed[18] != 0 || state.Input.Pressed[19] != 0) &&
            unchecked(host.Timer - (uint)state.AttackEntryClock) <= 4)
        {
            state.ZipDiveHandle = host.StoreTargetHandle(host.SelectTargetBaddy(0xBE, -4096, 4096, 0));
            state.ZipDiveClock = unchecked((int)host.Timer);
            Combat.ActionState = 0x02000000;
            host.PlaySingleAnimation(0x78, 0, -1);
            return;
        }
        int result = Track();
        if (result == 0) { state.HeadingDelta = 0; host.SwitchToStandMode(); return; }
        if (result == 2)
        { state.WebAttackMode = 1; Combat.ActionState = 0x4000; }
        else if (result == 3)
        { state.WebAttackMode = 2; Combat.ActionState = 0x8000; state.WebFiringSide = state.Input.Forward > 0 ? (byte)2 : state.Input.Forward < 0 ? (byte)1 : (byte)0; }
        else if (result == 6)
        { state.WebAttackMode = 2; Combat.ActionState = 0x10000; }
        else return;
        state.WebAttackSubstate = 0;
        state.WebAttackStarted = 0;
        if (state.AimTarget is null) host.LockTargetTorsoAngle();
        else host.SetTargetTorsoAngle(state.AimTarget.Position);
        state.AnimationFlags &= 0xFE;
    }

    public int GetDamage(int damage)
    {
        if (host.Suit is 2 or 3 or 4) damage = unchecked(damage * 2);
        if (host.Difficulty == 2) return damage;
        if (host.Difficulty == 0) return unchecked(damage << 13) >> 12;
        int triple = unchecked(damage * 3);
        return host.Difficulty == 1 ? unchecked(triple << 11) >> 12 : unchecked(triple << 10) >> 12;
    }

    private void RunAnimation(ushort animation, int frame)
    {
        host.SelectAndResetAnimationSounds(animation);
        host.RunAnimation(animation, frame, -1);
        Combat.AnimationId = animation;
        Combat.AnimationFrame = frame;
        state.AnimationFinished = false;
    }
    private void ClearAttackButtons()
    { foreach (int button in AttackButtons) { state.Input.Triggered[button] = 0; state.Input.Mirrors[button] = 0; } }
    private void LatchDirection(int current, int previous, int positive, int negative)
    {
        int index = current > 0 && previous <= 0 ? positive : current < 0 && previous >= 0 ? negative : -1;
        if (index >= 0) { state.Input.Triggered[index] = 1; state.Input.Mirrors[index] = 1; }
    }
    private void ApplyDistance(int bound, ref bool poseApplied)
    {
        if (!data.Distances.TryGetValue(Combat.AnimationId, out IReadOnlyList<byte>? distance)) return;
        int accum = 0, cursor = state.DistanceCursor, hook = -1;
        bool keepY = false;
        if (cursor > bound) state.DistanceCursor = unchecked((ushort)(bound + 1));
        else
        {
            while (true)
            {
                byte value = distance[cursor];
                if (value == 0xFF) { state.DistanceCursor = unchecked((ushort)cursor); break; }
                if (value is >= 0x80 and <= 0x87) { hook = value & 3; keepY = value < 0x84; }
                else { hook = -1; accum = unchecked(accum + (sbyte)value); }
                if (++cursor > bound) { state.DistanceCursor = unchecked((ushort)(bound + 1)); break; }
            }
        }
        if (hook < 0 && accum == 0) return;
        host.ApplyPlayerPose();
        poseApplied = true;
        FixedVector3 start = Combat.Position + new FixedVector3(0, 0x20000, 0), forward32 = Combat.ForwardDirection * 32;
        if (host.LineToBaddySphere(start, start - forward32, forward32, 0, 2048)) return;
        FixedVector3 forward128 = Combat.ForwardDirection * 128, right32 = state.RightDirection * 32;
        if (host.TraceWorld(start, start - right32 - forward128, 1)) return;
        if (host.TraceWorld(start, start + right32 - forward128, 1)) return;
        FixedVector3 upper = start + Combat.UpDirection * 64;
        if (host.TraceWorld(upper, upper - forward128, 1)) return;
        if (hook >= 0)
        {
            FixedVector3 slide = host.GetPlayerHookPosition(SlideHooks[hook]) - state.SlideHookOrigins[hook];
            if (!keepY) slide = slide with { Y = 0 };
            Combat.Position -= slide;
        }
        else Combat.Position -= Combat.ForwardDirection * accum;
    }
}
