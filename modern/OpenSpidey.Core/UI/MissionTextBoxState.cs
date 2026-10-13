using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Core.UI;

public readonly record struct MissionTextBoxDefinition(int X, int Y, int Width, int Height, ushort Lifetime,
    byte Red, byte Green, byte Blue);

/// <summary>Original CTextBox CBit: signed16 age, unsigned16 lifetime and retained60Hz timer delta.</summary>
public sealed class MissionTextBoxState(MissionTextBoxDefinition definition, int timer)
{
    public MissionTextBoxDefinition Definition { get; } = definition;
    public short Age { get; set; }
    public int LastTimer { get; private set; } = timer;
    public int ElapsedTicks { get; private set; }
    public bool Dead { get; private set; }
    public bool Protected { get; set; }
    public const byte DisplayAlpha = 0xA0;
    public const float DisplayDepth = 5;
    public uint PackedColour => 0xA0000000u | (uint)Definition.Red << 16 | (uint)Definition.Green << 8 | Definition.Blue;

    public void Move(int timer)
    {
        if (Dead) return;
        ElapsedTicks = unchecked(timer - LastTimer);
        LastTimer = timer;
        Age = unchecked((short)(Age + ElapsedTicks));
        if (Age <= Definition.Lifetime) return;
        // Original Die reports a protected-bit assertion but still writes mDead afterwards.
        Dead = true;
    }

    public HudSourceQuad DisplayQuad()
    {
        int y = Definition.Y, height = Definition.Height;
        int phase = Age;
        if (phase >= 16)
        {
            if (phase <= Definition.Lifetime - 16) return Store(y, height);
            phase = Definition.Lifetime - phase;
        }
        y = unchecked(y + (int)((uint)unchecked((16 - phase) * height) >> 5));
        height = unchecked((int)((uint)unchecked(phase * height) >> 4));
        return Store(y, height);
    }

    private HudSourceQuad Store(int y, int height) => new(unchecked((short)Definition.X), unchecked((short)y),
        unchecked((short)(Definition.X + Definition.Width)), unchecked((short)(y + height)));
}
