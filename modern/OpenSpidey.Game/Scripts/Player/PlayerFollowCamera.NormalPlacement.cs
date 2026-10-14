using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    private SourceHost? sourceHost;
    /// <summary>The session's original line-query owner. Without it the camera never pulls in against terrain.</summary>
    public Func<GodotActorWorldQuery?>? SourceWorld { get; set; }
    public bool TerrainObstructed => PoseOwned && ScriptRig.ModeState.CollisionFlag != 0;

    private sealed class SourceHost(PlayerFollowCamera camera) : ICameraAiHost
    {
        private GodotPlayer Player => camera.Target;

        public CameraFocusTarget? Tripod => camera.playerBody;
        public bool TripodDead => Player.ActorState.IsDead;
        public FixedVector3 TripodHookPosition(int hook) =>
            (Player.Melee ?? throw new InvalidOperationException("A camera tripod hook requires the real posed player.")).GetPlayerHookPosition(hook);
        public CameraFocusTarget? Boss => camera.SourceBoss?.Invoke();
        public uint TotalTime => (camera.SourceTotalTime ?? throw new InvalidOperationException("Camera shake lost its owning source clock."))();

        public CameraLineHit? CameraLineToItem(FixedVector3 start, FixedVector3 end)
        {
            if (camera.SourceWorld?.Invoke() is not { } world) return null;
            world.CameraCollisionCheck = true;
            try
            {
                if (world.Trace(start, end, includeEnvironmentalActors: true) is not { } hit) return null;
                var normal = GodotImpactWebNativeHost.Normal(hit);
                return new(PlayerLookaroundCameraSetup.SourceLineLength(start, hit.Position), hit.Position, normal.X, normal.Y, normal.Z);
            }
            finally { world.CameraCollisionCheck = false; }
        }
        public bool PlayerActionLocked => Player.Combat.State.Field8E8 != 0;
        public bool PlayerMoving => (Player.Controller.Velocity.X | Player.Controller.Velocity.Z) != 0;
        public bool PlayerHiddenWhenFar => Player.Combat.State.SuitArmorEnabled != 0;
        public short PlayerEffectiveHeading() => Player.Controller.Orientation.GetEffectiveHeading(Player.Controller.OnWall);
        public void HidePlayer() => Player.ActorState.ItemFlags |= 0x800;
        public void ShowPlayer() => Player.ActorState.ItemFlags &= unchecked((ushort)~0x800);
        public void SetCameraAngle(short heading) => camera.SetCameraAngle(heading, 0);
    }
}
