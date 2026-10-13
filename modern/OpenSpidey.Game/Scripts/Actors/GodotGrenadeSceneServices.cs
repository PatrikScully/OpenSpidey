using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Required actual scene-owned actors, assets, clocks and effect leaves for source grenade construction.</summary>
public sealed class GodotGrenadeSceneContext
{
    public required Node3D ProjectileRoot { get; init; }
    public required GodotActorWorldQuery World { get; init; }
    public required GodotActorSessionDirectory Actors { get; init; }
    public required GodotPlayerMeleeDirectory Baddies { get; init; }
    public required GodotPlayer Player { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required PsxAssetRegions Regions { get; init; }
    public required Func<PsxAssetLibrary> ObjectLibrary { get; init; }
    public required Func<int> Timer { get; init; }
    public required Func<int> TotalTime { get; init; }
    public required GodotThugVisualEffects Visuals { get; init; }
    public required SoundChannelController Sounds { get; init; }
    public required Func<SoundListener> Listener { get; init; }
    public required IGodotGrenadeEnvironmentHost Environment { get; init; }
}

/// <summary>Real grenade identities in the scene BulletList, linked actor blast views and generation-checked source model regions.</summary>
public sealed class GodotGrenadeSceneServices : IDisposable
{
    private sealed class Body(object identity, Func<FixedVector3> position, Action<ActorHit> hit,
        IProjectileBlastBody? next) : IProjectileBlastBody
    {
        public object Identity { get; } = identity;
        public FixedVector3 Position => position();
        public IProjectileBlastBody? NextBody { get; } = next;
        public void Hit(ActorHit contact) => hit(contact);
    }

    private readonly GodotGrenadeSceneContext context;
    private readonly List<GodotGrenadeProjectile> projectiles = [];
    private bool disposed;
    public GodotActorRegistry Registry => context.Actors.Registry;
    public IReadOnlyList<GodotGrenadeProjectile> SourceProjectileOrder => projectiles.AsReadOnly();
    public GodotGrenadeConfiguration Configuration { get; }
    public event Action<GodotGrenadeProjectile>? ProjectileAttached;
    public event Action<GodotGrenadeProjectile>? ProjectileDetached;

    public GodotGrenadeSceneServices(GodotGrenadeSceneContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        if (!ReferenceEquals(context.Actors.Registry.Player, context.Player))
            throw new ArgumentException("Grenade damage must use the actual player owned by its source actor scene.", nameof(context));
        Configuration = new()
        {
            World = context.World,
            ResolveRegion = ResolveRenderableBank,
            ObjectFileRegion = () => context.Regions.RequireRegion(context.ObjectLibrary()),
            DefaultGrenadeRegion = () => context.Regions.RequireRegion(context.Repository.RequireLoaded("items")),
            Timer = context.Timer,
            TotalTime = context.TotalTime,
            LevelId = () => context.Actors.Registry.Environment.LevelId,
            Random = context.Actors.Registry.RandomSource,
            Glows = context.Visuals.Glows,
            Explosions = context.Visuals.Explosions,
            ShadowFactory = context.Visuals.CreateProjectileShadow,
            PlayPositionSound = (sound, position, _) => context.Sounds.PlayAt(sound, position, context.Listener()),
            AttachProjectile = Attach,
            DetachProjectile = Detach,
            ApplyObjectBlast = ApplyObjectBlast,
            ApplyEnvironmentBlast = (_, position, damage, radius) =>
                ProjectileBlastDamage.GiveScaledDamageToEnviro(position, damage, radius, context.Environment.Head)
        };
    }

    public GodotGrenadeProjectile Spawn(GodotThugActor actor, ThugGrenadeRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ReferenceEquals(context.Actors.RecoverNative(actor), actor))
            throw new ArgumentException("Source grenade throw requires a live scene-owned native thug.", nameof(actor));
        GodotGrenadeProjectile projectile = GodotGrenadeProjectile.Create(request, Configuration);
        try { context.ProjectileRoot.AddChild(projectile); return projectile; }
        catch { projectile.Free(); throw; }
    }

