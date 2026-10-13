namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>The interpreter owns flow; actor adapters own variables, world effects and retirement.</summary>
public interface IActorScriptHost
{
    ActorScriptState ScriptState { get; }
    short ReadVariable(ushort variable, ActorScriptCursor operands);
    void WriteVariable(ushort variable, ActorScriptCursor operands);
    bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment);
    ActorScriptCursor ScriptAtNode(ushort nodeId);
    bool PlayerCeilingCheck(short x, short z);
    void FlashCompass();
    void SendDeathPulse();
    void Retire();
}
