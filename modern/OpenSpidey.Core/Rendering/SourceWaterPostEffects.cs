using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Rendering;

public readonly record struct SourceWaterQuad(float Width, float Height, uint Argb)
{
    public const int SourceSlot = 6;
}

/// <summary>Process post globals: actual pause5FAE98, timer60FA24 and ushort60FA28 share the player's process gWaterEffect.</summary>
public sealed class SourceWaterPostState(PlayerWaterEffectState water)
{
    public PlayerWaterEffectState Water { get; } = water ?? throw new ArgumentNullException(nameof(water));
    public int PauseValue { get; set; }
    public int Timer { get; set; }
    public ushort WordCountdown { get; set; }
    public SourceWaterQuad? Quad { get; internal set; }
}

public interface IPlayerWaterEffectFrameHost
{
    void RestoreSkyColourTarget();
}

/// <summary>Actual player pre-damage water reset and complete original46ACD0 post effect, retaining wrapped clock, signed sine shift and direct ARGB quad.</summary>
public static class SourceWaterPostEffects
{
    public static void PreparePlayer(PlayerWaterEffectState water, IPlayerWaterEffectFrameHost host)
    {
        if (water.Value != 1) return;
        host.RestoreSkyColourTarget(); water.Value = 0;
    }
    public static void AdvanceRender(SourceWaterPostState state, uint sourceClock, int gameWidth, int gameHeight,
        uint resolutionWidth, uint resolutionHeight)
    {
        state.Quad = null;
        if (state.Water.Value != 0)
        {
            state.Timer = unchecked((int)sourceClock * 48);
            if (state.PauseValue == 0)
            {
                (short sine, short cosine) = GameAngle.Heading(state.Timer / 2);
                uint green = unchecked((uint)(Math.Abs(sine >> 6) + 64));
                uint red = unchecked((uint)(Math.Abs(cosine >> 6) + 64));
                // PCGfx_DrawQuad2D takes ARGB directly; source 3D BGR half/bias conversion does not apply.
                uint colour = (red << 16) | (green << 8) | 0x30000032;
                float width = (float)((double)(float)gameWidth / (float)resolutionWidth * 512);
                float height = (float)((double)(float)gameHeight / (float)resolutionHeight * 240);
                state.Quad = new(width, height, colour);
            }
        }
        if (state.WordCountdown != 0) state.WordCountdown--;
    }
    public static uint ApplyBrightness(uint argb, ReadOnlySpan<byte> table)
    {
        if (table.Length != 256) throw new ArgumentException("Source PC brightness requires its complete256-entry table.", nameof(table));
        return (argb & 0xFF000000) | (uint)(table[(byte)argb] | table[(byte)(argb >> 8)] << 8 | table[(byte)(argb >> 16)] << 16);
    }
}
