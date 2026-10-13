using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Authored110/162 resource owners survive source body destruction until the command stream releases them.</summary>
public sealed class GodotMissionResourceCleanup(
    GodotMissionLibraries libraries, PsxAssetRegions regions, GodotActorCodeModules modules) : IMissionResourceCleanupHost
{
    public void ClearSceneLibraries() => libraries.ClearUnprotectedLibraries(regions);
    public void ClearCodeModules() => modules.UnloadAllCodeModules();
}
