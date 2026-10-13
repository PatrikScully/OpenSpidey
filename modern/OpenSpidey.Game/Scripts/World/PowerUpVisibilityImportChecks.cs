using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Source209 over actual pickup CItem regions/flags, persistent native items models and independent protected flats.</summary>
internal static class PowerUpVisibilityImportChecks
{
    public static void Verify(GodotActorRegistry registry, ImportedLevelWorld world, PsxLibraryRepository repository,
        GodotPowerUpActor actor, Action<bool, string> assert)
    {
        var host = new GodotVisibilityBoxHost(registry, new(() => world, repository), repository);
        IVisibilityBoxItem item = host.SourceItems(SourceVisibilityList.PowerUp).Single();
        PsxAssetLibrary bank = repository.RequireLoaded("items");
        ushort flags = actor.State.ItemFlags;
        MeshInstance3D? model = actor.RenderModel;
        Mesh? mesh = model?.Mesh;
        Transform3D transform = actor.Transform;
        var bit = actor.SourcePickupBit;
        try
        {
            assert(ReferenceEquals(actor.SourceVisibilityBank, bank) && item.RegionUsable &&
                item.SourcePosition == actor.SourcePosition && item.ItemFlags == flags,
                "Source209 pickup view uses its actual InitItem(items) bank generation, current CItem position and shared flag word.");
            assert(actor.State.IsThreeDimensional == 0
                    ? actor.Type == 18 && actor.ModelIndex == -1 && actor.State.ModelChecksum is null && model is null && bit is not null
                    : actor.State.ModelChecksum is uint checksum && actor.ModelIndex == bank.FindModel(checksum) && model is not null,
                "Only genuine model pickups select an items model; authored type18 retains the items region and its independent current-level protected flat without inventing geometry.");
            Execute(host, actor.SourcePosition, false);
            assert(actor.State.ItemFlags == (ushort)(flags | 1) && actor.Variables.ItemFlags == actor.State.ItemFlags &&
                item.ItemFlags == actor.State.ItemFlags && (model is null || !model.Visible),
                "Actual209 hides the canonical pickup CItem flag and native items mesh immediately.");
            Execute(host, actor.SourcePosition, true);
            assert(actor.State.ItemFlags == (ushort)(flags & 0xFFFE) && actor.Variables.ItemFlags == actor.State.ItemFlags &&
                (model is null || model.Visible == ((actor.State.ItemFlags & 0x8001) == 0)) &&
                actor.Transform == transform && ReferenceEquals(model?.Mesh, mesh) && ReferenceEquals(actor.SourcePickupBit, bit) &&
                (bit is null || bit.Attached),
                "Actual209 restores only CItem bit1; native geometry/pose and the separately protected type18 flat retain their actual identities and lifetime.");
        }
        finally { actor.SetSourceVisibilityFlags(flags); }
    }

    public static void VerifyUnloadedRegion(GodotActorRegistry registry, ImportedLevelWorld world,
        PsxLibraryRepository repository, Action<bool, string> assert)
    {
        GodotPowerUpActor[] actors = registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder.OfType<GodotPowerUpActor>().ToArray();
        ushort[] flags = actors.Select(actor => actor.State.ItemFlags).ToArray();
        PsxAssetLibrary bank = repository.RequireLoaded("items");
        var host = new GodotVisibilityBoxHost(registry, new(() => world, repository), repository);
        repository.Unload("items");
        assert(actors.Length > 0 && host.SourceItems(SourceVisibilityList.PowerUp).All(item => !item.RegionUsable),
            "Actual item-region unload makes source209 skip retained pickups before it reads or mutates their visibility flags.");
        foreach (GodotPowerUpActor actor in actors) Execute(host, actor.SourcePosition, (actor.State.ItemFlags & 1) != 0);
        assert(actors.Select(actor => actor.State.ItemFlags).SequenceEqual(flags),
            "Unusable source regions preserve every actual pickup flag through real209 execution.");
        PsxAssetLibrary replacement = repository.Load("items");
        assert(!ReferenceEquals(bank, replacement) && actors.All(actor => ReferenceEquals(actor.SourceVisibilityBank, bank)) &&
            host.SourceItems(SourceVisibilityList.PowerUp).All(item => !item.RegionUsable),
            "A new bank generation cannot rebind an existing CItem region/model owner to unrelated replacement geometry.");
        foreach (GodotPowerUpActor actor in actors) Execute(host, actor.SourcePosition, (actor.State.ItemFlags & 1) != 0);
        assert(actors.Select(actor => actor.State.ItemFlags).SequenceEqual(flags),
            "Source209 continues to skip stale pickup bank identities after actual unload/reload.");
    }

    private static void Execute(GodotVisibilityBoxHost host, FixedVector3 position, bool visible)
    {
        var arguments = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(arguments, visible ? (ushort)1 : (ushort)0);
        BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(2), 1);
        // At source offset0 the first box starts at absolute8 after the aligned padding word.
        BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(4), 0xA55A);
        FixedVector3 minimum = (position >> 12) - new FixedVector3(1, 1, 1), maximum = minimum + new FixedVector3(3, 3, 3);
        int[] coordinates = [minimum.X, minimum.Y, minimum.Z, maximum.X, maximum.Y, maximum.Z];
        for (int component = 0; component < coordinates.Length; component++)
            BinaryPrimitives.WriteInt32LittleEndian(arguments.AsSpan(6 + component * 4), coordinates[component]);
        BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(30), 255);
        new VisibilityBoxCommandHandler(209, host).Execute(new(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0),
            new(209, 0, arguments));
    }
}
