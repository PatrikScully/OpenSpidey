using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Outer Scorpion AI fields composed with the original constructor, damage, motion and inbox owners.</summary>
public sealed class ScorpionBrainState
{
    public ScorpionDamageState Damage { get; }
    public ScorpionState Body => Damage.Body;
    public ThugMessageQueue Messages { get; }
    public uint SignalFlags { get; set; } // Source0x218, independent of procedure0x288 and reaction0x324.
    public int StandingY { get; set; } // 0x29C.
    public int GroundY { get; set; } // 0x2A0.
    public int EnvironmentalTargetSuppressionUpdates { get; set; } // 0xC14.
    public int GloatSuppressionUpdates { get; set; } // 0xC18.
    public int FieldC1CUpdates { get; set; } // One of the five source countdown words; wider role is unproven.
    public byte TailGlowEnabled { get; set; } // 0x480, derived from the previous behavior's reaction bit4.
    public ushort SavedCameraHorizontalDistance { get; set; } // 0x328.
    public ushort SavedCameraVerticalDistance { get; set; } // 0x32A; restored as signed16.
    public int CameraDistancesSaved { get; set; } // 0x32C.
    public int LastFootstepSound { get; set; } // 0x3AC.
    public FixedVector3 PreviousPosition { get; set; } // 0x2FC.
    public FixedVector3 ObstaclePosition { get; set; } // 0x33C, message9 first vector.
    public FixedVector3 ObstacleWaypoint { get; set; } // 0x348, message9 second vector.
    public PlayerKickHandle TrapWeb { get; set; } // 0x104; clearing the pointer preserves its validation word.
    public PlayerKickHandle TugWeb { get; set; } // 0x10C.
    public byte CurrentTargetIsPlayer { get; set; } // 0xBDC, set after all behavior and tail work.

    public ScorpionBrainState(ScorpionDamageState damage, ThugMessageQueue messages)
    {
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentNullException.ThrowIfNull(messages);
        Damage = damage;
        Messages = messages;
    }
}
