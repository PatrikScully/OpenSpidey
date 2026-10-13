namespace OpenSpidey.Assets.Models;

/// <summary>Original M3dInit_ParsePSX face preparation, independent of collision metadata.</summary>
public static class PsxRenderPolicy
{
    public static uint PrepareFaceFlags(uint onDiskFlags) =>
        (onDiskFlags & 0x40) == 0 ? onDiskFlags ^ 0x80 : onDiskFlags;

    public static bool IsFaceVisible(uint onDiskFlags) => (PrepareFaceFlags(onDiskFlags) & 0xC0) != 0;

    public static bool IsModelVisible(PsxModel model) =>
        (model.Flags & 0x20) == 0 && model.Faces.Any(face => IsFaceVisible(face.Flags));
}
