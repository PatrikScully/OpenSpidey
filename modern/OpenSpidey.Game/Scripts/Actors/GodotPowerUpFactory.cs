using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.PowerUps;

namespace OpenSpidey.Game.Actors;

/// <summary>Original PowerUp_Create difficulty remapping and authored kind5/20 constructor/SetNode order.</summary>
public sealed class GodotPowerUpFactory(GodotPowerUpConfiguration configuration) : IGodotPowerUpFactory
{
    public GodotScriptActor? Create(TriggerNode node, GodotActorRegistry registry)
    {
        TriggerPowerUpDefinition definition = TriggerPowerUpDefinition.Read(node);
        ushort? type = PowerUpBehavior.RemapType(definition.Type, registry.Difficulty);
        return type is ushort actual ? GodotPowerUpActor.Create(definition, actual, registry, configuration) : null;
    }
}
