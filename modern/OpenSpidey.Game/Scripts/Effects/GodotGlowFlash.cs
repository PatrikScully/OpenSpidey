using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Source DisplayGlowList starburst caps and graded fringe strips, projected in the original viewport.</summary>
public partial class GodotGlowFlash : Node3D
{
    internal readonly record struct ProjectedVertex(Vector2 LogicalPosition, Color Colour);
    private readonly List<IGlowShape> flashes = [];
    private readonly ImmediateMesh mesh = new();
    private readonly StandardMaterial3D material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        AlbedoColor = new(1, 1, 1, 160 / 255f)
    };
    private readonly byte[] brightness = SourceBrightness();
    private bool disposed;
    public int Count => flashes.Count;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;
    internal IReadOnlyList<IGlowShape> SourceGlowOrder => flashes;
    /// <summary>Original half-world-unit glow focus; initial PC data is zero.</summary>
    public Vector3 SourceFocusPoint { get; set; }
    /// <summary>Original float at PC VA547E3C defaults to100; it changes depth bias, not section sizing.</summary>
    public float SourceNearThreshold { get; set; } = 100;
    internal ImmediateMesh RenderMesh => mesh;
    internal StandardMaterial3D RenderMaterial => material;
    internal bool ResourcesDisposed => disposed;

    public GodotGlowFlash() => TopLevel = true;
    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "SourceGlowTrianglesAndFringes", Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }

    public void Emit(IGlowShape state)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(state);
        if (flashes.Contains(state)) throw new InvalidOperationException("The source glow is already attached.");
        flashes.Insert(0, state); // CGlow::AttachTo adds at the head.
        SourceBitCreated?.Invoke(new(state, _ => AdvanceCapturedGlow(state),
            () => !disposed && flashes.Contains(state) && state is not GlowFlashState { IsDead: true },
            () => Remove(state)));
        Rebuild();
    }

    public void Remove(IGlowShape state)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (flashes.Remove(state)) SourceBitRemoved?.Invoke(state);
        Rebuild();
    }

    public GlowFlashState Emit(GlowFlashParameters parameters, Func<int, int> random)
    {
        var state = new GlowFlashState(parameters, random);
        Emit(state);
        return state;
    }

    public void Advance()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (IGlowShape state in flashes.ToArray()) AdvanceCapturedGlow(state);
        foreach (IGlowShape state in flashes.Where(state => state is GlowFlashState { IsDead: true }).ToArray()) Remove(state);
        Rebuild();
    }

    private void AdvanceCapturedGlow(IGlowShape state)
    {
        if (!disposed && flashes.Contains(state) && state is GlowFlashState { IsDead: false } flash) flash.Move();
    }

    public override void _Process(double delta) => Rebuild();

    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        foreach (IGlowShape state in flashes)
        {
            if (state is GlowFlashState { IsDead: true }) continue;
            Vector3 world = PsxCoordinates.FixedWorldPosition(state.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(world - camera.GlobalPosition);
            if (worldDepth <= 0) continue;
            int depth = SourceDepth(camera, state.Position);
            Vector2 logicalCentre = ToLogical(camera.UnprojectPosition(world), viewport);
            IReadOnlyList<ProjectedVertex> vertices = Project(state, depth, logicalCentre, brightness);
            if (vertices.Count == 0) continue;
            // Original invZ growFactor is used only for depth, while cap/fringe
            // geometry stays in the projected logical coordinates above.
            Vector3 half = new Vector3(state.Position.X, state.Position.Y, state.Position.Z) / 8192f;
            float distance = half.DistanceTo(SourceFocusPoint);
            float grow = distance - SourceNearThreshold >= 10 ? SourceNearThreshold / distance + 1 : 1;
            float planeDepth = worldDepth / (grow * 1.03f);
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            foreach (ProjectedVertex vertex in vertices)
            {
                mesh.SurfaceSetColor(vertex.Colour);
                mesh.SurfaceAddVertex(camera.ProjectPosition(ToViewport(vertex.LogicalPosition, viewport), planeDepth));
            }
            mesh.SurfaceEnd();
        }
    }

    internal static IReadOnlyList<ProjectedVertex> Project(IGlowShape state, int depth, Vector2 centre,
        byte[]? brightnessTable = null)
    {
        var result = new List<ProjectedVertex>();
        if (depth is < 5 or > 20000) return result;
        int cx = Math.Clamp(GameWord.TruncateToInt32(centre.X), -1024, 1023);
        int cy = Math.Clamp(GameWord.TruncateToInt32(centre.Y), -1024, 1023);
        if (cx is -1024 or 1023 || cy is -1024 or 1023) return result;
        int fade = depth < 700 ? 255 * depth / 700 + 1 : 0;
        byte[] table = brightnessTable ?? SourceBrightness();
        int count = state.Sections.Count;
        Vector2[] previous = Points(count - 1, state.Angle);
        int angle = state.Angle + state.StepAngle;
        for (int i = 0; i < count; i++, angle += state.StepAngle)
        {
            if (i == count - 1) angle = state.Angle;
            Vector2[] current = Points(i, angle);
            if (((state.Mask >> i) & 1) != 0)
            {
                if (!state.SkipTriangles)
                {
                    Color edge = Colour(state.Sections[i].PadBgr);
                    Add(new(cx, cy), Colour(state.CentreCodeBgr)); Add(previous[0], edge); Add(current[0], edge);
                    // The source submits its second degenerate cap triangle too.
                    Add(new(cx, cy), Colour(state.CentreCodeBgr)); Add(current[0], edge); Add(current[0], edge);
                }
                for (int row = 0; row < state.FringeCount; row++)
                {
                    Color outer = Colour(state.Fringes[row * count + i].CodeBgr);
                    Color inner = Colour(row == 0 ? state.Sections[i].PadBgr : state.Fringes[(row - 1) * count + i].CodeBgr);
                    // Original outerA,outerB,innerA,innerB POLY_FT4 corner order.
                    Add(previous[row + 1], outer); Add(current[row + 1], outer); Add(previous[row], inner);
                    // PCGfx_DrawQPoly3D's second triangle is (0,3,2), distinct
                    // from the indexed model and 2D quad submission templates.
                    Add(previous[row + 1], outer); Add(current[row], inner); Add(previous[row], inner);
                }
            }
            previous = current;
        }
        return result;

        Vector2[] Points(int section, int heading)
        {
            (short sine, short cosine) = GameAngle.Heading(heading);
            int sin = 400 * sine / 256;
            int radius = unchecked((int)state.Sections[section].Radius);
            var points = new Vector2[state.FringeCount + 1];
            for (int row = 0; row < points.Length; row++)
            {
                if (row != 0) radius = unchecked(radius + (int)state.Fringes[(row - 1) * count + section].Width);
                int projected = unchecked(400 * radius) / depth;
                points[row] = new(Math.Clamp(unchecked(cx + unchecked(sin * projected) / 4096), -100, 612),
                    Math.Clamp(unchecked(cy - unchecked(cosine * projected) / 4096), -100, 340));
            }
            return points;
        }
        Color Colour(uint packed)
        {
            return SourceColour(packed, fade, table);
        }
        void Add(Vector2 point, Color colour) => result.Add(new(point, colour));
    }

    internal static Vector2 ToViewport(Vector2 logical, Vector2 viewport)
    {
        float width = viewport.Y * (float)CameraProjection.OriginalAspect;
        return new(viewport.X / 2 + (logical.X - 256) * width / 512, logical.Y * viewport.Y / 240);
    }
    private static Vector2 ToLogical(Vector2 pixel, Vector2 viewport)
    {
        float width = viewport.Y * (float)CameraProjection.OriginalAspect;
        return new(256 + (pixel.X - viewport.X / 2) * 512 / width, pixel.Y * 240 / viewport.Y);
    }
    private static int SourceDepth(Camera3D camera, FixedVector3 position)
    {
        FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
        var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20, rotation.M01, rotation.M11, rotation.M21,
            rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
        FixedVector3 relative = (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12);
        return FixedRotation.Transform(view, relative).Z;
    }
    internal static byte[] SourceBrightness()
    {
        // Default PCGfx brightness index4, power1: doubled RGB with saturation.
        var table = new byte[256];
        for (int i = 0; i < table.Length; i++) table[i] = (byte)Math.Min(255, 2 * i);
        return table;
    }
    internal static Color SourceColour(uint packed, int fade, byte[] brightness)
    {
        uint rgb = 0;
        for (int shift = 0; shift < 24; shift += 8)
        {
            uint component = (packed >> shift) & 255;
            if (fade != 0) component = (uint)((fade * component) >> 8);
            rgb |= component << shift;
        }
        // PCGfx_DrawQPoly3D/ClipSendIndexedVertList halve and bias every
        // component when the entire RGB word is nonzero, then submitPoly
        // applies the selected brightness table. Pure black stays black.
        if (rgb != 0) rgb = ((rgb >> 1) & 0x7F7F7F) + 0x0F0F0F;
        return new(brightness[(byte)rgb] / 255f, brightness[(byte)(rgb >> 8)] / 255f, brightness[(byte)(rgb >> 16)] / 255f);
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (IGlowShape state in flashes.ToArray()) SourceBitRemoved?.Invoke(state);
        flashes.Clear();
        mesh.Dispose();
        material.Dispose();
    }
}
