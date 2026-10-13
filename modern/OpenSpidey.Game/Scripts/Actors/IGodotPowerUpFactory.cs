using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Game.Actors;

/// <summary>Authored kind5/20 constructor family; creation attaches its actor at the original constructor boundary.</summary>
public interface IGodotPowerUpFactory
{
    /// <returns>An already adopted source PowerUp actor, or null for the original hardest-difficulty type14 suppression.</returns>
    GodotScriptActor? Create(TriggerNode node, GodotActorRegistry registry);
}
