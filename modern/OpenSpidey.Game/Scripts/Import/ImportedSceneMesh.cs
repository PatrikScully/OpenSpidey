using Godot;

namespace OpenSpidey.Game.Import;

/// <summary>Static source instance identity retained for transfer to a live object actor.</summary>
public partial class ImportedSceneMesh : MeshInstance3D
{
    private sealed record TranslucentSurface(Material? Previous, StandardMaterial3D Original, StandardMaterial3D Owned, SourceMaterialSnapshot Snapshot);
    private readonly Dictionary<int, TranslucentSurface> translucentSurfaces = [];
    private Mesh? translucentGeometry;
    public int SourceInstanceIndex { get; internal set; }
    public int SourceModelIndex { get; internal set; }
    /// <summary>CItem flag800 alpha; shared source materials stay untouched and the static-world fog owner rebinds its own surfaces from this.</summary>
    public float? SourceTranslucency { get; private set; }

    public void SetSourceTranslucency(float? alpha)
    {
        if (SourceTranslucency == alpha && ReferenceEquals(translucentGeometry, Mesh)) return;
        if (!ReferenceEquals(translucentGeometry, Mesh)) ReleaseTranslucentSurfaces();
        SourceTranslucency = alpha;
        translucentGeometry = Mesh;
        if (Mesh is not { } geometry) return;
        for (int index = 0; index < geometry.GetSurfaceCount(); index++)
        {
            Material? current = GetSurfaceOverrideMaterial(index);
            if (translucentSurfaces.TryGetValue(index, out TranslucentSurface? surface))
            {
                SourceItemTranslucency.Apply(surface.Owned, surface.Snapshot, alpha);
                if (alpha is null && ReferenceEquals(current, surface.Owned))
                {
                    SetSurfaceOverrideMaterial(index, surface.Previous);
                    translucentSurfaces.Remove(index); surface.Owned.Dispose();
                }
                continue;
            }
            if (alpha is null || current is ShaderMaterial) continue;
            StandardMaterial3D original = (current ?? geometry.SurfaceGetMaterial(index)) as StandardMaterial3D ??
                throw new NotSupportedException("Source item translucency requires the imported original standard material.");
            var owned = (StandardMaterial3D)original.Duplicate();
            var snapshot = SourceMaterialSnapshot.Of(original);
            SourceItemTranslucency.Apply(owned, snapshot, alpha);
            SetSurfaceOverrideMaterial(index, owned);
            translucentSurfaces.Add(index, new(current, original, owned, snapshot));
        }
    }

    /// <summary>The imported material beneath this mesh's own translucency duplicate.</summary>
    internal Material? SourceSurfaceMaterial(int index) =>
        translucentSurfaces.TryGetValue(index, out TranslucentSurface? surface) ? surface.Original : null;

    internal void VerifySourceTranslucency(float? alpha)
    {
        if (SourceTranslucency != alpha) throw new InvalidDataException("Static source item translucency state differs from its flag800 alpha.");
        if (Mesh is not { } geometry || geometry.GetSurfaceCount() == 0) throw new InvalidDataException("Static source item has no rendered surfaces.");
        for (int index = 0; index < geometry.GetSurfaceCount(); index++)
        {
            Material? active = GetActiveMaterial(index);
            if (active is ShaderMaterial shader)
            {
                if (GodotObject.IsInstanceValid(shader.Shader) && shader.Shader.Code.Contains("blend_add") != (alpha is not null))
                    throw new InvalidDataException("Static source fog surface blend differs from flag800.");
                continue;
            }
            if (active is not StandardMaterial3D material) throw new InvalidDataException("Static source surface lost its imported material.");
            SourceMaterialSnapshot snapshot = translucentSurfaces.TryGetValue(index, out TranslucentSurface? surface) ? surface.Snapshot : SourceMaterialSnapshot.Of(material);
            if (!SourceItemTranslucency.IsApplied(material, snapshot, alpha))
                throw new InvalidDataException("Static source surface translucency differs from flag800.");
        }
    }

    private void ReleaseTranslucentSurfaces()
    {
        foreach ((int index, TranslucentSurface surface) in translucentSurfaces)
        {
            if (Mesh is { } geometry && index < geometry.GetSurfaceCount() && ReferenceEquals(GetSurfaceOverrideMaterial(index), surface.Owned))
                SetSurfaceOverrideMaterial(index, surface.Previous);
            surface.Owned.Dispose();
        }
        translucentSurfaces.Clear();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) ReleaseTranslucentSurfaces();
    }
}
