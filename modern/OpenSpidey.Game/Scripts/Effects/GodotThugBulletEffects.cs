using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Original textureless thug ribbons and persistent barrel line; explicit bit-update cadence.</summary>
public partial class GodotThugBulletEffects : Node3D
{
    private readonly List<ThugBulletTracerState> tracers = [];
    private readonly ImmediateMesh mesh = new();
    private readonly StandardMaterial3D material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        AlbedoColor = new(1, 1, 1, 128 / 255f)
    };
    private bool disposed;
    private object? barrelIdentity;
    public ThugBarrelLineState BarrelLine { get; } = new();
    public int Count => tracers.Count;
    public event Action<SourceParticleList, SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;
    public ushort NearDepth { get; set; } = 200;
    public ushort FarDepth { get; set; } = 8048;
    internal IReadOnlyList<ThugBulletTracerState> Tracers => tracers;
    internal ImmediateMesh RenderMesh => mesh;
    public GodotThugBulletEffects() => TopLevel = true;

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "SourceThugBulletRibbons", Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }

    // Original collision/ping/ricochet callbacks must finish before invoking this geometry constructor.
    public void Emit(FixedVector3 start, FixedVector3 end, byte red, byte green, byte blue, Func<int, int> random)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GodotThugBulletEffects));
        var tracer = new ThugBulletTracerState(start, end, red, green, blue, random);
        tracers.Insert(0, tracer);
        SourceBitCreated?.Invoke(SourceParticleList.NonRendered, new(tracer, _ => tracer.Advance(),
            () => !disposed && tracers.Contains(tracer) && tracer.Alive, () => RemoveTracer(tracer)));
        AttachRibbon(tracer, tracer.ColoredPoints);
        AttachRibbon(tracer, tracer.WhitePoints);
        Rebuild();
    }

    private void AttachRibbon(ThugBulletTracerState tracer, IReadOnlyList<ThugRibbonPoint> ribbon)
    {
        // CGouraudRibbon is a protected CSpecialDisplay. The controller moves it;
        // its inherited CBit::Move does nothing, and only its owner deletes it.
        SourceBitCreated?.Invoke(SourceParticleList.Special, new(ribbon, _ => { },
            () => !disposed && tracers.Contains(tracer), () => RemoveTracer(tracer)));
    }

    private void RemoveTracer(ThugBulletTracerState tracer)
    {
        if (!tracers.Remove(tracer)) return;
        SourceBitRemoved?.Invoke(tracer.ColoredPoints);
        SourceBitRemoved?.Invoke(tracer.WhitePoints);
        SourceBitRemoved?.Invoke(tracer);
        Rebuild();
    }

    public void SetBarrelLine(FixedVector3 start, FixedVector3 end)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GodotThugBulletEffects));
        bool create = !BarrelLine.Exists;
        BarrelLine.SetEndpoints(start, end);
        if (create)
        {
            object identity = barrelIdentity = new object();
            SourceBitCreated?.Invoke(SourceParticleList.GPolyLine, new(identity, _ => { },
                () => !disposed && ReferenceEquals(barrelIdentity, identity) && BarrelLine.Exists,
                () => DeleteBarrelLine()));
        }
        Rebuild();
    }

    public void DeleteBarrelLine()
    {
        BarrelLine.Clear();
        if (barrelIdentity is { } identity)
        {
            barrelIdentity = null;
            SourceBitRemoved?.Invoke(identity);
        }
        Rebuild();
    }

    public void Advance()
    {
        if (disposed) throw new ObjectDisposedException(nameof(GodotThugBulletEffects));
        foreach (ThugBulletTracerState tracer in tracers) tracer.Advance();
        foreach (ThugBulletTracerState tracer in tracers.Where(tracer => !tracer.Alive).ToArray()) RemoveTracer(tracer);
        Rebuild();
    }

    public override void _Process(double delta) => Rebuild();

    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 scale = new(viewport.Y / 240 * 0.625f, viewport.Y / 240);
        // Source constructor attaches colored ribbon first then white at the list head.
        foreach (ThugBulletTracerState tracer in tracers)
        {
            DrawRibbon(tracer.WhitePoints);
            DrawRibbon(tracer.ColoredPoints);
        }
        if (BarrelLine.Exists)
        {
            Vector3 start = PsxCoordinates.FixedWorldPosition(BarrelLine.Start), end = PsxCoordinates.FixedWorldPosition(BarrelLine.End);
            if (-camera.GlobalBasis.Z.Dot(start - camera.GlobalPosition) > 0 && -camera.GlobalBasis.Z.Dot(end - camera.GlobalPosition) > 0)
            {
                mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
                mesh.SurfaceSetColor(new(0, 0, 80 / 255f)); mesh.SurfaceAddVertex(start);
                mesh.SurfaceSetColor(new(0, 0, (byte)(BarrelLine.EndCodeRgb >> 16) / 255f)); mesh.SurfaceAddVertex(end);
                mesh.SurfaceEnd();
            }
        }

        void DrawRibbon(IReadOnlyList<ThugRibbonPoint> points)
        {
            var projected = new ThugRibbonScreenPoint[5];
            var logical = new Vector2[5];
            var worldDepths = new float[5];
            var normals = new ThugRibbonScreenNormal[5];
            for (int i = 0; i < 5; i++)
            {
                Vector3 center = PsxCoordinates.FixedWorldPosition(points[i].Position);
                worldDepths[i] = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
                logical[i] = camera.UnprojectPosition(center) / scale;
                int depth = SourceDepth(camera, points[i].Position);
                projected[i] = new(ThugRibbonProjection.Pack((int)logical[i].X, (int)logical[i].Y), depth, points[i].Width,
                    depth < NearDepth || depth > FarDepth || worldDepths[i] <= 0);
            }
            var previous = ThugRibbonProjection.Normal(projected[0], projected[1]);
            normals[0] = previous;
            for (int i = 1; i < 4; i++)
            {
                var next = ThugRibbonProjection.Normal(projected[i], projected[i + 1]);
                normals[i] = new(unchecked(previous.X + next.X) / 2, unchecked(previous.Y + next.Y) / 2);
                previous = next;
            }
            normals[4] = previous;
            Vector2 previousEdge = Edge(0);
            for (int i = 0; i < 4; i++)
            {
                // Source continue deliberately retains the prior corner through degenerate segments.
                if (normals[i] == default || normals[i + 1] == default) continue;
                Vector2 nextEdge = Edge(i + 1);
                mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
                DrawQuad([nextEdge, previousEdge, logical[i + 1], logical[i]],
                    [worldDepths[i + 1], worldDepths[i], worldDepths[i + 1], worldDepths[i]],
                    [Colors.Black, Colors.Black, Rgb(points[i + 1]), Rgb(points[i])]);
                mesh.SurfaceEnd();
                previousEdge = nextEdge;
            }
            Vector2 Edge(int index)
            {
                if (projected[index].Clipped) return logical[index];
                int packed = ThugRibbonProjection.Edge(projected[index], normals[index]);
                return logical[index] + new Vector2(unchecked((short)packed - (short)projected[index].PackedPosition),
                    unchecked((packed >> 16) - (projected[index].PackedPosition >> 16)));
            }
        }

        void DrawQuad(Vector2[] corners, float[] depths, Color[] colors)
        {
            // PC draws the same fade quad twice with swapped pairs; retain both additive passes.
            foreach (int index in new[] { 0, 1, 2, 1, 3, 2, 1, 0, 3, 0, 2, 3 })
            {
                mesh.SurfaceSetColor(colors[index]);
                mesh.SurfaceAddVertex(camera.ProjectPosition(corners[index] * scale, depths[index]));
            }
        }
    }

    private static Color Rgb(ThugRibbonPoint point) => new(point.Red / 255f, point.Green / 255f, point.Blue / 255f);

    private static int SourceDepth(Camera3D camera, FixedVector3 position)
    {
        FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
        var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20, rotation.M01, rotation.M11, rotation.M21,
            rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
        return FixedRotation.Transform(view, (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (ThugBulletTracerState tracer in tracers.ToArray()) RemoveTracer(tracer);
        DeleteBarrelLine();
        mesh.Dispose(); material.Dispose();
    }
}
