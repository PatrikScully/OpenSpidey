using System.Runtime.CompilerServices;
using Godot;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>One native player owner for the shared eight-slot source hook, after its complete AI and contact presentation.</summary>
public partial class GodotPlayerModuleHook : Node
{
    private static readonly ConditionalWeakTable<PlayerTerrainController, GodotPlayerModuleHook> owners = new();
    private PlayerModuleHook? hook;
    private SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor>? modules;
    private PlayerTerrainController? controller;
    private GodotNativeDestructionObserver? playerLifetime;
    private bool disposed, subscribed;
    internal int SourceHookVisits { get; private set; }
    internal bool PlayerHookBound => controller is not null;
    internal PlayerTerrainController? BoundPlayerController => controller;
    internal bool ResourcesDisposed => disposed;

    /// <summary>Attach the sole hook owner to the actual player; repeating the same binding preserves its original subscription.</summary>
    public static GodotPlayerModuleHook Bind(GodotPlayer player, PlayerModuleHook hook,
        SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> modules)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(modules);
        if (!GodotObject.IsInstanceValid(player)) throw new ObjectDisposedException(nameof(player));
        PlayerTerrainController controller = player.Controller;
        if (owners.TryGetValue(controller, out GodotPlayerModuleHook? existing))
        {
            if (!ReferenceEquals(existing.hook, hook) || !ReferenceEquals(existing.modules, modules))
                throw new InvalidOperationException("The actual player already owns a different source module hook binding.");
            return existing;
        }

        var owner = new GodotPlayerModuleHook { Name = "SourcePlayerModuleHook", hook = hook, modules = modules, controller = controller };
        owners.Add(controller, owner);
        try
        {
            player.AddChild(owner);
            owner.playerLifetime = new GodotNativeDestructionObserver
            {
                Name = "SourcePlayerModuleHookLifetime", Destroyed = () => owner.Release(playerDestroyed: true)
            };
            player.AddChild(owner.playerLifetime);
            controller.AfterPlayerAi += owner.Run;
            owner.subscribed = true;
            return owner;
        }
        catch
        {
            owner.Free();
            throw;
        }
    }

    private void Run()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SourceHookVisits++;
        hook!.Run(modules!.CallParameterlessUserFunction);
    }

    private void Release(bool playerDestroyed = false)
    {
        if (disposed) return;
        disposed = true;
        if (controller is { } bound)
        {
            if (subscribed) bound.AfterPlayerAi -= Run;
            owners.Remove(bound);
        }
        controller = null; hook = null; modules = null; subscribed = false;
        if (playerLifetime is { } lifetime && GodotObject.IsInstanceValid(lifetime))
        {
            lifetime.Destroyed = null;
            if (!playerDestroyed) lifetime.Free();
        }
        playerLifetime = null;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
