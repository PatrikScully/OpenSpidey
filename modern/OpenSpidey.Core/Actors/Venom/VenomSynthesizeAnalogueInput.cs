using OpenSpidey.Core.Actors.Cinematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::SynthesizeAnalogueInput (0x4E9B00): elapsed350, decode354, cursor358, commands35C; finish clears34D when idle.</summary>
public static class VenomSynthesizeAnalogueInput
{
    public static void Tick(VenomState state, IVenomHost host, CinematicActorScriptRuntime script)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(script);
        if (state.Field34D == 0) return;

        state.ScriptElapsedTicks = unchecked(state.ScriptElapsedTicks + host.ElapsedTicks);
        if (!script.Running) script.Begin();
        script.Update(host.ElapsedTicks, host.FastForwardRequested);
        state.Field354 = script.Decoding ? (byte)1 : (byte)0;
        state.Field34C = script.Running ? (byte)1 : (byte)0;
        if (script.CommandCount == 0 && !script.Decoding)
        {
            state.Field34D = 0;
            state.Field354 = 0;
            state.Field34C = 0;
        }
    }
}
