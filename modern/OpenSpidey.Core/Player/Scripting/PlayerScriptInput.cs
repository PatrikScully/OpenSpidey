namespace OpenSpidey.Core.Player.Scripting;

public sealed record PlayerScriptInput(PlayerAxes Axes, IReadOnlyList<PlayerButtonState> Buttons);
