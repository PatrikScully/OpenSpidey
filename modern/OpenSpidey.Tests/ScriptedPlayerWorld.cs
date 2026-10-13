using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal sealed class ScriptedPlayerWorld(params PlayerWorldHit?[] hits) : IPlayerWorldQuery
{
    private readonly Queue<PlayerWorldHit?> results = new(hits);
    private readonly List<(FixedVector3 Start, FixedVector3 End)> rays = [];
    public IReadOnlyList<(FixedVector3 Start, FixedVector3 End)> Rays => rays;

    public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
    {
        rays.Add((start, end));
        return results.Count == 0 ? null : results.Dequeue();
    }
}
