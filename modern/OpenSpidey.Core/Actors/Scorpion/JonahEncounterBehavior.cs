using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>One actor-owned AI composition; concrete Jonah state leaves share the original frame's timing.</summary>
public sealed class JonahEncounterBehavior
{
    private readonly JonahLocomotionState state;
    private readonly HostAdapter adapter;

    public JonahEncounterBehavior(JonahLocomotionState state, IJonahLocomotionHost services)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(services);
        this.state = state;
        adapter = new(state, services);
    }

    public void Update(int elapsedTicks)
    {
        adapter.ElapsedTicks = elapsedTicks;
        JonahBrain.Update(state.Brain, adapter, elapsedTicks);
    }

    private sealed class HostAdapter(JonahLocomotionState state, IJonahLocomotionHost services) : IJonahBrainHost
    {
        public int ElapsedTicks { get; set; }
        public uint Clock => services.Clock;
        public bool PlayerSynthesizingInput => services.PlayerSynthesizingInput;
        public object? RecoverScorpion(PlayerKickHandle handle) => services.RecoverScorpion(handle);
        public object? FindFirstScorpion() => services.FindFirstScorpion();
        public PlayerKickHandle MakeScorpionHandle(object scorpion) => services.MakeScorpionHandle(scorpion);
        public JonahState? ScorpionCurrentTarget(object scorpion) => services.ScorpionCurrentTarget(scorpion);
        public bool ScorpionVictorious(object scorpion) => services.ScorpionVictorious(scorpion);
        public bool ScorpionContactSignal(object scorpion) => services.ScorpionContactSignal(scorpion);
        public int Random(int exclusiveMaximum) => services.Random(exclusiveMaximum);
        public void RunAnimation(ushort clip, int startFrame, int endFrame) => services.RunAnimation(clip, startFrame, endFrame);
        public void CycleAnimation(ushort clip, sbyte direction) => services.CycleAnimation(clip, direction);
        public void TryLookAtScorpion(object scorpion, uint completionMask, int rate, int tolerance) =>
            services.TryLookAtScorpion(scorpion, completionMask, rate, tolerance);
        public void TrySendWaypointMessage(object scorpion, int code, int positionMode, FixedVector3 position, FixedVector3 waypoint) =>
            services.TrySendWaypointMessage(scorpion, code, positionMode, position, waypoint);
        public void PlayVoice(int bank, int index, FixedVector3 position, int flags) => services.PlayVoice(bank, index, position, flags);
        public void PlaySound(uint sound, FixedVector3 position, int pitchOffset) => services.PlaySound(sound, position, pitchOffset);
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => services.GroundHeight(position, above, below, flags);
        public FixedVector3 WaypointPosition(int waypoint) => services.WaypointPosition(waypoint);
        public JonahPathTrace TracePath(FixedVector3 start, FixedVector3 end, int clearance) => services.TracePath(start, end, clearance);
        public bool ScorpionBlocksPath(FixedVector3 start, FixedVector3 end, object scorpion) => services.ScorpionBlocksPath(start, end, scorpion);
        public bool AddPointToPath(FixedVector3 position, int flags) => services.AddPointToPath(position, flags);
        public void DoPhysics(int limitElapsed) => services.DoPhysics(limitElapsed);
        public void BuildTransform() => services.BuildTransform();
        public void SetMovementHeight(int flags, int clearance, int distance) => services.SetMovementHeight(flags, clearance, distance);
        public void FollowWaypoints() => JonahLocomotion.FollowWaypoints(state, services);
        public void Hide() => JonahLocomotion.Hide(state, services);
        public void TakeHit() => JonahLocomotion.TakeHit(state, services);
        public void Die() => JonahLocomotion.Die(state, services, ElapsedTicks);
        public void GoAroundScorpion(object scorpion) => services.GoAroundScorpion(scorpion);
        public void DrawShadow() => services.DrawShadow();
        public void SourceDiagnostic(string message) => services.SourceDiagnostic(message);
    }
}
