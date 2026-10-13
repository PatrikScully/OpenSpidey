using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Live posed web actions, source body contacts and effects sharing the scene's bit lists.</summary>
public partial class GodotPlayerWebRuntime : Node3D, IPlayerWebFrameHost
{
    private GodotPlayer player = null!;
    private byte[] originalBits = null!;
    private Action<uint> advanceOwners = null!;
    public GodotPlayerFireWeb Actions { get; private set; } = null!;
    public GodotWebStrands Strands { get; private set; } = null!;
    public GodotActorWebContacts Contacts { get; private set; } = null!;
    public GodotImpactWebProjectiles Projectiles { get; private set; } = null!;
    public GodotFlamingImpactWebs FlamingProjectiles { get; private set; } = null!;
    public GodotFireEmbers Embers { get; private set; } = null!;
    public GodotPlayerWebTug Tug { get; private set; } = null!;
    public GodotPlayerWebAttack? Attack { get; private set; }
    public GodotPlayerLookaroundAim? Lookaround { get; private set; }
    public GodotWebDomes? Domes { get; private set; }
    private GodotPlayerWebDomeAction? domeAction;

    public static GodotPlayerWebRuntime Create(PkrArchive archive, PsxLibraryRepository repository,
        GodotPlayer player, GodotPlayerCombatRuntime combat, PlayerFollowCamera camera,
        Func<uint> sourceClock, Func<int, int> random,
        Func<ushort, FixedVector3, int, int> playPositionSound)
    {
        ArgumentNullException.ThrowIfNull(sourceClock);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(playPositionSound);
        if (player.Melee is null || player.Inventory is null)
            throw new InvalidOperationException("Live web actions require the shared player melee and inventory owners.");
        var runtime = new GodotPlayerWebRuntime { Name = "PlayerWebRuntime", player = player };
        SourceParticleScheduler particles = combat.Particles;
        try
        {
            byte[] bits = archive.Read("data/bits.psx");
            runtime.originalBits = bits;
            runtime.Embers = GodotFireEmbers.Create(bits);
            runtime.AddChild(runtime.Embers);
            runtime.Embers.SourceBitCreated += bit => particles.Attach(SourceParticleList.Flat, bit);
            void CreateFirePulse(FixedVector3 position, int size, Func<int, int> sourceRandom) =>
                runtime.Embers.Emit(position, size, sourceRandom);
            var fragments = new GodotWebFragments();
            runtime.AddChild(fragments);
            var rings = GodotWebImpactRings.Create(bits);
            runtime.AddChild(rings);
            var surfaceSplats = GodotWebSurfaceSplats.Create(archive.Read("data/shell_dc.psx"));
            runtime.AddChild(surfaceSplats);
            var knottedSplats = GodotKnottedWebSplats.Create(bits);
            runtime.AddChild(knottedSplats);
            fragments.SourceBitCreated += bit => particles.Attach(SourceParticleList.GLine, bit);
            fragments.SourceBitRemoved += identity => particles.Detach(identity);
            rings.SourceBitCreated += bit => particles.Attach(SourceParticleList.Flat, bit);
            surfaceSplats.SourceBitCreated += bit => particles.Attach(SourceParticleList.Quad, bit);
            knottedSplats.SourceBitCreated += bit => particles.Attach(SourceParticleList.Quad, bit);

            var impact = new GodotImpactWebNativeHost(player, combat.Directory, combat.Actors,
                combat.Session, combat.World, combat.KillQuips, rings, surfaceSplats, fragments,
                () => combat.ItemShatter.LowMemory);
            runtime.Projectiles = GodotImpactWebProjectiles.Create(bits, impact, random);
            runtime.AddChild(runtime.Projectiles);
            runtime.Projectiles.SourceBitCreated += bit => particles.Attach(SourceParticleList.Flat, bit);
            runtime.FlamingProjectiles = GodotFlamingImpactWebs.Create(archive.Read("data/firebits.psx"),
                new GodotFlamingWebCollisionHost(impact, combat.World), random);
            runtime.AddChild(runtime.FlamingProjectiles);
            runtime.FlamingProjectiles.SourceFlatBitCreated += bit => particles.Attach(SourceParticleList.Flat, bit);
            runtime.FlamingProjectiles.SourcePixelBitCreated += bit => particles.Attach(SourceParticleList.Pixel, bit);

            var resources = new GodotThugResources(archive, repository);
            runtime.Contacts = GodotActorWebContacts.Create(bits, combat.Directory,
                actor => resources.CollisionBoxes(actor.State.ModelName!), fragments, random,
                position => GodotBouncingRocks.GetGroundY(combat.World, position),
                (position, size) => CreateFirePulse(position, size, random), combat.KillQuips.SetFirstContactDetails);
            runtime.AddChild(runtime.Contacts);
            runtime.Contacts.Emitted += bit => particles.Attach(SourceParticleList.NonRendered, bit);
            runtime.Contacts.LineEmitted += bit => particles.Attach(SourceParticleList.GPolyLine, bit);
            runtime.Contacts.QuadEmitted += bit => particles.Attach(SourceParticleList.Quad, bit);
            runtime.Contacts.Removed += identity => particles.Detach(identity);
            runtime.Contacts.LineRemoved += identity => particles.Detach(identity);
            runtime.Contacts.QuadRemoved += identity => particles.Detach(identity);

            runtime.Strands = GodotWebStrands.Create(bits);
            runtime.AddChild(runtime.Strands);
            runtime.Strands.ConfigureSourcePhase(sourceClock, random, CreateFirePulse);
            runtime.Strands.PolylineEmitted += bit => particles.Attach(SourceParticleList.GPolyLine, bit);
            runtime.Strands.KnotEmitted += bit => particles.Attach(SourceParticleList.Flat, bit);
            runtime.Strands.PolylineRemoved += identity => particles.Detach(identity);
            runtime.Strands.KnotRemoved += identity => particles.Detach(identity);
            runtime.Actions = new(player, combat.Directory, combat.Session, combat.World,
                runtime.Projectiles, runtime.Strands, runtime.Contacts, combat.Impacts.Lines, new()
                {
                    Clock = sourceClock,
                    CameraPositionWholeUnits = () => camera.SourcePosition >> 12,
                    Random = random,
                    ActuatorTime = combat.ControllerFeedback.RemainingTicks,
                    PlayPositionSound = playPositionSound,
                    CreateFireImpactWeb = (position, angles, damage) => runtime.FlamingProjectiles.Emit(position, angles, damage, sourceClock()),
                    EmitKnottedSplat = knottedSplats.Emit
                });
            runtime.Tug = new(player, runtime.Actions, runtime.Strands, new()
            {
                Clock = sourceClock,
                CameraPositionWholeUnits = () => camera.SourcePosition >> 12,
                Random = random,
                GroundY = position => GodotBouncingRocks.GetGroundY(combat.World, position),
                EmitFirePulse = CreateFirePulse,
                SetFirstContactDetails = combat.KillQuips.SetFirstContactDetails,
                EmitSnapGlow = combat.Glows.Emit
            });
            runtime.advanceOwners = _ => runtime.Actions.AdvanceOwnersBeforeInput();
            player.Controller.BeforePlayerInput += runtime.advanceOwners;
            player.Combat.WebToBlob = runtime.Actions.SwitchCurrentWebToBlob;
            return runtime;
        }
        catch
        {
            if (runtime.advanceOwners is not null) player.Controller.BeforePlayerInput -= runtime.advanceOwners;
            runtime.Free();
            throw;
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (player is null) return;
        DetachPlayerEffectPhases();
        if (advanceOwners is not null) player.Controller.BeforePlayerInput -= advanceOwners;
        Swing?.Dispose();
        if (player.Melee is { } melee) melee.BeforeHeldObjectPlacement -= FinishSwingAttachments;
    }

    public void UpdateAttach() => Actions.UpdateAttach();
    public void UpdateThrow() => Tug.UpdateThrow();
    public bool CheckWebShot() => RequireAttack().CheckWebShot();
    public void UpdateWindup() => RequireAttack().UpdateWindup();
    public void UpdateFiring() => RequireAttack().UpdateFiring();
    public void UpdateDome(ushort completedAnimation) => (domeAction ??
        throw new InvalidOperationException("Dome release requires the original dome model and actor owners.")).Update(completedAnimation);

    public void ConfigureDomes(GodotActorRegistry registry, GodotPlayerCombatRuntime combat,
        PsxLibraryRepository repository, PlayerFollowCamera camera, Func<uint> sourceClock, Func<int, int> random)
    {
        if (Attack is not null || Domes is not null)
            throw new InvalidOperationException("The player already owns its web attack controller.");
        var fragments = GodotItemBurstFragments.Create(
            position => GodotBouncingRocks.GetGroundY(combat.World, position), random);
        AddChild(fragments);
        fragments.SourceBitCreated += bit => combat.Particles.Attach(SourceParticleList.Quad, bit);
        Domes = GodotWebDomes.Create(new()
        {
            Player = player, Registry = registry, Baddies = combat.Directory, Actors = combat.Actors,
            Repository = repository, Embers = Embers, Fragments = fragments, Glows = combat.Glows,
            Camera = camera, Clock = sourceClock, Random = random, LowMemory = () => combat.ItemShatter.LowMemory
        });
        AddChild(Domes);
        Domes.SourceBitCreated += bit => combat.Particles.Attach(SourceParticleList.NonRendered, bit);
        ConfigureAttack(Domes.Spawn);
        domeAction = new(player, RequireAttack(), Domes);
    }

    public void ConfigureAttack(Func<byte, PlayerKickHandle> createDome)
    {
        ArgumentNullException.ThrowIfNull(createDome);
        if (Attack is not null) throw new InvalidOperationException("The player already owns its web attack controller.");
        Attack = new(player, Actions, createDome);
        Lookaround = new(player, Attack);
    }

    private GodotPlayerWebAttack RequireAttack() => Attack ??
        throw new InvalidOperationException("Web attack input requires the genuine dome factory before activation.");
}
