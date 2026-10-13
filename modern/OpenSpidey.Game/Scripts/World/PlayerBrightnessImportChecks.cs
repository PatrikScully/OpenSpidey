using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>The original sub-object mask suppresses submission after lighting, never the shared stitch-colour calculation.</summary>
internal static class PlayerBrightnessImportChecks
{
    internal static void VerifyHiddenModelLighting(GodotPlayer player, GodotPlayerBrightness brightness)
    {
        var model = player.Model;
        if (model.SourcePartCount != 18 || model.SourceGeometryParts.Count != 18 ||
            model.SourceGeometryParts.Any(part => !SourceVertexLighting.IsEnabled(part.Model, player.ActorState.ItemFlags)))
            throw new InvalidDataException("Native player lighting proof requires the actual eighteen lighting-enabled spidey.psx source models.");
        uint lower = model.SourceHiddenParts, upper = model.SourceHiddenPartsUpper;
        int visible = model.VisibleModelParts.Count;
        int lightUpdates = brightness.State.LightUpdates;
        if (visible != 16)
            throw new InvalidDataException("Native player lighting proof requires the genuine two masked hand alternatives and sixteen visible source models.");
        try
        {
            model.SetSourceHiddenParts(0, 0);
            brightness.SynchronizeSourceLighting();
            if (brightness.LitPartCount != 18 || brightness.VisibleLitPartCount != 18)
                throw new InvalidDataException("The actual unmasked player must compute and upload all eighteen source lighting parts.");
            int cursor = brightness.SourceColours.AttachmentCursor;
            uint[] completeScratch = ReadScratch(brightness.SourceColours);
            brightness.VerifyUploads();

            model.SetSourceHiddenParts(lower, upper);
            brightness.SynchronizeSourceLighting();
            if (brightness.LitPartCount != 18 || brightness.VisibleLitPartCount != visible ||
                brightness.SourceColours.AttachmentCursor != cursor ||
                !ReadScratch(brightness.SourceColours).SequenceEqual(completeScratch) ||
                brightness.State.LightUpdates != lightUpdates)
                throw new InvalidDataException("Original hand masking must preserve all eighteen computed parts and every stitch-colour float bit while restricting actual native uploads to sixteen visible models.");
            brightness.VerifyUploads();
            Godot.GD.Print("PASS: real player lighting computes18 source models before mask, uploads16 visible models, retains identical full stitch-colour scratch and verifies all18 posed joints.");
        }
        finally
        {
            model.SetSourceHiddenParts(lower, upper);
            brightness.SynchronizeSourceLighting();
        }
    }

    private static uint[] ReadScratch(SourceVertexLightingState state)
    {
        uint[] bits = new uint[256 * 3];
        for (int index = 0; index < 256; index++)
        {
            SourceLightColour colour = state.ReadAttachment(index);
            bits[index * 3] = BitConverter.SingleToUInt32Bits(colour.Red);
            bits[index * 3 + 1] = BitConverter.SingleToUInt32Bits(colour.Green);
            bits[index * 3 + 2] = BitConverter.SingleToUInt32Bits(colour.Blue);
        }
        return bits;
    }
}
