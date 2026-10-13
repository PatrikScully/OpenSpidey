using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugHostageHost : IThugRangedHost
{
    int DifficultyLevel { get; }
    ActorProcedureState? RecoverHostage();
    ActorBodyMotion? RecoverHostageBody(ActorProcedureState hostage);
    void SendHostageMessage(ActorProcedureState hostage, int code);
    void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition);
    void DrawHostageTracer(ThugShotLine line, ActorProcedureState hostage, byte red, byte green, byte blue);
}

/// <summary>Recovered CThug::ShootHostage state machine and target-specific thug-model shot.</summary>
public static class ThugHostageBehavior
{
    private static readonly ushort[] ShotFrames = [2, 6, 10, 14];

    public static void Step(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugHostageHost host, Func<int, int> random)
    {
        ActorProcedureState? hostage = host.RecoverHostage();
        if (hostage is null || (hostage.BehaviorFlags & 0x1000) != 0)
        {
            host.Neutralize();
            Recover(state);
            return;
        }
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                procedures.Attach(new ThugStateSwitchMessageProcedure(state, 17, host.SendSelfMessage));
                state.TurnRate = 160;
                ushort animation = state.Type == 304 ? (ushort)8 : (ushort)7;
                host.RunAnimation(animation, host.AnimationId == animation ? host.AnimationFrame : 0);
                procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => host.RecoverHostageBody(hostage), 2, 70, 200));
                state.Substate++;
                break;
            case 1:
                if ((actor.CompletionFlags & 2) != 0)
                {
                    actor.CompletionFlags &= ~2u;
                    host.PlaySound(unchecked((ushort)((random(3) + 14) | 0x8000)));
                    state.Substate++;
                }
                break;
            case 2:
                if (host.AnimationFinished != 0)
                {
                    if (host.LineOfSight(actor.Body.Position, hostage.Body.Position, 0))
                    {
                        host.SendHostageMessage(hostage, 2);
                        state.PauseUpdates = host.DifficultyLevel == 2 ? unchecked(random(50) + 75) : unchecked(random(32) + 49);
                        state.Substate++;
                        host.CycleAnimation(state.Type == 304 ? (ushort)4 : (ushort)1);
                    }
                    else Recover(state);
                }
                break;
            case 3:
                DrawShot(state, actor, host, hostage, random);
                state.PauseUpdates = unchecked(state.PauseUpdates - 1);
                if (state.PauseUpdates <= 0)
                {
                    // DrawShot may invalidate the stored handle; preserve the source's second recovery query.
                    hostage = host.RecoverHostage();
                    if (hostage is null || !host.LineOfSight(actor.Body.Position, hostage.Body.Position, 0))
                    {
                        Recover(state);
                        break;
                    }
                    host.SelectHostageCamera(actor.Body.Position, hostage.Body.Position);
                    host.RunAnimation(state.Type == 304 ? (ushort)7 : (ushort)8);
                    host.SendHostageMessage(hostage, 3);
                    state.Substate++;
                }
                break;
            case 4:
                DrawShot(state, actor, host, hostage, random);
                if (host.AnimationFinished != 0)
                {
                    host.PlaySound(0x8024);
                    state.State = 10;
                    state.Substate = 0;
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug hostage substate {state.Substate}.");
        }
    }

    private static void Recover(ThugState state) { state.State = 28; state.Substate = 0; }

    public static int DrawShot(ThugState state, ActorProcedureState actor, IThugHostageHost host,
        ActorProcedureState hostage, Func<int, int> random)
    {
        // The alternate source model ignores this target and still auto-aims at the player.
        if (state.Type != 304) return ThugRangedBehavior.DrawPlayerShot(state, actor, host, random, checkPlayer: false);
        host.ClearLaser();
        var line = new ThugShotLine { Start = host.GetHookPosition(1), End = host.GetHookPosition(0) };
        FixedVector3 direction = line.End - line.Start;
        line.Start += direction;
        direction <<= 6;
        line.End += direction;
        if (host.AnimationId != 7) return 0;
        uint shot = (state.CombatFlags >> 12) & 7;
        if (shot >= ShotFrames.Length || host.AnimationFrame < ShotFrames[shot]) return 0;
        if (state.GunSoundCooldownTicks == 0) state.GunSoundDelayTicks = 66;
        host.PlayRangedSound(unchecked((ushort)((random(2) + 2) | 0x8000)), 500);
        state.CombatFlags = (state.CombatFlags & ~0x7000u) | ((shot + 1) << 12);
        line.End = hostage.Body.Position with { Y = unchecked(hostage.Body.Position.Y - (40 << 12)) };
        direction = line.End - line.Start;
        int hit = host.LaserCollision(line, ref direction, checkPlayer: false);
        if (hit == -1) { host.ClearLaser(); return 0; }
        host.DrawHostageTracer(line, hostage, 100, 100, 255);
        if ((host.ItemFlags & 0x8000) == 0) host.DrawBarrelFlash(line, 100, 100, 255);
        return hit;
    }
}
