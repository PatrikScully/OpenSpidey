using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IControlActorEffectHost
{
    void CreateCompass(FixedVector3 position);
    void DestroyCompass();
    uint PlayGlobalSound(uint sound);
    uint PlayPositionSound(uint sound, FixedVector3 position);
    void StopSound(uint handle);
    void UpdateSoundPosition(uint handle, FixedVector3 position);
}

/// <summary>CScriptOnlyBaddy's actual compass ownership and328/32C sound fields.</summary>
public sealed class ControlActorEffects(ActorScriptState actor, IControlActorEffectHost host)
{
    private bool destroyed;
    public uint SoundHandle { get; set; }
    public short SoundPositionPeriod { get; set; } = -1;
    public ControlActorSmokeJet? SmokeJet { get; private set; }

    public bool Execute(ushort opcode, ActorScriptCursor operands, FixedVector3 position, ActorAngles angles, ActorScriptEnvironment environment)
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        switch (opcode)
        {
            case 0x450C:
                host.CreateCompass(position);
                actor.Flags |= 0x80;
                return true;
            case 0x428D:
                StopPreviousSound();
                SoundHandle = host.PlayGlobalSound(LevelSound(operands.ReadWord()));
                SoundPositionPeriod = -1;
                return true;
            case 0x450B:
                StopPreviousSound();
                SoundHandle = host.PlayPositionSound(LevelSound(operands.ReadWord()), position);
                SoundPositionPeriod = unchecked((short)operands.ReadWord());
                return true;
            case 0x4507:
                StopPreviousSound();
                SoundHandle = host.PlayGlobalSound(operands.ReadWord());
                SoundPositionPeriod = -1;
                return true;
            case 0x4508:
                StopPreviousSound();
                ushort sound = operands.ReadWord();
                SoundPositionPeriod = unchecked((short)operands.ReadWord());
                SoundHandle = host.PlayPositionSound(sound, position);
                return true;
            case 0x4509:
                StopPreviousSound();
                SoundHandle = 0;
                return true;
            case 0x428E: case 0x4290:
                ushort global = operands.ReadWord();
                if (!(actor.SkipDuringDeath && environment.FastForwardDeath))
                    host.PlayGlobalSound(ActorScriptSoundCode.Global(global, opcode == 0x428E));
                return true;
            case 0x428F: case 0x4291:
                ushort positional = operands.ReadWord();
                if (!(actor.SkipDuringDeath && environment.FastForwardDeath))
                {
                    uint code = unchecked((uint)(short)positional);
                    host.PlayPositionSound(code | (opcode == 0x428F ? 0x8000u : 0), position);
                }
                return true;
            case 0x429B:
                return StartSmokeJet(operands, position, angles, damageBaddies: true, ControlActorSmokeJet.SewerChecksum);
            case 0x42A1:
                return StartSmokeJet(operands, position, angles, damageBaddies: false, ControlActorSmokeJet.GroundChecksum);
            case 0x429F:
                DestroySmokeJet();
                return true;
            case 0x4513:
                // SimbyPokeRadius: enables the CScriptOnlyBaddy AI sweep that calls CSimby::ExplosionReaction.
                actor.Flags |= 0x100;
                actor.SimbyPokeRadius = unchecked((short)operands.ReadWord());
                return true;
            default: return false;
        }
    }

    /// <summary>The source AI uses signed TTime division and invokes ModifyPos even for a zero handle.</summary>
    public void Advance(uint clock, FixedVector3 position)
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        if (SoundPositionPeriod != -1 && unchecked((int)clock) % SoundPositionPeriod == 0)
            host.UpdateSoundPosition(SoundHandle, position);
        SmokeJet?.SetAnchor(position);
    }

    public void Destroy()
    {
        if (destroyed) return;
        destroyed = true;
        StopPreviousSound();
        DestroySmokeJet();
        if ((actor.Flags & 0x80) != 0) host.DestroyCompass();
    }

    private bool StartSmokeJet(ActorScriptCursor operands, FixedVector3 position, ActorAngles angles, bool damageBaddies, uint checksum)
    {
        // Original print_if_false then overwrites field_324 without freeing; managed code retires the previous jet.
        if (SmokeJet is not null) DestroySmokeJet();
        ControlActorSmokeJetSpec spec = ControlActorSmokeJet.ReadSpec(operands, damageBaddies, checksum);
        SmokeJet = new ControlActorSmokeJet(spec, position, angles);
        return true;
    }

    private void DestroySmokeJet()
    {
        if (SmokeJet is null) return;
        SmokeJet.Destroy();
        SmokeJet = null;
    }

    private void StopPreviousSound()
    {
        if (SoundHandle != 0) host.StopSound(SoundHandle);
    }
    private static uint LevelSound(ushort operand) => unchecked((uint)(short)operand) | 0x8000u;
}
