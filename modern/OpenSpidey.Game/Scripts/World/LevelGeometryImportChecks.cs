using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Original hidden map planes must retain source data without becoming visible scenery.</summary>
public static class LevelGeometryImportChecks
{
    public static void VerifyHiddenFaces(ImportedLevelWorld world, PsxScene original)
    {
        Node3D geometry = world.Geometry.GetChildren().OfType<Node3D>().Single(node =>
            node.Name.ToString().Equals("L1A1_G", StringComparison.OrdinalIgnoreCase));
        ImportedSceneMesh[] meshes = geometry.GetChildren().OfType<ImportedSceneMesh>().ToArray();
        for (int index = 0; index < 19; index++)
        {
            PsxModel model = original.Models[index];
            if (model.Faces.Count != 1 || model.Faces[0].Flags != 0x00100880 ||
                PsxRenderPolicy.IsModelVisible(model) || meshes.Any(mesh => mesh.SourceModelIndex == index))
                throw new InvalidDataException($"Original hidden L1A1 plane{index} was changed or drawn as a green wall.");
        }
        if (meshes.Length < 100 || world.Sources.Single(source => source.AssetPath.Equals(
            "data/L1A1_G.psx", StringComparison.OrdinalIgnoreCase)).Scene != original)
            throw new InvalidDataException("Hidden-face filtering removed ordinary scenery or modified the original collision source.");
        GD.Print($"Verified original map visibility: nineteen authored hidden planes excluded, {meshes.Length} ordinary G-library meshes retained, immutable geometry/collision source preserved.");
    }
}
