using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Choppers;
using OpenSpidey.Core.Actors.Cops;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Npcs;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Actors.Torch;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Modules;

namespace OpenSpidey.Game.Actors;

public readonly record struct GodotActorModuleArguments(TriggerNode Node, GodotActorRegistry Registry);

/// <summary>Real thug/blackcat module user functions and captured source BaddyList destruction. Factory registration never loads a module.</summary>
public sealed class GodotActorCodeModules : IMissionModuleLoader
{
    private readonly GodotActorRegistry registry;
    private readonly bool hostageConfigured;
    private readonly bool idleNpcsConfigured;
    private readonly bool torchConfigured;
    private readonly bool lizManConfigured;
    private readonly bool simbyConfigured;
    private readonly bool rhinoConfigured;
    private readonly bool venomConfigured;
    private readonly bool copConfigured;
    private readonly bool chopperConfigured;
    private bool factoriesRegistered;
    public SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> Modules { get; } = new();
    public GodotActorCodeModules(GodotActorRegistry registry, GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat,
        GodotHostageConfiguration? hostages = null, GodotIdleNpcConfiguration? idleNpcs = null,
        GodotTorchConfiguration? torch = null, GodotLizManConfiguration? lizMan = null,
        GodotSimbyConfiguration? simby = null, GodotRhinoConfiguration? rhino = null,
        GodotVenomConfiguration? venom = null,
        GodotCopConfiguration? cops = null, GodotChopperConfiguration? choppers = null)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(thugs); ArgumentNullException.ThrowIfNull(blackCat);
        hostageConfigured = hostages is not null;
        idleNpcsConfigured = idleNpcs is not null;
        torchConfigured = torch is not null;
        lizManConfigured = lizMan is not null;
        simbyConfigured = simby is not null;
        rhinoConfigured = rhino is not null;
        venomConfigured = venom is not null;
        copConfigured = cops is not null;
        chopperConfigured = choppers is not null;
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
        if (idleNpcs is not null)
        {
            foreach (string name in new[] { "mj", "lizard", "submarin" })
            {
                Modules.Register(name, module =>
                {
                    // The three modules share the Black Cat clear 0x4138D0, which deletes active type319 only.
                    module.SetClear(() => ClearSourceBaddies(319));
                    module.SetUserFunction(0, arguments =>
                    {
                        ValidateArguments(arguments);
                        return GodotIdleNpcActor.Create(arguments.Node, registry, idleNpcs);
                    });
                });
            }
        }
        if (torch is not null)
        {
            Modules.Register(TorchState.ModuleName, module =>
            {
                // Actual Torch_RelocatableModuleClear 0x4DC420 deletes active type328 only.
                module.SetClear(() => ClearSourceBaddies(TorchState.Type));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotTorchActor.Create(arguments.Node, registry, torch);
                });
            });
        }
        if (lizMan is not null)
        {
            Modules.Register(LizManState.ModuleName, module =>
            {
                // Actual LizMan_RelocatableModuleClear 0x44A5C0 deletes active type317 only.
                module.SetClear(() => ClearSourceBaddies(LizManState.Type));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotLizManActor.Create(arguments.Node, registry, lizMan);
                });
            });
        }
        if (simby is not null)
        {
            Modules.Register(SimbyState.ModuleName, module =>
            {
                // Actual Simby_RelocatableModuleClear 0x4A2470 deletes active type324 only.
                module.SetClear(() => ClearSourceBaddies(SimbyState.Type));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotSimbyActor.Create(arguments.Node, registry, simby);
                });
                module.SetUserFunction(1, _ => throw new NotSupportedException(
                    "Simby module slot1 CSimbyDroplet (trigger type411, 0x4A25E0) FlatBit adapter is not ported."));
                module.SetUserFunction(2, _ => throw new NotSupportedException(
                    "Simby module slot2 CFlamingImpactWeb (0x4A24F0) needs the shared flaming-impact web effect owner."));
                module.SetUserFunction(3, _ => throw new NotSupportedException(
                    "Simby module slot3 TestDrop is a no-op in source and has no Godot actor result."));
                module.SetUserFunction(4, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotPunchObActor.Create(arguments.Node, registry, simby);
                });
                module.SetUserFunction(5, _ => throw new NotSupportedException(
                    "Simby module slot5 CEmber (0x4A28C0) FlatBit adapter is not ported."));
            });
        }
        if (rhino is not null)
        {
            Modules.Register(RhinoState.ModuleName, module =>
            {
                // Actual Rhino_RelocatableModuleClear 0x47DD10 deletes active type307 only.
                module.SetClear(() => ClearSourceBaddies(RhinoState.Type));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotRhinoActor.Create(arguments.Node, registry, rhino);
                });
            });
        }
        if (venom is not null)
        {
            Modules.Register(VenomState.ModuleName, module =>
            {
                // Actual Venom_RelocatableModuleClear 0x4E7D50 deletes active type313 only.
                module.SetClear(() => ClearSourceBaddies(VenomState.Type));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotVenomActor.Create(arguments.Node, registry, venom);
                });
                module.SetParameterlessUserFunction(1, () =>
                {
                    if (registry.PlayerPosition is not { } player) return;
                    foreach (object identity in registry.SourceActors(ActorUpdateGroup.Enemy))
                    {
                        if (identity is not GodotVenomActor venom) continue;
                        VenomChaseBar.Update(venom.State, venom.Body.Position, player, registry.Difficulty,
                            postWaterEffect: false, venom.ElapsedTicks);
                        return;
                    }
                });
            });
        }
        if (cops is not null)
        {
            Modules.Register(CopState.ModuleName, module =>
            {
                module.SetClear(() => ClearSourceBaddies(306, 320));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotCopActor.Create(arguments.Node, registry, cops);
                });
            });
        }
        if (choppers is not null)
        {
            Modules.Register(ChopperState.ModuleName, module =>
            {
                module.SetClear(() => ClearSourceBaddies(318, 322, 323));
                module.SetUserFunction(0, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotChopperActor.Create(arguments.Node, registry, choppers);
                });
                module.SetUserFunction(1, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotChopperSearchlightActor.Create(arguments.Node, registry);
                });
                module.SetUserFunction(2, arguments =>
                {
                    ValidateArguments(arguments);
                    return GodotChopperSniperActor.Create(arguments.Node, registry);
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
        if (idleNpcsConfigured)
            foreach (short type in new short[] { 303, 326, 719 })
            {
                string module = IdleNpcState.ModuleNameOf(IdleNpcState.KindOf(type));
                registry.RegisterFactory(type, (node, owner) => Modules.CallUserFunction(module, 0, new(node, owner)));
            }
        if (torchConfigured)
            registry.RegisterFactory(TorchState.Type, (node, owner) => Modules.CallUserFunction(TorchState.ModuleName, 0, new(node, owner)));
        if (lizManConfigured)
            registry.RegisterFactory(LizManState.Type, (node, owner) => Modules.CallUserFunction(LizManState.ModuleName, 0, new(node, owner)));
        if (simbyConfigured)
        {
            registry.RegisterFactory(SimbyState.Type, (node, owner) => Modules.CallUserFunction(SimbyState.ModuleName, 0, new(node, owner)));
            registry.RegisterFactory(SimbyState.DropletTriggerType, (node, owner) => Modules.CallUserFunction(SimbyState.ModuleName, 1, new(node, owner)));
            registry.RegisterFactory(PunchObState.Type, (node, owner) => Modules.CallUserFunction(SimbyState.ModuleName, 4, new(node, owner)));
        }
        if (rhinoConfigured)
            registry.RegisterFactory(RhinoState.Type, (node, owner) => Modules.CallUserFunction(RhinoState.ModuleName, 0, new(node, owner)));
        if (venomConfigured)
            registry.RegisterFactory(VenomState.Type, (node, owner) => Modules.CallUserFunction(VenomState.ModuleName, 0, new(node, owner)));

        if (copConfigured)
            foreach (short type in new short[] { 306, 320 })
                registry.RegisterFactory(type, (node, owner) => Modules.CallUserFunction(CopState.ModuleName, 0, new(node, owner)));
        if (chopperConfigured)
        {
            registry.RegisterFactory(ChopperState.ChopperType, (node, owner) => Modules.CallUserFunction(ChopperState.ModuleName, 0, new(node, owner)));
            registry.RegisterFactory(ChopperState.SearchlightType, (node, owner) => Modules.CallUserFunction(ChopperState.ModuleName, 1, new(node, owner)));
            registry.RegisterFactory(ChopperState.SniperType, (node, owner) => Modules.CallUserFunction(ChopperState.ModuleName, 2, new(node, owner)));
        }

        factoriesRegistered = true;
    }
    public void LoadCodeModule(string name) => Modules.Load(name);
    public void UnloadAllCodeModules()
    {
        ClearSourceBaddies(313, 317, 307, 304, 305, 306, 320, 312, 315, 318, 322, 323);
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
