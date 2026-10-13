namespace OpenSpidey.Assets.Textures;

/// <summary>Original Pal_ProcessPalette: magenta key, transparent zero and opaque palette handling.</summary>
public static class PsxPalette
{
    public static void Process(Span<ushort> palette)
    {
        if (palette.Length is not (16 or 256)) throw new ArgumentException("Original PSX palettes contain16 or256 entries.", nameof(palette));
        bool keyed = false;
        foreach (ushort colour in palette) if ((colour & 0x7FFF) == 0x7C1F) { keyed = true; break; }
        bool transparentZero = palette.Contains((ushort)0);
        for (int index = 0; index < palette.Length; index++)
        {
            ushort colour = palette[index];
            if (keyed && (colour & 0x7FFF) == 0x7C1F) palette[index] = 0;
            else if (!keyed && transparentZero && colour == 0) palette[index] = 0;
            else palette[index] = (ushort)(colour | 0x8000);
        }
    }
}
