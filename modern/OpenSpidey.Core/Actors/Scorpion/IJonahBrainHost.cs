using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

public readonly record struct JonahPathTrace(int Result, FixedVector3 HitPosition);

/// <summary>Actual source transports and remaining behavior leaves required by Jonah's recovered AI.</summary>
public interface IJonahBrainHost
{
    uint Clock { get; }
    bool PlayerSynthesizingInput { get; }
    object? RecoverScorpion(PlayerKickHandle handle);
    object? FindFirstScorpion();
    PlayerKickHandle MakeScorpionHandle(object scorpion);
    JonahState? ScorpionCurrentTarget(object scorpion);
    bool ScorpionVictorious(object scorpion);
    bool ScorpionContactSignal(object scorpion);
    int Random(int exclusiveMaximum);
    void RunAnimation(ushort clip, int startFrame, int endFrame);
    void CycleAnimation(ushort clip, sbyte direction);
    void TryLookAtScorpion(object scorpion, uint completionMask, int rate, int tolerance);
    void TrySendWaypointMessage(object scorpion, int code, int positionMode, FixedVector3 position, FixedVector3 waypoint);
    void PlayVoice(int bank, int index, FixedVector3 position, int flags);
    void PlaySound(uint sound, FixedVector3 position, int pitchOffset);
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    FixedVector3 WaypointPosition(int waypoint);
    JonahPathTrace TracePath(FixedVector3 start, FixedVector3 end, int clearance);
    bool ScorpionBlocksPath(FixedVector3 start, FixedVector3 end, object scorpion);
    bool AddPointToPath(FixedVector3 position, int flags);
    void DoPhysics(int limitElapsed);
    void BuildTransform();
    void SetMovementHeight(int flags, int clearance, int distance);
    void FollowWaypoints();
    void Hide();
    void TakeHit();
    void Die();
    void GoAroundScorpion(object scorpion);
    void DrawShadow();
    void SourceDiagnostic(string message);
}
