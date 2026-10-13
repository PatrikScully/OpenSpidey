using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Game.Import;

internal static class AnimationImportChecks
{
    public static void Run(PkrArchive archive, Node3D host)
    {
        int libraries = 0, clips = 0;
        foreach (PkrEntry entry in archive.Entries.OrderBy(entry => entry.Path))
        {
            if (!entry.Path.EndsWith(".psx", StringComparison.OrdinalIgnoreCase)) continue;
            byte[] bytes = archive.Read(entry.Path);
            IAnimationPoseSource? animations = PsxCompressedAnimationReader.Read(bytes);
            animations ??= PsxMatrixAnimationReader.Read(bytes);
            if (animations is null) continue;
            ImportedAnimatedModel? model = null;
            try
            {
                model = ImportedAnimatedModel.Build(PsxSceneReader.Read(bytes), PsxTextureReader.Read(bytes), animations,
                    entry.Path.EndsWith("/spidey.psx", StringComparison.OrdinalIgnoreCase));
                host.AddChild(model);
                GD.Print($"Checking animation asset: {entry.Path}");
                model.VerifyAllClips();
                model.VerifyPlayback();
                libraries++;
                clips += model.ClipCount;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"Animation import failed: {entry.Path}", exception);
            }
            finally
            {
                if (model is not null) model.Free();
            }
        }
        GD.Print($"Animation import checks passed: {libraries} libraries, {clips} clips.");
    }
}
