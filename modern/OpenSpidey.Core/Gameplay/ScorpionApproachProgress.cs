using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Core.Gameplay;

public readonly record struct ScorpionProgressPiece(HudSourceQuad Quad, uint Colour);

public interface IScorpionApproachProgressHost
{
    int Difficulty { get; }
    int ScreenOffsetY { get; }
    bool AlternateStory { get; }
    bool SpeechAllowed { get; }
    bool SpeechPlaying { get; }
    int SpeechBank { get; }
    int SpeechSlot { get; }
    bool TexturePresent(int index);
    void FindTexture(int index, string name);
    void PlaySpeech(int bank, int slot, int priority);
    ScorpionProgressPiece CreatePiece(int texture, int x, int y, int width, int height);
    void DrawPiece(int texture, ScorpionProgressPiece piece, float depth);
}

/// <summary>Original4477F0 speech thresholds, portrait pulse, signed-short panel geometry and final portrait overlap.</summary>
public static class ScorpionApproachProgress
{
    public static IReadOnlyList<string> TextureNames { get; } = Array.AsReadOnly(new[]
    {
        "scorpion_approach", "jonah_approach", "scorpion_approach_bar",
        "scorpion_approach_bar_leftend", "scorpion_approach_bar_rightend"
    });
    private static readonly uint[] divisors = [59, 59, 46, 33];

    public static void Display(MissionCountdown countdown, ScorpionApproachState state, IScorpionApproachProgressHost host)
    {
        ArgumentNullException.ThrowIfNull(countdown); ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host);
        for (int texture = 0; texture < TextureNames.Count; texture++)
            if (!host.TexturePresent(texture)) host.FindTexture(texture, TextureNames[texture]);

        // The original samples speech before any warning below starts or replaces playback.
        int scorpionGrow = 0, jonahGrow = 0;
        if (host.SpeechPlaying && host.SpeechBank == 8)
        {
            if (host.SpeechSlot is 6 or 11 or 9 or 14) scorpionGrow = 2048;
            else if (host.SpeechSlot is 7 or 12 or 10 or 15) jonahGrow = 2048;
        }
        for (int warning = 0; warning < 4; warning++)
        {
            if (state.PrimaryWarnings[warning] != 0)
            {
                if (state.SecondaryWarnings[warning] != 0 || !host.SpeechAllowed) continue;
                state.SecondaryWarnings[warning] = 1;
                bool alternate = host.AlternateStory;
                int slot = warning switch
                {
                    0 => alternate ? 15 : 9,
                    1 => alternate ? 13 : -1,
                    2 => alternate ? -1 : 7,
                    3 => alternate ? 15 : 10,
                    _ => throw new InvalidOperationException()
                };
                if (slot >= 0) host.PlaySpeech(8, slot, 0);
            }
            else
            {
                uint duration = ScorpionApproachCountdown.Duration(host.Difficulty);
                uint threshold = warning switch { 0 => duration * 3 / 4, 1 => duration / 2, 2 => duration / 4, 3 => duration / 8,
                    _ => throw new InvalidOperationException() };
                if (countdown.RemainingTicks >= threshold) continue;
                state.PrimaryWarnings[warning] = 1;
                int slot = warning switch
                {
                    0 => host.AlternateStory ? 14 : 10,
                    1 => host.AlternateStory ? 11 : 8,
                    2 => host.AlternateStory ? 12 : 6,
                    3 => host.AlternateStory ? 14 : 9,
                    _ => throw new InvalidOperationException()
                };
                host.PlaySpeech(8, slot, 0);
            }
        }

        uint remaining = countdown.RemainingTicks;
        int ramp = 0;
        if (remaining < 32) { scorpionGrow = unchecked((int)(remaining << 7)); ramp = 32 - (int)remaining; }
        int difficulty = host.Difficulty;
        uint x = unchecked((ScorpionApproachCountdown.Duration(difficulty) - remaining) / divisors[difficulty] + (uint)ramp + 144);
        ScorpionProgressPiece? scorpion = null;
        if (host.TexturePresent(0))
        {
            scorpion = Expand(host.CreatePiece(0, unchecked((int)x), unchecked(host.ScreenOffsetY + 11), 20, 26), scorpionGrow);
            host.DrawPiece(0, scorpion.Value, 1);
        }
        if (host.TexturePresent(1))
        {
            ScorpionProgressPiece jonah = countdown.RemainingTicks >= 32
                ? Expand(host.CreatePiece(1, 446, unchecked(host.ScreenOffsetY + 11), 20, 26), jonahGrow)
                : scorpion ?? throw new InvalidDataException("Final Scorpion approach overlap requires the preceding genuine portrait poly.");
            host.DrawPiece(1, jonah, 2);
        }
        // Texture presence is reread for each tile, as in the original callback-bearing draw loop.
        for (int tile = 138; tile < 480; tile += 18)
            if (host.TexturePresent(2)) host.DrawPiece(2, host.CreatePiece(2, tile, unchecked(host.ScreenOffsetY + 23), 12, 10), 4);
        if (host.TexturePresent(3)) host.DrawPiece(3, host.CreatePiece(3, 133, unchecked(host.ScreenOffsetY + 23), 4, 8), 3);
        if (host.TexturePresent(4)) host.DrawPiece(4, host.CreatePiece(4, 480, unchecked(host.ScreenOffsetY + 23), 4, 8), 3);
    }

    /// <summary>Texture overload of462CD0: no animation-frame offsets, original6554 width rounding and short stores.</summary>
    public static ScorpionProgressPiece CreatePanelPiece(int x, int y, int width, int height)
    {
        int stretched = unchecked(width * 6554);
        if ((stretched & 4095) >= 2048) stretched = unchecked(stretched + 4096);
        stretched >>= 12;
        return new(new(unchecked((short)x), unchecked((short)y), unchecked((short)(x + stretched)), unchecked((short)(y + height))), 0xFF808080);
    }

    private static ScorpionProgressPiece Expand(ScorpionProgressPiece piece, int growth)
    {
        HudSourceQuad quad = piece.Quad;
        int x = unchecked(growth * (quad.Right - quad.Left)) >> 12;
        int y = unchecked(growth * (quad.Bottom - quad.Top)) >> 12;
        return piece with { Quad = new(unchecked((short)(quad.Left - x)), unchecked((short)(quad.Top - y)),
            unchecked((short)(quad.Right + x)), unchecked((short)(quad.Bottom + y))) };
    }
}
