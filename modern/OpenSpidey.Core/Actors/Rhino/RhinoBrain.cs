using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>An environmental-list body seen by StompGround (type 401 is CManipOb, whose vtable slot5 is Smash 0x456D40).</summary>
public readonly record struct RhinoEnvironmentBody(object Identity, int Type, FixedVector3 Position);

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
    bool PlayerIsCrawling { get; }
    int Difficulty { get; }
    int WebDomeCount { get; }
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    void SetHeight(bool initial, int above, int below);
    void RunAnimation(int animation);
    void Neutralize();
    void BuildTransform();
    void DoPhysics(int mode);
    void SetGlobalBoss(bool enabled);
    void SetBossCameraMode();
    int Random(int bound);
    ActorBodyMotion? RecoverPlayerBody();
    void AttachPlayerLookAt(uint completionMask, int rate, int tolerance);
    void PlayPositionalSound(int id);
    RhinoWallChunkLists WallChunks { get; }
    bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact);
    void ChunkItemByChecksum(uint checksum);
    bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius);
    void HitPlayerFromCharge(FixedVector3 delta);
    ReadOnlySpan<RhinoSpeechEntry> SpeechEntries { get; }
    bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority);
    bool PlayerCanTalkNow();
    void AttachRhinoSpeech(int bank, int slot);
    void AttachPlayerSpeech(int bank, int slot);

    /// <summary>CAIProc_AccZ (0x4016B0).</summary>
    void AttachAcceleration(int acceleration, int targetSpeed, uint completionMask) =>
        Procedures.Attach(new ActorAccelerationProcedure(Actor, acceleration, targetSpeed, completionMask));
    /// <summary>CAIProc_MoveTo (0x4018D0) from an SMoveToInfo {destination, speed, turn rate, tolerance}.</summary>
    void AttachMoveTo(FixedVector3 destination, int speed, int turnRate, int tolerance, uint completionMask) =>
        Procedures.AttachMove(new ActorMoveToProcedure(Actor, destination, speed, turnRate, tolerance, completionMask));
    /// <summary>CAIProc_LookAt yaw form (0x4012F0).</summary>
    void AttachLookAtYaw(int yaw, uint completionMask, int rate, int tolerance) =>
        Procedures.Attach(ActorLookAtProcedure.ForYaw(Actor, yaw, completionMask, rate, tolerance));
    /// <summary>CAIProc_StateSwitchSendMessage (0x401E60) sending <paramref name="message"/> to this Rhino.</summary>
    void AttachStateSwitchMessage(RhinoState state, int message) =>
        Procedures.Attach(new RhinoStateSwitchMessageProcedure(state, message, code => Messages.Enqueue(new ThugMessage(code, this))));
    /// <summary>CRhino::PlayXAPlease (0x47E0A0).</summary>
    void PlayXAPlease(RhinoState state, int bankOffset, int slotCount, int rndAddend) =>
        RhinoPlayXAPlease.Execute(state, SpeechEntries, this, bankOffset, slotCount, rndAddend);

    // Boundaries of the remaining CRhino::AI leaves. Defaults fail loudly for fixture hosts that never reach them.
    /// <summary>CItem 0x128 mFrame.</summary>
    int AnimationFrame => throw Missing(nameof(AnimationFrame));
    /// <summary>CItem::RunAnim(animation, frame, -1) (0x460C10).</summary>
    void RunAnimation(int animation, int frame) => throw Missing(nameof(RunAnimation));
    /// <summary>CItem::CycleAnim(animation, 1) (0x460D00).</summary>
    void CycleAnimation(int animation) => throw Missing(nameof(CycleAnimation));
    /// <summary>CItem 0x194 animation speed.</summary>
    void SetAnimationSpeed(int speed) => throw Missing(nameof(SetAnimationSpeed));
    ThugMessageQueue Messages => throw Missing(nameof(Messages));
    /// <summary>CBaddy 0x2F0 waypoint flags.</summary>
    uint RouteFlags => throw Missing(nameof(RouteFlags));
    /// <summary>CBaddy::GetNextWaypoint (0x402F60).</summary>
    bool TryAdvanceWaypoint() => throw Missing(nameof(TryAdvanceWaypoint));
    /// <summary>CBaddy::PathCheck (0x403310).</summary>
    ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int clearance) =>
        throw Missing(nameof(PathCheck));
    /// <summary>M3dColij_InitLineInfo + M3dZone_LineToItem(.., 1), with Spool_GetModelChecksum of the hit item.</summary>
    bool TryLineToItem(FixedVector3 start, FixedVector3 end, out uint checksum, out FixedVector3 hitPosition) =>
        throw Missing(nameof(TryLineToItem));
    /// <summary>CBody 0xE4 mPlayerDist written by the item-list distance pass.</summary>
    uint PlayerDistance => throw Missing(nameof(PlayerDistance));
    /// <summary>CPlayer 0xE1C mActionState.</summary>
    int PlayerActionState => throw Missing(nameof(PlayerActionState));
    /// <summary>CPlayer 0x8E8 || 0x8E9 (wall/ceiling crawl).</summary>
    bool PlayerOnWallOrCeiling => throw Missing(nameof(PlayerOnWallOrCeiling));
    /// <summary>CPlayer 0xE48 held object.</summary>
    object? PlayerHeldObject => throw Missing(nameof(PlayerHeldObject));
    /// <summary>MechList-&gt;Hit (vtable+0xC); returns the player's hit-taken result.</summary>
    bool HitPlayer(ActorHit hit) => throw Missing(nameof(HitPlayer));
    /// <summary>CPlayer::KnockSpideyFromCrawlPosition (0x4BB710).</summary>
    void KnockPlayerFromCrawl() => throw Missing(nameof(KnockPlayerFromCrawl));
    /// <summary>CPlayer::SetIgnoreInputTimer (0x4C30A0).</summary>
    void SetPlayerIgnoreInputTimer(int ticks) => throw Missing(nameof(SetPlayerIgnoreInputTimer));
    /// <summary>CBaddy::Baddy_SendSignal (0x403230).</summary>
    void SendSignal() => throw Missing(nameof(SendSignal));
    /// <summary>Mem_RecoverPointer(field_104) trap web.</summary>
    IThugTrapWeb? RecoverTrapWeb() => throw Missing(nameof(RecoverTrapWeb));
    /// <summary>field_104.pWhatever != 0.</summary>
    bool TrapWebHandleSet => throw Missing(nameof(TrapWebHandleSet));
    /// <summary>field_104.pWhatever = 0.</summary>
    void ReleaseTrapWebHandle() => throw Missing(nameof(ReleaseTrapWebHandle));
    /// <summary>Effects_Electrify (0x438E20) / Effects_UnElectrify (0x438EE0).</summary>
    void Electrify() => throw Missing(nameof(Electrify));
    void UnElectrify() => throw Missing(nameof(UnElectrify));
    /// <summary>SFX_PlayPos returning the voice handle, and SFX_Stop (0x4723D0).</summary>
    int PlayTrackedSound(int id) => throw Missing(nameof(PlayTrackedSound));
    void StopSound(int handle) => throw Missing(nameof(StopSound));
    /// <summary>CAIProc_MonitorAttack (0x401C40).</summary>
    void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask) =>
        throw Missing(nameof(AttachAttackMonitor));
    /// <summary>CCamera::Shake(mPos, CAMERASHAKE_BIG) (0x416880).</summary>
    void ShakeCamera() => throw Missing(nameof(ShakeCamera));
    /// <summary>gSaveGame field_7B vibration option, Pad_GetActuatorTime (0x4794A0) and Pad_ActuatorOn (0x4794D0).</summary>
    bool VibrationEnabled => throw Missing(nameof(VibrationEnabled));
    int ActuatorTime(int controller, int actuator) => throw Missing(nameof(ActuatorTime));
    void ActuatorOn(int controller, int duration, int actuator, int strength) => throw Missing(nameof(ActuatorOn));
    /// <summary>Effects_RhinoStomp (0x43A400).</summary>
    void EmitStompEffect() => throw Missing(nameof(EmitStompEffect));
    /// <summary>CRhinoWallImpact (0x43A550) built from the GonnaHitWall line hit.</summary>
    void CreateWallImpact(RhinoChargeWallContact contact) => throw Missing(nameof(CreateWallImpact));
    /// <summary>G_ENVIRONMENTAL_OBJECT_LIST (0x60DAAC) in source order.</summary>
    IReadOnlyList<RhinoEnvironmentBody> EnvironmentalBodies => throw Missing(nameof(EnvironmentalBodies));
    /// <summary>The type-401 vtable slot5 call, CManipOb::Smash (0x456D40).</summary>
    void SmashEnvironmentalBody(object? identity) => throw Missing(nameof(SmashEnvironmentalBody));
    /// <summary>CBody::StopMyXA (0x460400).</summary>
    void StopMyXA() => throw Missing(nameof(StopMyXA));
    /// <summary>CBaddy::Die(0) (0x404320).</summary>
    void Die() => throw Missing(nameof(Die));
    bool CameraPresent => throw Missing(nameof(CameraPresent));
    /// <summary>CCamera field_236 view heading.</summary>
    short CameraAngle => throw Missing(nameof(CameraAngle));
    /// <summary>CCamera::SetCamAngle (0x4178E0).</summary>
    void SetCameraAngle(short heading, ushort duration) => throw Missing(nameof(SetCameraAngle));
    /// <summary>DieRhino case0 camera: CAMERAMODE_DEMO, tripod and focus on this body, offsets 0, XZ 0x1A8, Y -0x68.</summary>
    void BeginDeathCamera() => throw Missing(nameof(BeginDeathCamera));
    /// <summary>CRhino::DoMGSShadow (0x4825E0) footprint quad.</summary>
    void UpdateShadow() => throw Missing(nameof(UpdateShadow));
    /// <summary>CRhino::DoDazedEffect (0x480480) bit lifetimes after the source angle/speed/Rnd update.</summary>
    void UpdateDazedStars(ReadOnlySpan<ActorHook> hooks, bool spawn) => throw Missing(nameof(UpdateDazedStars));
    /// <summary>CRhinoNasalSteam constructed from two dynamic hooks (velocity = second - first) and its Rnd(4096) spin.</summary>
    void EmitNasalSteam(ActorHook position, ActorHook direction, int spin) => throw Missing(nameof(EmitNasalSteam));

    private static InvalidOperationException Missing(string member) =>
        new($"This Rhino host does not provide {member}, which CRhino::AI (0x4819C0) requires.");
}

