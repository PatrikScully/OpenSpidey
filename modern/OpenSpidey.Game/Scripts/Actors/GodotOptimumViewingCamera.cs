using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Authored viewing nodes and source zoned camera rays feeding the actual fixed-position/orientation camera owner.</summary>
public sealed class GodotOptimumViewingCamera : IGodotHostageSceneCamera, IOptimumViewingCameraHost
{
    private readonly TriggerFile triggers;
    private readonly GodotActorWorldQuery world;
    private readonly PlayerFollowCamera camera;
    private readonly IVisibilityBoxHost visibility;
    private readonly OptimumViewingNodeSelector selector;

    public GodotOptimumViewingCamera(TriggerFile triggers, GodotActorWorldQuery world,
        PlayerFollowCamera camera, IVisibilityBoxHost visibility)
    {
        this.triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
        selector = new(this);
    }

    public int SelectViewingNode(FixedVector3 shooter, FixedVector3 hostage) => selector.Select(shooter, hostage);
    public IEnumerable<IVisibilityBoxItem> SourceItems(SourceVisibilityList list) => visibility.SourceItems(list);
    public bool CameraCollisionCheck { get => world.CameraCollisionCheck; set => world.CameraCollisionCheck = value; }
    public bool CameraRayOccluded(FixedVector3 start, FixedVector3 end) => world.Trace(start, end) is not null;
    public void SetFixedCamera(FixedVector3 position, AimAngles angles) => camera.ScriptRig.SetFixedPositionAndRotation(
        camera.SourcePosition, position, camera.SourceRotation, SourceCameraEulerRotation.FromAngles(angles), 0);

    public IEnumerable<SourceViewingNode> SourceNodes
    {
        get
        {
            foreach (TriggerNode node in triggers.Nodes)
            {
                if (node.Id < 1 || node.TypeCode != 13) continue;
                TriggerPosition position = TriggerPlacement.Read(node).Position;
                yield return new(node.Id, node.TypeCode, new FixedVector3(position.X, position.Y, position.Z) << 12);
            }
        }
    }
}
