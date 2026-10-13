namespace OpenSpidey.Core.Effects;

/// <summary>The original GameFade packed word advanced once by Utils_VblankProcessing.</summary>
public sealed class SourceGameFadeState
{
    public const uint SepiaWord = 0x1E005A;
    public uint Word { get; private set; }
    public bool Active => Word != 0;
    public void Set(uint value) => Word = value;
    public void BeginSepia() => Set(SepiaWord);
    public void AdvanceVblank()
    {
        if (Word != 0) Word = unchecked(Word - ((Word & 0xFFFF0000) != 0 ? 0x10000u : 1u));
    }
}

/// <summary>Screen_SepiaFade's exact Q12 conversion of a source RGB5 pixel, retaining its high bit.</summary>
public static class SourceSepiaPixel
{
    public static ushort Convert(ushort pixel)
    {
        int sum = (pixel & 31) + ((pixel >> 5) & 31) + ((pixel >> 10) & 31);
        int blue = sum * 1365 >> 12;
        int redGreen = sum * 682 >> 12;
        return (ushort)((pixel & 0x8000) | blue << 10 | redGreen << 5 | redGreen);
    }
}
