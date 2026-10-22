using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Core.Gameplay;

public readonly record struct SubwayProgressPiece(HudSourceQuad Quad, uint Colour);

public interface ISubwayProgressHost
{
    int ScreenOffsetY { get; }
    int WideScreenValue { get; }
    uint Clock { get; }
    ushort EndLevelNode { get; }
    bool TexturePresent(int index);
    void FindTexture(int index, string name);
    void SendPulse(ushort node);
    SubwayProgressPiece CreatePiece(int texture, int x, int y, int width, int height);
    void DrawPiece(int texture, SubwayProgressPiece piece, float depth);
}

/// <summary>Original L5A5LSC_DisplayProgressBar (0x4489B0): subway panel textures, shared bomb countdown arm, EndLevelNode pulse.</summary>
public static class SubwayProgress
{
    public const uint DurationTicks = 9000;

    public static IReadOnlyList<string> TextureNames { get; } = Array.AsReadOnly(new[]
    {
        "SubwayPanel_Track", "SubwayPanel_RightEnd", "SubwayPanel_LeftEnd", "SubwayPanel_Car"
    });

    private static readonly (int X, int Y, int W, int H)[] BarRects =
    [
        (-2, 0, 26, 10),
        (6, 0, 19, 10),
        (7, 2, 16, 10),
        (6, 2, 16, 10)
    ];

    public static void Display(MissionCountdown countdown, ISubwayProgressHost host)
    {
        ArgumentNullException.ThrowIfNull(countdown);
        ArgumentNullException.ThrowIfNull(host);
        for (int texture = 0; texture < TextureNames.Count; texture++)
            if (!host.TexturePresent(texture)) host.FindTexture(texture, TextureNames[texture]);

        byte wasArmed = countdown.DisplayEnabled;
        countdown.Rate = 4096;
        if (wasArmed == 0)
        {
            countdown.DisplayEnabled = countdown.Running = 1;
            countdown.RemainingTicks = DurationTicks;
            countdown.LastUpdateTick = host.Clock;
            if (host.WideScreenValue > 0) return;
        }
        else if (countdown.RemainingTicks == 0)
        {
            host.SendPulse(host.EndLevelNode);
            return;
        }

        int offset = host.ScreenOffsetY;
        int carOffset = unchecked((int)(((DurationTicks - countdown.RemainingTicks) * 176) >> 12)) + 0x26;
        (int x, int y, int w, int h) = BarRects[0];
        host.DrawPiece(3, host.CreatePiece(3, x + carOffset, y - offset + 0xD3, w, h), 1);
        for (int tile = 0; tile < 0x1A4; tile += 0x1E)
        {
            (x, y, w, h) = BarRects[1];
            host.DrawPiece(0, host.CreatePiece(0, tile + x + 0x26, y - offset + 0xD9, w, h), 4);
        }
        (x, y, w, h) = BarRects[2];
        host.DrawPiece(2, host.CreatePiece(2, x + 0xB, y - offset + 0xD7, w, h), 3);
        (x, y, w, h) = BarRects[3];
        host.DrawPiece(1, host.CreatePiece(1, x + 0x1CA, y - offset + 0xD7, w, h), 3);
    }

    /// <summary>Texture overload of 462CD0: no animation-frame offsets, original 6554 width rounding and short stores.</summary>
    public static SubwayProgressPiece CreatePanelPiece(int x, int y, int width, int height)
    {
        int stretched = unchecked(width * 6554);
        if ((stretched & 4095) >= 2048) stretched = unchecked(stretched + 4096);
        stretched >>= 12;
        return new(new(unchecked((short)x), unchecked((short)y), unchecked((short)(x + stretched)), unchecked((short)(y + height))), 0xFF808080);
    }

    public static void Clear(MissionCountdown countdown, uint clock, Action<int> releaseTexture)
    {
        ArgumentNullException.ThrowIfNull(countdown);
        ArgumentNullException.ThrowIfNull(releaseTexture);
        countdown.DisplayEnabled = countdown.Running = 0;
        countdown.LastUpdateTick = clock;
        for (int texture = 0; texture < TextureNames.Count; texture++) releaseTexture(texture);
    }
}
