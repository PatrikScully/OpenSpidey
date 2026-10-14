using System.Buffers.Binary;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player.Scripting;

public interface IPlayerSynthesizedInputHost
{
    void SetVelocity(FixedVector3 velocity);
    bool SmokeTrailPresent(int slot);
    void FadeSmokeTrail(int slot);
    object? RecoverDome();
    void DestroyDome(object dome);
    void ClearDomeHandle();
    object? HeldObject { get; }
    void DestroyHeldObject(object item);
    void ClearHeldObject();
    void SelectAndResetAnimationSounds(ushort animation);
    void RunAnimation(ushort animation, int firstFrame, int lastFrame);
    /// <summary>Clear native input aliases for the same twenty player button records.</summary>
    void ClearInputAliases();
}

/// <summary>Whole4BC1A0 state transition. Script replacement retains the original concurrent VM commands and elapsed clock.</summary>
public static class PlayerSynthesizedInput
{
    public static void Begin(PlayerCombatState combat, PlayerCutsceneSkipState skip,
        Span<PlayerButtonState> pad, IPlayerSynthesizedInputHost host, Action activateScript)
    {
        ArgumentNullException.ThrowIfNull(combat); ArgumentNullException.ThrowIfNull(skip);
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(activateScript);
        if (pad.Length != 20) throw new ArgumentException("The original player pad contains twenty records.", nameof(pad));
        host.SetVelocity(default);
        combat.SynthesizedInputActive = true;
        activateScript();
        combat.FieldAE5 = 0;
        skip.BeginScript();
        for (int slot = 0; slot < 2; slot++)
            if (host.SmokeTrailPresent(slot)) host.FadeSmokeTrail(slot);
        if (host.RecoverDome() is { } dome)
        {
            host.DestroyDome(dome);
            host.ClearDomeHandle();
        }
        if (host.HeldObject is { } item)
        {
            host.DestroyHeldObject(item);
            host.ClearHeldObject();
            ushort animation = (combat.ActionState & 0x10) != 0 ? (ushort)21 : (ushort)0;
            host.SelectAndResetAnimationSounds(animation);
            host.RunAnimation(animation, 0, -1);
        }
        pad.Clear();
        host.ClearInputAliases();
    }

    /// <summary>Original TrigSkipCutSceneScript/SwitchToSynthesizedInput end scan; FF is accepted only after a zero word.</summary>
    public static int StreamLength(ReadOnlySpan<byte> source, int sourceOffset = 0)
    {
        int cursor = 0;
        do
        {
            while (ReadWord(source, ref cursor, sourceOffset) != 0) { }
            RequireWord(source, cursor, sourceOffset);
        } while (BinaryPrimitives.ReadUInt16LittleEndian(source[cursor..]) != 255);
        return checked(cursor + 2);
    }

    private static ushort ReadWord(ReadOnlySpan<byte> source, ref int cursor, int sourceOffset)
    {
        RequireWord(source, cursor, sourceOffset);
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(source[cursor..]);
        cursor += 2;
        return value;
    }
    private static void RequireWord(ReadOnlySpan<byte> source, int cursor, int sourceOffset)
    {
        if (cursor > source.Length - 2)
            throw new InvalidDataException($"Player input stream at 0x{sourceOffset + cursor:X} has no zero/FF terminator.");
    }
}
