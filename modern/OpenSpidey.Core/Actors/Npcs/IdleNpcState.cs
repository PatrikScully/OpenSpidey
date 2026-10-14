namespace OpenSpidey.Core.Actors.Npcs;

/// <summary>Source CMJ (303), CLizard (326) and CSubmariner (719): CBaddy bodies with a toggle/idle AI and no bytecode.</summary>
public enum IdleNpcKind { MaryJane, Lizard, Submariner }

/// <summary>Original constructor fields (0x459330, 0x44A290, 0x4CBA80) and the CBaddy::Die(0) flags they share.</summary>
public sealed class IdleNpcState
{
    public IdleNpcState(IdleNpcKind kind, int node, int sourceConstructionOrdinal)
    {
        Kind = kind;
        SourceNode = node;
        SourceConstructionOrdinal = sourceConstructionOrdinal;
    }

    public IdleNpcKind Kind { get; }
    public int SourceNode { get; }
    public int SourceConstructionOrdinal { get; }
    public byte ActorPhase => unchecked((byte)SourceConstructionOrdinal);
    public short TriggerType => TriggerTypeOf(Kind);
    /// <summary>CItem 0x38. CSubmariner's constructor stores 326, the CLizard type, rather than its trigger type 719.</summary>
    public short BodyType => Kind == IdleNpcKind.MaryJane ? (short)303 : (short)326;
    public string ModelName => Kind switch { IdleNpcKind.MaryJane => "mj", IdleNpcKind.Lizard => "lizard", _ => "mariner" };
    public string ModuleName => ModuleNameOf(Kind);
    /// <summary>Per-class SLight records M3d_MJLight, M3d_LizardLight and M3d_SubmarinerLight.</summary>
    public uint LightAddress => Kind switch { IdleNpcKind.MaryJane => 0x54D830, IdleNpcKind.Lizard => 0x54B530, _ => 0x5573D0 };
    /// <summary>Only CMJ and CSubmariner test the abort byte 0x60CFC4 at construction; CLizard never dies by itself.</summary>
    public bool DiesOnFastForwardAtConstruction => Kind != IdleNpcKind.Lizard;
    public ushort MinorRadius => 0;
    /// <summary>CBody 0x46.</summary>
    public uint BodyFlags { get; set; } = 0x216;
    /// <summary>CBaddy 0x2A8; bit 0x4000 suppresses the death pulse.</summary>
    public uint CombatFlags { get; set; }
    /// <summary>CBaddy 0x211.</summary>
    public bool DeathPulseSent { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    public static short TriggerTypeOf(IdleNpcKind kind) => kind switch
    {
        IdleNpcKind.MaryJane => 303,
        IdleNpcKind.Lizard => 326,
        _ => 719
    };
    public static string ModuleNameOf(IdleNpcKind kind) => kind switch
    {
        IdleNpcKind.MaryJane => "mj",
        IdleNpcKind.Lizard => "lizard",
        _ => "submarin"
    };
    public static IdleNpcKind KindOf(short triggerType) => triggerType switch
    {
        303 => IdleNpcKind.MaryJane,
        326 => IdleNpcKind.Lizard,
        719 => IdleNpcKind.Submariner,
        _ => throw new NotSupportedException($"Source type {triggerType} is not an idle CBaddy NPC.")
    };

    /// <summary>CBaddy::Die (0x404320) state 0.</summary>
    public void Die(IIdleNpcHost host)
    {
        if (IsDead) return;
        if ((CombatFlags & 0x4000) == 0 && !DeathPulseSent && SourceNode != ushort.MaxValue)
        {
            DeathPulseSent = true;
            host.SendDeathPulse();
        }
        BodyFlags |= 0x40;
        host.ItemFlags |= 1;
        host.MarkDead();
    }
}

public interface IIdleNpcHost
{
    /// <summary>CBody 0x44 input flags; bit 1 is the trigger signal.</summary>
    ushort InputFlags { get; set; }
    ushort ItemFlags { get; set; }
    /// <summary>CItem 0x12A, read as unsigned.</summary>
    int AnimationId { get; }
    bool HasMessages { get; }
    bool FastForwardRequested { get; }
    void CleanUpMessages(bool all, bool keep);
    void CycleAnimation(int animation, int direction);
    void BuildTransform();
    void SendDeathPulse();
    void MarkDead();
}

/// <summary>Original CMJ::AI (0x459450), CLizard::AI (0x44A3A0) and CSubmariner::AI (0x4CBBA0).</summary>
public sealed class IdleNpcBrain(IdleNpcState state, IIdleNpcHost host)
{
    public void Construct()
    {
        host.CycleAnimation(0, 1);
        if (state.DiesOnFastForwardAtConstruction && host.FastForwardRequested) state.Die(host);
    }

    public void Advance()
    {
        switch (state.Kind)
        {
            case IdleNpcKind.MaryJane:
                if (host.HasMessages) host.CleanUpMessages(true, false);
                if (ConsumeSignal()) host.CycleAnimation(1 - host.AnimationId, 1);
                host.BuildTransform();
                break;
            case IdleNpcKind.Lizard:
                if (ConsumeSignal()) host.CycleAnimation(host.AnimationId == 0 ? 5 : 0, 1);
                if (host.HasMessages) host.CleanUpMessages(true, false);
                host.BuildTransform();
                break;
            case IdleNpcKind.Submariner:
                if (host.FastForwardRequested) state.Die(host);
                else host.BuildTransform();
                break;
            default:
                throw new InvalidDataException("Idle NPC kind is outside the source class set.");
        }
    }

    private bool ConsumeSignal()
    {
        ushort flags = host.InputFlags;
        if ((flags & 1) == 0) return false;
        host.InputFlags = unchecked((ushort)(flags & 0xFFFE));
        return true;
    }
}
