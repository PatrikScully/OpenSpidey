using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

public interface IRhinoHost
{
    ActorBodyMotion Body { get; }
    ActorProcedureState Actor { get; }
    ActorProcedureList Procedures { get; }
    int AnimationId { get; }
    bool AnimationFinished { get; }
    ushort ItemFlags { get; set; }
    int ElapsedTicks { get; }
    int AttackClock { get; }
    FixedVector3 PlayerPosition { get; }
    int PlayerInputSuppression { get; }
    bool HasMessages { get; }
    bool PlayerIsCrawling { get; }
    int Difficulty { get; }
    int WebDomeCount { get; }
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    void SetHeight(bool initial, int above, int below);
    void RunAnimation(int animation);
    void Neutralize();
    void BuildTransform();
    void ProcessMessages();
    void PlaySounds();
    void DoPhysics(int mode);
    void SetGlobalBoss(bool enabled);
    void SetBossCameraMode();
    int Random(int bound);
    ActorBodyMotion? RecoverPlayerBody();
    void AttachPlayerLookAt(uint completionMask, int rate, int tolerance);
    void PlayPositionalSound(int id);
    bool LineOfSightToPlayer(int argument);
    RhinoWallChunkLists WallChunks { get; }
    bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact);
    void ChunkItemByChecksum(uint checksum);
    void RewindChargePosition(FixedVector3 position);
    void NotifyChargeBarrierVisible(object? collider);
    void ApplyChargeBarrierCollision(int mode);
    void ApplyChargeScrapeDamage();
    void BeginStuckHorn(RhinoChargeWallContact contact, int shake);
    bool IsPlayerBody(object? collider);
    bool TryDetermineFightState(RhinoState state, int argument);
    bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius);
    void HitPlayerFromCharge(FixedVector3 delta);
    ReadOnlySpan<RhinoSpeechEntry> SpeechEntries { get; }
    bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority);
    bool PlayerCanTalkNow();
    void AttachRhinoSpeech(int bank, int slot);
    void AttachPlayerSpeech(int bank, int slot);
}

/// <summary>CRhino constructor (0x47DE40) and AI (0x4819C0) including ChargePlayer (0x47F800).</summary>
public sealed class RhinoBrain(RhinoState state, RhinoStateFlags flags, IRhinoHost host)
{
    public static void Construct(RhinoState state, IRhinoHost host, Action attachToBaddyList, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(attachToBaddyList);
        ArgumentNullException.ThrowIfNull(random);
        host.ItemFlags |= 0x480;
        attachToBaddyList();
        state.AttackRelatedSnapshot = host.AttackClock;
        state.Field344 = unchecked(host.AttackClock - 240);
        state.MinorRadius = 175;
        state.State = 0;
        state.Substate = 0;
        host.Actor.BehaviorFlags |= 0x02000201;
        state.BehaviorFlags = host.Actor.BehaviorFlags;
        state.Health = 1400;
        state.MovementAnimations = RhinoState.SetupWord0;
        state.IdleAnimations = RhinoState.SetupWord1;
        for (int index = 0; index < RhinoState.DazedStarCount; index++)
            state.DazedStarSpeeds[index] = random(4096);
    }

