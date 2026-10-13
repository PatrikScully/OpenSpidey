using Godot;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;

namespace OpenSpidey.Game.Player;

/// <summary>Engine input bindings; source input correction and heading selection stay in Core.</summary>
internal static class PlayerControls
{
    private const string Forward = "player_forward";
    private const string Backward = "player_backward";
    private const string Left = "player_left";
    private const string Right = "player_right";
    private const string Zip = "player_zip";
    private const string Jump = "player_jump";
    private const string Punch = "player_punch";
    private const string Kick = "player_kick";
    private const string Web = "player_web";
    private const string Target = "player_target";
    private const string Swing = "player_swing";
    private const string RawCross = "source_pad_cross";
    private const string RawCircle = "source_pad_circle";
    private const string RawSquare = "source_pad_square";
    private const string RawTriangle = "source_pad_triangle";
    private const string RawStart = "source_pad_start";
    private const string RawSelect = "source_pad_select";
    private static readonly (string Action, PlayerButton Button)[] buttons =
        [(Zip, PlayerButton.Zip), (Jump, PlayerButton.Jump), (Punch, PlayerButton.Punch), (Kick, PlayerButton.Kick),
         (Web, PlayerButton.Web), (Target, PlayerButton.Target), (Swing, PlayerButton.Swing)];
    private static readonly (int Slot, string Action)[] physicalButtons =
        [(0, RawTriangle), (1, RawSquare), (2, RawCircle), (3, RawCross),
         (4, Target), (6, Zip), (7, Swing), (8, Left), (9, Right), (10, Forward), (11, Backward),
         (14, RawStart), (15, RawSelect), (16, Jump), (17, Web), (18, Kick), (19, Punch)];

    public static void Register()
    {
        Bind(Forward, Key.W, Key.Up, JoyAxis.LeftY, -1);
        Bind(Backward, Key.S, Key.Down, JoyAxis.LeftY, 1);
        Bind(Left, Key.A, Key.Left, JoyAxis.LeftX, -1);
        Bind(Right, Key.D, Key.Right, JoyAxis.LeftX, 1);
        BindButton(Zip, Key.E, JoyButton.RightShoulder);
        BindButton(Jump, Key.Space, JoyButton.A);
        BindButton(Punch, Key.J, JoyButton.X);
        BindButton(Kick, Key.K, JoyButton.B);
        BindButton(Web, Key.L, JoyButton.Y);
        BindTrigger(Target, Key.Q, JoyAxis.TriggerLeft);
        BindTrigger(Swing, Key.R, JoyAxis.TriggerRight);
        // Physical source buttons stay separate from remappable virtual action slots16..19.
        BindButton(RawCross, Key.Space, JoyButton.A);
        bool circleRegistered = InputMap.HasAction(RawCircle);
        BindButton(RawCircle, Key.K, JoyButton.B);
        if (!circleRegistered) InputMap.ActionAddEvent(RawCircle, new InputEventKey { PhysicalKeycode = Key.Escape });
        BindButton(RawSquare, Key.J, JoyButton.X);
        BindButton(RawTriangle, Key.L, JoyButton.Y);
        BindButton(RawStart, Key.Enter, JoyButton.Start);
        BindButton(RawSelect, Key.Tab, JoyButton.Back);
    }

    /// <summary>Original PC action masks resolve to the game's current registered engine actions.</summary>
    public static string? SourceActionName(ushort sourceAction)
    {
        string? action = sourceAction switch
        {
            0x10 => Jump, 0x20 => Kick, 0x40 => Punch, 0x80 => Web,
            0x100 => Target, 0x200 => Zip, 0x400 => Swing, _ => null
        };
        return action is not null && InputMap.HasAction(action) ? action : null;
    }

    public static PlayerButtonInput ReadButtons()
    {
        PlayerButton held = PlayerButton.None, triggered = PlayerButton.None;
        foreach ((string action, PlayerButton button) in buttons)
        {
            if (Input.IsActionPressed(action)) held |= button;
            if (Input.IsActionJustPressed(action)) triggered |= button;
        }
        return new(held, triggered);
    }

    public static void ReadPhysicalPad(Span<PlayerButtonState> pad)
    {
        if (pad.Length != 20) throw new ArgumentException("The source pad has twenty button records.", nameof(pad));
        foreach ((int slot, string action) in physicalButtons)
            pad[slot] = new(Input.IsActionPressed(action) ? (byte)1 : (byte)0,
                Input.IsActionJustPressed(action) ? (byte)1 : (byte)0, pad[slot].Auxiliary);
    }

    private static void BindTrigger(string name, Key key, JoyAxis axis)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = 1 });
    }

    private static void BindButton(string name, Key key, JoyButton button)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(name, new InputEventJoypadButton { ButtonIndex = button });
    }

    public static PlayerInputSample Read()
    {
        Vector2 movement = Input.GetVector(Left, Right, Forward, Backward);
        return new(true, new((sbyte)(movement.Y * 127), (sbyte)(movement.X * 127)),
            false, false, false, false, false, false);
    }

    private static void Bind(string name, Key primary, Key alternative, JoyAxis axis, float value)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name, 0.2f);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = primary });
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = alternative });
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = value });
    }
}
