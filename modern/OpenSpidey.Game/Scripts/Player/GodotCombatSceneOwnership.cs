using OpenSpidey.Core.Effects;
using Godot;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Source body identities and bit lists exist before CPlayer and survive until scene destruction.</summary>
public sealed class GodotCombatSceneOwnership : IDisposable
{
    private bool disposed;
    private GodotActorWorldQuery? world;
    public GodotActorRegistry Registry { get; }
    public GodotPlayerMeleeDirectory Directory { get; } = new();
    public GodotActorSessionDirectory Actors { get; }
    public SourceParticleScheduler Particles { get; } = new();
    public GodotActorWorldQuery World
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return world ??= new(Registry.GetParent() as Node3D
                ?? throw new InvalidOperationException("Source world queries require the registry in its actual scene."), Registry.Triggers);
        }
    }

    public GodotCombatSceneOwnership(GodotActorRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Directory.Bind(registry);
        try { Actors = new(registry, Directory, new GodotActorSessionPlatforms()); }
        catch { Directory.Dispose(); throw; }
    }

    public void RequireRegistry(GodotActorRegistry registry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ReferenceEquals(Registry, registry))
            throw new ArgumentException("Combat scene ownership cannot be transferred to a different source registry.", nameof(registry));
    }

    /// <summary>Release after genuine actor/effect destruction, never on suspension or reparenting.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Actors.Dispose();
        Directory.Dispose();
        Particles.Clear();
    }
}
