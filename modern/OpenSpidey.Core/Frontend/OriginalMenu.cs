using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Frontend;

public readonly record struct MenuColor(byte R, byte G, byte B);

/// <summary>One CMenu SEntry (0x20 bytes at CMenu+0x3C).</summary>
public sealed class OriginalMenuEntry
{
    public required string Name { get; set; }
    /// <summary>val_a/val_b: current and target highlight value; Display skips entries whose current value is not positive.</summary>
    public short Value { get; set; }
    public short Target { get; set; }
    public short SelectedValue { get; set; }
    public short NormalValue { get; set; }
    /// <summary>unk_a: extra pixels added before this line.</summary>
    public byte ExtraY { get; set; }
    /// <summary>unk_b: shown and counted; EntryOff clears it.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>what: nonzero entries are shown but skipped by navigation.</summary>
    public int Blocked { get; set; }
    public MenuColor SelectedTop { get; set; } = new(0x80, 0x80, 0x80);
    public MenuColor NormalTop { get; set; } = new(69, 60, 107);
    public MenuColor SelectedBottom { get; set; } = new(69, 60, 107);
    public MenuColor NormalBottom { get; set; } = new(40, 35, 62);
    internal bool Selectable => Enabled && Blocked == 0;
}

public readonly record struct OriginalMenuLine(int Index, int Y, OriginalMenuEntry Entry, bool Selected, MenuColor Top, MenuColor Bottom);

/// <summary>Original front-end CMenu list (front.cpp; ctor 0x43F9B0, Update 0x440600, Display 0x4401E0).</summary>
public sealed class OriginalMenu
{
    public const int UpOrDown = 0x3003, Up = 0x1001, Down = 0x2002;
    public const int MoveSound = 0x29;
    private readonly List<OriginalMenuEntry> entries = [];
    public IReadOnlyList<OriginalMenuEntry> Entries => entries;
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Mess_SetTextJustify value: 0 centred, 1 left, 2 right.</summary>
    public byte Justification { get; }
    public int LineSeparation { get; set; }
    public int Line { get; private set; }
    public int CursorLine { get; private set; }
    /// <summary>field_1B; 0xFF shows every line.</summary>
    public int VisibleLines { get; set; } = 0xFF;
    /// <summary>scrollbar_zero: navigation wraps at either end.</summary>
    public bool Wrap { get; set; } = true;
    /// <summary>scrollbar_one: held repeat divisor after the 20-frame delay.</summary>
    public int RepeatRate { get; set; } = 2;
    /// <summary>field_1E: selected-line colours pulse with the Q12 sine table.</summary>
    public bool Pulse { get; set; }
    public short PulsePhase { get; private set; }
    /// <summary>mZoomBoxType: Shell zoom modes 0 full-width, 1/2 entry frame (front.cpp Zoom 0x440xxx).</summary>
    public int ZoomBoxType { get; private set; }
    public OriginalExpandingBox? ZoomBox { get; private set; }
    public int EnabledCount => entries.Count(entry => entry.Enabled);
    private readonly short selected;
    private readonly short normal;

    public OriginalMenu(int x, int y, byte justification, short selectedValue, short normalValue, int lineSeparation)
    {
        X = x;
        Y = y;
        Justification = justification;
        selected = selectedValue;
        normal = normalValue;
        LineSeparation = lineSeparation;
    }

    /// <summary>Mess_TextWidth 0x4586C0 in the font current when AddEntry runs.</summary>
    public Func<string, int>? MeasureText { get; init; }
    /// <summary>menu_width (u16 at +8) and AdjustWidth 0x4401A0 padding (+0xC).</summary>
    public int Width { get; private set; }
    public int WidthPadding { get; set; }

    /// <summary>AddEntry 0x43FFF0; the ctor's per-slot values apply because SetLine ran before any entry existed.</summary>
    public OriginalMenuEntry Add(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (entries.Count >= 40) throw new InvalidOperationException("Too many entries added to menu");
        if (MeasureText is not null)
        {
            int width = MeasureText(name);
            if (width > (ushort)Width) Width = (ushort)(width + WidthPadding);
        }
        short value = entries.Count == Line ? selected : normal;
        var entry = new OriginalMenuEntry { Name = name, SelectedValue = selected, NormalValue = normal, Value = value, Target = value };
        entries.Add(entry);
        return entry;
    }

