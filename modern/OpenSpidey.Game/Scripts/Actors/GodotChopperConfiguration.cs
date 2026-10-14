using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

public sealed class GodotChopperConfiguration
{
    public required Func<ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<GodotPlayer?> PlayerProvider { get; init; }
    public required Func<uint> Timer { get; init; }
    public required Func<int> SourceConstructionOrdinal { get; init; }
    public required Func<GodotChopperActor, bool> IsSourceCulled { get; init; }
}
