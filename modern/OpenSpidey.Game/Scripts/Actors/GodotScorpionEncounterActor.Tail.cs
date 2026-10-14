using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Scorpion;

namespace OpenSpidey.Game.Actors;

partial class GodotScorpionEncounterActor
{
    private PsxDynamicRegionLease? tailGeometryLease;
    private PsxDynamicRegionLease? tailGlowLease;
    private GodotScorpionTailModel? primaryTail;

    /// <summary>CScorpion_InitialiseTailPSX489050: first-free protected S slot, primary tail geometry and field_240.mRegion.</summary>
    internal void InitializeTailGeometry()
    {
        if (State is not ScorpionState body || body.TailGeometryRegion != byte.MaxValue) return;
        GodotScorpionEncounterRuntime encounter = runtime;
        PsxAssetRegions regions = encounter.Regions;
        OriginalScorpionTailData data = OriginalScorpionTailData.Read(encounter.OriginalExecutable);
        PsxAssetLibrary bank = encounter.Repository.RequireLoaded("Scorpion");
        uint checksum = NamedTextureTable.FindFirst(encounter.Archive.Read("data/scorpion.tex"), ScorpionTailMeshLayout.TextureName);
        DecodedTexture sourceTexture = bank.Textures.First(texture => texture.Checksum == checksum);
        tailGeometryLease = regions.AllocateDynamic("S", ReleasePrimaryTailResources);
        primaryTail = GodotScorpionTailModel.Create(bank, sourceTexture, data);
        AddChild(primaryTail);
        body.TailGeometryRegion = tailGeometryLease.Slot;
    }

    /// <summary>Scorpion tail glow489370: protected SW slot; ring-sweep draw remains unported.</summary>
    internal void InitializeTailGlow()
    {
        if (State is not ScorpionState body || body.TailGlowRegion != byte.MaxValue) return;
        if (tailGlowLease is null)
            tailGlowLease = runtime.Regions.AllocateDynamic("SW", () => { });
        body.TailGlowRegion = tailGlowLease.Slot;
    }

    internal void ReleaseTailRegions()
    {
        tailGeometryLease?.Dispose();
        tailGeometryLease = null;
        tailGlowLease?.Dispose();
        tailGlowLease = null;
    }

    private void ReleasePrimaryTailResources()
    {
        if (primaryTail is { } tail && IsInstanceValid(tail)) tail.QueueFree();
        primaryTail = null;
    }
}
