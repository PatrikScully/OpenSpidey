using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;
using OpenSpidey.Assets.Models;

namespace OpenSpidey.Game.Actors;

/// <summary>Production CScorpion::AI488590 host over the actor's body, procedure, message and path owners plus the live scene.</summary>
internal sealed partial class GodotScorpionAi : IScorpionBrainHost, IScorpionTargetHost, IScorpionPathOwner, IScorpionBehaviorHost
{
    private static readonly ushort[] ShadowHooks = [16, 13, 7, 10];
    private readonly GodotScorpionEncounterActor actor;
    private readonly ScorpionTargetState targets;
    private readonly ScorpionEncounterBehavior behavior;
    private readonly ActorPathController path;
    private readonly ActorBodyMotion playerBody = new();
    private GodotCinematicFootprintShadow? shadow;

    public GodotScorpionAi(GodotScorpionEncounterActor actor, ScorpionDamageState damage)
    {
        this.actor = actor;
        targets = new(new ScorpionBrainState(damage, actor.Messages));
        path = new(actor.Motion, World) { Radius = actor.State.MinorRadius, Clearance = actor.State.SurfaceClearance };
        behavior = new(targets, this, this, this, Encounter.Registry.RandomSource);
    }

    private GodotScorpionEncounterRuntime Encounter => actor.Encounter;
    private GodotActorWorldQuery World => Encounter.SceneOwnership.World;
    private GodotPlayerMeleeDirectory Handles => Encounter.SceneOwnership.Directory;
    private ScorpionState Body => targets.Brain.Body;
    internal byte CurrentTargetIsPlayer => targets.Brain.CurrentTargetIsPlayer;

    public void Update(int elapsedTicks) => behavior.Update(elapsedTicks);
    public bool Hit(ref ActorHit hit) => behavior.Hit(ref hit);

    /// <summary>Scorpion module slot1 483260: the recovered current target while target kind0xBF8 is nonzero.</summary>
    internal object? CurrentTargetForModule() => targets.Brain.Damage.TargetKind != 0 ? RecoverHandle(ref targets.CurrentTarget) : null;