    private PsxAssetLibrary ResolveRenderableBank(byte region)
    {
        PsxAssetLibrary bank = context.Regions.Resolve(region);
        IReadOnlyList<OpenSpidey.Assets.Textures.DecodedTexture> textures = bank.Textures;
        if (bank.AssetPath.EndsWith("_O.psx", StringComparison.OrdinalIgnoreCase) &&
            context.Repository.Loaded.TryGetValue(bank.AssetPath[..^6] + "_L.psx", out PsxAssetLibrary? shared))
            textures = shared.Textures.Concat(bank.Textures).ToArray();
        return ReferenceEquals(textures, bank.Textures) ? bank : bank with { Textures = textures };
    }

    private void Attach(GodotGrenadeProjectile projectile)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (projectiles.Contains(projectile)) throw new InvalidOperationException("Source grenade attached twice.");
        projectiles.Insert(0, projectile);
        ProjectileAttached?.Invoke(projectile);
    }
    private void Detach(GodotGrenadeProjectile projectile)
    {
        if (!projectiles.Remove(projectile)) throw new InvalidOperationException("Source grenade detached without its actual list membership.");
        ProjectileDetached?.Invoke(projectile);
    }

    private IProjectileBlastBody? PlayerHead() =>
        GodotObject.IsInstanceValid(context.Player) && !context.Player.IsQueuedForDeletion()
            ? new Body(context.Player, () => context.Player.Controller.Position, hit => context.Player.Combat.Hit(hit), null) : null;

    private IProjectileBlastBody? ActorHead(IReadOnlyList<GodotScriptActor> actors)
    {
        IProjectileBlastBody? head = null;
        for (int index = actors.Count - 1; index >= 0; index--)
        {
            GodotScriptActor actor = actors[index];
            if (context.Actors.RecoverNative(actor) is null)
                throw new InvalidOperationException("Source blast list contains an unowned native actor.");
            head = new Body(actor, () => actor.SourcePosition, hit => context.Actors.ApplyHit(actor, hit), head);
        }
        return head;
    }

    public IProjectileBlastBody? BlastHead(GrenadeDamageList list, GrenadeTargetList targets)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return list switch
        {
            GrenadeDamageList.PrimaryTargets when targets == GrenadeTargetList.Player => PlayerHead(),
            GrenadeDamageList.PrimaryTargets when targets == GrenadeTargetList.Baddies =>
                ActorHead(context.Baddies.SourceBodies.Select(body => body.Native).ToArray()),
            GrenadeDamageList.EnvironmentalBodies => ActorHead(context.Actors.Registry.SourceActors(ActorUpdateGroup.Environment)),
            GrenadeDamageList.Player => PlayerHead(),
            _ => throw new ArgumentOutOfRangeException(nameof(list), "Unknown source grenade target list.")
        };
    }

    private void ApplyObjectBlast(GodotGrenadeProjectile projectile, GrenadeDamageRequest request) =>
        ProjectileBlastDamage.GiveScaledDamageToObjects(request.Position, request.Damage, request.Radius,
            request.SlideDistance, request.SlideFrames, request.AttackKind, BlastHead(request.List, projectile.State.Targets));

    /// <summary>Called by the shared source BulletList after it captures this body's actual next link.</summary>
    public void AdvanceCapturedProjectile(GodotGrenadeProjectile projectile)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (projectiles.Contains(projectile) && GodotObject.IsInstanceValid(projectile)) projectile.Advance();
    }

    public void Dispose()
    {
        if (disposed) return;
        // Detach callbacks remain valid while their real bullet destructors run.
        foreach (GodotGrenadeProjectile projectile in projectiles.ToArray())
            if (GodotObject.IsInstanceValid(projectile)) projectile.Free();
        disposed = true;
        if (projectiles.Count != 0) throw new InvalidOperationException("Source grenade teardown retained native bullet-list identities.");
    }
}
