using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Real L1A3 actors and static ENV ownership. This level has no authored grenade model, so no replacement model is spawned.</summary>
internal static class GrenadeSceneServicesImportChecks
{
    private sealed class OutsideCrateHost : IEnvironmentCrateHost
    {
        private static InvalidOperationException Outside() =>
            new("Ordinary null-face grenade damage unexpectedly requested a crate-only effect.");
        public bool LowMemory => throw Outside();
        public bool Shatter(IEnvironmentCrate crate, bool checkSeen, bool markProcessed) => throw Outside();
        public void TriggerCommandPoint(uint checksum) => throw Outside();
        public int Random(int range) => throw Outside();
        public void CreateCrateExplosion(FixedVector3 position, bool small) => throw Outside();
    }

    public static void VerifyFixture(PlayerInteractionFixture fixture, PkrArchive archive,
        PsxLibraryRepository repository, GodotThugVisualEffects visuals, SoundChannelController sounds,
        GodotThugActor thug, GodotHostageActor hostage)
    {
        using var directory = new GodotActorSessionDirectory(fixture.Registry, fixture.Directory, new GodotActorSessionPlatforms());
        using var regions = new PsxAssetRegions(repository);
        var environment = new GodotGrenadeEnvironment(() => fixture.World, repository,
            path => archive.Read(path), fixture.Session, new OutsideCrateHost());
        using var services = new GodotGrenadeSceneServices(new()
        {
            ProjectileRoot = fixture.Root,
            World = fixture.WorldQuery,
            Actors = directory,
            Baddies = fixture.Directory,
            Player = fixture.Player,
            Repository = repository,
            Regions = regions,
            ObjectLibrary = fixture.Registry.ObjectLibrary,
            Timer = () => unchecked((int)fixture.Clock),
            TotalTime = () => unchecked((int)fixture.Clock),
            Visuals = visuals,
            Sounds = sounds,
            Listener = () => new(fixture.Camera.SourcePosition >> 12),
            Environment = environment
        });
        using var bulletBinding = new GodotGrenadeWorldListBinding(fixture.Registry, services, visuals.Explosions);
        int initialBullets = fixture.Registry.SourceList(ActorUpdateGroup.Bullet).Count;
        int checks = 0;
        GodotScriptActor[] baddies = fixture.Directory.SourceBodies.Select(body => body.Native).ToArray();
        VerifyBodyOrder(services.BlastHead(GrenadeDamageList.PrimaryTargets, GrenadeTargetList.Baddies), baddies);
        GodotScriptActor[] environmentalBodies = fixture.Registry.SourceActors(ActorUpdateGroup.Environment).ToArray();
        Assert(environmentalBodies.Any(actor => actor is GodotManipulationActor),
            "Blast ENV-body proof requires the still-live actual original manipulation actor.");
        VerifyBodyOrder(services.BlastHead(GrenadeDamageList.EnvironmentalBodies, GrenadeTargetList.Baddies), environmentalBodies);
        IProjectileBlastBody player = services.BlastHead(GrenadeDamageList.Player, GrenadeTargetList.Baddies)!;
        Assert(player.Position == fixture.Player.Controller.Position && player.NextBody is null &&
            services.BlastHead(GrenadeDamageList.PrimaryTargets, GrenadeTargetList.Player)!.Position == player.Position,
            "Player-target and explicit player passes use the actual shared native player body.");
        short before = thug.State.Health;
        short hostageHealth = hostage.State.Health;
        ProjectileBlastDamage.GiveScaledDamageToObjects(thug.Position, 1, 1, 100, 10, 29,
            services.BlastHead(GrenadeDamageList.PrimaryTargets, GrenadeTargetList.Baddies));
        Assert(thug.State.Health == before - 1 && hostage.State.Health == hostageHealth,
            "Source blast adapter delivers one real health damage to the genuine initializing thug, preserving inherited hostage Hit semantics.");
        GodotManipulationActor manipulation = environmentalBodies.OfType<GodotManipulationActor>().First();
        short manipulationHealth = manipulation.Variables.Health;
        ProjectileBlastDamage.GiveScaledDamageToObjects(manipulation.SourcePosition, 1, 1, 100, 10, 29,
            services.BlastHead(GrenadeDamageList.EnvironmentalBodies, GrenadeTargetList.Baddies));
        Assert(manipulation.Variables.Health == manipulationHealth,
            "Actual CManipOb accepts blast contact through its real inherited no-damage Hit override.");

        GodotGrenadeEnvironmentItem[] items = environment.SourceItems.ToArray();
        ImportedLevelSource source = fixture.World.Sources.Single(source => source.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase));
        Assert(items.Length == source.Scene.Instances.Count && items.Length == 301 &&
            items.Select(item => item.InstanceIndex).SequenceEqual(Enumerable.Range(0, items.Length)),
            "Genuine L1A3 EnviroList retains all301 source records in ascending loader order and excludes the23 OBJ prototypes.");
        foreach (GodotGrenadeEnvironmentItem item in items)
        {
            GodotSwitchEnvironmentItem? claimed = fixture.Environment.Items.SingleOrDefault(native => native.SourceInstanceIndex == item.InstanceIndex);
            object identity = claimed?.State.Identity ?? fixture.Session.ResolveStaticItem(source, item.InstanceIndex);
            Assert(ReferenceEquals(item.Identity, identity), "Blast ENV identity is the exact canonical shared static or claimed switch item.");
            item.HitEnvironment(10);
        }
        Assert(ReferenceEquals(environment.Head, items[0]) && ReferenceEquals(items[0].NextEnvironment, items[1]) && items[^1].NextEnvironment is null,
            "Source ENV blast views preserve actual head/next/tail traversal.");
        byte objectRegion = services.Configuration.ObjectFileRegion();
        byte defaultRegion = services.Configuration.DefaultGrenadeRegion();
        Assert(services.Configuration.ResolveRegion(objectRegion).AssetPath == "data/l1a3_o.psx" &&
            ReferenceEquals(services.Configuration.ResolveRegion(objectRegion).Scene, fixture.Registry.ObjectLibrary().Scene) &&
            services.Configuration.ResolveRegion(defaultRegion).AssetPath == "data/items.psx",
            "Source grenade region bytes resolve the real currently selected OBJ bank and original default items bank.");