    public int Difficulty => Encounter.Difficulty;
    public uint PlayerFlags => throw new NotSupportedException("CScorpion::Hit reads CPlayer0xE1C, which has no modern player owner.");
    public FixedVector3 PlayerPosition => Encounter.Player.Controller.Position;
    public object? MakePlayerHandle() => new ScorpionTargetHandle(MakeHandle(PlayerIdentity));
    /// <summary>The first BaddyList body of type316.</summary>
    public object? FindJonah()
    {
        foreach (object identity in Encounter.Registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder)
            if (identity is GodotScorpionEncounterActor { State: JonahState } jonah && Encounter.Registry.Owns(jonah)) return jonah;
        return null;
    }
    public void TrySendJonahMessage(object jonah, int code) => RequireJonah(jonah).ReceiveMessage(new ThugMessage(code, actor));
    public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority) => Encounter.PlayPositionSpeech(bank, slot, priority);
    public void AttachPlayerSpeech(int bank, int slot) =>
        throw new NotSupportedException("CScorpion's player-attached speech transport is not ported.");
    public void PlayPositionSound(uint sound, FixedVector3 position, int pitch)
    {
        if (pitch != 0) throw new NotSupportedException("Scorpion source SFX uses pitch offset0.");
        (Encounter.Registry.Sounds ?? throw new InvalidOperationException("Scorpion source SFX requires the scene sound channels."))
            .PlayAt(sound, position, Encounter.Registry.SoundListener());
    }
    public void RunAnimation(ushort clip, int from, int to) => actor.RunAnimation(clip, from, to);
    /// <summary>CBaddy::PathCheck403310 with a hit out-pointer.</summary>
    public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit)
    {
        ActorPathCheckResult result = path.PathCheck(from, to, true, clearance);
        hit = result.HitPoint;
        return result.Code;
    }
    public void TryAttachPlayerLookAt(uint completionMask, int rate, int tolerance) =>
        actor.Procedures.Attach(ActorLookAtProcedure.ForBody(actor.Motion, RecoverPlayerBody, completionMask, rate, tolerance));
    public void ReportUnknownRecoverySubstate(int substate) =>
        throw new InvalidOperationException($"Original CScorpion print_if_false: unknown recovery substate {substate}.");
    public void DetermineTarget() =>
        throw new InvalidOperationException("ScorpionEncounterBehavior owns CScorpion_DetermineTarget488140.");

    public PlayerCombatState Player => Encounter.Player.Combat.State;
    public bool PlayerIsCrawling => Encounter.Player.Combat.State.IsCrawling;
    public short PlayerFieldAa => (short)Encounter.Player.Controller.Velocity.Y;
    public object? PlayerHeldItemIdentity => Encounter.ReadHeldActor();
    public IScorpionBossCameraHost? Camera => Encounter.BossCamera;
    public SourceMusicState Music => Encounter.Music;
    public void SetGlobalBoss(ScorpionState? boss)
    {
        if (boss is null) { Encounter.PublishGlobalBoss(null); return; }
        if (!ReferenceEquals(boss, Body)) throw new InvalidOperationException("CScorpion SetGlobalBoss requires its own body.");
        Encounter.SetGlobalBoss(actor);
    }
    public void PutPlayerCameraBehind(int argument) => (Encounter.BoundCamera ?? throw new InvalidOperationException("The source Scorpion AI requires a bound player camera.")).PutBehind(argument);
    public void InitializeTailGeometry() => actor.InitializeTailGeometry();
    public void InitializeTailGlow() => actor.InitializeTailGlow();
    public void DoPhysics(int limitElapsed) => actor.DoPhysics(limitElapsed);
    public void BuildTransform() => actor.BuildTransform();
    public object? RecoverHandle(object? handle) => handle switch
    {
        null => null,
        ScorpionTargetHandle target => RecoverHandle(ref target.Value),
        _ => throw new NotSupportedException($"CScorpion handle recovery has no adapter for {handle.GetType().Name}.")
    };
    public object? RecoverWeb(PlayerKickHandle handle) =>
        throw new NotSupportedException("CScorpion trap/tug CTrapWebEffect recovery is not ported.");
    public void BurstWeb(object web) => throw new NotSupportedException("CTrapWebEffect::Burst4F8600 is not ported.");
    public void CycleAnimation(ushort clip, sbyte direction) => actor.CycleAnimation(clip, direction);
    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("Utils_GetGroundHeight with a body out-pointer is not used by the Scorpion.");
        return World.GroundHeight(position, above, below);
    }
    public FixedVector3 JonahPosition(object jonah) => RequireJonah(jonah).Motion.Body.Position;
    public ushort JonahRadius(object jonah) => RequireJonah(jonah).State.MinorRadius;
    /// <summary>CScorpion_DoMGSShadow489F60: hooks16,13,7,10 and the ground register into one protected Shadow2 CQuadBit.</summary>
    public void UpdateFootprint()
    {
        Span<FixedVector3> hooks = stackalloc FixedVector3[ShadowHooks.Length];
        for (int index = 0; index < hooks.Length; index++) hooks[index] = actor.DynamicHookPosition(new ActorHook(0, 0, 0, ShadowHooks[index]));
        FixedVector3[] corners = CinematicActorPose.ShadowCorners(actor.Motion.Body.Position, actor.SourceTransformBasis, actor.Variables.Registers[0], hooks);
        if (shadow is null) { shadow = GodotCinematicFootprintShadow.Create(Encounter.ShadowTexture); actor.AddChild(shadow); }
        shadow.SetCorners(corners);
    }
    public Memory<FixedVector3> TailTrail => Encounter.TailTrail;
    /// <summary>CScorpion_BuildTail488370.</summary>
    public void UpdateTailPath() => ScorpionTailPath.Update(Body.TailControlPoints, Body.TailNodes, Body.TailHistory,
        part => actor.DynamicHookPosition(new ActorHook(0, 0, 0, part)));
    public void TryCreateTailImpact(Func<FixedVector3> readPosition) =>
        throw new NotSupportedException("CScorpExplosion482F20 is not ported.");
    public void SourceDiagnostic(string message) => throw new InvalidOperationException("Original CScorpion print_if_false: " + message);

    public object PlayerIdentity => Encounter.Player;
    public uint CallJonahDecisionModule(object jonah) =>
        Encounter.Modules.CallTypedUserFunction<GodotScorpionEncounterActor, uint>("jonah", 1, RequireJonah(jonah));
    public PlayerKickHandle JonahHidingPlace(object jonah) => RequireJonah(jonah).RequireJonahAi().HidingPlace;
    public PlayerKickHandle MakeHandle(object identity) => identity is GodotPlayer player
        ? new(new PlayerTarget(player), 0)
        : Handles.MakeHandle(identity);
    public object? RecoverHandle(ref PlayerKickHandle handle)
    {
        if (handle.Target is not PlayerTarget player) return Handles.Recover(ref handle);
        if (ReferenceEquals(player.Player, Encounter.Player)) return player.Player;
        handle = handle with { Target = null };
        return null;
    }
    public object? FirstEnvironment => Encounter.Registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.FirstOrDefault();
    public object? NextEnvironment(object identity)
    {
        bool found = false;
        foreach (object candidate in Encounter.Registry.SourceList(ActorUpdateGroup.Environment).SourceOrder)
        {
            if (found) return candidate;
            found = ReferenceEquals(candidate, identity);
        }
        return found ? null : throw new InvalidOperationException("The source EnvironmentList walk lost its current body.");
    }
    public ushort BodyType(object identity) => identity is GodotScriptActor body ? checked((ushort)body.SourceActorType)
        : throw new NotSupportedException($"Source body type has no adapter for {identity.GetType().Name}.");
    public FixedVector3 BodyPosition(object identity) => identity switch
    {
        GodotPlayer => PlayerPosition,
        GodotScriptActor body => body.SourcePosition,
        _ => throw new NotSupportedException($"Source body position has no adapter for {identity.GetType().Name}.")
    };
    public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument)
    {
        if (argument != 0) throw new NotSupportedException("CScorpion line-of-sight with a hit out-pointer is not established.");
        FixedVector3 hit = default;
        return World.LineOfSight(from, to, flags, ref hit);
    }
    public void TryAttachTargetLookAt(object? target, uint completionMask, int rate, int tolerance)
    {
        Func<ActorBodyMotion?> recover = target switch
        {
            GodotPlayer => RecoverPlayerBody,
            PlayerTarget => RecoverPlayerBody,
            GodotScriptActor body => RecoverBody(body),
            null => () => null,
            _ => RecoverBody(Handles.HandlePointer(target))
        };
        actor.Procedures.Attach(ActorLookAtProcedure.ForBody(actor.Motion, recover, completionMask, rate, tolerance));
    }

    public void StorePathDestination(FixedVector3 point)
    {
        path.SetPoint(1, point);
        path.PathCount = 1;
    }

    private Func<ActorBodyMotion?> RecoverBody(GodotScriptActor body) => () => Encounter.SceneOwnership.Actors.RecoverActor(body)?.Body;
    private ActorBodyMotion? RecoverPlayerBody()
    {
        GodotPlayer player = Encounter.Player;
        playerBody.Position = player.Controller.Position;
        playerBody.Velocity = player.Controller.Velocity;
        playerBody.Angles = new(0, player.Controller.Orientation.Heading, 0);
        return playerBody;
    }

    private static GodotScorpionEncounterActor RequireJonah(object jonah) => jonah as GodotScorpionEncounterActor is { State: JonahState } actor ? actor :
        throw new ArgumentException("The Scorpion's Jonah identity must be a live source CJonah.", nameof(jonah));

    private sealed record PlayerTarget(GodotPlayer Player);
}
