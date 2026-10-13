using System.Text;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public interface IMissionModuleLoader { void LoadCodeModule(string name); }

/// <summary>Original189 module name command; source names and initialization remain owned by the scene module registry.</summary>
public sealed class ModuleLoadCommandHandler(IMissionModuleLoader modules) : IMissionCommandHandler
{
    public ushort Opcode => 189;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode) throw new InvalidDataException("Module loader received a different source opcode.");
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0 || ((end + 2) & ~1) != instruction.Arguments.Length)
            throw new InvalidDataException($"Node {context.Node.Id}: source module name must be terminated and word-aligned.");
        modules.LoadCodeModule(Encoding.Latin1.GetString(instruction.Arguments.Span[..end]));
    }
}
