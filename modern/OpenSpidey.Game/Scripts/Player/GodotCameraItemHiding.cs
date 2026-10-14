using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Native CItem owners for camera F9 hiding: mode1 ENV/environmental-object ray hits and the player's held object (player+0xE48).</summary>
public sealed class GodotCameraItemHiding
{
    /// <summary>A freed generation owns no forty-slot region, so it never equals a captured live region byte.</summary>
    private const byte UnloadedRegion = 0xFF;
    private readonly PlayerFollowCamera camera;
    private readonly Func<GodotActorWorldQuery?> world;
    private readonly GodotEnvironmentVisibility visibility;
    private readonly PsxLibraryRepository repository;
    private readonly PsxAssetRegions regions;
    private readonly Func<GodotScriptActor, bool> liveActor;
    private readonly Func<GodotScriptActor?> heldItem;
    public CameraHiddenItems<object> Items { get; }
    public Action? OverflowDiagnostic { get; set; }
    internal GodotEnvironmentVisibility Visibility => visibility;
    internal Func<GodotActorWorldQuery?> World => world;

    public GodotCameraItemHiding(PlayerFollowCamera camera, Func<GodotActorWorldQuery?> world, GodotEnvironmentVisibility visibility,
        PsxLibraryRepository repository, PsxAssetRegions regions, Func<GodotScriptActor, bool> liveActor, Func<GodotScriptActor?> heldItem)
    {
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.liveActor = liveActor ?? throw new ArgumentNullException(nameof(liveActor));
        this.heldItem = heldItem ?? throw new ArgumentNullException(nameof(heldItem));
        Items = new(Read, Write);
    }

    /// <summary>CScorpion AI's direct camera0x184 slot read; Identity is the same owner the player's held-object slot returns.</summary>
    public IScorpionCameraHiddenItem? GetBossHiddenItem(int index) => Items.GetItem(index) is { } item ? new BossHiddenItem(this, item) : null;

    private sealed class BossHiddenItem(GodotCameraItemHiding owner, object item) : IScorpionCameraHiddenItem
    {
        public object Identity => item;
        public ushort ItemFlags
        {
            get => owner.Read(item).Flags;
            set { if (owner.Read(item).Region != UnloadedRegion) owner.Write(item, value); }
        }
    }

    /// <summary>The CameraPlacement.Move hideItems step, after the mode placement wrote the camera position and display rotation.</summary>
    public void HideItems()
    {
        CameraModeState state = camera.ScriptRig.ModeState;
        Items.Update(state.Position, camera.Tripod.Position, () => state.DisplayRotation, Trace, () => heldItem(), OverflowDiagnostic);
    }

    private CameraItemTrace<object>? Trace(FixedVector3 from, FixedVector3 to)
    {
        GodotActorWorldQuery query = world() ?? throw new InvalidOperationException("Camera item hiding requires the session's source line-query owner.");
        if (query.CameraCollisionCheck) throw new InvalidOperationException("Camera item rays are ordinary mode1 queries without CameraCollisionCheck.");
        if (query.Trace(from, to, includeEnvironmentalActors: true) is not { } hit) return null;
        return new(Resolve(hit), hit.Position, PlayerLookaroundCameraSetup.SourceLineLength(from, hit.Position));
    }

    private object Resolve(GodotActorWorldHit hit)
    {
        if (hit.SupportId is { } id)
            return GodotObject.InstanceFromId(id) as GodotManipulationActor ??
                throw new NotSupportedException("Camera item hiding has no native CItem flag adapter for this environmental object.");
        GodotEnvironmentVisibilityItem? item = hit.SourceAsset is { } asset ? visibility.SourceItems.FirstOrDefault(candidate =>
            candidate.InstanceIndex == hit.InstanceIndex && candidate.Source.AssetPath.Equals(asset, StringComparison.OrdinalIgnoreCase)) : null;
        return item ?? throw new NotSupportedException($"Camera item hiding found no current ENV CItem for {hit.SourceAsset} instance {hit.InstanceIndex}.");
    }

    private CameraHiddenItemSnapshot Read(object item) => item switch
    {
        GodotEnvironmentVisibilityItem environment => visibility.IsCurrentSourceItem(environment) && environment.RegionUsable
            ? new(regions.RequireRegion(repository.RequireLoaded(environment.Source.AssetPath)), checked((ushort)environment.InstanceIndex), environment.ItemFlags)
            : new(UnloadedRegion, unchecked((ushort)environment.InstanceIndex), 0),
        GodotManipulationActor actor => liveActor(actor) && actor.SourceVisibilityBank is { } bank && IsLoaded(bank)
            ? new(regions.RequireRegion(bank), checked((ushort)actor.ModelIndex), actor.State.ItemFlags)
            : new(UnloadedRegion, 0, 0),
        _ => throw new NotSupportedException($"Camera item hiding has no native CItem adapter for {item.GetType().Name}.")
    };

    private void Write(object item, ushort flags)
    {
        switch (item)
        {
            case GodotEnvironmentVisibilityItem environment: visibility.WriteSourceItemFlags(environment, flags); break;
            case GodotManipulationActor actor: actor.SetSourceVisibilityFlags(flags); break;
            default: throw new NotSupportedException($"Camera item hiding has no native CItem adapter for {item.GetType().Name}.");
        }
    }

    private bool IsLoaded(PsxAssetLibrary bank) =>
        repository.Loaded.TryGetValue(bank.AssetPath, out PsxAssetLibrary? live) && ReferenceEquals(live, bank);
}
