using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

/// <summary>Draws the recovered paired web polylines in world space.</summary>
public partial class GodotZipWebStrand : Node3D
{
    private readonly ImmediateMesh mesh = new();
    private readonly StandardMaterial3D material = new()
        { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(162 / 255f, 162 / 255f, 162 / 255f) };
    public ZipWebStrand Strand { get; private set; } = null!;
    private PlayerEffectTextures? effects;
    private readonly ImmediateMesh knotMesh = new();
    private readonly List<Vector3> knotVertices = [];
    public int VisibleKnots => knotVertices.Count / 6;
    private readonly StandardMaterial3D knotMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new(1, 1, 1, 0.5f),
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
    };

    public static GodotZipWebStrand Create(FixedVector3 anchor, FixedVector3 hook, PlayerEffectTextures? effects) =>
        new() { Name = "ZipWebStrand", TopLevel = true, Strand = new(anchor, hook), effects = effects };

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Mesh = mesh });
        if (effects is null) return;
        knotMaterial.AlbedoTexture = effects.WebKnot;
        AddChild(new MeshInstance3D { Mesh = knotMesh });
    }

    public override void _Process(double delta) => DrawKnots();

    public void Advance(FixedVector3 hook, FixedVector3 camera, int ticks)
    {
        Strand.Step(hook, camera, ticks);
        if (Strand.Finished) { QueueFree(); return; }
        mesh.ClearSurfaces();
        Draw(Strand.Outer);
        Draw(Strand.Inner);
        DrawKnots();
    }

    private void DrawKnots()
    {
        if (effects is null || Strand.Finished) return;
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        knotMesh.ClearSurfaces();
        knotVertices.Clear();
        bool begun = false;
        foreach (FixedVector3 knot in Strand.Knots)
        {
            Vector3 center = PsxCoordinates.FixedWorldPosition(knot);
            float depth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            FlatSpriteRectangle? rectangle = FlatSpriteProjection.Project(effects.WebKnotFrame, 176, (int)(depth * 100));
            if (rectangle is not { } rect) continue;
            float unitsPerPixel = 2 * depth * Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2) / 480;
            Vector3 right = camera.GlobalBasis.X * unitsPerPixel;
            Vector3 down = -camera.GlobalBasis.Y * unitsPerPixel;
            Vector3 origin = center + right * rect.X + down * rect.Y;
            Vector3[] corners = [origin, origin + right * rect.Width, origin + down * rect.Height,
                origin + right * rect.Width + down * rect.Height];
            if (!begun) { knotMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, knotMaterial); begun = true; }
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                knotMesh.SurfaceSetUV(new((corner & 1) != 0 ? 0.99f : 0.01f, (corner & 2) != 0 ? 0.99f : 0.01f));
                knotMesh.SurfaceAddVertex(corners[corner]);
                knotVertices.Add(corners[corner]);
            }
        }
        if (begun) knotMesh.SurfaceEnd();
    }

    public void VerifyMesh()
    {
        if (mesh.GetSurfaceCount() != 2) throw new InvalidDataException("The zip strand lost one of its paired polylines.");
        VerifySurface(0, Strand.Outer);
        VerifySurface(1, Strand.Inner);
        if (effects is not null && knotVertices.Count != 0)
        {
            if (knotMesh.GetSurfaceCount() != 1 || knotMaterial.AlbedoTexture != effects.WebKnot)
                throw new InvalidDataException("The web knot sprites lost their original texture or billboard surface.");
            using Godot.Collections.Array arrays = knotMesh.SurfaceGetArrays(0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (vertices.Length != knotVertices.Count) throw new InvalidDataException("The web knot sprites changed their vertex count.");
            for (int index = 0; index < vertices.Length; index++)
            {
                float error = vertices[index].DistanceTo(knotVertices[index]);
                if (!float.IsFinite(error) || error > 0.000001f)
                    throw new InvalidDataException("The rendered knot sprite differs from its camera-facing geometry.");
            }
        }
    }

    private void VerifySurface(int surface, IReadOnlyList<FixedVector3> expected)
    {
        using Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (vertices.Length != expected.Count) throw new InvalidDataException("The zip strand changed its source point count.");
        for (int index = 0; index < vertices.Length; index++)
        {
            float error = vertices[index].DistanceTo(PsxCoordinates.FixedWorldPosition(expected[index]));
            if (!float.IsFinite(error) || error > 0.000001f)
                throw new InvalidDataException("The rendered zip strand differs from its recovered point geometry.");
        }
    }

    private void Draw(IReadOnlyList<FixedVector3> points)
    {
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, material);
        foreach (FixedVector3 point in points) mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(point));
        mesh.SurfaceEnd();
    }

    public override void _ExitTree()
    {
        mesh.Dispose();
        material.Dispose();
        knotMesh.Dispose();
        knotMaterial.Dispose();
    }
}
