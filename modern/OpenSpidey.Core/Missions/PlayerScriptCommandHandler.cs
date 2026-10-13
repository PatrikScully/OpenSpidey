using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Decodes CutSceneScript before transferring ownership to the game's player controller.</summary>
public sealed class PlayerScriptCommandHandler(Action<PlayerScriptProgram> startScript) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.PlayerInputScript;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        PlayerScriptProgram program = PlayerScriptProgram.Read(instruction.Arguments.Span,
            checked(instruction.SourceOffset + 2));
        startScript(program);
    }
}
