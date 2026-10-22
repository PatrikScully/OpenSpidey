namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>Control actor scheduling state, owned by its actor rather than fixed-address fields.</summary>
public sealed class ActorScriptState
{
    public short ActorType { get; init; } = 203;
    public ushort NodeId { get; init; } = ushort.MaxValue;
    public bool Active { get; set; }
    public bool Retired { get; internal set; }
    public uint Flags { get; set; }
    public uint BehaviorFlags { get; set; }
    public ushort PendingSignals { get; set; }
    public ushort SignalSnapshot { get; internal set; }
    public byte PulsesReceived { get; set; }
    public int WaitTicks { get; set; }
    public int WaitFrames { get; set; }
    public bool SkipDuringDeath { get; set; }
    public bool DeathPulseSent { get; internal set; }
    /// <summary>CScriptOnlyBaddy 0x32E, radius for SimbyPokeRadius (0x4513).</summary>
    public short SimbyPokeRadius { get; set; }

    public bool TryMarkDeathPulseSent()
    {
        if (DeathPulseSent || NodeId == ushort.MaxValue) return false;
        DeathPulseSent = true;
        return true;
    }
}

public readonly record struct ActorScriptEnvironment(bool FastForwardDeath = false, bool WhatIf = false, bool Symbiote = false,
    int LevelId = 0);

public readonly record struct ActorScriptStep(int Instructions, bool BudgetReached);
