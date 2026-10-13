using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Effects;

/// <summary>Scene ownership and source bit scheduling for actual thug visual effects.</summary>
public partial class GodotThugVisualEffects : Node3D
{
    private readonly Dictionary<GodotThugActor, GodotThugAppearance> appearances = [];
    private readonly Dictionary<GodotThugActor, GodotThugBulletEffects> barrelLines = [];
    private readonly Dictionary<GodotThugActor, GodotNativeDestructionObserver> actorObservers = [];
    private DecodedTexture shadowTexture = null!;
    private GodotThugBulletHost bulletHost = null!;
    private Func<int, int> random = null!;
    private Func<uint> sourceClock = null!;
    private bool disposed;
    public GodotThugResources Resources { get; private set; } = null!;
    public GodotThugSpitParticles SpitParticles { get; private set; } = null!;
    public GodotActorSpriteRing SpriteRings { get; private set; } = null!;
    public GodotThugBulletEffects Tracers { get; private set; } = null!;
    public GodotThugImpactEffects Impacts { get; private set; } = null!;
    public GodotThugCombatRings CombatRings { get; private set; } = null!;
    public GodotGlowFlash Glows { get; private set; } = null!;
    public GodotGrenadeExplosions Explosions { get; private set; } = null!;
    public event Action<SourceParticleList, SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;
    public event Action<GodotExplosionModel>? BulletModelAttached;
    public event Action<GodotExplosionModel>? BulletModelDetached;

