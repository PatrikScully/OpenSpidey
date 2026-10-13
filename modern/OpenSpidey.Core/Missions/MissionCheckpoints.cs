using System.Collections.Frozen;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public readonly record struct CheckpointSelection(RestartPoint Point, bool SaveRequired, bool ShowMessage);

/// <summary>Mission checkpoint selection; selecting a checkpoint does not teleport the player.</summary>
public sealed class MissionCheckpoints
{
    private readonly FrozenDictionary<string, RestartPoint> points;
    public RestartPoint? Current { get; private set; }

    // The recovered flag is set by a non-death selection and remains set until the mission is reset.
    public bool HasNonDeathRestart { get; private set; }

    public MissionCheckpoints(TriggerFile file)
    {
        var index = new Dictionary<string, RestartPoint>(StringComparer.Ordinal);
        foreach (TriggerNode node in file.Nodes)
        {
            if (node.TypeCode != (ushort)TriggerNodeKind.Restart) continue;
            RestartPoint point = RestartPoint.Read(node);
            // Trig_SetRestart takes the first matching node when names repeat.
            index.TryAdd(NormalizeName(point.Name), point);
        }
        points = index.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public CheckpointSelection Select(string name, bool silent = false)
    {
        RestartPoint? previous = Current;
        Current = null;
        string normalized = NormalizeName(name);
        if (!points.TryGetValue(normalized, out RestartPoint? point))
            throw new InvalidDataException($"Restart point '{name}' does not exist in this mission.");
        Current = point;
        if (normalized != "re_start_death") HasNonDeathRestart = true;
        bool saveRequired = previous is not null && previous.NodeId != point.NodeId;
        return new(point, saveRequired, saveRequired && !silent);
    }

    private static string NormalizeName(string name) => string.Create(name.Length, name, (characters, source) =>
    {
        for (int index = 0; index < source.Length; index++)
        {
            char value = source[index];
            characters[index] = value is >= 'A' and <= 'Z' ? (char)(value + ('a' - 'A')) : value;
        }
    });
}
