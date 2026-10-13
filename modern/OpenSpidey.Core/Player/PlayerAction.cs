namespace OpenSpidey.Core.Player;

/// <summary>Original action words. Additional actions are introduced as their controllers are ported.</summary>
public enum PlayerAction : uint
{
    Idle = 1,
    Jumping = 2,
    Falling = 4,
    WallAttached = 8,
    GroundMovement = 16,
    JumpWindup = 64,
    Dead = 0x80,
    SwingWindup = 0x100,
    SwingRelease = 0x200,
    Swinging = 0x400,
    PunchCombo = 0x800,
    SurfaceLanding = 0x1000,
    FenceTransition = 0x2000,
    WebWindup = 0x4000,
    WebFiring = 0x8000,
    WebThrow = 0x20000,
    WebAttach = 0x10000,
    WebDome = 0x20000000,
    JumpingSmash = 0x01000000,
    ZipDive = 0x02000000,
    ZipDivePunch = 0x04000000,
    ZipDiveKick = 0x08000000,
    ZipWebPull = 0x40000,
    ScriptedStandCompletion = 0x80000,
    Pickup = 0x100000,
    Throw = 0x200000,
    FlickSwitch = 0x80000000,
    Rolling = 0x400000,
    KnockdownRecovery = 0x800000,
    ScriptedDelay = 0x40000000
}
