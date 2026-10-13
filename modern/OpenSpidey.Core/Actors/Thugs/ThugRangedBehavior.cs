using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public sealed class ThugShotLine
{
    public FixedVector3 Start { get; set; }
    public FixedVector3 End { get; set; }
}

public enum ThugProjectileTargetList { Baddies }
public readonly record struct ThugGrenadeRequest(FixedVector3 Start, FixedVector3 Target, int Speed, int Damage,
    int Radius, ThugProjectileTargetList TargetList, uint ModelChecksum, byte ExplodeOnContact, byte Flag);

public interface IThugRangedHost : IThugAttackHost, IThugFightHost
{
    ushort ItemFlags { get; }
    FixedVector3 GetHookPosition(int index);
    FixedVector3 GetDynamicHookPosition(short partX, short partY, short partZ, int offset);
    void RotateTorsoToAimAtPlayer(FixedVector3 origin);
    int LaserCollision(ThugShotLine line, ref FixedVector3 direction, bool checkPlayer);
    void ClearLaser();
    void SetLaser(FixedVector3 start, FixedVector3 end);
    void DrawTracer(ThugShotLine line, bool attachToPlayer, byte red, byte green, byte blue);
    void DrawBarrelFlash(ThugShotLine line, byte red, byte green, byte blue);
    void DrawGlowFlash(FixedVector3 position, IReadOnlyList<int> constructorArguments);
    void PlayRangedSound(ushort sound, int attenuation);
    void ApplyPlayerHit(uint flags, int damage);
    void Victorious();
    void SpawnGrenade(ThugGrenadeRequest request);
    void SendSelfMessage(int code);
}

/// <summary>Source shooting/grenade substates and frame-gated barrel schedules with explicit world/pose effects.</summary>
public static class ThugRangedBehavior
{
    private static readonly ushort[] ThugShotFrames = [2, 6, 10, 14];
    private static readonly ushort[] HenchmanShotFrames = [6, 11];
    private static readonly IReadOnlyList<int> HenchmanFlashArguments =
        Array.AsReadOnly(new[] { 5, 255, 128, 0, 32, 0, 0, 0, 0, 50, 20, 1, 20, 10, 40, 20, 10, 1 });

