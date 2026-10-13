using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class MissionLibraryImportChecks
{
    public static async Task VerifyFirstLevelAsync(Node owner, GodotMissionLibraries libraries, GodotPlayer player)
    {
        const string objects = "data/l1a1_o.psx";
        var repository = libraries.Repository;
        var world = libraries.World ?? throw new InvalidDataException("The mission has no environment.");
        if (!ReferenceEquals(libraries.ObjectLibrary, repository.Load("DATA\\L1A1_O.PSX")) ||
            repository.RequireLoaded("henchman").Animations?.ClipCount is not > 0 ||
            repository.RequireLoaded("blackcat").Animations?.ClipCount is not > 0 ||
            !world.Sources.All(source => ReferenceEquals(source.Scene, repository.RequireLoaded(source.AssetPath).Scene)))
            throw new InvalidDataException("The original autoexec libraries did not retain their selected model/animation identities.");
        var query = new GodotPlayerWorldQuery(player);
        // Earlier actor fixtures claim their model prototypes and retire their native bodies.
        // Probe a remaining imported roof after deferred cleanup, independently of the player's fixture position.
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        FixedVector3 start = default, end = default;
        bool foundRoof = false;
        foreach (ImportedCollisionBody body in world.Collision.GetChildren().SelectMany(layer => layer.GetChildren())
            .OfType<ImportedCollisionBody>().Where(body => string.Equals(body.SourceAsset, objects, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var triangle in body.SourceTriangles)
            {
                if (PsxCoordinates.RotateOriginalNormal(triangle.FixedNormal, body.SourceRotation).Y >= 0) continue;
                Transform3D instance = PsxCoordinates.InstanceTransform(libraries.ObjectLibrary!.Scene.Instances[body.InstanceIndex]);
                Vector3 center = body.GlobalTransform * (instance * PsxCoordinates.WorldPosition((triangle.A + triangle.B + triangle.C) / 3));
                start = PsxCoordinates.OriginalWorldPosition(center + Vector3.Up);
                end = PsxCoordinates.OriginalWorldPosition(center - Vector3.Up);
                if (query.Trace(start, end) is not { } hit || !string.Equals(hit.SourceAsset, objects, StringComparison.OrdinalIgnoreCase) ||
                    hit.SupportId is not null || hit.ModelIndex != body.ModelIndex || hit.ModelChecksum is null ||
                    hit.ModelChecksum != libraries.ObjectLibrary!.Scene.Models[body.ModelIndex].Checksum)
                    continue;
                foundRoof = true;
                break;
            }
            if (foundRoof) break;
        }
        if (!foundRoof) throw new InvalidDataException("The library lifecycle check found no remaining original static object roof after actor cleanup.");
        libraries.UnloadLibrary("L1A1_O");
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!ReferenceEquals(libraries.World, world) || world.Sources.Count != 1 || world.InstanceCount != 137 ||
            world.Geometry.GetChildCount() != 1 || world.Collision.GetChildCount() != 1 ||
            libraries.ObjectLibrary is not null || repository.Loaded.ContainsKey(objects) ||
            string.Equals(query.Trace(start, end)?.SourceAsset, objects, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unloading object models removed the environment or retained their native roof collision.");
        GD.Print("Verified mission libraries: original autoexec resources, shared model identities, object selection and native partial-library unloading.");
    }
}
