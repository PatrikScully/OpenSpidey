namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::AI (0x4EC040) teleport fade tail: field_218 0x100 fades out, 0x80 fades back in.</summary>
public static class VenomFade
{
    public static void Update(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        int ticks = host.ElapsedTicks;
        if ((state.Field218 & 0x100) != 0) FadeOut(state, host, ticks);
        else if ((state.Field218 & 0x80) != 0) FadeIn(state, host, ticks);
    }

    private static void FadeOut(VenomState state, IVenomHost host, int ticks)
    {
        state.BodyFlags &= ~0x10u;
        state.Field458 = 0;
        state.Field45C = unchecked(state.Field45C + ticks);
        state.MinorRadius = 0;
        if (state.Field45C < 50)
        {
            GrowOutline(state, ticks);
            return;
        }
        host.ItemFlags = unchecked((ushort)((host.ItemFlags & 0xFF7F) | 0x0C00));
        state.Field454 = unchecked(state.Field454 - 4 * ticks);
        if (state.Field454 <= 0)
        {
            state.Field454 = 0;
            state.OutlineSemiTransparent = true;
            state.Field3AC = unchecked(state.Field3AC - 2 * ticks);
            if (state.Field3AC < 0)
            {
                state.OutlineEnabled = false;
                host.ItemFlags |= 1;
                state.Field218 &= ~0x100u;
                state.Field3AC = 0;
                state.Field45C = 0;
                state.Field33D = 0;
            }
            else
                SetOutline(state, state.Field3AC / 2, state.Field3AC / 2, state.Field3AC);
        }
        state.ItemColor = Grey(state.Field454);
    }

    private static void FadeIn(VenomState state, IVenomHost host, int ticks)
    {
        host.ItemFlags &= unchecked((ushort)~1);
        state.Field33D = 1;
        state.Field45C = 0;
        state.Field458 = unchecked(state.Field458 + ticks);
        if (state.Field458 < 50)
        {
            host.ItemFlags |= 0x0C00;
            GrowOutline(state, ticks);
            return;
        }
        if ((host.ItemFlags & 0x800) != 0)
        {
            state.Field454 = unchecked(state.Field454 + 4 * ticks);
            state.MinorRadius = 0xC0;
            if (state.Field454 < 255)
                state.ItemColor = Grey(state.Field454);
            else
            {
                state.Field454 = 255;
                host.ItemFlags = unchecked((ushort)((host.ItemFlags & 0xF37F) | 0x480));
                if (host.LevelId is 0x502 or 0x604) host.SetPlayerFocusLockTarget();
            }
            state.Field3AC = 128;
            SetOutline(state, 128, 128, 128);
            return;
        }
        state.OutlineSemiTransparent = true;
        state.Field3AC = unchecked(state.Field3AC - 2 * ticks);
        if (state.Field3AC < 0)
        {
            state.OutlineEnabled = false;
            state.BodyFlags |= 0x10;
            state.Field218 &= ~0x80u;
            host.ItemFlags &= unchecked((ushort)~0x800);
            state.Field3AC = 0;
            state.Field458 = 0;
        }
        else
            SetOutline(state, state.Field3AC / 2, state.Field3AC / 2, state.Field3AC);
    }

    private static void GrowOutline(VenomState state, int ticks)
    {
        state.OutlineEnabled = true;
        state.Field3AC = unchecked(state.Field3AC + 2 * ticks);
        if (state.Field3AC > 255) state.Field3AC = 255;
        SetOutline(state, state.Field3AC / 2, state.Field3AC / 2, state.Field3AC);
        state.OutlineSemiTransparent = true;
    }

    /// <summary>CSuper::SetOutlineRGB (0x460BE0) takes byte arguments.</summary>
    private static void SetOutline(VenomState state, int red, int green, int blue) =>
        state.OutlineColor = (unchecked((byte)red), unchecked((byte)green), unchecked((byte)blue));

    private static uint Grey(int value) => unchecked((uint)(value | ((value | (value << 8)) << 8)));
}