    public void Advance()
    {
        if (state.Field3D0 != 0)
        {
            state.Field3D0 = unchecked(state.Field3D0 - host.ElapsedTicks);
            if (state.Field3D0 < 0) state.Field3D0 = 0;
        }

        if (state.State == 2)
        {
            state.Field3D4 = unchecked(state.Field3D4 + host.ElapsedTicks);
            if (state.Field3D4 > 0x168)
            {
                state.State = 0x14;
                state.Substate = 0;
            }
        }
        else state.Field3D4 = 0;

        if (state.BossCameraEnabled == 0 && host.PlayerInputSuppression == 0)
        {
            host.SetBossCameraMode();
            host.SetGlobalBoss(true);
            state.BossCameraEnabled = 1;
        }

        state.Field35C = unchecked(state.Field35C + 1);

        ushort flag = flags.Check(state.State);
        if ((flag & 4) == 0) host.DoPhysics(0);
        if ((flag & 0x40) != 0)
        {
            if (state.Field324 != 0)
            {
                int dy = unchecked(host.Body.Position.Y - host.PlayerPosition.Y);
                int absDy = dy < 0 ? unchecked(-dy) : dy;
                if (absDy > 0x64000)
                {
                    state.Field324 = unchecked(state.Field324 - host.ElapsedTicks);
                    if (state.Field324 <= 0)
                    {
                        state.Field324 = 0;
                        state.State = 0xD;
                        state.Substate = 0;
                    }
                }
                else state.Field324 = 0;
            }
            if (state.Field328 != 0)
            {
                state.Field328 = unchecked(state.Field328 - host.ElapsedTicks);
                if (state.Field328 <= 0)
                {
                    state.Field328 = 0;
                    state.State = 0xD;
                    state.Substate = 0;
                }
            }
            if (state.Field32C != 0)
            {
                state.Field32C = unchecked(state.Field32C - host.ElapsedTicks);
                if (state.Field32C <= 0)
                {
                    state.Field32C = 0;
                    PlayXAPlease(0xF, 3, 1);
                }
            }
        }
        else
        {
            state.Field328 = 0;
            state.Field324 = 0;
        }

        if (host.HasMessages) host.ProcessMessages();
        host.Procedures.Execute(host.ElapsedTicks);
        state.Field348 = 0;

        if ((flags.Check(state.State) & 0x10) != 0) host.Actor.BehaviorFlags |= 0x10000;
        else host.Actor.BehaviorFlags &= ~0x10000u;

        if (state.State == 0x16)
        {
            state.BodyFlags |= 0x10;
            throw new NotSupportedException("CRhino::DetermineFightState (0x481300) after state22 is not ported yet.");
        }
        else if ((state.State == 2 || state.State == 1) && (host.AttackClock & 3) == 0)
        {
            if (RhinoDetermineFightState.TryAdvance(state, host, argument: 0))
            {
                state.Field328 = 0;
                state.Field324 = 0;
            }
            else if (state.Field328 == 0)
                state.Field328 = Difficulty(0x2710, 0x1B58, 0x1388, 0xBB8);
        }

        host.PlaySounds();
        if (state.State != 0) host.SetHeight(false, 0x64, 0x258);

        switch (state.State)
        {
            case 0: RhinoInit(); host.SetHeight(false, 0x64, 0x258); break;
            case 2: StandStill(); break;
            case 1:
                throw new NotSupportedException("CRhino::FollowWaypoints (0x47F320) is not ported yet.");
            case 6:
                throw new NotSupportedException("CRhino::ChasePlayer (0x47F490) is not ported yet.");
            case 7:
            case 8:
                state.Field358 = host.AttackClock;
                host.Actor.BehaviorFlags |= 0x20000000;
                ChargePlayer();
                break;
            case 9:
                throw new NotSupportedException("CRhino::AttackPlayer (0x480170) is not ported yet.");
            case 10:
            case 11:
                throw new NotSupportedException("CRhino::StuckInWall (0x47E680) is not ported yet.");
            case 12:
                throw new NotSupportedException("CRhino::GetShocked (0x47E420) is not ported yet.");
            case 13:
                throw new NotSupportedException("CRhino::GetLaunched (0x47ECA0) is not ported yet.");
            case 14:
                throw new NotSupportedException("CRhino::TakeHit (0x480820) is not ported yet.");
            case 15:
                throw new NotSupportedException("CRhino::StompGround (0x4810C0) is not ported yet.");
            case 16:
                throw new NotSupportedException("CRhino::GetTrapped (0x480A40) is not ported yet.");
            case 17:
                throw new NotSupportedException("CRhino::HitWall (0x480F50) is not ported yet.");
            case 18:
                throw new NotSupportedException("CRhino::Laugh (0x480090) is not ported yet.");
            case 20:
                throw new NotSupportedException("CRhino::DieRhino (0x480DA0) is not ported yet.");
            case 21:
                throw new NotSupportedException($"CRhino::AI state {state.State} (substate {state.Substate}) is not ported yet; original dispatch 0x4819C0.");
            default:
                throw new NotSupportedException(
                    $"CRhino::AI state {state.State} (substate {state.Substate}) is not ported yet; original dispatch 0x4819C0.");
        }

        state.BehaviorFlags = host.Actor.BehaviorFlags;
        if ((host.ItemFlags & 4) == 0) host.BuildTransform();
    }

    private void RhinoInit()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field358 = unchecked(host.AttackClock - 155);
                int ground = host.GroundHeight(host.Body.Position, 300, 300, 0);
                if (ground == -1) return;
                int height = unchecked(ground - (state.SurfaceClearance << 12));
                host.Body.Position = host.Body.Position with { Y = height };
                state.ShadowPosition = host.Body.Position with { Y = height };
                state.ShadowGroundY = ground;
                state.Substate++;
                PlaySingleAnim(0);
                break;
            case 1:
                state.State = 8;
                state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown Rhino initialization substate {state.Substate}.");
        }
    }

    private void StandStill()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Substate++;
                goto case 1;
            case 1:
                if (host.AnimationId != 0) PlaySingleAnim(0);
                break;
            default:
                throw new InvalidDataException($"Unknown Rhino stand-still substate {state.Substate}.");
        }
    }

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
                if (Difficulty(1, 1, 0, 0) == 0 && !host.LineOfSightToPlayer(1))
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
                host.Procedures.Attach(new ActorAccelerationProcedure(host.Actor,
                    Difficulty(0x50, 0x50, 0x50, 0x50),
                    unchecked(-Difficulty(500, 500, 500, 600)),
                    8));
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
                            host.Procedures.Attach(new ActorAccelerationProcedure(host.Actor, 0x50, 0, 8));
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
                host.Procedures.Attach(new ActorAccelerationProcedure(host.Actor, 0x50, 0, 8));
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

    /// <summary>CRhino::GonnaHitWall motion gate at 0x47E8A0.</summary>
    private int GonnaHitWall(int mode) =>
        RhinoGonnaHitWall.Evaluate(state, host, host.WallChunks, mode, PlayXAPlease);

    private int Difficulty(int easy, int normal, int hard, int alternative) => host.Difficulty switch
    {
        0 => easy,
        1 => normal,
        2 => hard,
        _ => alternative
    };

    private void PlaySingleAnim(int animation)
    {
        state.Field388 = 0;
        host.RunAnimation(animation);
    }

    /// <summary>CRhino::PlayXAPlease (0x47E0A0): a2 bank offset, a3 slot count, a4 Rnd addend only.</summary>
    private void PlayXAPlease(int bankOffset, int slotCount, int rndAddend) =>
        RhinoPlayXAPlease.Execute(state, host.SpeechEntries, host, bankOffset, slotCount, rndAddend);
}
