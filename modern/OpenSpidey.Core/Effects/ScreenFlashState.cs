namespace OpenSpidey.Core.Effects;

/// <summary>Recovered Flash_Reset, Flash_Screen and Flash_Update state from effects/flash.cpp.</summary>
public sealed class ScreenFlashState
{
    public int FadeCountdown { get; set; }
    public int FlashCountdown { get; set; }
    public int Fading { get; set; }
    public byte Importance { get; set; }
    public int Sort { get; set; }
    public uint CurrentRed { get; set; }
    public uint CurrentGreen { get; set; }
    public uint CurrentBlue { get; set; }
    public uint DeltaRed { get; set; }
    public uint DeltaGreen { get; set; }
    public uint DeltaBlue { get; set; }

    public bool FadeFinished => FadeCountdown == 0;
    public bool Visible => FlashCountdown != 0 || Fading != 0;
    public byte Red => unchecked((byte)(CurrentRed >> 16));
    public byte Green => unchecked((byte)(CurrentGreen >> 16));
    public byte Blue => unchecked((byte)(CurrentBlue >> 16));
    public const byte DisplayAlpha = 0x20;

    public void Reset()
    {
        FlashCountdown = 0;
        FadeCountdown = 0;
        Fading = 0;
        Importance = 0;
    }

    public void Screen(byte red, byte green, byte blue, int frames, byte importance, int sort)
    {
        if (importance < Importance || frames == 0) return;
        CurrentRed = (uint)red << 16;
        CurrentGreen = (uint)green << 16;
        CurrentBlue = (uint)blue << 16;
        // Source unsigned accumulators convert even a negative divisor to uint.
        uint divisor = unchecked((uint)frames);
        DeltaRed = CurrentRed / divisor;
        DeltaGreen = CurrentGreen / divisor;
        DeltaBlue = CurrentBlue / divisor;
        FlashCountdown = frames;
        Importance = importance;
        Sort = sort;
    }

    public void Update()
    {
        unchecked
        {
            if (Fading != 0)
            {
                if (FadeCountdown == 0) return;
                if (--FadeCountdown == 0)
                    CurrentRed = CurrentGreen = CurrentBlue = 0xFF0000;
                else
                {
                    // Recovered source uses dR for all three axes, including unequal deltas.
                    CurrentRed += DeltaRed;
                    CurrentGreen += DeltaRed;
                    CurrentBlue += DeltaRed;
                }
            }
            else if (FlashCountdown != 0)
            {
                if (--FlashCountdown == 0) Importance = 0;
                else
                {
                    CurrentRed -= DeltaRed;
                    CurrentGreen -= DeltaGreen;
                    CurrentBlue -= DeltaBlue;
                }
            }
        }
    }
}
