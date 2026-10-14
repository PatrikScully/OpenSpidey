using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

public readonly record struct JonahWaypointLink(int Node, int Type, int Role, int Parameter);

/// <summary>World/procedure/player/camera services consumed by the complete Jonah movement decisions.</summary>
public interface IJonahLocomotionHost : IJonahBrainHost
{
    IReadOnlyList<JonahWaypointLink> ReadWaypointLinks(int node, int maximumCount);
    object? FindEnvironmentBody(int node);
    PlayerKickHandle MakeEnvironmentHandle(object body);
    object? RecoverEnvironmentBody(PlayerKickHandle handle);
    object? PlayerHeldObject { get; }
    short PlayerHealth { get; }
    FixedVector3 ScorpionPosition(object scorpion);
    int PathPointCount { get; set; }
    FixedVector3 PathPoint(int index);
    void ExecuteWaypointScript(int node);
    void TryMoveToPoint(FixedVector3 destination, int speed, int turnRate, int facingTolerance, uint completionMask);
    void TrySendScorpionParameterMessage(object scorpion, int code, int parameter);
    void SuppressPlayerInput(int ticks);
    bool HasCamera { get; }
    short CameraAngle { get; }
    void SetCameraMode(int mode);
    void TrackJonahPositionAndOrientation(JonahState jonah);
    void SetCameraAngle(short angle, ushort ticks);
    void SetCameraXOffset(short offset, ushort ticks);
    void SetCameraYOffset(short offset, ushort ticks);
    void SetCameraZOffset(short offset, ushort ticks);
    void SetCameraHorizontalDistance(ushort distance, ushort ticks);
    void SetCameraVerticalDistance(short distance, ushort ticks);
    void SetLevelStatus(int status);
}
