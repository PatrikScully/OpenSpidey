using OpenSpidey.Core.Gameplay;

namespace OpenSpidey.Core.UI;

/// <summary>Persistent original panel scan bars and last displayed seconds; countdown decay belongs to the shared MissionCountdown.</summary>
public sealed class MissionTimerDisplayState
{
    public byte FirstScanActive { get; set; }
    public byte SecondScanActive { get; set; }
    public int FirstScanPosition { get; set; }
    public int SecondScanPosition { get; set; }
    public int LastSeconds { get; set; }
}

public interface IMissionTimerDisplayHost
{
    int LevelId { get; }
    bool PlayerPresent { get; }
    int PlayerScriptGate { get; }
    bool PlayerSynthesizedInput { get; }
    bool BorderTexturePresent { get; }
    int Random(int range);
    void CallModule(string module, uint function);
    void PlaySound(ushort sound, short volume, int pitch);
    void DrawBorder(int x, int y, int width, int height, bool mirrored);
    void SetTextScale(int scale);
    void SetTextJustification(byte justification);
    void SetTextColour(byte red, byte green, byte blue, int argument);
    string GetCurrentFontName();
    void SetCurrentFont(string name);
    void DrawText(int x, int y, ReadOnlyMemory<byte> text, int argument, uint depth);
    void DrawFlat(float depth, int x, int y, int width, int height, byte red, byte green, byte blue, int argument, int blend);
}

/// <summary>Actual PC Panel_DisplayTimer461D00: unsigned sixty-tick digits, font switching, border and per-render source scan bars.</summary>
public static class MissionTimerDisplay
{
    public static void Display(MissionCountdown countdown, MissionTimerDisplayState state, int screenOffsetY, IMissionTimerDisplayHost host)
    {
        switch (host.LevelId)
        {
            case 513:
                if (!host.PlayerPresent || (host.PlayerScriptGate == 0 && !host.PlayerSynthesizedInput)) host.CallModule("l2a1lsc", 1);
                return;
            case 1281:
                if (!host.PlayerPresent || (host.PlayerScriptGate == 0 && !host.PlayerSynthesizedInput)) host.CallModule("venom", 1);
                return;
            case 1285: host.CallModule("l5a5lsc", 1); return;
            case 2054: host.CallModule("superock", 1); return;
        }
        if (countdown.DisplayEnabled == 0) return;
        if (state.FirstScanActive == 0 && host.Random(20) == 0) { state.FirstScanActive = 1; state.FirstScanPosition = 69632; }
        if (state.SecondScanActive == 0 && host.Random(30) == 0) { state.SecondScanActive = 1; state.SecondScanPosition = 94208; }
        int minutes = unchecked((int)(countdown.RemainingTicks / 3600));
        int seconds = unchecked((int)(countdown.RemainingTicks / 60 - 60 * (countdown.RemainingTicks / 3600)));
        byte[] text = [(byte)'0', (byte)'0', (byte)':', (byte)'0', (byte)'0'];
        if (minutes >= 10) { text[0] = unchecked((byte)(minutes / 10 + 48)); minutes %= 10; }
        text[1] = unchecked((byte)(minutes + 48));
        if (seconds != state.LastSeconds) { host.PlaySound(32, 8192, 0); state.LastSeconds = seconds; }
        if (seconds >= 10) { text[3] = unchecked((byte)(seconds / 10 + 48)); seconds %= 10; }
        text[4] = unchecked((byte)(seconds + 48));
        if (host.BorderTexturePresent)
        {
            host.DrawBorder(219, unchecked(screenOffsetY + 19), 27, 24, false);
            host.DrawBorder(261, unchecked(screenOffsetY + 19), 27, 24, true);
        }
        host.SetTextScale(256); host.SetTextJustification(1); host.SetTextColour(96, 96, 128, 0);
        string savedFont = host.GetCurrentFontName();
        host.SetCurrentFont("sp_fnt01.fnt");
        host.DrawText(234, unchecked(screenOffsetY + 38), text, 0, 4096);
        host.SetCurrentFont(savedFont);
        host.DrawFlat(.5f, 222, unchecked(screenOffsetY + 23), 78, 20, 0, 0, 0, 0, 1);
        if (state.FirstScanActive != 0)
        {
            int position = state.FirstScanPosition >> 12;
            if (position >= 23)
                host.DrawFlat(.5f, 222, unchecked(screenOffsetY + position), 78, position <= 36 ? 7 : unchecked(43 - position), 24, 24, 24, 0, 2);
            else host.DrawFlat(.5f, 222, unchecked(screenOffsetY + 23), 78, unchecked(position - 16), 24, 24, 24, 0, 2);
            state.FirstScanPosition = unchecked(state.FirstScanPosition + 3798);
            if (state.FirstScanPosition > 176128) state.FirstScanActive = 0;
        }
        if (state.SecondScanActive != 0)
        {
            host.DrawFlat(.5f, 222, unchecked(screenOffsetY + (state.SecondScanPosition >> 12)), 78, 1, 32, 32, 32, 0, 2);
            state.SecondScanPosition = unchecked(state.SecondScanPosition + 2334);
            if (state.SecondScanPosition > 176128) state.SecondScanActive = 0;
        }
    }
}
