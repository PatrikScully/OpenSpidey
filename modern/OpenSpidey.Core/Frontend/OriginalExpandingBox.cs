namespace OpenSpidey.Core.Frontend;

/// <summary>One PShell_DrawMenuBox call emitted by CExpandingBox::Display 0x47AF10.</summary>
public readonly record struct MenuBoxDrawCall(int X, int Y, int Width, int Height, int A5, int Scrollbar, int ScrollPosition, int ScrollThumbHeight);

/// <summary>CExpandingBox 0x34 bytes in pshell.cpp: animated menu frame with optional scrollbar (ctor 0x47AE80, Display 0x47AF10).</summary>
public sealed class OriginalExpandingBox
{
    public int CurrentWidth { get; private set; }
    public int CurrentHeight { get; private set; }
    public int TargetWidth { get; }
    public int TargetHeight { get; }
    public int WidthStep { get; }
    public int HeightStep { get; }
    public int AnchorX { get; }
    public int AnchorY { get; }
    public bool Scrollbar { get; set; }
    public int ScrollPosition { get; set; }
    public int ScrollThumbHeight { get; private set; } = 28;
    /// <summary>field_30: set once CurrentWidth/Height reach their targets; CMenu::FinishedZooming reads this.</summary>
    public bool Finished { get; private set; }
    public MenuBoxDrawCall? LastDraw { get; private set; }

    private OriginalExpandingBox(int anchorX, int anchorY, int targetWidth, int targetHeight, int initialWidth, int initialHeight,
        int widthStep, int heightStep, int scrollbar)
    {
        AnchorX = anchorX;
        AnchorY = anchorY;
        TargetWidth = targetWidth;
        TargetHeight = targetHeight;
        CurrentWidth = initialWidth;
        CurrentHeight = initialHeight;
        WidthStep = widthStep;
        HeightStep = heightStep;
        Scrollbar = scrollbar != 0;
    }

    /// <summary>Original ctor argument order: anchor x/y, target width/height, initial width/height, step width/height, scrollbar flag.</summary>
    public static OriginalExpandingBox Create(int anchorX, int anchorY, int targetWidth, int targetHeight, int initialWidth, int initialHeight,
        int widthStep, int heightStep, int scrollbar) =>
        new(anchorX, anchorY, targetWidth, targetHeight, initialWidth, initialHeight, widthStep, heightStep, scrollbar);

    /// <summary>Display 0x47AF10: grow toward the target size, then invoke the menu-box draw callback.</summary>
    public int Display(Func<MenuBoxDrawCall, int>? drawMenuBox = null)
    {
        CurrentWidth += WidthStep;
        if (CurrentWidth > TargetWidth) CurrentWidth = TargetWidth;
        CurrentHeight += HeightStep;
        if (CurrentHeight > TargetHeight) CurrentHeight = TargetHeight;
        if (CurrentWidth == TargetWidth && CurrentHeight == TargetHeight) Finished = true;
        var call = new MenuBoxDrawCall(
            AnchorX + TargetWidth / 2 - CurrentWidth / 2,
            AnchorY + TargetHeight / 2 - CurrentHeight / 2,
            CurrentWidth,
            CurrentHeight,
            1,
            Scrollbar ? 1 : 0,
            ScrollPosition,
            ScrollThumbHeight);
        LastDraw = call;
        return drawMenuBox?.Invoke(call) ?? 0;
    }

    /// <summary>ScrollBarHitTest 0x47AFB0; returns 0 when the bar is hidden or the zoom has not finished.</summary>
    public int ScrollBarHitTest(int x, int y)
    {
        if (!Scrollbar || !Finished) return 0;
        int left = AnchorX - 14;
        if ((ushort)x < (ushort)left || (ushort)x > (ushort)(left + 14)) return 0;
        int top = AnchorY - 3;
        if ((ushort)y < (ushort)top) return 0;
        int bottom = (ushort)(CurrentHeight + 6) + (ushort)(AnchorY - 3);
        if ((ushort)y > (ushort)bottom) return 0;
        int thumb = ((CurrentHeight - ScrollThumbHeight - 8) * ScrollPosition >> 8) + AnchorY + 4;
        if ((ushort)y <= (ushort)(AnchorY + 7)) return 1;
        if ((ushort)y < (ushort)(CurrentHeight + AnchorY - 5)) return 2;
        if ((ushort)y < (ushort)thumb) return 4;
        if ((ushort)y > (ushort)(ScrollThumbHeight + thumb)) return 5;
        return 3;
    }
}
