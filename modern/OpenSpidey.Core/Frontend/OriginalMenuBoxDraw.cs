namespace OpenSpidey.Core.Frontend;

/// <summary>
/// One panel draw submitted by PShell_DrawMenuBox 0x47A700, in submission order.
/// Textured: DCPanel_DrawTexturedPoly 0x4626A0 with gAnimTable[15] frame FrameIndex; unless Width and Height are both
/// nonzero the quad takes the frame's own Width/Height. Flat: DCPanel_DrawFlatShadedPoly 0x462D60 with BlendMode.
/// Depth is the float zOffset.
/// </summary>
public readonly record struct MenuBoxPrimitive(int FrameIndex, int X, int Y, int Width, int Height, bool Flat, byte Red, byte Green, byte Blue,
    float Depth = 5, int BlendMode = 0);

/// <summary>PShell_DrawMenuBox 0x47A700: nine-slice menu chrome, gMenuBoxSlices 0x5512F0 offsets, optional scrollbar and fill.</summary>
public static class OriginalMenuBoxDraw
{
    /// <param name="sort">HIWORD of the Mess state at 0x60D5A4 (zero until a shell screen sets it).</param>
    public static IReadOnlyList<MenuBoxPrimitive> Plan(MenuBoxDrawCall call, IReadOnlyList<OriginalMenuBoxSlice> slices, int sort = 0)
    {
        ArgumentNullException.ThrowIfNull(slices);
        if (slices.Count != 13) throw new ArgumentException("The original menu box uses thirteen slice records.", nameof(slices));
        int x = call.X, y = call.Y, width = call.Width, height = call.Height;
        bool scrollbar = call.Scrollbar != 0;
        ushort order = unchecked((ushort)sort);
        float depth = order is >= 0xFFE and <= 0xFFF ? -2f : 5f;
        var output = new List<MenuBoxPrimitive>(10);
        if (scrollbar)
            output.Add(new(-1, slices[0].A + x - 10,
                slices[0].B + ((call.ScrollPosition * (height - call.ScrollThumbHeight - 8)) >> 8) + y + 4,
                slices[0].C + 7, call.ScrollThumbHeight + slices[0].D, true, 0xFF, 0xFF, 0xFF, depth - 1, 1));
        // Edge geometry is stored in POLY_FT4 shorts and read back sign-extended for the size arguments.
        short bottom = (short)(height + y);
        if (scrollbar)
        {
            output.Add(Textured(0, slices[1].A + x - 14, slices[1].B + y - 3, slices[1].C, slices[1].D, depth));
            short left = (short)(x - 14), top = (short)(y + 7), lower = (short)(bottom - 7);
            output.Add(Textured(1, slices[2].A + left, slices[2].B + top, slices[2].C + (short)x - left, slices[2].D + lower - top, depth));
            output.Add(Textured(2, slices[3].A + x - 14, y + slices[3].B + height - 7, slices[3].C, slices[3].D, depth));
        }
        else
        {
            output.Add(Textured(7, slices[4].A + x - 6, slices[4].B + y - 3, slices[4].C, slices[4].D, depth));
            output.Add(Textured(8, slices[5].A + x - 6, y + slices[5].B + height - 3, slices[5].C, slices[5].D, depth));
            short left = (short)(x - 6), top = (short)(y + 3), lower = (short)(bottom - 3);
            output.Add(Textured(5, left + slices[6].A, slices[6].B + top, slices[6].C + (short)x - left, slices[6].D + lower - top, depth));
        }
        output.Add(Textured(4, x + slices[7].A + width - 2, slices[7].B + y - 3, slices[7].C, slices[7].D, depth));
        short edgeLeft = (short)(scrollbar ? x : x + 2), edgeRight = (short)(x + width - 2);
        short topEdge = (short)(y - 3), topEdgeLower = (short)(y + 1);
        output.Add(Textured(3, edgeLeft + slices[8].A, slices[8].B + topEdge, slices[8].C + edgeRight - edgeLeft,
            slices[8].D + topEdgeLower - topEdge, depth));
        output.Add(Textured(6, x + slices[9].A + width - 2, y + slices[9].B + height - 3, slices[9].C, slices[9].D, depth));
        short bottomEdgeLower = (short)(bottom + 4);
        output.Add(Textured(3, slices[10].A + edgeLeft, slices[10].B + bottom, slices[10].C + edgeRight - edgeLeft,
            slices[10].D + bottomEdgeLower - bottom, depth));
        short rightEdge = (short)(width + x), rightEdgeRight = (short)(width + x + 6), rightTop = (short)(y + 3), rightLower = (short)(bottom - 3);
        output.Add(Textured(5, slices[11].A + rightEdge, slices[11].B + rightTop, slices[11].C + rightEdgeRight - rightEdge,
            slices[11].D + rightLower - rightTop, depth));
        if (call.A5 != 0)
            output.Add(new(-1, x + slices[12].A, y + slices[12].B, width + slices[12].C, height + slices[12].D, true, 0x19, 0x19, 0x50, depth, 1));
        return output;
    }

    /// <summary>Panel_DrawTexturedPoly 0x462BB0 seeds r0/g0/b0 with 0x80; flags 0 selects that colour.</summary>
    private static MenuBoxPrimitive Textured(int frame, int x, int y, int width, int height, float depth) =>
        new(frame, x, y, width, height, false, 0x80, 0x80, 0x80, depth);
}
