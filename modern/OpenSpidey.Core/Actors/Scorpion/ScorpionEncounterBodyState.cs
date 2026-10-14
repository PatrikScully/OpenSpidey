using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Shared CBaddy fields used by the original Scorpion encounter constructors.</summary>
public abstract class ScorpionEncounterBodyState(int constructionOrdinal)
{
    private ActorProcedureState? procedures;
    private uint unboundBehaviorFlags;
    private int unboundWaitUpdates;
    public int ConstructionOrdinal { get; } = constructionOrdinal;
    public byte ActorPhase { get; set; } = unchecked((byte)constructionOrdinal); // Source0x21D.
    public short Type { get; internal set; }
    public ushort SourceNode { get; internal set; } = ushort.MaxValue;
    public int CurrentWaypoint { get; set; }
    public string? ModelName { get; internal set; }
    public ushort ItemFlags { get; set; } = 2;
    public ushort BodyFlags { get; set; } = 0x216;
    public ushort MinorRadius { get; set; } = 128;
    public int FieldF4 { get; set; } = 128; // CBaddy's independent source field; its role is not established.
    public short SurfaceClearance { get; set; }
    public short GeneralParameter { get; set; } = 32;
    public int PushValue { get; set; } = 64;
    public short Health { get; set; }
    public ushort Damage { get; set; }
    public int WaitUpdates
    {
        get => procedures?.Body.ScriptWaitTicks ?? unboundWaitUpdates;
        set
        {
            if (procedures is null) unboundWaitUpdates = value;
            else procedures.Body.ScriptWaitTicks = value;
        }
    }
    public int State { get; set; }
    public int Substate { get; set; }
    public uint BehaviorFlags
    {
        get => procedures?.BehaviorFlags ?? unboundBehaviorFlags;
        set
        {
            if (procedures is null) unboundBehaviorFlags = value;
            else procedures.BehaviorFlags = value;
        }
    }
    public uint MovementAnimations { get; internal set; }
    public uint IdleAnimations { get; internal set; }
    public AnimationPlaybackState Animation { get; set; } = AnimationPlaybackState.Initial;

    /// <summary>Shares source behavior0x2A8 and timer0x230 with their procedure/motion owner for the body's lifetime.</summary>
    public void BindProcedures(ActorProcedureState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (procedures is not null)
        {
            if (!ReferenceEquals(procedures, owner))
                throw new InvalidOperationException("An encounter body already belongs to another procedure owner.");
            return;
        }
        owner.BehaviorFlags = unboundBehaviorFlags;
        owner.Body.ScriptWaitTicks = unboundWaitUpdates;
        procedures = owner;
    }
}
