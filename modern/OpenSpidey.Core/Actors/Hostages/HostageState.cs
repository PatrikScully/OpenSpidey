using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Core.Actors.Hostages;

public enum HostageBehaviorState { Initializing, WaitingForPlayer, FollowingWaypoints, Begging, GettingUp, Disappearing, Dying }

/// <summary>Original CHostage fields and male/female animation banks.</summary>
public sealed class HostageState(int sourceNode, int sourceConstructionOrdinal = 0)
{
    private ushort inputFlags;
    private ActorScriptState? sourceSignals;
    public int SourceNode { get; } = sourceNode;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public byte ActorPhase { get; set; } = unchecked((byte)sourceConstructionOrdinal);
    public int CurrentWaypoint { get; set; } = sourceNode;
    public int Type { get; private set; }
    public string? ModelName { get; private set; }
    public short SurfaceClearance { get; set; }
    public ushort MinorRadius { get; set; } = 128;
    public short Health { get; set; }
    public int PushValue { get; set; } = 64;
    public short GeneralParameter { get; set; } = 32;
    public uint BodyFlags { get; set; } = 0x216;
    public uint RescueFlags { get; set; }
    public ushort InputFlags
    {
        get => sourceSignals?.PendingSignals ?? inputFlags;
        set
        {
            if (sourceSignals is { } source) source.PendingSignals = value;
            else inputFlags = value;
        }
    }
    public ushort ItemFlags { get; set; }
    public uint TintColor { get; set; }
    public uint Transparency { get; set; }
    public int ShadowScale { get; set; } = 48;
    public FixedVector3 ShadowPosition { get; set; }
    public int GroundReferenceY { get; set; }
    public int GroundHeight { get; set; }
    public int WaitUpdates { get; set; }
    public int DeathUpdates { get; set; }
    public int DeathFadeUpdates { get; set; }
    public int DistanceSampleClock { get; set; }
    public int CachedPlayerDistance { get; set; }
    public HostageBehaviorState State { get; set; }
    public int Substate { get; set; }
    public object? ShooterHandle { get; set; }
    public uint MovementAnimations { get; internal set; } = 0x03030404;
    public uint IdleAnimations { get; internal set; } = 4;
    public byte IdleAnimation => (byte)IdleAnimations;

    /// <summary>Source mInputFlags is one retained ushort shared by Trig_SendSignal and CHostage::CheckIfFreed.</summary>
    public void BindSourceSignals(ActorScriptState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceSignals is not null && !ReferenceEquals(sourceSignals, source))
            throw new InvalidOperationException("Hostage signal ownership cannot be transferred to another source body.");
        sourceSignals = source;
    }

    // The factory calls this after the constructor's original script parsing.
    public void SetHostageType(int type, Action<string> initializeModel)
    {
        string model = type switch { 305 => "hostage", 315 => "hostagef", _ => throw new NotSupportedException($"Source CHostage type{type} is unsupported.") };
        Type = type; ModelName = model;
        initializeModel(model);
        SurfaceClearance = 100;
        MovementAnimations = type == 305 ? 0x03030404u : 0x04040504u;
        IdleAnimations = type == 305 ? 4u : 3u;
    }
}

public sealed class HostageMessage(int code, object? senderHandle = null)
{
    public FixedVector3 Vector0 { get; set; }
    public int Code { get; } = code;
    public object? SenderHandle { get; } = senderHandle;
    public int Flags { get; set; }
    public int Age { get; set; }
    public HostageMessage? Next { get; internal set; }
}

/// <summary>Original CMessage head insertion and deferred deletion for a hostage actor.</summary>
public sealed class HostageMessageQueue
{
    public HostageMessage? Head { get; private set; }
    public void Enqueue(HostageMessage message) { message.Next = Head; Head = message; }
    public void Cleanup(bool all = false)
    {
        HostageMessage? previous = null;
        for (HostageMessage? current = Head; current is not null;)
        {
            HostageMessage? next = current.Next;
            if (all || (current.Flags & 1) != 0 || current.Code == 0)
            {
                if (previous is null) Head = next; else previous.Next = next;
                current.Next = null;
            }
            else previous = current;
            current = next;
        }
    }
}
