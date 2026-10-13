namespace OpenSpidey.Core.Player;

/// <summary>Movement axes and action buttons selected at the player's input phase.</summary>
public readonly record struct PlayerControlFrame(PlayerInputSample Movement, PlayerButtonInput Buttons);
