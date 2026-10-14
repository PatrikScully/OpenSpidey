using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Modules;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Story palette commands retain the loaded module's typed user-function ownership.</summary>
internal sealed class GodotMissionPaletteFade(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> modules)
    : IMissionPaletteFadeHost
{
    public void FadeUp(PaletteFadeColour colour) => modules.CallUserAction("mysterio", 1, colour);
    // Original slot2 ignores both pointer arguments, including the command's uninitialized stack word.
    public void FadeDown() => modules.CallParameterlessUserFunction("mysterio", 2);
}
