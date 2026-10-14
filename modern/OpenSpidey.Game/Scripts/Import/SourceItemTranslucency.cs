using Godot;

namespace OpenSpidey.Game.Import;

/// <summary>Imported material state that CItem flag800 replaces and later restores.</summary>
public readonly record struct SourceMaterialSnapshot(Color Color, BaseMaterial3D.TransparencyEnum Transparency,
    BaseMaterial3D.BlendModeEnum Blend, BaseMaterial3D.DepthDrawModeEnum DepthDraw)
{
    public static SourceMaterialSnapshot Of(StandardMaterial3D material) =>
        new(material.AlbedoColor, material.Transparency, material.BlendMode, material.DepthDrawMode);
}

/// <summary>M3d_Render 473D61: CItem flag800 forces global face override0x40, which the PC renders as blend mode2 (SRCALPHA, ONE, no depth write).</summary>
public static class SourceItemTranslucency
{
    /// <summary>DCModel_RenderModel 47787D keeps vertex alpha0xFF under the override.</summary>
    public const float ModelAlpha = 1f;
    /// <summary>DC_PSXModel_RenderModel, selected by DCModelData flag4000 (chunked ENV), uses vertex alpha0x80.</summary>
    public const float PsxModelAlpha = 128 / 255f;

    public static float? Alpha(ushort itemFlags, float alpha = ModelAlpha) => (itemFlags & 0x800) != 0 ? alpha : null;

    public static void Apply(StandardMaterial3D material, SourceMaterialSnapshot original, float? alpha)
    {
        Color color = original.Color;
        material.AlbedoColor = new(color.R, color.G, color.B, alpha ?? color.A);
        material.Transparency = alpha is null ? original.Transparency : BaseMaterial3D.TransparencyEnum.Alpha;
        material.BlendMode = alpha is null ? original.Blend : BaseMaterial3D.BlendModeEnum.Add;
        material.DepthDrawMode = alpha is null ? original.DepthDraw : BaseMaterial3D.DepthDrawModeEnum.Disabled;
    }

    public static bool IsApplied(StandardMaterial3D material, SourceMaterialSnapshot original, float? alpha) =>
        material.AlbedoColor.A == (alpha ?? original.Color.A) &&
        material.Transparency == (alpha is null ? original.Transparency : BaseMaterial3D.TransparencyEnum.Alpha) &&
        material.BlendMode == (alpha is null ? original.Blend : BaseMaterial3D.BlendModeEnum.Add) &&
        material.DepthDrawMode == (alpha is null ? original.DepthDraw : BaseMaterial3D.DepthDrawModeEnum.Disabled);
}

/// <summary>CItem flag800 over the materials one native item owns exclusively.</summary>
internal sealed class OwnedMaterialTranslucency(PsxMaterialLibrary materials)
{
    private readonly Dictionary<StandardMaterial3D, SourceMaterialSnapshot> snapshots = [];
    public float? Alpha { get; private set; }

    public void Set(float? alpha)
    {
        if (Alpha == alpha && materials.Materials.All(snapshots.ContainsKey)) return;
        foreach (StandardMaterial3D material in materials.Materials)
        {
            if (!snapshots.TryGetValue(material, out SourceMaterialSnapshot snapshot))
                snapshots.Add(material, snapshot = SourceMaterialSnapshot.Of(material));
            SourceItemTranslucency.Apply(material, snapshot, alpha);
        }
        Alpha = alpha;
    }

    public void Verify(float? alpha, IEnumerable<MeshInstance3D?> meshes)
    {
        int surfaces = 0;
        foreach (MeshInstance3D? instance in meshes)
        {
            if (instance?.Mesh is not { } mesh) continue;
            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++, surfaces++)
            {
                if (instance.GetActiveMaterial(surface) is not StandardMaterial3D material || !materials.Materials.Contains(material))
                    throw new InvalidDataException("A rendered source surface does not use its item's own imported material.");
                SourceMaterialSnapshot snapshot = snapshots.TryGetValue(material, out SourceMaterialSnapshot stored) ? stored : SourceMaterialSnapshot.Of(material);
                if (!SourceItemTranslucency.IsApplied(material, snapshot, alpha))
                    throw new InvalidDataException($"Rendered source surface translucency differs from flag800 alpha {alpha?.ToString() ?? "off"}.");
            }
        }
        if (surfaces == 0) throw new InvalidDataException("Source translucency check found no rendered surfaces.");
    }
}
