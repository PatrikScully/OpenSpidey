using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

public sealed partial class RhinoBrain
{
    /// <summary>CRhino::ChargePlayer at 0x47F800. Public for original-exe fixture replay.</summary>
    public void ChargePlayer()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field310 = 0x64;
                host.Neutralize();
                host.AttachPlayerLookAt(2, 0x50, 0);
                state.Field330 = unchecked(host.Random(30) + 0x5A);
                if (state.State == 8) state.Substate = 2;
                else
                {
                    PlaySingleAnim(9);
                    host.PlayPositionalSound(0x8049);
                    host.PlayPositionalSound(0x804A);
                    state.Field334 = unchecked(host.Random(30) + 0x3C);
                    state.Substate++;
                }
                break;
            case 1:
                state.Field334 = unchecked(state.Field334 - host.ElapsedTicks);
                if (state.Field334 < 0) state.Field334 = 0;
                host.DoPhysics(0);
                if (!host.AnimationFinished) break;
                if (Difficulty(1, 1, 0, 0) == 0 && !LineOfSightCheck(host.PlayerPosition, 1))
                {
                    state.State = 0x16;
                    state.Substate = 0;
                    break;
                }
                if (state.Field334 != 0)
                {
                    PlaySingleAnim(9);
                    host.PlayPositionalSound(0x8049);
                }
                else state.Substate++;
                break;
            case 2:
                host.DoPhysics(0);
                state.Field330 = unchecked(state.Field330 - host.ElapsedTicks);
                if ((host.Actor.CompletionFlags & 2) != 0)
                    host.Actor.CompletionFlags &= ~2u;
                else if (state.Field330 > 0) return;
                state.Field330 = 0;
                if (host.PlayerIsCrawling)
                {
                    state.State = 0xD;
                    state.Substate = 0;
                    break;
                }
                PlaySingleAnim(4);
                host.PlayPositionalSound(0x8042);
                state.State = 8;
                state.Substate++;
                break;
            case 3:
                if (!host.AnimationFinished) break;
                PlaySingleAnim(5);
                host.AttachAcceleration(Difficulty(0x50, 0x50, 0x50, 0x50),
                    unchecked(-Difficulty(500, 500, 500, 600)),
                    8);
                state.CombatFlags &= ~2u;
                state.Field334 = ThugPerception.ApproximateHorizontalDistance(host.Body.Position, host.PlayerPosition);
                state.Substate = 5;
                break;
            case 5:
                if ((GonnaHitWall(1) & 0xD) != 0) break;
                if (host.AnimationFinished && (host.AnimationId == 5 || host.AnimationId == 0xC))
                    PlaySingleAnim(5);
                {
                    int dist = ThugPerception.ApproximateHorizontalDistance(host.Body.Position, host.PlayerPosition);
                    if (dist < Difficulty(0x258, 0x190, 0x154, 0x12C))
                    {
                        host.Procedures.Mark(category: 0x100);
                        host.Body.AngularVelocity = host.Body.AngularVelocity with { Y = 0 };
                        host.Body.AngularAcceleration = host.Body.AngularAcceleration with { Y = 0 };
                    }
                    if (host.WebDomeCount != 0)
                    {
                        if (host.ChargeLineHitsPlayer(state.PreviousPosition, host.Body.Position, 0x2AF8))
                        {
                            state.State = 9;
                            state.Substate = 0;
                            return;
                        }
                    }
                    else if (host.ChargeLineHitsPlayer(state.PreviousPosition, host.Body.Position, 0x1800))
                    {
                        host.HitPlayerFromCharge(host.PlayerPosition - host.Body.Position);
                        PlaySingleAnim(0xC);
                        state.CombatFlags |= 1;
                        host.Procedures.Mark(category: 0x100);
                        state.Substate = 0xA;
                        state.Field330 = 5;
                        state.Field344 = host.AttackClock;
                        return;
                    }
                    if ((host.Actor.CompletionFlags & 8) != 0)
                    {
                        host.Actor.CompletionFlags &= ~8u;
                        state.CombatFlags |= 2;
                    }
                    if (host.AnimationFinished || ((state.CombatFlags & 2) != 0 && state.Field334 < dist))
                    {
                        state.Field330 = 10;
                        if (state.Field334 < 300) PlayXAPlease(0, 3, 1);
                        state.Substate = 0xA;
                        state.CombatFlags &= ~1u;
                    }
                    else
                    {
                        state.Field330 = unchecked(state.Field330 + host.ElapsedTicks);
                        state.Field334 = dist;
                        if (state.Field330 >= 0x12C)
                        {
                            PlaySingleAnim(8);
                            host.AttachAcceleration(0x50, 0, 8);
                            state.Substate = 7;
                        }
                    }
                }
                break;
            case 7:
                if ((GonnaHitWall(1) & 0xD) != 0) break;
                if ((host.Actor.CompletionFlags & 8) == 0) break;
                host.Actor.CompletionFlags &= ~8u;
                host.Neutralize();
                state.BodyFlags |= 0x10;
                state.State = 2;
                state.Substate = 0;
                break;
            case 10:
                if ((GonnaHitWall(1) & 0xD) != 0) break;
                state.Field330 = unchecked(state.Field330 - host.ElapsedTicks);
                if (state.Field330 > 0) break;
                PlaySingleAnim(8);
                host.AttachAcceleration(0x50, 0, 8);
                state.Substate++;
                break;
            case 11:
                if ((GonnaHitWall(1) & 0xD) != 0) break;
                if ((host.Actor.CompletionFlags & 8) == 0) break;
                host.Actor.CompletionFlags &= ~8u;
                if ((state.CombatFlags & 1) == 0)
                {
                    state.State = 2;
                    state.Substate = 0;
                    break;
                }
                host.Neutralize();
                host.AttachPlayerLookAt(2, 0x1E, 0);
                PlaySingleAnim(0x14);
                host.PlayPositionalSound(((host.AttackClock & 1) == 0 ? 1 : 0) | 0x8046);
                state.BodyFlags |= 0x10;
                state.Substate++;
                break;
            case 12:
                if (host.AnimationFinished)
                {
                    state.State = 2;
                    state.Substate = 0;
                }
                break;
            default:
                throw new InvalidDataException($"Unknown Rhino ChargePlayer substate {state.Substate}.");
        }
    }
}
