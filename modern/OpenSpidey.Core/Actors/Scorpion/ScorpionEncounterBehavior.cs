using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Source CBaddy0x1A8 path owner: point1 and count0x1F0.</summary>
public interface IScorpionPathOwner
{
    void StorePathDestination(FixedVector3 point);
}

/// <summary>One actor's completed damage/outer-AI/target/intro composition; remaining behavior owners are explicit required services.</summary>
public sealed class ScorpionEncounterBehavior
{
    private readonly ScorpionTargetState state;
    private readonly HostAdapter adapter;
    private readonly Func<int, int> random;

    public ScorpionEncounterBehavior(ScorpionTargetState state, IScorpionBrainHost world, IScorpionTargetHost targets,
        IScorpionPathOwner path, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(random);
        this.state = state;
        this.random = random;
        adapter = new(state, world, targets, path, random);
    }

    public int Update(int elapsedTicks)
    {
        adapter.ElapsedTicks = elapsedTicks;
        return ScorpionBrain.Update(state.Brain, adapter, elapsedTicks, random);
    }
    public bool Hit(ref OpenSpidey.Core.Combat.ActorHit hit) => ScorpionDamageBehavior.Hit(state.Brain.Damage, adapter, ref hit, random);

    private sealed class HostAdapter(ScorpionTargetState state, IScorpionBrainHost world, IScorpionTargetHost targets,
        IScorpionPathOwner path, Func<int, int> random) : IScorpionBrainHost, IScorpionIdleHost
    {
        public bool PlayerIsCrawling => world.Player.IsCrawling;
        public object PlayerIdentity => targets.PlayerIdentity;
        public uint CallJonahDecisionModule(object jonah) => targets.CallJonahDecisionModule(jonah);
        public PlayerKickHandle JonahHidingPlace(object jonah) => targets.JonahHidingPlace(jonah);
        public PlayerKickHandle MakeHandle(object identity) => targets.MakeHandle(identity);
        public object? RecoverHandle(ref PlayerKickHandle handle) => targets.RecoverHandle(ref handle);
        public object? FirstEnvironment => targets.FirstEnvironment;
        public object? NextEnvironment(object identity) => targets.NextEnvironment(identity);
        public ushort BodyType(object identity) => targets.BodyType(identity);
        public FixedVector3 BodyPosition(object identity) => targets.BodyPosition(identity);
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument) => targets.LineOfSight(from, to, flags, argument);
        public void TryAttachTargetLookAt(object? target, uint completionMask, int rate, int tolerance) => targets.TryAttachTargetLookAt(target, completionMask, rate, tolerance);
        public void StorePathDestination(FixedVector3 point) => path.StorePathDestination(point);
        public int ElapsedTicks { get; set; }
        public PlayerCombatState Player => world.Player;
        public short PlayerFieldAa => world.PlayerFieldAa;
        public object? PlayerHeldItemIdentity => world.PlayerHeldItemIdentity;
        public IScorpionBossCameraHost? Camera => world.Camera;
        public SourceMusicState Music => world.Music;
        public int Difficulty => world.Difficulty;
        public uint PlayerFlags => world.PlayerFlags;
        public FixedVector3 PlayerPosition => world.PlayerPosition;
        public Memory<FixedVector3> TailTrail => world.TailTrail;
        public object? MakePlayerHandle() => new ScorpionTargetHandle(targets.MakeHandle(targets.PlayerIdentity));
        public object? FindJonah() => targets.FindJonah();
        public object? RecoverHandle(object? handle) => targets.RecoverHandle(ref state.CurrentTarget);
        public void TrySendJonahMessage(object jonah, int code) => world.TrySendJonahMessage(jonah, code);
        public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority) => world.PlayPositionSpeech(bank, slot, position, priority);
        public void AttachPlayerSpeech(int bank, int slot) => world.AttachPlayerSpeech(bank, slot);
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch) => world.PlayPositionSound(sound, position, pitch);
        public void RunAnimation(ushort clip, int from, int to) => world.RunAnimation(clip, from, to);
        public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit) => world.PathCheckWithHit(from, to, clearance, out hit);
        public void TryAttachPlayerLookAt(uint completionMask, int rate, int tolerance) => world.TryAttachPlayerLookAt(completionMask, rate, tolerance);
        public void ReportUnknownRecoverySubstate(int substate) => world.ReportUnknownRecoverySubstate(substate);
        public void SetGlobalBoss(ScorpionState? boss) => world.SetGlobalBoss(boss);
        public void PutPlayerCameraBehind(int argument) => world.PutPlayerCameraBehind(argument);
        public void InitializeTailGeometry() => world.InitializeTailGeometry();
        public void InitializeTailGlow() => world.InitializeTailGlow();
        public void DoPhysics(int limitElapsed) => world.DoPhysics(limitElapsed);
        public void BuildTransform() => world.BuildTransform();
        public object? RecoverWeb(PlayerKickHandle handle) => world.RecoverWeb(handle);
        public void BurstWeb(object web) => world.BurstWeb(web);
        public void CycleAnimation(ushort clip, sbyte direction) => world.CycleAnimation(clip, direction);
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => world.GroundHeight(position, above, below, flags);
        public void DetermineTarget() => ScorpionTargetSelection.DetermineTarget(state, targets, random);
        public void StepBehavior(ScorpionBehaviorRoutine routine, object? target = null)
        {
            switch (routine)
            {
                case ScorpionBehaviorRoutine.DoIntroSequence: ScorpionTargetSelection.Intro(state, targets, ElapsedTicks); break;
                case ScorpionBehaviorRoutine.Idle: ScorpionIdle.Step(state, this); break;
                default: world.StepBehavior(routine, target); break;
            }
        }
        public FixedVector3 JonahPosition(object jonah) => world.JonahPosition(jonah);
        public ushort JonahRadius(object jonah) => world.JonahRadius(jonah);
        public void UpdateFootprint() => world.UpdateFootprint();
        public void UpdateTailPath() => world.UpdateTailPath();
        public void TryCreateTailImpact(Func<FixedVector3> readPosition) => world.TryCreateTailImpact(readPosition);
        public void SourceDiagnostic(string message) => world.SourceDiagnostic(message);
    }
}
