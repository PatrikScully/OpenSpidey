using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Modules;

namespace OpenSpidey.Game.Actors;

public readonly record struct GodotActorModuleArguments(TriggerNode Node, GodotActorRegistry Registry);

/// <summary>Real thug/blackcat module user functions and captured source BaddyList destruction. Factory registration never loads a module.</summary>
public sealed class GodotActorCodeModules : IMissionModuleLoader
{
    private readonly GodotActorRegistry registry;
    private readonly bool hostageConfigured;
    private bool factoriesRegistered;
    public SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> Modules { get; } = new();
    public GodotActorCodeModules(GodotActorRegistry registry, GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat,
        GodotHostageConfiguration? hostages = null)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(thugs); ArgumentNullException.ThrowIfNull(blackCat);
        hostageConfigured = hostages is not null;
        Modules.Register("thug", module =>
        {
            module.SetClear(() => ClearSourceBaddies(304, 312));
            module.SetUserFunction(0, arguments =>
            {
                ValidateArguments(arguments);
                return GodotThugActor.Create(arguments.Node, registry, thugs);
            });
        });
        Modules.Register("blackcat", module =>
        {
            module.SetClear(() => ClearSourceBaddies(319));
            module.SetUserFunction(0, arguments =>
            {
                ValidateArguments(arguments);
                return GodotBlackCatActor.Create(arguments.Node, registry, blackCat);
            });
        });
        if (hostages is not null)
        {
            Modules.Register("hostage", module =>
            {
                // Actual442430 clears305 only; the separate UnloadAll pre-clear owns315.
                module.SetClear(() => ClearSourceBaddies(305));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotHostageActor.Create(arguments.Node, registry, hostages);
                });
            });
        }
    }
    public void RegisterFactories()
    {
        if (factoriesRegistered) throw new InvalidOperationException("Source module actor factories are already registered.");
        foreach (short type in new short[] { 304, 312 })
            registry.RegisterFactory(type, (node, owner) => Modules.CallUserFunction("thug", 0, new(node, owner)));
        registry.RegisterFactory(319, (node, owner) => Modules.CallUserFunction("blackcat", 0, new(node, owner)));
        if (hostageConfigured)
            foreach (short type in new short[] { 305, 315 })
                registry.RegisterFactory(type, (node, owner) => Modules.CallUserFunction("hostage", 0, new(node, owner)));
        factoriesRegistered = true;
    }
    public void LoadCodeModule(string name) => Modules.Load(name);
    public void UnloadAllCodeModules()
    {
        ClearSourceBaddies(317, 304, 305, 306, 320, 312, 315);
        Modules.UnloadAll();
    }
    private void ValidateArguments(GodotActorModuleArguments arguments)
    {
        if (!ReferenceEquals(arguments.Registry, registry))
            throw new InvalidOperationException("Source module factory cannot transfer ownership to a different scene registry.");
    }
    private void ClearSourceBaddies(params short[] types)
    {
        // These original clears delete only the active BaddyList; suspended bodies are a separate source list.
        foreach (object identity in registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder)
            if (identity is GodotScriptActor actor && registry.Owns(actor) && types.Contains(actor.SourceActorType))
                registry.DestroyImmediately(actor);
    }
}
