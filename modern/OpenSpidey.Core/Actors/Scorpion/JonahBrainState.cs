using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Jonah AI fields composed with the same constructor, motion, procedure and message owners.</summary>
public sealed class JonahBrainState
{
    private ActorScriptState? sourceInputs;
    private ushort unboundInputSignals;
    public JonahState Jonah { get; }
    public ActorProcedureState Actor { get; }
    public ActorProcedureList Procedures { get; }
    // The existing queue implements the shared CMessage layout, insertion and cleanup rules.
    public ThugMessageQueue Messages { get; }
    public ushort InputSignals
    {
        get => sourceInputs?.PendingSignals ?? unboundInputSignals;
        set
        {
            if (sourceInputs is null) unboundInputSignals = value;
            else sourceInputs.PendingSignals = value;
        }
    } // Source +0x44, independent of physical collision +0xE0.
    public PlayerKickHandle ScorpionHandle { get; set; } // Source +0x35C.
    public int WaitTicks
    {
        get => Actor.Body.LimitedMotionTicks;
        set => Actor.Body.LimitedMotionTicks = value;
    } // Source +0x1F8, shared with limited physical motion; independent of constructor +0x230.
    public int SpeechSuppressionTicks { get; set; } // Source +0x328.
    public int PanicSoundTicks { get; set; } // Source +0x32C.
    public int PreviousDisplacementX { get; set; } // Source +0x334.
    public int PreviousDisplacementZ { get; set; } // Source +0x338.
    public FixedVector3 PanicTarget { get; set; } // Source +0x34C.
    public int LastFootstepSound { get; set; } // Source +0x358.
    // The PC stores both the alternate voice selection and its timestamp in this one word.
    public uint PleaClockOrAlternateVoice { get; set; } // Source +0x378.
    public int LastRegularPleaVoice { get; set; } // Source +0x37C.
    public int StandingY { get; set; } // Source +0x29C.
    public int GroundY { get; set; } // Source +0x2A0.
    public short CachedStateIndex { get; set; } // Source +0x314.

    public JonahBrainState(JonahState jonah, ActorProcedureState actor, ActorProcedureList procedures,
        ThugMessageQueue messages)
    {
        ArgumentNullException.ThrowIfNull(jonah);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(procedures);
        ArgumentNullException.ThrowIfNull(messages);
        Jonah = jonah;
        Actor = actor;
        Procedures = procedures;
        Messages = messages;
        Jonah.BindProcedures(actor);
    }

    public FixedVector3 Position { get => Actor.Body.Position; set => Actor.Body.Position = value; }
    public void EnterState(int state) { Jonah.State = state; Jonah.Substate = 0; }
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void SelectAnimationSet(uint movement, uint idle)
    { Jonah.MovementAnimations = movement; Jonah.IdleAnimations = idle; }

    /// <summary>Shares the existing script/registry input word without overwriting already queued native signals.</summary>
    public void BindInputSignals(ActorScriptState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (sourceInputs is not null && !ReferenceEquals(sourceInputs, owner))
            throw new InvalidOperationException("Jonah input signals already belong to another source body.");
        sourceInputs = owner;
    }

    public ushort CheckStateFlags()
    {
        ReadOnlySpan<ushort> states = [8, 6, 666];
        if (CachedStateIndex < 0)
        {
            if (Jonah.State == -CachedStateIndex) return 0;
        }
        else if (CachedStateIndex < states.Length && Jonah.State == states[CachedStateIndex]) return 1;
        for (short index = 0; index < states.Length; index++)
        {
            if (Jonah.State != states[index]) continue;
            CachedStateIndex = index;
            return 1;
        }
        CachedStateIndex = unchecked((short)-Jonah.State);
        return 0;
    }
}
