using Godot;
using OpenSpidey.Core.Input;

namespace OpenSpidey.Game.Player;

/// <summary>DirectInput keyboard device for PCINPUT: buffered DIK records, gKeyState and the gKeyboardMappings table.</summary>
public sealed class GodotPcKeyboard
{
    private readonly List<PcKeyEvent> buffered = new(PcKeyboardState.RecordsPerPoll);
    public PcKeyboardMappings Mappings { get; } = new();
    public PcKeyboardState State { get; } = new();

    /// <summary>DXINPUT_SetupKeyboard 501590 sets DIPROP_BUFFERSIZE16; the poll reads that same count, so nothing is split across polls.</summary>
    public void Record(PcKeyEvent record)
    {
        if (buffered.Count == PcKeyboardState.RecordsPerPoll)
            throw new NotSupportedException("The DirectInput keyboard buffer overflowed between source polls; DI_BUFFEROVERFLOW ordering is not modelled.");
        buffered.Add(record);
    }

    /// <summary>DirectInput reports physical scan codes once per transition, never key-repeat echoes.</summary>
    public void Record(InputEvent input)
    {
        if (input is not InputEventKey { Echo: false } key || ScanCode(key) is not { } code) return;
        Record(new PcKeyEvent(code, key.Pressed));
    }

    /// <summary>Pad_Update 505720 -> PCINPUT_GetMappedStates 50A190 -> PCINPUT_PollKeyboard: one DirectInput poll per source update.</summary>
    public void Poll()
    {
        State.Poll(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffered));
        buffered.Clear();
    }

    /// <summary>PCINPUT_GetKeyboardMappingForAction then PCINPUT_IsKeyPressed((u8)key, 1).</summary>
    public bool ActionTriggered(uint action) => State.IsKeyPressed(unchecked((byte)Mappings.Get(action)), true);

    /// <summary>DIK_* scan codes; keys DirectInput has no code for produce no buffered record.</summary>
    public static byte? ScanCode(InputEventKey key)
    {
        Key physical = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        bool right = key.Location == KeyLocation.Right;
        return physical switch
        {
            Key.Shift => right ? (byte)0x36 : (byte)0x2A,
            Key.Ctrl => right ? (byte)0x9D : (byte)0x1D,
            Key.Alt => right ? (byte)0xB8 : (byte)0x38,
            Key.Meta => right ? (byte)0xDC : (byte)0xDB,
            _ => codes.TryGetValue(physical, out byte code) ? code : null,
        };
    }

    private static readonly Dictionary<Key, byte> codes = new()
    {
        [Key.Escape] = 0x01, [Key.Key1] = 0x02, [Key.Key2] = 0x03, [Key.Key3] = 0x04, [Key.Key4] = 0x05, [Key.Key5] = 0x06,
        [Key.Key6] = 0x07, [Key.Key7] = 0x08, [Key.Key8] = 0x09, [Key.Key9] = 0x0A, [Key.Key0] = 0x0B, [Key.Minus] = 0x0C,
        [Key.Equal] = 0x0D, [Key.Backspace] = 0x0E, [Key.Tab] = 0x0F, [Key.Q] = 0x10, [Key.W] = 0x11, [Key.E] = 0x12,
        [Key.R] = 0x13, [Key.T] = 0x14, [Key.Y] = 0x15, [Key.U] = 0x16, [Key.I] = 0x17, [Key.O] = 0x18, [Key.P] = 0x19,
        [Key.Bracketleft] = 0x1A, [Key.Bracketright] = 0x1B, [Key.Enter] = 0x1C, [Key.A] = 0x1E, [Key.S] = 0x1F,
        [Key.D] = 0x20, [Key.F] = 0x21, [Key.G] = 0x22, [Key.H] = 0x23, [Key.J] = 0x24, [Key.K] = 0x25, [Key.L] = 0x26,
        [Key.Semicolon] = 0x27, [Key.Apostrophe] = 0x28, [Key.Quoteleft] = 0x29, [Key.Backslash] = 0x2B, [Key.Z] = 0x2C,
        [Key.X] = 0x2D, [Key.C] = 0x2E, [Key.V] = 0x2F, [Key.B] = 0x30, [Key.N] = 0x31, [Key.M] = 0x32, [Key.Comma] = 0x33,
        [Key.Period] = 0x34, [Key.Slash] = 0x35, [Key.KpMultiply] = 0x37, [Key.Space] = 0x39, [Key.Capslock] = 0x3A,
        [Key.F1] = 0x3B, [Key.F2] = 0x3C, [Key.F3] = 0x3D, [Key.F4] = 0x3E, [Key.F5] = 0x3F, [Key.F6] = 0x40, [Key.F7] = 0x41,
        [Key.F8] = 0x42, [Key.F9] = 0x43, [Key.F10] = 0x44, [Key.Numlock] = 0x45, [Key.Scrolllock] = 0x46, [Key.Kp7] = 0x47,
        [Key.Kp8] = 0x48, [Key.Kp9] = 0x49, [Key.KpSubtract] = 0x4A, [Key.Kp4] = 0x4B, [Key.Kp5] = 0x4C, [Key.Kp6] = 0x4D,
        [Key.KpAdd] = 0x4E, [Key.Kp1] = 0x4F, [Key.Kp2] = 0x50, [Key.Kp3] = 0x51, [Key.Kp0] = 0x52, [Key.KpPeriod] = 0x53,
        [Key.F11] = 0x57, [Key.F12] = 0x58, [Key.F13] = 0x64, [Key.F14] = 0x65, [Key.F15] = 0x66, [Key.KpEnter] = 0x9C,
        [Key.KpDivide] = 0xB5, [Key.Print] = 0xB7, [Key.Pause] = 0xC5, [Key.Home] = 0xC7, [Key.Up] = 0xC8, [Key.Pageup] = 0xC9,
        [Key.Left] = 0xCB, [Key.Right] = 0xCD, [Key.End] = 0xCF, [Key.Down] = 0xD0, [Key.Pagedown] = 0xD1, [Key.Insert] = 0xD2,
        [Key.Delete] = 0xD3, [Key.Menu] = 0xDD,
    };
}
