using OpenSpidey.Core.Actors.Cops;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

public sealed class GodotCopConfiguration
{
    public required Func<string, ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<GodotPlayer?> PlayerProvider { get; init; }
    public required CopCombatCoordinator CopCombat { get; init; }
    public required ThugCombatCoordinator SharedCombat { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<uint> Timer { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<int> SourceConstructionOrdinal { get; init; }
    public required Func<GodotCopActor, bool> IsSourceCulled { get; init; }
    public required GodotActorSessionServices Session { get; init; }
    public required Action<ushort, FixedVector3, int> PlayPositionSound { get; init; }
}
