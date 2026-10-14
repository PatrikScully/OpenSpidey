using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Actual Jonah module, ordered environmental list, body queries and source weak-handle services.</summary>
public interface IScorpionTargetHost : IScorpionDamageHost
{
    object PlayerIdentity { get; }
    uint CallJonahDecisionModule(object jonah); // Original relocatable module "jonah", slot1.
    PlayerKickHandle JonahHidingPlace(object jonah); // Copy the canonical JonahLocomotionState0x364/368 slot.
    PlayerKickHandle MakeHandle(object identity);
    object? RecoverHandle(ref PlayerKickHandle handle);
    object? FirstEnvironment { get; }
    object? NextEnvironment(object identity);
    ushort BodyType(object identity);
    FixedVector3 BodyPosition(object identity);
    bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument);
    void TryAttachTargetLookAt(object? target, uint completionMask, int rate, int tolerance);
    void SourceDiagnostic(string message);
}
