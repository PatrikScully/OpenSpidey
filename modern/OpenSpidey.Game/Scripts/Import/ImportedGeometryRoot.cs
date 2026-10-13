using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Game.Import;

/// <summary>One imported source bank owns every built mesh (including uninstanced models) and its private texture/material cache.</summary>
public partial class ImportedGeometryRoot : Node3D
{
    private readonly List<ArrayMesh> meshes = [];
    private readonly List<PsxMaterialLibrary> deferredMaterials = [];
    private PsxMaterialLibrary? materials;
    public bool ResourcesDisposed { get; private set; }
    internal IReadOnlyList<ArrayMesh> OwnedMeshes => meshes;
    internal IReadOnlyList<PsxMaterialLibrary> DeferredMaterialLibraries => deferredMaterials;
    internal PsxMaterialLibrary MaterialLibrary => materials ?? throw new InvalidOperationException("Imported geometry has no material owner.");
    internal void Initialize(PsxMaterialLibrary library)
    {
        if (materials is not null) throw new InvalidOperationException("Imported geometry already owns a material cache.");
        materials = library;
    }
    internal void Own(ArrayMesh mesh)
    {
        ObjectDisposedException.ThrowIf(ResourcesDisposed, this);
        if (meshes.Contains(mesh)) throw new InvalidOperationException("Imported model mesh already has this source-bank owner.");
        meshes.Add(mesh);
    }

    /// <summary>Deferred visible models belong to this source-bank root through its actual destruction, including their private textures/materials.</summary>
    internal ArrayMesh? BuildOwnedModel(PsxModel model, IReadOnlyList<uint> colors, IReadOnlyList<DecodedTexture> textures,
        bool sourceFacesPrepared = false)
    {
        ObjectDisposedException.ThrowIf(ResourcesDisposed, this);
        var library = new PsxMaterialLibrary(textures);
        ArrayMesh? mesh = null;
        try
        {
            mesh = PsxGeometryBuilder.BuildModel(model, colors, library, sourceFacesPrepared: sourceFacesPrepared);
            if (mesh is null) { library.Dispose(); return null; }
            Own(mesh); deferredMaterials.Add(library);
            return mesh;
        }
        catch
        {
            if (mesh is not null) { meshes.Remove(mesh); mesh.Dispose(); }
            library.Dispose(); throw;
        }
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || ResourcesDisposed) return;
        ResourcesDisposed = true;
        // Resources survive ordinary ExitTree/reparent operations. Actual destruction retires the whole bank atomically.
        foreach (MeshInstance3D instance in Descendants(this).OfType<MeshInstance3D>())
            if (instance.Mesh is ArrayMesh mesh && meshes.Contains(mesh)) instance.Mesh = null;
        foreach (ArrayMesh mesh in meshes) mesh.Dispose(); meshes.Clear();
        foreach (PsxMaterialLibrary library in deferredMaterials) library.Dispose(); deferredMaterials.Clear();
        materials?.Dispose(); materials = null;
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        { yield return child; foreach (Node nested in Descendants(child)) yield return nested; }
    }
}
