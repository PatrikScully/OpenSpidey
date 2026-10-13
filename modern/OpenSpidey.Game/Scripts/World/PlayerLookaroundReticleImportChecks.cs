using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

internal static class PlayerLookaroundReticleImportChecks
{
    public static async Task VerifyAsync(Node owner, byte[] originalBits, PlayerFollowCamera camera, PlayerLookaroundTargetState target)
    {
        if (target.Transition.ReticleType == 0)
            throw new InvalidDataException("The private L1A1 proof requires its actual world target reticle.");
        GodotPlayerLookaroundReticle reticle = GodotPlayerLookaroundReticle.Create(originalBits, camera,
            () => new(target.Transition.ReticleType, target.TargetPosition, target.ReticleColour));
        owner.AddChild(reticle);
        byte sourceType = target.Transition.ReticleType;
        try
        {
            DecodedTexture source = PsxTextureReader.Read(originalBits).Single(texture => texture.Checksum == reticle.SourceFrame.TextureChecksum);
            using (Image uploaded = reticle.SourceTexture.GetImage())
                if (source.Checksum != 0xC1F4DD58 || reticle.SourceFrame.OffsetX != -32 || reticle.SourceFrame.OffsetY != -32 ||
                    reticle.SourceFrame.Width != 64 || reticle.SourceFrame.Height != 64 || uploaded.GetWidth() != source.Width ||
                    uploaded.GetHeight() != source.Height || !uploaded.GetData().AsSpan().SequenceEqual(source.RgbaPixels))
                    throw new InvalidDataException("Native lookaround must upload the genuine original Reticle frame/texture and palette without replacing its artwork.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (reticle.Drawn is not { } drawn || drawn.Scale < 768 || (drawn.Argb >> 24) != 0xB0 || source.Flags != reticle.SourceTextureFlags)
                throw new InvalidDataException("The genuine world-selected DC0/DE4/DE8 must draw its original unsigned-scale reticle quad, B0 alpha and source texture flags in the live camera viewport.");
            uint expectedColour = 0xB0000000 | ((target.ReticleColour & 255) << 16) | (target.ReticleColour & 0xFF00) | ((target.ReticleColour >> 16) & 255);
            if (drawn.Argb != expectedColour)
                throw new InvalidDataException("Native world targeting lost the original red/green/default reticle colour owner.");
            target.Transition.ReticleType = 0;
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (reticle.Drawn is not null)
                throw new InvalidDataException("Clearing the actual DE4 flag must stop the native world reticle immediately.");
            GD.Print("Verified native world-lookaround reticle: genuine ReticleC1F4DD58 pixels/64 frame, actual selected world target and colour, original scale/alpha/texture flags, source visibility and texture cleanup.");
        }
        finally
        {
            target.Transition.ReticleType = sourceType;
            reticle.Free();
        }
        if (!reticle.ResourcesDisposed) throw new InvalidDataException("Native lookaround reticle teardown leaked the original uploaded texture.");
    }
}
