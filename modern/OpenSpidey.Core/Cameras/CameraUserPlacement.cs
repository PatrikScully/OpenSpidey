using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Whole416B10 mode16/17 placement branch. These modes skip terrain pull-in and retain the separate item-hiding phase.</summary>
public static class CameraUserPlacement
{
    public static void Move(CameraModeState camera, CameraNormalProjectionCache projection, FixedVector3 tripod, FixedVector3 offset,
        bool tripodPresent, bool trackingEnabled, bool postWaterEffect, Action hidePlayer, Action? hideItems, Action<string>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(hidePlayer);
        if (camera.Mode is not (16 or 17)) throw new NotSupportedException("This source placement leaf owns only camera modes16 and17.");
        CameraPlacement.Move(camera, projection, tripod, offset, 0, 0, tripodPresent, trackingEnabled, postWaterEffect,
            new PlayerOnlyHost(hidePlayer), hideItems, diagnostic);
    }

    private sealed class PlayerOnlyHost(Action hidePlayer) : ICameraPlacementHost
    {
        public CameraLineHit? CameraLineToItem(FixedVector3 start, FixedVector3 end) => throw Unused();
        public bool PlayerActionLocked => throw Unused();
        public bool PlayerMoving => throw Unused();
        public bool PlayerHiddenWhenFar => throw Unused();
        public short PlayerEffectiveHeading() => throw Unused();
        public void HidePlayer() => hidePlayer();
        public void ShowPlayer() => throw Unused();
        public void SetCameraAngle(short heading) => throw Unused();
        private static InvalidOperationException Unused() => new("Camera modes16 and17 never run the terrain pull-in queries.");
    }
}