        // The source L1A3 bank lacks76921FB1. Verify modern bounded constructor cleanup;
        // do not silently render model0 after the original missing-checksum diagnostic.
        GodotGrenadeProjectile? attached = null, detached = null;
        services.ProjectileAttached += projectile => attached = projectile;
        services.ProjectileDetached += projectile => detached = projectile;
        int glows = visuals.Glows.Count;
        try
        {
            services.Spawn(thug, new(thug.Position, fixture.Player.Controller.Position, 40, 10, 512,
                ThugProjectileTargetList.Baddies, 0x76921FB1, 0, 0));
            throw new InvalidDataException("A missing original grenade model was silently substituted.");
        }
        catch (KeyNotFoundException)
        {
            Assert(attached is not null && ReferenceEquals(attached, detached) && !GodotObject.IsInstanceValid(attached) &&
                services.SourceProjectileOrder.Count == 0 && visuals.Glows.Count == glows,
                "Failed real model lookup unwinds the actual constructor-attached bullet identity without orphaned glow or native node.");
            Assert(bulletBinding.Count == 0 && fixture.Registry.SourceList(ActorUpdateGroup.Bullet).Count == initialBullets,
                "Failed source model construction leaves the scene's actual canonical mixed BulletList unchanged.");
        }
        GD.Print($"Passed {checks} native grenade scene checks: actual Baddy/EnvBody/Player lists and Hit,301 canonical G-region ENV identities, null-face policy, real source bank regions and missing-model constructor cleanup.");

        void VerifyBodyOrder(IProjectileBlastBody? head, IReadOnlyList<GodotScriptActor> expected)
        {
            foreach (GodotScriptActor actor in expected)
            {
                Assert(head is not null && head.Position == actor.SourcePosition,
                    "Blast body traversal preserves the actual current source-family head order and live fixed position.");
                head = head!.NextBody;
            }
            Assert(head is null, "Blast body traversal has no invented or duplicate actor tail.");
        }
        void Assert(bool condition, string evidence)
        {
            if (!condition) throw new InvalidDataException($"Native grenade scene: {evidence}");
            checks++;
        }
    }
}