/// <summary>CRhino constructor (0x47DE40) and AI (0x4819C0) with every state leaf it dispatches.</summary>
public sealed partial class RhinoBrain(RhinoState state, RhinoStateFlags flags, IRhinoHost host)
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
        state.Field230 = 0;
        state.State = 0;
        state.Substate = 0;
        host.Actor.BehaviorFlags |= 0x02000201;
        state.BehaviorFlags = host.Actor.BehaviorFlags;
        state.Health = 1400;
        state.MovementAnimations = RhinoState.SetupWord0;
        state.IdleAnimations = RhinoState.SetupWord1;
        for (int index = 0; index < RhinoState.DazedStarCount; index++)
            state.DazedStarSpeeds[index] = unchecked((short)random(4096));
    }

    /// <summary>CRhino::AI (0x4819C0).</summary>
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
            if (state.Field3D4 > 0x168) SetState(0x14);
        }
        else state.Field3D4 = 0;

        if (state.BossCameraEnabled == 0 && host.PlayerInputSuppression == 0)
        {
            host.SetBossCameraMode();
            host.SetGlobalBoss(true);
            state.BossCameraEnabled = 1;
        }

        state.Field35C = unchecked(state.Field35C + 1);

        if ((flags.Check(state.State) & 4) == 0) host.DoPhysics(0);
        if ((flags.Check(state.State) & 0x40) != 0)
        {
            if (state.Field324 != 0)
            {
                if (Abs(unchecked(host.Body.Position.Y - host.PlayerPosition.Y)) > 0x64000)
                {
                    state.Field324 = RunTimer(state.Field324);
                    if (state.Field324 == 0) SetState(0xD);
                }
                else state.Field324 = 0;
            }
            if (state.Field328 != 0)
            {
                state.Field328 = RunTimer(state.Field328);
                if (state.Field328 == 0) SetState(0xD);
            }
            if (state.Field32C != 0)
            {
                state.Field32C = RunTimer(state.Field32C);
                if (state.Field32C == 0) PlayXAPlease(0xF, 3, 1);
            }
        }
        else
        {
            state.Field328 = 0;
            state.Field324 = 0;
        }

        ProcessMessages();
        if (host.Procedures.Count != 0) host.Procedures.Execute(host.ElapsedTicks);
        state.Field348 = 0;

        if ((flags.Check(state.State) & 0x10) != 0) host.Actor.BehaviorFlags |= 0x10000;
        else host.Actor.BehaviorFlags &= ~0x10000u;

        if (state.State == 0x16)
        {
            state.BodyFlags |= 0x10;
            state.Field310 = 0;
            if (!DetermineFightState(0)) SetState(1);
        }
        else if ((state.State == 2 || state.State == 1) && (host.AttackClock & 3) == 0)
        {
            if (DetermineFightState(0))
            {
                state.Field328 = 0;
                state.Field324 = 0;
            }
            else if (state.Field328 == 0)
                state.Field328 = Difficulty(0x2710, 0x1B58, 0x1388, 0xBB8);
        }

        PlaySounds();
        if (state.State != 0) host.SetHeight(false, 0x64, 0x258);

        switch (state.State)
        {
            case 0: RhinoInit(); host.SetHeight(false, 0x64, 0x258); break;
            case 1: FollowWaypoints(); break;
            case 2:
                state.Field1F0 = 0;
                StandStill();
                break;
            case 3: Laugh(); break;
            case 4:
                state.Field358 = host.AttackClock;
                ChasePlayer(2);
                break;
            case 5:
                state.Field358 = host.AttackClock;
                ChasePlayer(1);
                break;
            case 6:
                host.Actor.BehaviorFlags |= 0x20000000;
                state.Field358 = host.AttackClock;
                AttackPlayer();
                break;
            case 7:
            case 8:
                state.Field358 = host.AttackClock;
                host.Actor.BehaviorFlags |= 0x20000000;
                ChargePlayer();
                break;
            case 9: HitWall(); break;
            case 10:
            case 11: StuckInWall(); break;
            case 12: GetShocked(); break;
            case 13:
            case 20:
                state.Field358 = host.AttackClock;
                StompGround();
                break;
            case 14:
                TakeHit();
                state.Field358 = host.AttackClock;
                break;
            case 15: GetLaunched(); break;
            case 16:
            case 17:
                state.Field358 = host.AttackClock;
                GetTrapped();
                break;
            case 18:
                state.Field358 = host.AttackClock;
                if ((host.Actor.BehaviorFlags & 8) != 0) StandStill();
                else SetState(0x16);
                break;
            case 21: DieRhino(); break;
            default:
                // The original only prints "Unknown state!" through nullsub_1 and continues with the tail.
                break;
        }

        if (host.AnimationFinished)
        {
            if (host.AnimationId == 0) PlaySingleAnim(0);
            else if (host.AnimationId == 2) PlaySingleAnim(2);
            else if (host.AnimationId == 5) PlaySingleAnim(5);
        }

        host.SetAnimationSpeed((state.Field348 & 2) != 0 ? 0x8000 : 0x10000);
        host.BuildTransform();
        host.UpdateShadow();
        DoDazedEffect();
        DoNasalSteam();
        state.BehaviorFlags = host.Actor.BehaviorFlags;
    }

    /// <summary>The AI message walk: codes 5 (trap), 0xE (burst web) and 0x11 (unelectrify).</summary>
    private void ProcessMessages()
    {
        ThugMessageQueue messages = host.Messages;
        if (messages.Head is null) return;
        for (ThugMessage? message = messages.Head; message is not null; message = message.Next)
        {
            bool consumed = true;
            if (state.State != 0x15 || message.Code >= 11)
            {
                switch (message.Code)
                {
                    case 5:
                        if ((flags.Check(state.State) & 0x10) != 0) consumed = false;
                        else if (state.State != 0x10)
                        {
                            host.Neutralize();
                            SetState(0x10);
                        }
                        else if ((state.Field348 & 1) != 0)
                        {
                            state.Field1F8 = 0;
                            state.Substate = 1;
                        }
                        else consumed = false;
                        break;
                    case 0xE:
                        host.RecoverTrapWeb()?.Burst();
                        break;
                    case 0x11:
                        host.UnElectrify();
                        if (state.Field338 != 0) host.StopSound(state.Field338);
                        state.Field338 = 0;
                        break;
                }
            }
            if (consumed) message.Flags |= 1;
        }
        messages.Cleanup();
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
                SetState(8);
                break;
        }
    }

    /// <summary>CRhino::StandStill, inlined by AI for states 2 and 18.</summary>
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
        }
    }

    private int Difficulty(int easy, int normal, int hard, int alternative) => host.Difficulty switch
    {
        0 => easy,
        1 => normal,
        2 => hard,
        _ => alternative
    };

    /// <summary>CBaddy::RunTimer (0x402BE0).</summary>
    private int RunTimer(int timer)
    {
        timer = unchecked(timer - host.ElapsedTicks);
        return timer < 0 ? 0 : timer;
    }

    private void SetState(int value)
    {
        state.State = value;
        state.Substate = 0;
    }

    private void PlaySingleAnim(int animation)
    {
        state.Field388 = 0;
        host.RunAnimation(animation);
    }

    /// <summary>CRhino::PlayXAPlease (0x47E0A0): a2 bank offset, a3 slot count, a4 Rnd addend only.</summary>
    private void PlayXAPlease(int bankOffset, int slotCount, int rndAddend) =>
        host.PlayXAPlease(state, bankOffset, slotCount, rndAddend);

    /// <summary>CRhino::ShakePad, inlined at every PC call site.</summary>
    private void ShakePad()
    {
        if (!host.VibrationEnabled) return;
        if (unchecked((ushort)host.ActuatorTime(0, 0)) <= 2) host.ActuatorOn(0, 6, 0, 1);
        if (unchecked((ushort)host.ActuatorTime(0, 1)) <= 2) host.ActuatorOn(0, 10, 1, 200);
    }


    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
