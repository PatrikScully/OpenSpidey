using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Original Trig_ExecuteAutoexec selection: the first alternate node, or every normal node in file order.</summary>
public static class MissionAutoexecSelection
{
    public static IEnumerable<TriggerNode> Select(TriggerFile triggers, bool alternative = false)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        if (alternative)
        {
            TriggerNode? selected = triggers.Nodes.FirstOrDefault(node => node.TypeCode == (ushort)TriggerNodeKind.AlternativeAutoexec);
            if (selected is not null)
            {
                yield return selected;
                yield break;
            }
        }
        foreach (TriggerNode node in triggers.Nodes)
            if (node.TypeCode == (ushort)TriggerNodeKind.Autoexec) yield return node;
    }
}
