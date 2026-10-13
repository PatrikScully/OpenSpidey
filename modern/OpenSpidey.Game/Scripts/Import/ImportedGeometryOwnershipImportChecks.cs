using Godot;
using OpenSpidey.Assets.Models;

namespace OpenSpidey.Game.Import;

/// <summary>Real imported-bank resource lifetime, including models without instances and a rejected corrupted instance index.</summary>
internal static class ImportedGeometryOwnershipImportChecks
{
    public static void Verify(Node3D host, PsxAssetLibrary level, PsxAssetLibrary items)
    {
        Require(items.Scene.Models.Count == 9 && items.Scene.Instances.Count == 9,
            "The genuine items bank must retain its nine source models and nine authored instances.");
        var first = new Node3D(); var second = new Node3D(); host.AddChild(first); host.AddChild(second);
        ImportedGeometryRoot? geometry = null;
        ImportedGeometryRoot? uninstanced = null;
        int modelCount = 0;
        try
        {
            geometry = (ImportedGeometryRoot)PsxGeometryBuilder.Build(items.Scene, items.Textures);
            PsxMaterialLibrary materials = geometry.MaterialLibrary;
            ArrayMesh[] meshes = geometry.OwnedMeshes.ToArray();
            Rid[] identities = meshes.Select(mesh => mesh.GetRid()).ToArray();
            modelCount = meshes.Length;
            first.AddChild(geometry);
            ImportedSceneMesh[] instances = geometry.GetChildren().OfType<ImportedSceneMesh>().ToArray();
            Require(meshes.Length > 0 && instances.Length > 0 && instances.All(instance =>
                instance.SourceModelIndex == items.Scene.Instances[instance.SourceInstanceIndex].ModelIndex &&
                instance.Mesh is ArrayMesh mesh && meshes.Contains(mesh)),
                "Drawable items instances must refer to their actual authored model indices and the bank's owned GPU meshes.");
            PsxModel deferredSource = items.Scene.Models.First(PsxRenderPolicy.IsModelVisible);
            ArrayMesh deferredMesh = geometry.BuildOwnedModel(deferredSource, items.Scene.ColorTable, items.Textures) ??
                throw new InvalidDataException("The actual deferred items source model lost its drawable faces.");
            PsxMaterialLibrary deferredMaterials = geometry.DeferredMaterialLibraries.Single();
            Rid deferredIdentity = deferredMesh.GetRid();
            geometry.AddChild(new MeshInstance3D { Name = "DeferredModelOwnershipProof", Mesh = deferredMesh });
            Require(geometry.OwnedMeshes.Contains(deferredMesh) && deferredMesh.GetSurfaceCount() > 0,
                "Deferred ENV model construction must register its real mesh and private material cache with the source bank owner.");
            double emptyBefore = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            ArrayMesh? empty = geometry.BuildOwnedModel(deferredSource with { Faces = Array.Empty<PsxFace>() },
                items.Scene.ColorTable, items.Textures);
            Require(empty is null && Performance.GetMonitor(Performance.Monitor.ObjectResourceCount) == emptyBefore &&
                geometry.DeferredMaterialLibraries.Count == 1,
                "A deferred model with no drawable faces must release its temporary mesh and material cache without entering the owner's live resource list.");

            // This variant changes only placement records. Its immutable model, face, color and texture records are the actual items bank.
            var withoutInstances = items.Scene with { Instances = Array.Empty<PsxInstance>() };
            uninstanced = (ImportedGeometryRoot)PsxGeometryBuilder.Build(withoutInstances, items.Textures);
            PsxMaterialLibrary uninstancedMaterials = uninstanced.MaterialLibrary;
            ArrayMesh[] uninstancedMeshes = uninstanced.OwnedMeshes.ToArray();
            Rid[] uninstancedIdentities = uninstancedMeshes.Select(mesh => mesh.GetRid()).ToArray();
            first.AddChild(uninstanced);
            Require(uninstanced.GetChildCount() == 0 && uninstancedMeshes.Length == meshes.Length &&
                uninstancedMeshes.All(mesh => mesh.GetSurfaceCount() > 0) &&
                !uninstancedMeshes.Any(mesh => meshes.Contains(mesh)),
                "Removing authored placements must retain every real drawable model in a separate owned bank, including models without nodes.");
            geometry.Reparent(second);
            uninstanced.Reparent(second);
            first.Free();
            Require(!geometry.ResourcesDisposed && !materials.IsDisposed && meshes.Select(mesh => mesh.GetRid()).SequenceEqual(identities) &&
                meshes.All(mesh => mesh.GetSurfaceCount() > 0), "Imported-bank meshes and textures must survive genuine native reparent and former-parent destruction.");
            Require(!deferredMaterials.IsDisposed && deferredMesh.GetRid() == deferredIdentity && deferredMesh.GetSurfaceCount() > 0,
                "Deferred model/material ownership must survive the same native reparent as the initially imported bank.");
            Require(!uninstanced.ResourcesDisposed && !uninstancedMaterials.IsDisposed &&
                uninstancedMeshes.Select(mesh => mesh.GetRid()).SequenceEqual(uninstancedIdentities),
                "Owned uninstanced model resources must also survive native reparent and former-parent destruction.");
            geometry.Free();
            Require(geometry.ResourcesDisposed && materials.IsDisposed, "Actual Predelete must retire every cached model and material bank exactly once.");
            Require(deferredMaterials.IsDisposed, "Actual bank Predelete must also retire the deferred visibility model's private material cache.");
            Require(!uninstanced.ResourcesDisposed && !uninstancedMaterials.IsDisposed &&
                uninstancedMeshes.All(mesh => mesh.GetSurfaceCount() > 0),
                "Destroying one imported bank must leave another bank's independently built meshes and material cache alive.");
            uninstanced.Free();
            Require(uninstanced.ResourcesDisposed && uninstancedMaterials.IsDisposed,
                "Actual Predelete must retire uninstanced model resources as well as resources referenced by scene nodes.");
        }
        finally
        {
            if (geometry is not null && GodotObject.IsInstanceValid(geometry)) geometry.Free();
            if (uninstanced is not null && GodotObject.IsInstanceValid(uninstanced)) uninstanced.Free();
            if (GodotObject.IsInstanceValid(first)) first.Free();
            if (GodotObject.IsInstanceValid(second)) second.Free();
        }
        // Only the rejected instance index is corrupted; all model/face/texture records remain the actual source bank.
        PsxInstance source = level.Scene.Instances.First(instance => (instance.Flags & 0x1001) == 0);
        var malformed = level.Scene with { Instances = [source with { ModelIndex = ushort.MaxValue }] };
        double before = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
        bool rejected = false;
        try { PsxGeometryBuilder.Build(malformed, level.Textures); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        catch (IndexOutOfRangeException) { rejected = true; }
        double after = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
        Require(rejected && after == before,
            $"Malformed source index must release its partially built model/material/texture bank deterministically (resources {before}→{after}).");
        GD.Print($"Verified imported geometry ownership: nine real authored items instances, {modelCount} drawable items models retained by an uninstanced variant, independent banks and stable native identities after reparent, real Predelete and complete malformed-instance failure cleanup without GC.");
    }
    private static void Require(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native imported geometry: {evidence}"); }
}
