using Godot;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>World-space line trails emitted by the recovered thug spit behavior.</summary>
public partial class GodotThugSpitParticles : Node3D
{
    private sealed record Particle(LineParticleMotion Motion, ThugSpitParticle Appearance);
    private readonly List<Particle> particles = [];
    private readonly ImmediateMesh mesh = new();
    private readonly StandardMaterial3D opaque = Material(false);
    private readonly StandardMaterial3D transparent = Material(true);
    private bool disposed;
    public int Count => particles.Count;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;
    internal ImmediateMesh RenderMesh => mesh;

    public GodotThugSpitParticles() => TopLevel = true;

    public override void _Ready() => AddChild(new MeshInstance3D
    {
        Name = "LineTrails", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    public void Emit(ThugSpitParticle particle, uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var entry = new Particle(new(particle.Position, particle.Velocity, particle.Lifetime, particle.VerticalAcceleration, clock), particle);
        particles.Insert(0, entry); // CGLine base constructor attaches at the GLine head.
        SourceBitCreated?.Invoke(new(entry.Motion, entry.Motion.Advance,
            () => !disposed && particles.Contains(entry) && entry.Motion.Alive,
            () => Remove(entry)));
        Rebuild();
    }

    public void Advance(uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (Particle particle in particles) particle.Motion.Advance(clock);
        foreach (Particle particle in particles.Where(particle => !particle.Motion.Alive).ToArray()) Remove(particle);
        Rebuild();
    }

    private void Remove(Particle particle)
    {
        if (!particles.Remove(particle)) return;
        SourceBitRemoved?.Invoke(particle.Motion);
        Rebuild();
    }

    private void Rebuild()
    {
        if (disposed) return;
        mesh.ClearSurfaces();
        for (int blend = 0; blend < 2; blend++)
        {
            bool begun = false;
            foreach (Particle particle in particles)
            {
                bool semiTransparent = (particle.Appearance.AdditionalCodeFlags & 0x02000000) != 0;
                if (semiTransparent != (blend == 1) || particle.Motion.Head == particle.Motion.Tail) continue;
                if (!begun)
                {
                    mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, semiTransparent ? transparent : opaque);
                    begun = true;
                }
                mesh.SurfaceSetColor(Rgb(particle.Appearance.StartRgb));
                mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(particle.Motion.Head));
                mesh.SurfaceSetColor(Rgb(particle.Appearance.EndRgb));
                mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(particle.Motion.Tail));
            }
            if (begun) mesh.SurfaceEnd();
        }
    }

    private static Color Rgb(uint packed) => new((packed & 255) / 255f, ((packed >> 8) & 255) / 255f,
        ((packed >> 16) & 255) / 255f);

    private static StandardMaterial3D Material(bool semiTransparent) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = semiTransparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        AlbedoColor = new(1, 1, 1, semiTransparent ? 0.5f : 1)
    };

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (Particle particle in particles.ToArray()) SourceBitRemoved?.Invoke(particle.Motion);
        particles.Clear();
        mesh.Dispose();
        opaque.Dispose();
        transparent.Dispose();
    }
}