    /// <summary>SetLine 0x43FF80 / Reset 0x43FFC0.</summary>
    public void SetLine(int line)
    {
        Line = line & 0xFF;
        for (int index = 0; index < entries.Count; index++)
            entries[index].Value = entries[index].Target = index == Line ? entries[index].SelectedValue : entries[index].NormalValue;
    }

    public void EntryOff(string name) => entries[Find(name)].Enabled = false;
    public void EntryOn(string name) => entries[Find(name)].Enabled = true;
    public bool ChoiceIs(string name) => Line < entries.Count && OriginalCheats.CompareStrings(entries[Line].Name, name);
    public OriginalMenuEntry Selected => entries[Line];

    /// <summary>CentreX 0x43FF30 over the 512-pixel design width.</summary>
    public void CentreX() => X = Justification switch
    {
        0 => 256,
        1 => (512 - Width) / 2,
        2 => Width + (512 - Width) / 2,
        _ => X
    };

    /// <summary>CentreY 0x43FEC0 over the 240-line design screen.</summary>
    public void CentreY()
    {
        int extra = 0, count = 0;
        for (int index = CursorLine; index < entries.Count && index < CursorLine + VisibleLines; index++)
            if (entries[index].Enabled) { extra += entries[index].ExtraY; count++; }
        Y = (240 - LineSeparation * (count - 1) - extra) / 2;
    }

    public int Height
    {
        get
        {
            int height = 0;
            for (int index = CursorLine; index < entries.Count && index < CursorLine + VisibleLines; index++)
                if (entries[index].Enabled) height += entries[index].ExtraY + LineSeparation;
            return height;
        }
    }

    /// <summary>
    /// Update 0x440600 for keyboard/pad input; held tests PCSHELL_CheckTriggers(mask, 0, 0). Mouse hover is a separate owner.
    /// Returns true when the move sound 0x29 plays.
    /// </summary>
    public bool Update(Func<int, bool> held, ref int repeatTimer)
    {
        ArgumentNullException.ThrowIfNull(held);
        if (Line > 40) return false;
        if (!entries.Any(entry => entry.Selectable)) throw new InvalidOperationException("A CMenu needs a selectable entry; the original loops forever.");
        while (!entries[Line].Selectable)
        {
            entries[Line].Target = entries[Line].Value = entries[Line].NormalValue;
            Line = (Line + 1) % entries.Count;
            entries[Line].Target = entries[Line].SelectedValue;
            entries[Line].Value = entries[Line].NormalValue;
        }
        int start = Line;
        if (held(UpOrDown))
        {
            if (repeatTimer == 0 || repeatTimer > 20 && repeatTimer % RepeatRate == 0)
            {
                if (held(Up))
                {
                    entries[Line].Target = entries[Line].NormalValue;
                    do
                    {
                        if (Line != 0) { Line--; PulsePhase = 0x400; }
                        else if (Wrap) { Line = entries.Count - 1; PulsePhase = 0x400; }
                        else if (!entries[Line].Selectable) throw new InvalidOperationException("The original CMenu::Update loops forever at an unselectable non-wrapping top.");
                    }
                    while (!entries[Line].Selectable);
                    entries[Line].Target = entries[Line].SelectedValue;
                }
                if (held(Down))
                {
                    entries[Line].Target = entries[Line].NormalValue;
                    do
                    {
                        Line++;
                        if (Line < entries.Count) PulsePhase = 0x400;
                        else if (Wrap) { Line = 0; PulsePhase = 0x400; }
                        else
                        {
                            Line--;
                            if (!entries[Line].Selectable) throw new InvalidOperationException("The original CMenu::Update loops forever at an unselectable non-wrapping bottom.");
                        }
                    }
                    while (!entries[Line].Selectable);
                    entries[Line].Target = entries[Line].SelectedValue;
                }
            }
            repeatTimer++;
        }
        else repeatTimer = 0;
        foreach (OriginalMenuEntry entry in entries)
        {
            if (entry.Value < entry.Target)
            {
                entry.Value = (short)(entry.Value + 40);
                if (entry.Value > entry.Target) entry.Value = entry.Target;
            }
            if (entry.Value > entry.Target)
            {
                entry.Value = (short)(entry.Value - 40);
                if (entry.Value < entry.Target) entry.Value = entry.Target;
            }
        }
        if (Line != start)
        {
            if (Line < CursorLine) CursorLine = Line;
            else
            {
                int active = 0;
                for (int index = CursorLine; index <= Line; index++)
                    if (entries[index].Enabled) active++;
                if (active > VisibleLines)
                {
                    CursorLine = Line;
                    if (Line != 0)
                    {
                        int visible = 0;
                        do
                        {
                            if (entries[CursorLine].Enabled && ++visible == VisibleLines) break;
                            CursorLine--;
                        }
                        while (CursorLine != 0);
                    }
                }
            }
        }
        return Line != start;
    }

