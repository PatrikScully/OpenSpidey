namespace OpenSpidey.Core.Input;

/// <summary>One buffered DIDEVICEOBJECTDATA record: dwOfs is the DIK scan code and dwData bit0x80 is key-down.</summary>
public readonly record struct PcKeyEvent(byte Key, bool Down);

/// <summary>gKeyboardMappings 568398: {action, key} pairs ended by action0x8000; default copy at 53C87C.</summary>
public sealed class PcKeyboardMappings
{
    public const uint Terminator = 0x8000, Unmapped = 0x4000;
    private static readonly (uint Action, uint Key)[] defaults =
    [
        (0x1, 0xC8), (0x2, 0xD0), (0x4, 0xCB), (0x8, 0xCD), (0x10, 0x50), (0x20, 0x4D), (0x40, 0x4B),
        (0x80, 0x48), (0x100, 0x47), (0x200, 0x51), (0x400, 0x49), (0x1000, 0x1C), (0x2000, 0x0F), (Terminator, 0),
    ];
    private readonly (uint Action, uint Key)[] entries = (ValueTuple<uint, uint>[])defaults.Clone();

    public IReadOnlyList<(uint Action, uint Key)> Entries => entries;

    /// <summary>PCINPUT_GetKeyboardMappingForAction 50A400: first matching action before the terminator, else0x4000.</summary>
    public uint Get(uint action)
    {
        foreach ((uint entryAction, uint key) in entries)
        {
            if (entryAction == Terminator) break;
            if (entryAction == action) return key;
        }
        return Unmapped;
    }

    /// <summary>PCINPUT_SetKeyboardMappingForAction 50A4A0: rewrites the first matching action; absent actions are ignored.</summary>
    public void Set(uint action, uint key)
    {
        for (int index = 0; index < entries.Length; index++)
        {
            if (entries[index].Action == Terminator) return;
            if (entries[index].Action == action) { entries[index].Key = key; return; }
        }
    }

    /// <summary>PCINPUT_RestoreDefaultKeyboardSettings 50A520.</summary>
    public void RestoreDefaults() => defaults.CopyTo(entries, 0);
}

/// <summary>gKeyState 6B792C as written by DXINPUT_PollKeyboard 501B80 from a sixteen-record DirectInput buffer.</summary>
public sealed class PcKeyboardState
{
    /// <summary>501B93: GetDeviceData is asked for at most sixteen records per poll.</summary>
    public const int RecordsPerPoll = 16;
    private readonly byte[] state = new byte[256];

    public ReadOnlySpan<byte> State => state;

    /// <summary>501C51: every key loses bit0x80, then each record stores0xFF on down or0x80 on up in buffer order.</summary>
    public int Poll(ReadOnlySpan<PcKeyEvent> records)
    {
        if (records.Length > RecordsPerPoll)
            throw new ArgumentException("DXINPUT_PollKeyboard reads at most sixteen buffered records per poll.", nameof(records));
        for (int key = 0; key < state.Length; key++) state[key] &= 0x7F;
        foreach (PcKeyEvent record in records) state[record.Key] = record.Down ? (byte)0xFF : (byte)0x80;
        return records.Length;
    }

    /// <summary>DXINPUT_Initialize and DXINPUT shutdown: memset(gKeyState, 0).</summary>
    public void Clear() => Array.Clear(state);

    /// <summary>PCINPUT_IsKeyPressed 50A650: newPress tests state==0xFF, otherwise state&amp;0x7F.</summary>
    public bool IsKeyPressed(byte key, bool newPress) => newPress ? state[key] == 0xFF : (state[key] & 0x7F) != 0;
}
