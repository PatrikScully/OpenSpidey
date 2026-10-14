using OpenSpidey.Core.Cameras;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>The CCamera members CScorpion AI488590 and its destructor reach through camera global0x56F3B8.</summary>
public sealed class ScorpionBossCamera : IScorpionBossCameraHost
{
    private readonly CameraAi camera;
    private readonly Func<int, IScorpionCameraHiddenItem?> hiddenItem;
    private readonly Action<int> clearHiddenItem;

    /// <param name="hiddenItem">Source camera0x184 slot; null ends the source scan.</param>
    public ScorpionBossCamera(CameraAi camera, Func<int, IScorpionCameraHiddenItem?> hiddenItem, Action<int> clearHiddenItem)
    {
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.hiddenItem = hiddenItem ?? throw new ArgumentNullException(nameof(hiddenItem));
        this.clearHiddenItem = clearHiddenItem ?? throw new ArgumentNullException(nameof(clearHiddenItem));
    }

    public CameraModeState Mode => camera.Mode;
    public int SpinRate { get => camera.Boss.SpinRate; set => camera.Boss.SpinRate = value; }
    public int StationaryRadius { get => camera.Boss.StationaryRadius; set => camera.Boss.StationaryRadius = value; }
    public ushort GetHorizontalDistance() => unchecked((ushort)camera.Rig.Parameter(CameraParameter.HorizontalDistance).Value);
    public short GetVerticalDistance() => unchecked((short)camera.Rig.Parameter(CameraParameter.VerticalDistance).Value);
    public void SetHorizontalDistance(ushort distance, ushort duration) =>
        camera.Rig.Parameter(CameraParameter.HorizontalDistance).Set(distance, duration, camera.Rig.DistanceInterpolationEnabled);
    public void SetVerticalDistance(short distance, ushort duration) =>
        camera.Rig.Parameter(CameraParameter.VerticalDistance).Set(distance, duration, camera.Rig.DistanceInterpolationEnabled);
    public void SetMode(uint mode) => camera.Rig.SetMode(checked((ushort)mode));
    public IScorpionCameraHiddenItem? GetHiddenItem(int index) => hiddenItem(index);
    public void ClearHiddenItem(int index) => clearHiddenItem(index);
}
