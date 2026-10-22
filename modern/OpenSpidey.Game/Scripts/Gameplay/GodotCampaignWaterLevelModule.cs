using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Gameplay;

public sealed class GodotCampaignWaterLevelConfiguration
{
    public required GodotActorRegistry Registry { get; init; }
    public required CampaignSceneWaterState SceneWater { get; init; }
    public required PlayerModuleHook PlayerHook { get; init; }
    public required Action<ushort> SendPulse { get; init; }
}

/// <summary>Registers l5a6lsc / l5a7lsc / l6a1lsc / l6a2lsc / l6a3lsc module0 monitors and the shared player hook bit.</summary>
public partial class GodotCampaignWaterLevelModule : Node, ILevelWaterMonitorHost
{
    private static readonly uint[] L6A1PoolModelChecksums = [0x6A6967FF, 0x9E0EE08E];
    private static readonly uint[] L5A6PoolModelChecksums = [0x8D1EE7F8, 0x6A6967FF, 0xD7B2D512, 0xD7B2D512];
    private const uint L5A7PoolModelChecksum = 0xC50DC421;

    private GodotCampaignWaterLevelConfiguration configuration = null!;
    private int drownTimer;
    private string? activeModule;
    private bool disposed;

    public static GodotCampaignWaterLevelModule Create(GodotCampaignWaterLevelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.Registry);
        ArgumentNullException.ThrowIfNull(configuration.SceneWater);
        ArgumentNullException.ThrowIfNull(configuration.PlayerHook);
        ArgumentNullException.ThrowIfNull(configuration.SendPulse);
        return new() { Name = "CampaignWaterLevelModule", configuration = configuration };
    }

    public void Register(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> registry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(registry);
        RegisterModule(registry, "l5a6lsc", () => LevelWaterMonitor.MonitorL5A6(this));
        RegisterModule(registry, "l5a7lsc", () => LevelWaterMonitor.MonitorL5A7(this));
        RegisterModule(registry, "l6a1lsc", () => LevelWaterMonitor.MonitorL6A1(this));
        RegisterModule(registry, "l6a2lsc", () => LevelWaterMonitor.MonitorL6A2(this));
        RegisterModule(registry, "l6a3lsc", () => LevelWaterMonitor.MonitorL6A3(this));
    }

    private void RegisterModule(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> registry, string name, Action monitor)
    {
        registry.Register(name, module =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            module.SetClear(ClearModule);
            module.SetParameterlessUserFunction(0, () =>
            {
                if (!ReferenceEquals(activeModule, name))
                    throw new InvalidOperationException($"Source module {name} monitor ran without owning the active water-script slot.");
                monitor();
            });
            activeModule = name;
            drownTimer = 0;
            configuration.PlayerHook.Set(name, 1);
        });
    }

    private void ClearModule()
    {
        drownTimer = 0;
        configuration.SceneWater.PlayerInWaterVolume = false;
        configuration.SceneWater.SceneWaterLevelY = 0;
        if (activeModule is not null && configuration.PlayerHook.ModuleName == activeModule)
            configuration.PlayerHook.Set(null, 0);
        activeModule = null;
    }

    FixedVector3 ILevelWaterMonitorHost.PlayerPosition =>
        configuration.Registry.Player?.Controller.Position ??
        throw new InvalidOperationException("Level water monitors require the scene player.");

    int ILevelWaterMonitorHost.PlayerElapsedTicks =>
        configuration.Registry.Player?.Combat.State.ElapsedTicks ??
        throw new InvalidOperationException("Level water monitors require the scene player.");

    CampaignSceneWaterState ILevelWaterMonitorHost.SceneWater => configuration.SceneWater;

    int ILevelWaterMonitorHost.DrownTimer
    {
        get => drownTimer;
        set => drownTimer = value;
    }

    TriggerFile ILevelWaterMonitorHost.Triggers => configuration.Registry.Triggers;

    void ILevelWaterMonitorHost.SendDrownPulse(ushort node) => configuration.SendPulse(node);

    void ILevelWaterMonitorHost.TeleportPlayer(FixedVector3 position)
    {
        GodotPlayer player = configuration.Registry.Player ?? throw new InvalidOperationException("Drowning relocation requires the scene player.");
        player.Combat.State.Position = position;
        player.Controller.ApplyCombatState();
    }

    int ILevelWaterMonitorHost.ObtainPoolWaterLevelL6A1(int poolIndex)
    {
        if ((uint)poolIndex >= L6A1PoolModelChecksums.Length)
            throw new ArgumentOutOfRangeException(nameof(poolIndex));
        return FindPoolWaterLevel(L6A1PoolModelChecksums[poolIndex], null);
    }

    int ILevelWaterMonitorHost.ObtainPoolWaterLevelL5A6(int poolIndex)
    {
        if ((uint)poolIndex >= L5A6PoolModelChecksums.Length)
            throw new ArgumentOutOfRangeException(nameof(poolIndex));
        Func<GodotPlatformActor, bool>? filter = poolIndex switch
        {
            2 => platform =>
            {
                int x = platform.SourcePosition.X >> 12;
                int z = platform.SourcePosition.Z >> 12;
                return x > -360 && x < 1600 && z > 6800 && z < 10900;
            },
            3 => platform =>
            {
                int x = platform.SourcePosition.X >> 12;
                int z = platform.SourcePosition.Z >> 12;
                return x > -360 && x < 1600 && z > -2100 && z < 2000;
            },
            _ => null
        };
        return FindPoolWaterLevel(L5A6PoolModelChecksums[poolIndex], filter);
    }

    int ILevelWaterMonitorHost.ObtainPoolWaterLevelL5A7() => FindPoolWaterLevel(L5A7PoolModelChecksum, null);

    private int FindPoolWaterLevel(uint checksum, Func<GodotPlatformActor, bool>? filter)
    {
        PsxAssetLibrary library = configuration.Registry.ObjectLibrary();
        int modelIndex = library.FindModel(checksum);
        foreach (object identity in configuration.Registry.SourceActors(ActorUpdateGroup.Environment))
        {
            if (identity is not GodotPlatformActor platform || platform.SourceActorType != 402) continue;
            if (platform.ModelIndex != modelIndex && platform.ModelChecksum != checksum) continue;
            if (filter is not null && !filter(platform)) continue;
            return platform.SourcePosition.Y >> 12;
        }

        throw new InvalidOperationException("Pool object not found");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) disposed = true;
    }
}