    public static GodotThugVisualEffects Create(PkrArchive archive, PsxLibraryRepository libraries,
        SoundChannelController sounds, Func<SoundListener> listener, Func<int, int> random, Func<uint> sourceClock)
    {
        byte[] bits = archive.Read("data/bits.psx");
        var effects = new GodotThugVisualEffects { Name = "ThugVisualEffects", random = random, sourceClock = sourceClock,
            Resources = new(archive, libraries),
            shadowTexture = libraries.Load("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828) };
        try
        {
            effects.SpitParticles = new() { Name = "SpitParticles" }; effects.AddChild(effects.SpitParticles);
            effects.SpriteRings = GodotActorSpriteRing.Create(bits); effects.AddChild(effects.SpriteRings);
            effects.Tracers = new() { Name = "BulletTracers" }; effects.AddChild(effects.Tracers);
            effects.Impacts = GodotThugImpactEffects.Create(bits); effects.AddChild(effects.Impacts);
            effects.CombatRings = GodotThugCombatRings.Create(bits); effects.AddChild(effects.CombatRings);
            effects.Glows = new() { Name = "GlowShapes" }; effects.AddChild(effects.Glows);
            effects.Explosions = GodotGrenadeExplosions.Create(archive.Read("data/expgrnd.psx")); effects.AddChild(effects.Explosions);
            effects.ConnectSourceLists();
            effects.bulletHost = new(effects.Resources, effects.Impacts, sounds, listener);
            return effects;
        }
        catch { effects.Free(); throw; }
    }

    private void ConnectSourceLists()
    {
        SpitParticles.SourceBitCreated += bit => SourceBitCreated?.Invoke(SourceParticleList.GLine, bit);
        SpitParticles.SourceBitRemoved += ForwardRemoved;
        SpriteRings.SourceBitCreated += bit => SourceBitCreated?.Invoke(SourceParticleList.Flat, bit);
        SpriteRings.SourceBitRemoved += ForwardRemoved;
        ConnectBulletEffects(Tracers);
        Impacts.SourceBitCreated += ForwardCreated;
        Impacts.SourceBitRemoved += ForwardRemoved;
        CombatRings.SourceBitCreated += bit => SourceBitCreated?.Invoke(SourceParticleList.Flat, bit);
        Glows.SourceBitCreated += bit => SourceBitCreated?.Invoke(SourceParticleList.Glow, bit);
        Glows.SourceBitRemoved += ForwardRemoved;
        Explosions.SourceBitCreated += bit => SourceBitCreated?.Invoke(SourceParticleList.NonRendered, bit);
        Explosions.SourceBitRemoved += ForwardRemoved;
        Explosions.ModelAttached += model => BulletModelAttached?.Invoke(model);
        Explosions.ModelDetached += model => BulletModelDetached?.Invoke(model);
    }

    private void ConnectBulletEffects(GodotThugBulletEffects effects)
    {
        effects.SourceBitCreated += ForwardCreated;
        effects.SourceBitRemoved += ForwardRemoved;
    }
    private void ForwardCreated(SourceParticleList list, SourceParticleRegistration bit) => SourceBitCreated?.Invoke(list, bit);
    private void ForwardRemoved(object identity) => SourceBitRemoved?.Invoke(identity);

    public GodotActorShadow CreateProjectileShadow() => GodotActorShadow.Create(shadowTexture);

    public void CreateShadow(GodotThugActor actor)
    {
        CheckAlive();
        if (appearances.ContainsKey(actor)) throw new InvalidOperationException("The actor already owns its source shadow.");
        appearances.Add(actor, GodotThugAppearance.Create(actor, shadowTexture));
        var observer = new GodotNativeDestructionObserver { Name = "SourceThugAppearanceDestructor", Destroyed = () => ReleaseActor(actor) };
        actorObservers.Add(actor, observer);
        try { actor.AddChild(observer); }
        catch { ReleaseActor(actor); throw; }
    }
    public GodotThugAppearance AppearanceFor(GodotThugActor actor) => appearances[actor];
    public void KillShadow(GodotThugActor actor) => appearances[actor].KillShadow();
    public void UpdateAppearanceAndShadow(GodotThugActor actor) => appearances[actor].Synchronize();

    public void ClearBarrelLine(GodotThugActor actor)
    {
        if (barrelLines.TryGetValue(actor, out GodotThugBulletEffects? effect)) effect.DeleteBarrelLine();
    }
    public void SetBarrelLine(GodotThugActor actor, FixedVector3 start, FixedVector3 end)
    {
        CheckAlive();
        if (!barrelLines.TryGetValue(actor, out GodotThugBulletEffects? effect))
        {
            effect = new() { Name = $"BarrelLine{actor.GetInstanceId()}" };
            ConnectBulletEffects(effect);
            AddChild(effect);
            barrelLines.Add(actor, effect);
        }
        effect.SetBarrelLine(start, end);
    }
    public void DrawBarrelFlash(GodotThugActor actor, ThugShotLine line, byte red, byte green, byte blue)
    {
        Glows.Emit(new GlowFlashParameters(line.Start, 5, red, green, blue, 32,
            0, 0, 0, 0, 50, 20, 1, 20, 10, 40, 20, 10, 1), random);
        SetBarrelLine(actor, line.Start, line.End);
    }
    public void DrawGlowFlash(FixedVector3 position, IReadOnlyList<int> arguments) =>
        Glows.Emit(GlowFlashParameters.FromSourceArguments(position, arguments), random);
    public void CreateCombatImpact(GodotThugActor actor, FixedVector3 position, int flags) =>
        CombatRings.Emit(actor.Type, position, sourceClock(), random);

    public void DrawTracer(GodotThugActor actor, ThugShotLine line, object? attachment, byte red, byte green, byte blue)
    {
        ThugRicochetContact? contact = null;
        if (actor.LastLaserHit is { } hit)
        {
            FixedVector3 normal = hit.Triangle.FixedNormal;
            if (hit.Collider is ImportedCollisionBody body)
                normal = PsxCoordinates.RotateOriginalNormal(normal, body.SourceRotation);
            contact = new(line.Start, line.End, hit.Position, normal);
        }
        ThugBulletImpact.Create(line.Start, line.End, attachment, contact, bulletHost, random, red, green, blue);
        Tracers.Emit(line.Start, line.End, red, green, blue, random);
    }

    public void AdvanceBulletModels() => Explosions.AdvanceModels();

    public void AdvanceBits(uint clock)
    {
        CheckAlive();
        // Tracer controllers/carriers are nonrendered bits. Radial flashes
        // choose new random radii later, in the glow-list phase.
        Tracers.Advance();
        Explosions.AdvanceCarriers();
        CombatRings.Advance(clock);
        SpriteRings.Advance();
        Impacts.Advance();
        Glows.Advance();
        SpitParticles.Advance(clock);
    }

    private void ReleaseActor(GodotThugActor actor)
    {
        if (actorObservers.Remove(actor, out GodotNativeDestructionObserver? observer))
        {
            bool inNativeDestruction = observer.Destroyed is null;
            observer.Destroyed = null;
            if (!inNativeDestruction && GodotObject.IsInstanceValid(observer)) observer.Free();
        }
        if (appearances.Remove(actor, out GodotThugAppearance? appearance)) appearance.Dispose();
        if (barrelLines.Remove(actor, out GodotThugBulletEffects? line) && GodotObject.IsInstanceValid(line)) line.Free();
    }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(disposed, this);
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (GodotThugActor actor in appearances.Keys.Union(barrelLines.Keys).ToArray()) ReleaseActor(actor);
    }
}
