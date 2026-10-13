using Godot;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual CSpark PixelList ownership and fixed-size native pixel rendering.</summary>
public partial class GodotSparkParticles : Node3D, IActorSparkEmitter
{
    private readonly List<SparkParticle> sparks = [];
    private readonly ImmediateMesh mesh = new();
    private readonly StandardMaterial3D opaque = Material(false);
    private readonly StandardMaterial3D semiTransparent = Material(true);
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private SourceParticleScheduler particles = null!;
    private Func<int, int> random = null!;
    private bool disposed;
    public SparkSettings Settings { get; private set; } = null!;
    public int LiveBitCount => particles.Count;
    public IReadOnlyList<SparkParticle> SourceOrder => sparks;
    internal ImmediateMesh PixelMesh => mesh;
    internal SourceParticleScheduler SourceScheduler => particles;
    public bool ResourcesDisposed => disposed;
    /// <summary>Actual CPixel head publication before CSpark consumes its first random value.</summary>
    public event Action<SparkParticle>? Spawned;
    internal Resource[] CaptureOwnedResources() => [mesh, opaque, semiTransparent];

    public static GodotSparkParticles Create(SourceParticleScheduler particles, SparkSettings settings, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(particles); ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(random);
        return new() { Name = "OriginalSparkPixels", TopLevel = true, particles = particles, Settings = settings, random = random };
    }

    public override void _Ready() => AddChild(new MeshInstance3D
    {
        Name = "OriginalPixelListGeometry", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    public void TrySpawnSpark(SparkSpawnRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SparkParticle? allocated = null;
        try
        {
            _ = new SparkParticle(Settings, request, random, spark =>
            {
                allocated = spark;
                sparks.Insert(0, spark);
                particles.Attach(SourceParticleList.Pixel, new(spark, _ => spark.AdvanceBits(),
                    () => !disposed && spark.Alive && sparks.Contains(spark), () => Remove(spark)));
                Spawned?.Invoke(spark);
            });
        }
        catch
        {
            if (allocated is not null) Remove(allocated);
            throw;
        }
    }

    private void Remove(SparkParticle spark)
    {
        particles.Detach(spark); sparks.Remove(spark);
    }

    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        Vector2 pixelScale = new(size.Y / 240 * .625f, size.Y / 240);
        foreach (SparkParticle spark in sparks)
        {
            if (!spark.Alive || spark.DisplayWidth == 0) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(spark.Position);
            float depth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            if (depth <= 0) continue;
            float inverseDepth = 1 / (depth * 100);
            float restored = (inverseDepth - 7.0710726f) + 7.0710678f;
            if (restored <= 0) continue;
            float renderDepth = 1 / restored / 100;
            Vector2 origin = camera.UnprojectPosition(center), extent = pixelScale * spark.DisplayWidth;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, (spark.Code & 2) == 0 ? opaque : semiTransparent);
            mesh.SurfaceSetColor(new(brightness[spark.Red] / 255f, brightness[spark.Green] / 255f, brightness[spark.Blue] / 255f));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
                mesh.SurfaceAddVertex(camera.ProjectPosition(origin + new Vector2(
                    (corner & 1) != 0 ? extent.X : 0, (corner & 2) != 0 ? extent.Y : 0), renderDepth));
            mesh.SurfaceEnd();
        }
    }

    private static StandardMaterial3D Material(bool transparent) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        BlendMode = transparent ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        AlbedoColor = new(1, 1, 1, transparent ? 128 / 255f : 1)
    };

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (SparkParticle spark in sparks.ToArray()) particles.Detach(spark);
        sparks.Clear();
        Spawned = null;
        mesh.Dispose(); opaque.Dispose(); semiTransparent.Dispose();
    }
}
