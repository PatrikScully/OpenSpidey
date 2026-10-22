using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Six-word CSmokeJet script operands from CScriptOnlyBaddy (0x407A50) into ctor 0x4AF470.</summary>
public readonly record struct ControlActorSmokeJetSpec(
    ushort Length,
    ushort RawDuration,
    ushort Red,
    ushort Green,
    ushort Blue,
    byte ScrollSpeed,
    uint TextureChecksum,
    bool DamageBaddies);

/// <summary>Owned field_324 smoke jet: anchor pose, duration bookkeeping and teardown.</summary>
public sealed class ControlActorSmokeJet
{
    public ControlActorSmokeJet(ControlActorSmokeJetSpec spec, FixedVector3 anchor, ActorAngles angles)
    {
        Spec = spec;
        Anchor = anchor;
        Angles = angles;
        Duration = NormalizeDuration(spec.RawDuration, out bool extended);
        ExtendedDuration = extended;
    }

    public ControlActorSmokeJetSpec Spec { get; }
    public FixedVector3 Anchor { get; private set; }
    public ActorAngles Angles { get; }
    public ushort Duration { get; }
    public bool ExtendedDuration { get; }
    public bool Alive { get; private set; } = true;

    /// <summary>Immediate pushed for sewer / damage-baddies jets (0x429B).</summary>
    public const uint SewerChecksum = 0xB159E2ABu;
    /// <summary>Immediate pushed for ground jets (0x42A1).</summary>
    public const uint GroundChecksum = 0x68B7B136u;

    public static ControlActorSmokeJetSpec ReadSpec(ActorScriptCursor operands, bool damageBaddies, uint checksum)
    {
        ushort length = operands.ReadWord();
        ushort duration = operands.ReadWord();
        ushort red = operands.ReadWord();
        ushort green = operands.ReadWord();
        ushort blue = operands.ReadWord();
        byte scroll = unchecked((byte)operands.ReadWord());
        return new(length, duration, red, green, blue, scroll, checksum, damageBaddies);
    }

    public void SetAnchor(FixedVector3 position) => Anchor = position;

    public void Destroy() => Alive = false;

    private static ushort NormalizeDuration(ushort raw, out bool extended)
    {
        if (raw >= 0x3E8)
        {
            extended = true;
            return unchecked((ushort)(raw - 0x3E8));
        }
        extended = false;
        return raw;
    }
}
