using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorSpeechCommandHost
{
    void PlayGlobalSpeech(SpeechRequest request);
    void PlayPositionSpeech(SpeechRequest request, FixedVector3 position);
}

/// <summary>Inherited CBaddy speech leaves, including the control actor's death fast-forward guard.</summary>
public static class ActorSpeechCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, ActorScriptState actor,
        ActorScriptEnvironment environment, FixedVector3 position, IActorSpeechCommandHost host)
    {
        if (opcode is not (0x42A3 or 0x450A)) return false;
        ArgumentNullException.ThrowIfNull(operands);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(host);
        // The shipped PC command zero-extends all three words, despite the recovered C++ short locals.
        var request = new SpeechRequest(operands.ReadWord(), operands.ReadWord(), operands.ReadWord());
        if (actor.ActorType == 203 && actor.SkipDuringDeath && environment.FastForwardDeath) return true;
        if (opcode == 0x42A3) host.PlayGlobalSpeech(request);
        else host.PlayPositionSpeech(request, position);
        return true;
    }
}
