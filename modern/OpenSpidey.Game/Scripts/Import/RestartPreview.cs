using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Import;

/// <summary>Places the original player model at a named mission restart for inspection.</summary>
internal static class RestartPreview
{
    public static ImportedAnimatedModel Build(PkrArchive archive, string levelAsset, string restartName)
    {
        if (!levelAsset.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Restart previews require a level geometry file.");
        string triggerAsset = levelAsset[..^6] + "_T.trg";
        TriggerFile triggers = TriggerFile.Read(archive.Read(triggerAsset));
        RestartPoint point = new MissionCheckpoints(triggers).Select(restartName).Point;
        byte[] bytes = archive.Read("data/spidey.psx");
        CompressedAnimationLibrary animations = PsxCompressedAnimationReader.Read(bytes)
            ?? throw new InvalidDataException("The player model has no compressed animation library.");
        ImportedAnimatedModel player = ImportedAnimatedModel.Build(PsxSceneReader.Read(bytes),
            PsxTextureReader.Read(bytes), animations, playerPartOrder: true);
        // SetStartOrientation uses only yaw; the other restart angle words are not player pitch/roll.
        Transform3D transform = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(
            new(0, point.Orientation.Y, 0, 0, 0, 0)));
        transform.Origin = PsxCoordinates.WorldPosition(new(point.Position.X, point.Position.Y, point.Position.Z));
        player.Transform = transform;
        GD.Print($"Player restart preview: {point.Name}, node {point.NodeId}, source position {point.Position}, Godot position {transform.Origin}.");
        return player;
    }
}