    /// <summary>KillBox 0x440xxx: drop the zoom overlay.</summary>
    public void KillBox() => ZoomBox = null;

    /// <summary>FinishedZooming 0x440xxx: no box, or the CExpandingBox reached its target size (field_30).</summary>
    public bool FinishedZooming => ZoomBox is null || ZoomBox.Finished;

    /// <summary>
    /// Zoom 0x440xxx: allocate a CExpandingBox for the menu open animation. smallFontMenu matches sp_fnt03.fnt padding in the original.
    /// </summary>
    public void Zoom(int zoomBoxType, bool smallFontMenu = false)
    {
        if (zoomBoxType is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(zoomBoxType), "CMenu::Zoom accepts types 0, 1 and 2.");
        ZoomBoxType = zoomBoxType;
        KillBox();
        ZoomBox = zoomBoxType switch
        {
            0 => OriginalExpandingBox.Create(0, Y - 18, 512, Height + 27, 0, 0, 1000, 15, 0),
            1 or 2 => OriginalExpandingBox.Create(X - 5, Y - (smallFontMenu ? 10 : 12), Width + 12,
                (smallFontMenu ? 14 : 17) + Height, 0, 0, 30, 15, 0),
            _ => null
        };
    }

    /// <summary>Display tail: advance the zoom box; type 2 enables the description scrollbar (field_24).</summary>
    public MenuBoxDrawCall? AdvanceZoomBox(Func<MenuBoxDrawCall, int>? drawMenuBox = null)
    {
        if (ZoomBox is null) return null;
        if (ZoomBoxType == 2) ZoomBox.Scrollbar = true;
        ZoomBox.Display(drawMenuBox);
        return ZoomBox.LastDraw;
    }

    /// <summary>Display 0x4401E0 line walk and colour choice; entries stay hidden until FinishedZooming (front.cpp 0x4401b0).</summary>
    public IEnumerable<OriginalMenuLine> Lines()
    {
        if (!FinishedZooming) yield break;
        int y = Y;
        for (int index = CursorLine; index < entries.Count && index < CursorLine + VisibleLines; index++)
        {
            OriginalMenuEntry entry = entries[index];
            y += entry.ExtraY;
            if (!entry.Enabled) continue;
            if (entry.Value <= 0) { y += LineSeparation; continue; }
            bool current = index == Line;
            if (current && Pulse)
            {
                int weight = GameAngle.Heading(PulsePhase).Sine;
                PulsePhase = unchecked((short)(PulsePhase + 200));
                yield return new(index, y, entry, true, Blend(entry.SelectedTop, entry.NormalTop, weight),
                    Blend(entry.SelectedBottom, entry.NormalBottom, weight));
            }
            else if (current) yield return new(index, y, entry, true, entry.SelectedTop, entry.SelectedBottom);
            else yield return new(index, y, entry, false, entry.NormalTop, entry.NormalBottom);
            y += LineSeparation;
        }
    }

    private static MenuColor Blend(MenuColor selected, MenuColor normal, int weight) =>
        new(Channel(selected.R, normal.R, weight), Channel(selected.G, normal.G, weight), Channel(selected.B, normal.B, weight));

    private static byte Channel(int selected, int normal, int weight) =>
        (byte)Math.Min(255, (((selected + normal) >> 1) + (((selected - normal) * weight) >> 13)) * 350 / 256);

    private int Find(string name)
    {
        for (int index = 0; index < entries.Count; index++)
            if (OriginalCheats.CompareStrings(name, entries[index].Name)) return index;
        throw new InvalidOperationException($"Entry not found: {name}");
    }
}
