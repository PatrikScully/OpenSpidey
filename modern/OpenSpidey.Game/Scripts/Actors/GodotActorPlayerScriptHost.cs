using Godot;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Resolve the actual process player at each source script-only command, including the original required-player dependency.</summary>
public sealed class GodotActorPlayerScriptHost(Func<GodotPlayer?> player) : IActorPlayerScriptHost
{
    private GodotPlayer Current => player() is { } current && GodotObject.IsInstanceValid(current) ? current :
        throw new InvalidOperationException("The original script-only player command requires the allocated CPlayer.");

    public void SetIgnoreInputTimer(ushort frames) => Current.Controller.SetIgnoreInputTimer(frames);
    public int SwitchToSynthesizedInput(ReadOnlyMemory<byte> source, int sourceOffset) =>
        (Current.Scripts ?? throw new InvalidOperationException("The actual CPlayer input VM has not been composed."))
            .StartEmbedded(source, sourceOffset);
    public void SetMechListField8EB(bool enabled) => Current.Combat.State.Field8EB = enabled ? (byte)1 : (byte)0;
}
