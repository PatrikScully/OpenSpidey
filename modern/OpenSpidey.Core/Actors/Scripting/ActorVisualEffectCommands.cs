using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorVisualEffectHost
{
    int Random(int bound);
    void CreateLaserFlash(LaserFlashParameters parameters);
    void TryCreateWibbling(ExplosionModelParameters parameters);
}

/// <summary>Original42A7/42A8 laser words and42A6's exact operand widths/positional RNG order.</summary>
public static class ActorVisualEffectCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, FixedVector3 position, IActorVisualEffectHost host)
    {
        if (opcode is not (0x42A6 or 0x42A7 or 0x42A8)) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(host);
        if (opcode is 0x42A7 or 0x42A8)
        {
            ushort scale = operands.ReadWord(), half = operands.ReadWord();
            byte red = unchecked((byte)operands.ReadWord()), green = unchecked((byte)operands.ReadWord());
            byte blue = unchecked((byte)operands.ReadWord()), lifetime = unchecked((byte)operands.ReadWord());
            host.CreateLaserFlash(new(position, scale, half, red, green, blue, lifetime));
            return true;
        }
        ushort effect = operands.ReadWord();
        if (effect == 2) throw new NotSupportedException("Original42A6 sub2 requires its genuine CFlameExplosion owner.");
        if (effect != 3) return true; // The original0/1/default branches perform no effect and consume only this word.
        // The PC decoder zero-extends the first seven words, then sign-extends vertical speed/range and spread.
        int model = operands.ReadWord(), delay = operands.ReadWord(), scaleStart = operands.ReadWord(), scaleStep = operands.ReadWord(), verticalScale = operands.ReadWord();
        int growthTicks = operands.ReadWord(), fade = operands.ReadWord(), verticalSpeed = SignedWord(), verticalRange = SignedWord(), spread = SignedWord();
        int bound = 2 * spread + 1;
        FixedVector3 jitter = new(unchecked((host.Random(bound) - spread) << 12),
            unchecked((host.Random(bound) - spread) << 12), unchecked((host.Random(bound) - spread) << 12));
        host.TryCreateWibbling(new(position + jitter, "expgrnd", model, delay, scaleStart, scaleStep, verticalScale,
            growthTicks, fade, verticalSpeed, verticalRange));
        return true;

        int SignedWord() => unchecked((short)operands.ReadWord());
    }
}
