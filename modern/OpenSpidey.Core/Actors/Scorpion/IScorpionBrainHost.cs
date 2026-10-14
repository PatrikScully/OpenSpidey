using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

public interface IScorpionCameraHiddenItem
{
    object Identity { get; }
    ushort ItemFlags { get; set; }
}

/// <summary>Transport into the existing camera mode/parameter and actual hidden-item owners.</summary>
public interface IScorpionBossCameraHost
{
    CameraModeState Mode { get; }
    int SpinRate { get; set; } // Source camera0x2A4.
    int StationaryRadius { get; set; } // Source camera0x2A8.
    ushort GetHorizontalDistance();
    short GetVerticalDistance();
    void SetHorizontalDistance(ushort distance, ushort duration);
    void SetVerticalDistance(short distance, ushort duration);
    void SetMode(uint mode);
    IScorpionCameraHiddenItem? GetHiddenItem(int index);
    void ClearHiddenItem(int index);
}

/// <summary>Required original behavior leaves; enum values retain their actual entry addresses.</summary>
public enum ScorpionBehaviorRoutine
{
    FollowWaypoints = 0x484240,
    Idle = 0x486980,
    PursueTarget = 0x485120,
    AttackEnvironmentalObject = 0x485D90,
    FireTailLaser = 0x487230,
    GetTrapped = 0x486DA0,
    GetTugged = 0x486C00,
    DoIntroSequence = 0x487FC0,
    ReactToJonah = 0x487CD0,
    WindUpEnvironmentalAttack = 0x484A90,
    Gloat = 0x487740,
    AvoidObstacle = 0x484C60,
    Die = 0x487B80
}

/// <summary>Real world/camera/tail/effect owners and still-unported behavior dependencies around shipped488590.</summary>
public interface IScorpionBrainHost : IScorpionDamageHost
{
    PlayerCombatState Player { get; }
    short PlayerFieldAa { get; } // Exact source signed16 read; do not infer it from a normalized modern vector.
    object? PlayerHeldItemIdentity { get; }
    IScorpionBossCameraHost? Camera { get; }
    SourceMusicState Music { get; }
    void SetGlobalBoss(ScorpionState? boss);
    void PutPlayerCameraBehind(int argument);
    void InitializeTailGeometry();
    void InitializeTailGlow();
    void DoPhysics(int limitElapsed);
    void BuildTransform();
    object? RecoverHandle(object? handle);
    object? RecoverWeb(PlayerKickHandle handle);
    void BurstWeb(object web);
    void CycleAnimation(ushort clip, sbyte direction);
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    void DetermineTarget();
    void StepBehavior(ScorpionBehaviorRoutine routine, object? target = null);
    FixedVector3 JonahPosition(object jonah);
    ushort JonahRadius(object jonah);
    void UpdateFootprint();
    Memory<FixedVector3> TailTrail { get; } // The six global source vectors at0x6A73F8, shared by the trail owner.
    void UpdateTailPath();
    void TryCreateTailImpact(Func<FixedVector3> readPosition);
    void SourceDiagnostic(string message);
}
