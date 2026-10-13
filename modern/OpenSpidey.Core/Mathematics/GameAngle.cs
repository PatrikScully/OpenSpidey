namespace OpenSpidey.Core.Mathematics;

/// <summary>Recovered 12-bit angle conversion and the original initialized Q12 heading table.</summary>
public static class GameAngle
{
    private static readonly (short Sine, short Cosine)[] headings = BuildHeadings();

    public static int Atan2(int x, int y)
    {
        if (y == 0) return Math.Sign(x) * 1024;
        return (int)(Math.Atan2(x, y) * 651.0006103515625);
    }

    public static (short Sine, short Cosine) Heading(int angle) => headings[angle & 4095];

    private static (short, short)[] BuildHeadings()
    {
        var table = new (short, short)[4096];
        for (int i = 0; i < table.Length; i++)
        {
            double angle = i * 0.001536096911877394;
            table[i] = ((short)(Math.Sin(angle) * 4096.0), (short)(Math.Cos(angle) * 4096.0));
        }
        return table;
    }
}
