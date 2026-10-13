using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual Init_AtStart protected banks and immediate native world/resource destruction for source110.</summary>
internal static class MissionResourceCleanupImportChecks
{
    public static void Verify(Node3D owner, PkrArchive archive)
    {
        var repository = new PsxLibraryRepository(archive);
        using var regions = new PsxAssetRegions(repository);
        var libraries = new GodotMissionLibraries { Repository = repository };
        owner.AddChild(libraries);
        try
        {
            libraries.LoadProtectedCommonLibraries(regions);
            PsxAssetLibrary[] protectedBanks = repository.Loaded.Values.ToArray();
            Require(protectedBanks.Length == 10 && protectedBanks.Select(bank => bank.AssetPath).SequenceEqual(new[]
            {
                "data/webdome2.psx", "data/webdome3.psx", "data/spidey.psx", "data/bits.psx", "data/sparmor.psx",
                "data/shell_dc.psx", "data/costarm.psx", "data/items.psx", "data/ring.psx", "data/sparmour.psx"
            }) && protectedBanks.All(regions.IsProtected),
                "Original full-graphics common banks must be protected in Init_AtStart's exact first-free region order.");
            libraries.LoadEnvironment("L1A1_G");
            libraries.SelectObjectLibrary("L1A1_O");
            ImportedLevelWorld world = libraries.World ?? throw new InvalidDataException("Source110 proof has no native original world.");
            Node[] worldChildren = Descendants(world).ToArray();
            ImportedGeometryRoot[] geometryBanks = worldChildren.OfType<ImportedGeometryRoot>().ToArray();
            Require(world.Sources.Count == 2 && geometryBanks.Length == 2 && worldChildren.OfType<ImportedCollisionBody>().Any(),
                "The proof must retain genuine G/O geometry, material owners and original native collision before source cleanup.");
            PsxAssetLibrary[] expectedUnload = Enumerable.Range(0, PsxAssetRegions.Capacity)
                .Select(index => Resolve(index)).Where(bank => bank is not null && !regions.IsProtected(bank))
                .Cast<PsxAssetLibrary>().ToArray();
            var unloaded = new List<PsxAssetLibrary>();
            repository.LibraryUnloaded += unloaded.Add;
            libraries.ClearUnprotectedLibraries(regions);
            Require(unloaded.SequenceEqual(expectedUnload) && libraries.World is null && libraries.ObjectLibrary is null &&
                !GodotObject.IsInstanceValid(world) && worldChildren.All(node => !GodotObject.IsInstanceValid(node)) &&
                geometryBanks.All(bank => bank.ResourcesDisposed) && regions.Count == 10 &&
                protectedBanks.All(bank => ReferenceEquals(repository.RequireLoaded(bank.AssetPath), bank) && regions.IsProtected(bank)),
                "Actual source110 must immediately retire both native world banks/collision/materials in region order while preserving each protected generation.");
            libraries.ClearUnprotectedLibraries(regions);
            Require(unloaded.Count == expectedUnload.Length, "Repeated source110 must run no second native or repository destructor.");
            repository.LibraryUnloaded -= unloaded.Add;
            GD.Print("Verified native source110: all ten genuine protected common banks, original L1A1 G/O world, exact region-order unload, immediate geometry/collision/material destruction and retained protected generations.");
        }
        finally { libraries.Free(); }

        PsxAssetLibrary? Resolve(int index)
        {
            try { return regions.Resolve((byte)index); }
            catch (InvalidDataException) { return null; }
        }
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        { yield return child; foreach (Node nested in Descendants(child)) yield return nested; }
    }
    private static void Require(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException("Native source library cleanup: " + evidence); }
}
