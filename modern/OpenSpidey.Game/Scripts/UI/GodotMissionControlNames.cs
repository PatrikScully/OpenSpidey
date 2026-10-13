using Godot;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.UI;

/// <summary>Reads the real current keyboard binding for a canonical game action, including the user's keyboard layout.</summary>
public sealed class GodotMissionControlNames(Func<ushort, string?> actionName) : IMissionControlNames
{
    public MissionControlName Read(ushort sourceAction)
    {
        ArgumentNullException.ThrowIfNull(actionName);
        string? action = actionName(sourceAction);
        if (action is null || !InputMap.HasAction(action)) return new(false, "");
        InputEventKey? binding = InputMap.ActionGetEvents(action).OfType<InputEventKey>().FirstOrDefault();
        if (binding is null) return new(false, "");
        Key key = binding.KeyLabel != Key.None ? binding.KeyLabel : binding.Keycode;
        if (key == Key.None && binding.PhysicalKeycode != Key.None)
        {
            key = DisplayServer.GetName() == "headless" ? binding.PhysicalKeycode
                : DisplayServer.KeyboardGetLabelFromPhysical(binding.PhysicalKeycode);
            if (key == Key.None) key = binding.PhysicalKeycode;
        }
        return key == Key.None ? new(false, "") : new(true, OS.GetKeycodeString(key));
    }
}
