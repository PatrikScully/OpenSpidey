using Godot;

namespace OpenSpidey.Game.Frontend;

/// <summary>
/// PCSHELL_CheckTriggers 0x50C180 bits over Godot devices: 0x1-0x8 arrows, 0x10 Enter, 0x20/0x40 Esc, 0x200 right mouse,
/// 0x1000-0x8000 pad directions, 0x10000 pad accept, 0x20000 pad back, 0x40000 pad start.
/// </summary>
public sealed class FrontendInput
{
    private const int LatchedBits = 0x10 | 0x20 | 0x200 | 0x10000 | 0x20000 | 0x40000;
    private int held, pressed, latched, injectedHeld, injectedPressed;
    private readonly List<char> typed = [];
    private readonly List<char> injectedTyped = [];
    public IReadOnlyList<char> Typed => typed;

    /// <summary>Smoke driver input for the next poll only.</summary>
    public void Inject(int heldMask, int pressedMask = 0, string text = "")
    {
        injectedHeld |= heldMask;
        injectedPressed |= pressedMask | heldMask;
        injectedTyped.AddRange(text);
    }

    public void Poll()
    {
        int now = Device();
        int previous = held;
        held = now | injectedHeld;
        pressed = (now & ~previous) | injectedPressed;
        latched &= held;
        typed.Clear();
        typed.AddRange(pendingKeys);
        typed.AddRange(injectedTyped);
        pendingKeys.Clear();
        injectedHeld = injectedPressed = 0;
        injectedTyped.Clear();
    }

    private readonly List<char> pendingKeys = [];

    /// <summary>DXINPUT key names of length one plus BACK and SPACE, as Shell_InputName reads them.</summary>
    public void KeyEvent(InputEventKey key)
    {
        if (!key.Pressed || key.Echo) return;
        Key code = key.Keycode;
        if (code is >= Key.A and <= Key.Z) pendingKeys.Add((char)('A' + (code - Key.A)));
        else if (code is >= Key.Key0 and <= Key.Key9) pendingKeys.Add((char)('0' + (code - Key.Key0)));
        else if (code == Key.Space) pendingKeys.Add(' ');
        else if (code == Key.Backspace) pendingKeys.Add('\b');
    }

    /// <summary>CheckTriggers(mask, triggered, consume).</summary>
    public bool Check(int mask, bool triggered, bool consume = false)
    {
        int source = (triggered ? pressed : held) & mask & ~latched;
        if (source == 0) return false;
        if (consume) latched |= source & LatchedBits;
        return true;
    }

    public void Clear()
    {
        pressed = 0;
        latched = held;
    }

    private static int Device()
    {
        int mask = 0;
        if (Input.IsKeyPressed(Key.Up)) mask |= 0x1;
        if (Input.IsKeyPressed(Key.Down)) mask |= 0x2;
        if (Input.IsKeyPressed(Key.Left)) mask |= 0x4;
        if (Input.IsKeyPressed(Key.Right)) mask |= 0x8;
        if (Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.KpEnter)) mask |= 0x10;
        if (Input.IsKeyPressed(Key.Escape)) mask |= 0x60;
        if (Input.IsMouseButtonPressed(MouseButton.Right)) mask |= 0x200;
        foreach (int device in Input.GetConnectedJoypads())
        {
            if (Input.IsJoyButtonPressed(device, JoyButton.DpadUp) || Input.GetJoyAxis(device, JoyAxis.LeftY) < -0.5f) mask |= 0x1000;
            if (Input.IsJoyButtonPressed(device, JoyButton.DpadDown) || Input.GetJoyAxis(device, JoyAxis.LeftY) > 0.5f) mask |= 0x2000;
            if (Input.IsJoyButtonPressed(device, JoyButton.DpadLeft) || Input.GetJoyAxis(device, JoyAxis.LeftX) < -0.5f) mask |= 0x4000;
            if (Input.IsJoyButtonPressed(device, JoyButton.DpadRight) || Input.GetJoyAxis(device, JoyAxis.LeftX) > 0.5f) mask |= 0x8000;
            if (Input.IsJoyButtonPressed(device, JoyButton.A)) mask |= 0x10000;
            if (Input.IsJoyButtonPressed(device, JoyButton.B)) mask |= 0x20000;
            if (Input.IsJoyButtonPressed(device, JoyButton.Start)) mask |= 0x40000;
        }
        return mask;
    }
}
