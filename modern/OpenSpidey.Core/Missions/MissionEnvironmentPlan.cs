using System.Text;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Identifies authored startup geometry without loading it ahead of its actual command.</summary>
public sealed record MissionEnvironmentPlan(string AssetPath, bool LoadsInAutoexec)
{
    public static MissionEnvironmentPlan Read(TriggerFile triggers, string? savedRestart = null, bool alternativeAutoexec = false)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? restartName = null;
        foreach (TriggerNode node in MissionAutoexecSelection.Select(triggers, alternativeAutoexec))
        {
            foreach (TriggerInstruction instruction in TriggerCommandReader.Read(node))
            {
                if (instruction.Opcode == (ushort)MissionOpcode.LoadEnvironment)
                    paths.Add(PsxLibraryRepository.AssetPath(ReadName(instruction)));
                else if (instruction.Opcode is (ushort)MissionOpcode.SetRestart or (ushort)MissionOpcode.SetRestartSilently)
                    restartName = ReadName(instruction);
            }
        }
        bool autoexec = paths.Count != 0;
        if (!autoexec)
        {
            string name = string.IsNullOrEmpty(savedRestart) ? restartName ??
                throw new InvalidDataException("The mission selects no authored startup restart.") : savedRestart;
            var checkpoints = new MissionCheckpoints(triggers);
            ushort restart = checkpoints.Select(name).Point.NodeId;
            foreach (TriggerInstruction instruction in TriggerCommandReader.Read(triggers.Nodes[restart]))
                if (instruction.Opcode == (ushort)MissionOpcode.LoadEnvironment)
                    paths.Add(PsxLibraryRepository.AssetPath(ReadName(instruction)));
        }
        if (paths.Count != 1)
            throw new NotSupportedException($"The authored startup requests {paths.Count} distinct environments; its world composition must be implemented explicitly.");
        return new(paths.Single(), autoexec);
    }

    private static string ReadName(TriggerInstruction instruction)
    {
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException($"Source command {instruction.Opcode} has no name terminator.");
        return Encoding.Latin1.GetString(instruction.Arguments.Span[..end]);
    }
}