    public static void ShootPlayer(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugRangedHost host, Func<int, int> random, int elapsedTicks)
    {
        switch (state.Substate)
        {
            case 0:
                state.AlertTicks = 600;
                state.TurnRate = 160;
                host.Neutralize();
                if (ThugAnimationBehavior.SetMode(state, 1, true, host)) state.Substate = 6;
                else
                {
                    state.CombatFlags &= ~0x7000u;
                    procedures.Attach(new ThugStateSwitchMessageProcedure(state, 12, host.SendSelfMessage));
                    procedures.Attach(new ThugStateSwitchMessageProcedure(state, 17, host.SendSelfMessage));
                    procedures.Attach(ActorLookAtProcedure.ForBody(actor, host.RecoverPlayerBody, 2, 130, 4));
                    host.CycleAnimation(state.Type != 304 ? (ushort)1 : (ushort)4);
                    state.ActionCounter = 120;
                    state.Substate++;
                }
                break;
            case 1:
                state.ActionCounter = Math.Max(0, unchecked(state.ActionCounter - elapsedTicks));
                if (state.ActionCounter != 0)
                {
                    if (host.PlayerHealth <= 0 || host.Player.HoldsObject) goto FinishShooting;
                    DrawPlayerShot(state, actor, host, random);
                    if ((actor.CompletionFlags & 2) != 0)
                    {
                        actor.CompletionFlags &= ~2u;
                        state.Substate++;
                        state.AimPlayerY = host.Player.Position.Y;
                        state.PauseUpdates = 15;
                        state.ActionCounter = unchecked(random(3) + 1);
                    }
                }
                else
                {
                    host.Neutralize();
                    state.State = 4;
                    state.Substate = 0;
                }
                break;
            case 2:
                if (host.PlayerHealth <= 0 || host.Player.HoldsObject) goto FinishShooting;
                DrawPlayerShot(state, actor, host, random);
                int pause = state.PauseUpdates;
                state.PauseUpdates = unchecked(pause - 1);
                if (pause == 7)
                {
                    if (!host.Player.HoldsObject) host.PlayRangedSound(0x801D, 500);
                }
                else if (pause == 1)
                {
                    state.Substate++;
                    ushort clip = state.Type == 304 ? (ushort)8 : (ushort)7;
                    host.RunAnimation(clip, host.AnimationId == clip ? host.AnimationFrame : 0);
                }
                break;
            case 3:
                if (host.AnimationFinished != 0)
                {
                    host.RunAnimation(state.Type != 304 ? (ushort)8 : (ushort)7);
                    state.Substate++;
                }
                break;
            case 4:
                if (host.AnimationFinished != 0)
                {
                    state.ActionCounter = unchecked(state.ActionCounter - 1);
                    if (state.ActionCounter > 0 && ThugPerception.PlayerIsVisible(state, actor, host))
                    {
                        if (state.Type != 304 && host.DistanceToPlayer(2) < 650 && HeightWithin())
                        {
                            state.Substate++;
                            break;
                        }
                        state.CombatFlags &= ~0x7000u;
                        host.RunAnimation(state.Type != 304 ? (ushort)8 : (ushort)7);
                    }
                    else { state.Substate++; break; }
                }
                if (host.Player.HoldsObject) goto FinishShooting;
                if (DrawPlayerShot(state, actor, host, random) != 0)
                {
                    host.ApplyPlayerHit(4, state.Field38C);
                    if (host.PlayerHealth <= 0) host.Victorious();
                }
                if (state.Type == 304 && (host.AttackClock & 3) == 0 && host.DistanceToPlayer(2) < 650 && HeightWithin())
                    state.Substate++;
                break;
            case 5:
                host.ClearLaser();
                goto FinishShooting;
            case 6:
                if (host.AnimationFinished != 0) state.Substate = 0;
                break;
            default: throw new InvalidOperationException($"Unknown thug shooting substate {state.Substate}.");
        }
        return;
    FinishShooting:
        state.State = 10;
        state.Substate = 0;

        bool HeightWithin() => ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) < 409600;
    }

    public static void TossGrenade(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugRangedHost host, Func<int, int> random)
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.RangedCooldownTicks = 1200;
                procedures.Attach(ActorLookAtProcedure.ForBody(actor, host.RecoverPlayerBody, 2, 100, 0));
                state.Substate++;
                break;
            case 1:
                if ((actor.CompletionFlags & 2) != 0)
                {
                    actor.CompletionFlags &= ~2u;
                    host.RunAnimation(31);
                    state.Substate++;
                }
                break;
            case 2:
                if (host.AnimationFrame >= 37)
                {
                    host.GetDynamicHookPosition(0, 0, 0, 14);
                    host.SpawnGrenade(new(actor.Body.Position, host.Player.Position, 1024 / unchecked(random(8) + 24),
                        state.Field388, 512, ThugProjectileTargetList.Baddies, 1989287857, 0, 0));
                    state.CombatFlags |= 1;
                    state.GrenadeElapsedTicks = unchecked(450 - random(150));
                    state.Substate++;
                }
                break;
            case 3:
                if (host.AnimationFinished != 0) { state.State = 28; state.Substate = 0; }
                break;
            default: throw new InvalidOperationException($"Unknown thug grenade substate {state.Substate}.");
        }
    }

    public static int DrawPlayerShot(ThugState state, ActorProcedureState actor, IThugRangedHost host,
        Func<int, int> random, bool checkPlayer = true)
    {
        host.ClearLaser();
        if (state.Type != 304) return DrawHenchmanShot(state, host, random, checkPlayer);
        var line = new ThugShotLine { Start = host.GetHookPosition(1), End = host.GetHookPosition(0) };
        FixedVector3 direction = line.End - line.Start;
        line.Start += direction;
        direction <<= 6;
        line.End += direction;
        if (checkPlayer) host.RotateTorsoToAimAtPlayer(line.Start);
        if (host.AnimationId != 7) return 0;
        uint shot = (state.CombatFlags >> 12) & 7;
        if (shot >= 4 || host.AnimationFrame < ThugShotFrames[shot]) return 0;
        if (state.GunSoundCooldownTicks == 0) state.GunSoundDelayTicks = 66;
        host.PlayRangedSound(unchecked((ushort)((random(2) + 2) | 0x8000)), 500);
        state.CombatFlags = (state.CombatFlags & ~0x7000u) | ((shot + 1) << 12);
        if (checkPlayer) direction = AutoAim(state, line, host.Player.Position, state.Field380, random);
        int hit = host.LaserCollision(line, ref direction, checkPlayer);
        if (hit == -1) { host.ClearLaser(); return 0; }
        host.DrawTracer(line, hit != 0, 100, 100, 255);
        if ((host.ItemFlags & 0x8000) == 0) host.DrawBarrelFlash(line, 100, 100, 255);
        return hit;
    }

    private static int DrawHenchmanShot(ThugState state, IThugRangedHost host, Func<int, int> random, bool checkPlayer)
    {
        if (host.AnimationId != 8) return 0;
        uint shot = (state.CombatFlags >> 12) & 7;
        if (shot >= 2 || host.AnimationFrame < HenchmanShotFrames[shot]) return 0;
        if (state.GunSoundCooldownTicks == 0) state.GunSoundDelayTicks = 66;
        host.PlayRangedSound(unchecked((ushort)((random(2) + 9) | 0x8000)), 500);
        shot++;
        state.CombatFlags = (state.CombatFlags & ~0x7000u) | (shot << 12);
        int startHook = shot == 1 ? 2 : 0;
        int endHook = shot == 1 ? 3 : 1;
        var line = new ThugShotLine { Start = host.GetHookPosition(startHook), End = host.GetHookPosition(endHook) };
        FixedVector3 direction = line.End - line.Start;
        line.Start += direction * 3;
        direction <<= 8;
        line.End += direction;
        // Alternate model calls AutoAim even when checkPlayer is zero.
        direction = AutoAim(state, line, host.Player.Position, state.Field380, random);
        int hit = host.LaserCollision(line, ref direction, checkPlayer);
        if (hit == -1) return 0;
        host.DrawTracer(line, shot == 1 && hit != 0, 255, 128, 0);
        if ((host.ItemFlags & 0x8000) != 0) return hit;
        host.DrawGlowFlash(line.Start, HenchmanFlashArguments);
        host.SetLaser(line.Start, line.End);
        return hit;
    }

    public static FixedVector3 AutoAim(ThugState state, ThugShotLine line, FixedVector3 playerPosition,
        int spread, Func<int, int> random)
    {
        FixedVector3 direction = playerPosition - line.Start;
        state.AimPlayerY = unchecked(((unchecked(playerPosition.Y - state.AimPlayerY)) >> (state.Type != 304 ? 2 : 1)) + state.AimPlayerY);
        direction = direction with { Y = unchecked(direction.Y + state.AimPlayerY - playerPosition.Y) };
        while (unchecked(ThugPerception.SourceAbs(direction.Z) + ThugPerception.SourceAbs(direction.X)) < 0xBB8000)
        {
            if ((direction.X | direction.Z) == 0)
                throw new InvalidOperationException("Source auto aim cannot terminate for a zero horizontal direction.");
            direction <<= 1;
            spread = unchecked(spread << 1);
        }
        if (direction.X > direction.Z)
            direction = direction with { Z = unchecked(direction.Z + ((random(spread) - (spread >> 1)) << 12)) };
        else direction = direction with { X = unchecked(direction.X + ((random(spread) - (spread >> 1)) << 12)) };
        direction = direction with { Y = unchecked(direction.Y + ((random(spread) - (spread >> 1)) << 12)) };
        line.End = line.Start + direction;
        return direction;
    }
}
