using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

/// <summary>Original textured surface quad driven by the source splat lifecycle.</summary>
public partial class GodotWebImpactSplat : Node3D
{
    private readonly ImmediateMesh mesh = new();
    private readonly StandardMaterial3D material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
    };
    public WebImpactSplat Splat { get; private set; } = null!;

    public static GodotWebImpactSplat Create(FixedVector3 position, FixedVector3 normal, Texture2D texture)
    {
        var node = new GodotWebImpactSplat { Name = "WebImpactSplat", TopLevel = true,
            Splat = new(position, normal, Random.Shared.Next(4096)) };
        node.material.AlbedoTexture = texture;
        return node;
    }

    public override void _Ready() => AddChild(new MeshInstance3D { Mesh = mesh });

    public void Advance()
    {
        Splat.Step();
        if (Splat.Finished) { QueueFree(); return; }
        float tint = Splat.Tint / 128f;
        material.AlbedoColor = new(tint, tint, tint, 0.5f);
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
        {
            mesh.SurfaceSetUV(new((corner & 1) != 0 ? 1 : 0, (corner & 2) != 0 ? 1 : 0));
            mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(Splat.Corners[corner]));
        }
        mesh.SurfaceEnd();
    }

    public void VerifyMesh()
    {
        if (mesh.GetSurfaceCount() != 1 || material.AlbedoTexture is null)
            throw new InvalidDataException("The impact splat lost its textured surface quad.");
        using Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        int[] order = [0, 1, 2, 1, 3, 2];
        if (vertices.Length != order.Length) throw new InvalidDataException("The impact splat changed its quad geometry.");
        for (int index = 0; index < order.Length; index++)
        {
            float error = vertices[index].DistanceTo(PsxCoordinates.FixedWorldPosition(Splat.Corners[order[index]]));
            if (!float.IsFinite(error) || error > 0.000001f)
                throw new InvalidDataException("The rendered impact splat differs from its recovered corners.");
        }
        if (Mathf.Abs(material.AlbedoColor.R - Splat.Tint / 128f) > 0.000001f)
            throw new InvalidDataException("The impact splat's material lost its source tint fade.");
    }

    public override void _ExitTree()
    {
        mesh.Dispose();
        material.Dispose();
    }
}
