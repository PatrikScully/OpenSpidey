using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Target selection shares the brain/damage owners; only the sourceC00 environmental aim vector is additional.</summary>
public sealed class ScorpionTargetState(ScorpionBrainState brain)
{
    public ScorpionBrainState Brain { get; } = brain ?? throw new ArgumentNullException(nameof(brain));
    public FixedVector3 EnvironmentAimPosition { get; set; } // Source0xC00.

    /// <summary>The existing sourceBF0/BF4 slot stores one typed pointer/validation pair without a second target cache.</summary>
    public ref PlayerKickHandle CurrentTarget
    {
        get
        {
            if (Brain.Damage.CurrentTargetHandle is ScorpionTargetHandle bound) return ref bound.Value;
            var owner = Brain.Damage.CurrentTargetHandle switch
            {
                null => new ScorpionTargetHandle(default),
                PlayerKickHandle handle => new ScorpionTargetHandle(handle),
                _ => throw new InvalidOperationException("Scorpion target services require a typed source pointer/validation handle in the canonical damage slot.")
            };
            Brain.Damage.CurrentTargetHandle = owner;
            return ref owner.Value;
        }
    }
}

/// <summary>One source pointer/validation slot whose actual value can be passed by reference to recovery.</summary>
public sealed class ScorpionTargetHandle(PlayerKickHandle initial)
{
    private PlayerKickHandle value = initial;
    public ref PlayerKickHandle Value => ref value;
}
