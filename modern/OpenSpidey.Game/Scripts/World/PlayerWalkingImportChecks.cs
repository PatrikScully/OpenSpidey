using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Production walking guards and genuine imported-face surface/pose/camera composition, with exact bounded-state restoration.</summary>
internal static class PlayerWalkingImportChecks
{
    public static void Verify(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerWebRuntime webs, ImportedLevelWorld world)
    {
        var walking = player.Controller.Walking as GodotPlayerWalking ?? throw new InvalidDataException("Production walking owner is absent.");
        GodotPlayerMelee melee = player.Melee!;
        PlayerTerrainController controller = player.Controller;
        PlayerCombatState combat = player.Combat.State;
        if (!controller.IsGrounded || controller.Action != PlayerAction.Idle || player.Model.ClipId != 0 ||
            controller.HeadingTurn.RemainingTicks != 0 || webs.Actions.State.CurrentStrand is not null || webs.Swing!.SwingPresent)
            throw new InvalidDataException("Bounded walking proof requires the actual settled, unheld first-level roof player.");
        camera.InitializeSourceTracking();
        PlayerWorldHit wall = FindActualWall();
        var saved = new CombatSnapshot(combat);
        PlayerOrientationSnapshot orientation = controller.Orientation.Capture();
        PlayerHeadingTurn turn = controller.HeadingTurn;
        FixedVector3 orientationReference = controller.OrientationReference;
        Transform3D transform = player.Transform;
        var primaryLine = walking.CapturePrimaryLine();
        var surface = new SurfaceSnapshot(walking.Surface);
        var heading = (camera.HeadingTransition.Heading, camera.HeadingTransition.Target,
            camera.HeadingTransition.RemainingTicks, camera.HeadingTransition.Rate, camera.HeadingTransition.PublishedHeading);
        var tripod = (camera.Tripod.Position, camera.Tripod.Target, camera.Tripod.Rate, camera.Tripod.RemainingTicks);
        CameraParameter[] parameters = Enum.GetValues<CameraParameter>();
        CameraParameterTransitionSnapshot[] parameterStates = parameters.Select(parameter => camera.ScriptRig.Parameter(parameter).Capture()).ToArray();
        byte[] pressed = melee.State.Input.Pressed.ToArray(), triggered = melee.State.Input.Triggered.ToArray(), mirrors = melee.State.Input.Mirrors.ToArray();
        sbyte primary = melee.State.Input.Sideways, secondary = melee.State.Input.Forward;
        int effective = melee.State.EffectiveHeading; FixedVector3 right = melee.State.RightDirection;
        bool finished = melee.State.AnimationFinished;
        byte playbackFinished = player.Model.Playback.Finished;
        int playbackSpeed = player.Model.PlaybackSpeed;
        var itemFlags = player.ActorState.ItemFlags;
        bool sourcePoseEnabled = player.Model.SourcePoseEnabled;
        try
        {
            Array.Clear(melee.State.Input.Pressed); Array.Clear(melee.State.Input.Triggered); Array.Clear(melee.State.Input.Mirrors);
            SetFloorWalking(21);
            melee.State.Input.Sideways = -127; melee.State.Input.Forward = 0;
            camera.SetCameraAngle(unchecked((short)((controller.Orientation.GetEffectiveHeading(false) - controller.MovementInput.Heading) & 4095)), 0);
            walking.UpdateWalking(ushort.MaxValue);
            Assert(combat.ActionState == 16 && combat.AnimationId == 21 && combat.FieldEA6 == 0,
                "Actual production walking must run all genuine web/jump/kick/airborne/corner guards and retain forward gait21 on the real roof.");
            melee.State.Input.Sideways = melee.State.Input.Forward = 0;
            walking.UpdateWalking(ushort.MaxValue);
            Assert(combat.ActionState == 1 && player.Model.ClipId is 11 or 12 or 13,
                "Released movement must use actual SwitchToStandMode's original21 stop clip.");

            SetFloorWalking(21); melee.State.Input.Sideways = -127;
            camera.SetCameraAngle(unchecked((short)((controller.Orientation.GetEffectiveHeading(false) + 2048 - controller.MovementInput.Heading) & 4095)), 0);
            walking.UpdateWalking(ushort.MaxValue);
            Assert(combat.ActionState == 0x400000 && combat.AnimationId == 25 && combat.Velocity == default && combat.HeadingTurnTicksRemaining == 0,
                "Actual rearward input must select original25, zero all velocity and call the real torso lock before source rolling.");

            VerifySurfaceEntry(exterior: false);
            VerifySurfaceEntry(exterior: true);

            SetFloorWalking(21);
            SeedPrimary(exterior: false);
            // Approach the genuine wall from the floor using the same target-based orientation as source steering.
            // The counter advances only when the player's applied forward axis faces the wall normal.
            melee.OrientToNormal(true, wall.Contact.Normal);
            FixedVector3 facing = combat.ForwardDirection, wallNormal = walking.Surface.LineNormal;
            int alignment = unchecked(((facing.X * wallNormal.X) >> 12) + ((facing.Y * wallNormal.Y) >> 12) +
                ((facing.Z * wallNormal.Z) >> 12));
            Assert(alignment > 3800, "The actual floor orientation must face the retained imported wall before its source counter can advance.");
            combat.HeldObjectPresent = false; combat.CollisionFlags |= 1; combat.FieldAD7 = 20; combat.ElapsedTicks = 2;
            Assert(walking.CheckRunIntoWall() && combat.ActionState == 0x80000 && player.Model.ClipId == 14 && combat.FieldAD7 == 0,
                "The actual imported wall face and byteAD7 must enter source80000/clip14 through the genuine native animation/sound owner.");
            controller.ApplyCombatState();
            combat.AnimationFrame = player.Model.FrameCount - 1; melee.ApplyPlayerPose(); player.Model.SetAnimationFinished(1);
            FixedVector3 lastHook = melee.GetPlayerHookPosition(2);
            walking.UpdateWallCompletion();
            Assert(combat.ActionState == 1 && combat.IsCrawling && combat.Position == lastHook && combat.CrawlSurfaceNormal == wall.Contact.Normal &&
                controller.OrientationReference == new FixedVector3(0, 4096, 0) && combat.HeadingTurnTicksRemaining == 0,
                "Actual80000 completion must use posedhook2, the genuine retained wall normal, world-up reference and original stand recovery.");
            melee.ApplyPlayerPose(); player.Model.VerifyCurrentPose();
            GD.Print($"Verified production walking: complete real guard composition, original21 stop/25 roll, genuine {wall.SourceAsset} face{wall.FaceIndex} interior/exterior clips, last-frame posedhook2 tripod/frame0 restart, actual1000/80000 recovery and exact camera presets.");
        }
        finally
        {
            surface.Restore(walking.Surface); walking.RestorePrimaryLine(primaryLine);
            saved.Restore(combat); controller.Orientation.Restore(orientation); controller.OrientationReference = orientationReference;
            controller.ApplyCombatState();
            melee.PlaySingleAnimation(saved.Animation, 0, -1); combat.AnimationFrame = saved.Frame; melee.ApplyPlayerPose();
            player.Model.ApplySourceJointPose(sourcePoseEnabled);
            player.Model.SetAnimationFinished(playbackFinished); player.Model.PlaybackSpeed = playbackSpeed;
            saved.Restore(combat); controller.ApplyCombatState(); player.Transform = transform; player.ActorState.ItemFlags = itemFlags;
            Array.Copy(pressed, melee.State.Input.Pressed, pressed.Length); Array.Copy(triggered, melee.State.Input.Triggered, triggered.Length);
            Array.Copy(mirrors, melee.State.Input.Mirrors, mirrors.Length); melee.State.Input.Sideways = primary; melee.State.Input.Forward = secondary;
            melee.State.EffectiveHeading = effective; melee.State.RightDirection = right; melee.State.AnimationFinished = finished;
            camera.SetCameraAngle(heading.Heading, 0);
            (camera.HeadingTransition.Heading, camera.HeadingTransition.Target, camera.HeadingTransition.RemainingTicks,
                camera.HeadingTransition.Rate, camera.HeadingTransition.PublishedHeading) = heading;
            (camera.Tripod.Position, camera.Tripod.Target, camera.Tripod.Rate, camera.Tripod.RemainingTicks) = tripod;
            for (int i = 0; i < parameters.Length; i++) camera.ScriptRig.Parameter(parameters[i]).Restore(parameterStates[i]);
            Assert(controller.HeadingTurn == turn, "Walking proof must restore the exact source heading target/step/ticks.");
        }

        void SetFloorWalking(ushort clip)
        {
            saved.Restore(combat); controller.Orientation.Restore(orientation); controller.ApplyCombatState();
            melee.PlaySingleAnimation(clip, 0, -1); combat.AnimationFrame = 0; combat.ActionState = 16;
            controller.ApplyCombatState(); melee.ApplyPlayerPose();
            walking.RestorePrimaryLine(primaryLine); surface.Restore(walking.Surface);
        }
        void SeedPrimary(bool exterior)
        {
            // State setup uses the authentic ray's entire primary hit. No normal, face, distance or geometry is fabricated.
            var step = new PlayerCrawlStep(new(controller.Position, controller.Velocity), combat.CollisionFlags,
                combat.CrawlSurfaceNormal, 0, wall, null, null, controller.HardSurface, false, exterior, 0, 0, null, null, false, false)
                { SourceLineWasQueried = true, SourceLineQueryHit = wall };
            walking.FinishCrawlPhysics(step);
        }
        void VerifySurfaceEntry(bool exterior)
        {
            SetFloorWalking(19);
            combat.IsCrawling = true; combat.Field8E8 = combat.Field8E9 = 0; controller.ApplyCombatState();
            SeedPrimary(exterior);
            bool accepted = exterior ? walking.CheckExteriorSurfaceTransition() : walking.CheckInteriorSurfaceTransition();
            ushort clip = combat.AnimationId;
            Assert(accepted && combat.ActionState == (exterior ? 0x2000u : 0x1000u) && combat.AnimationFrame == 0 &&
                player.Model.Playback.Frame == 0 && camera.Tripod.RemainingTicks == player.Model.FrameCount * 2 && combat.FloorCameraState == 1,
                "Actual source corner entry must run the imported transition clip, last-frame tripod and second frame0 RunAnim with wall-camera16.");
            FixedVector3 tripodHook = camera.Tripod.Target;
            Assert(melee.GetPlayerHookPosition(2) == tripodHook,
                "Source frame0 RunAnim must retain the last-frame applied hook pose until the later source pose phase.");
            combat.AnimationFrame = player.Model.FrameCount - 1;
            melee.ApplyPlayerPose();
            Assert(melee.GetPlayerHookPosition(2) == tripodHook, "Stored corner tripod must equal the actual original transition clip's last-frame posed hook2.");
            player.Model.VerifyCurrentPose();
            foreach (CameraParameter parameter in parameters)
            {
                short preset = camera.ScriptRig.Profiles.Get(CameraSurface.Wall, parameter);
                int expected = parameter == CameraParameter.HorizontalDistance ? unchecked((ushort)preset) : preset;
                Assert(camera.ScriptRig.Parameter(parameter).Target == expected,
                    "Native wall preset must use all five actual source profile scalars, retaining unsigned horizontal distance.");
            }
            combat.AnimationFrame = 0; melee.ApplyPlayerPose(); controller.ApplyCombatState();
            if (!exterior)
            {
                FixedVector3 start = melee.GetPlayerHookPosition(2);
                walking.UpdateInterior(clip);
                int slide = clip == 60 ? 66 : 55;
                Assert(combat.ActionState == 16 && combat.CrawlSurfaceNormal == wall.Contact.Normal && combat.Field8E8 == 1 && combat.Field8E9 == 0 &&
                    combat.Position == start + combat.UpDirection * slide && walking.Surface.ApplyHookPosition == 1,
                    "Actual1000 completion must take its genuine posed hook, orient to the real wall, restore source steering and apply its exact source slide.");
            }
        }
        PlayerWorldHit FindActualWall()
        {
            var query = new GodotPlayerWorldQuery(player);
            foreach (ImportedCollisionBody body in Descendants(world.Collision).OfType<ImportedCollisionBody>())
            {
                if ((body.CollisionLayer & CollisionLayers.PlayerTerrain) == 0) continue;
                var shape = body.GetChildren().OfType<CollisionShape3D>().Single().Shape as ConcavePolygonShape3D;
                if (shape is null) continue;
                Vector3[] faces = shape.GetFaces();
                for (int index = 0; index < body.SourceTriangles.Count; index++)
                {
                    var triangle = body.SourceTriangles[index];
                    FixedVector3 normal = PsxCoordinates.RotateOriginalNormal(triangle.FixedNormal, body.SourceRotation);
                    if (normal.Y is < -2000 or > 2000 || (triangle.PackedMetadata & 0x40000) != 0) continue;
                    Vector3 centroid = body.ToGlobal((faces[index * 3] + faces[index * 3 + 1] + faces[index * 3 + 2]) / 3);
                    Vector3 direction = PsxCoordinates.FixedWorldPosition(normal).Normalized();
                    if (query.Trace(PsxCoordinates.OriginalWorldPosition(centroid + direction), PsxCoordinates.OriginalWorldPosition(centroid - direction)) is { } hit &&
                        hit.Contact.Normal.Y is >= -2000 and <= 2000 && (hit.Contact.SurfaceFlags & 0x40000) == 0) return hit;
                }
            }
            throw new InvalidDataException("Native walking proof found no genuine crawlable wall face in original L1A1.");
        }
    }

    private sealed class SurfaceSnapshot(PlayerSurfaceTransitionState state)
    {
        private readonly FixedVector3 normal = state.LineNormal;
        private readonly byte fence = state.FenceDetected, exterior = state.ExteriorCornerDetected, hook = state.ApplyHookPosition,
            wallToCeiling = state.WallToCeilingMotion, ceilingToWall = state.CeilingToWallMotion, wall = state.WallMotionRequested,
            ceilingInput = state.CeilingToWallInputRequested, ceiling = state.CeilingMotionRequested;
        private readonly int interiorSlide = state.InteriorSlideRemaining, exteriorSlide = state.ForwardSlideRemaining;
        public void Restore(PlayerSurfaceTransitionState value)
        {
            value.LineNormal = normal; value.FenceDetected = fence; value.ExteriorCornerDetected = exterior; value.ApplyHookPosition = hook;
            value.WallToCeilingMotion = wallToCeiling; value.CeilingToWallMotion = ceilingToWall; value.WallMotionRequested = wall;
            value.CeilingToWallInputRequested = ceilingInput; value.CeilingMotionRequested = ceiling;
            value.InteriorSlideRemaining = interiorSlide; value.ForwardSlideRemaining = exteriorSlide;
        }
    }
    private sealed class CombatSnapshot(PlayerCombatState state)
    {
        public ushort Animation { get; } = state.AnimationId;
        public int Frame { get; } = state.AnimationFrame;
        private readonly uint action = state.ActionState;
        private readonly FixedVector3 position = state.Position, velocity = state.Velocity, normal = state.CrawlSurfaceNormal,
            forward = state.ForwardDirection, up = state.UpDirection;
        private readonly ushort collision = state.CollisionFlags;
        private readonly int elapsed = state.ElapsedTicks, headingTicks = state.HeadingTurnTicksRemaining, floorCamera = state.FloorCameraState;
        private readonly bool crawl = state.IsCrawling, held = state.HeldObjectPresent;
        private readonly byte wall = state.Field8E8, ceiling = state.Field8E9, ae4 = state.FieldAE4, ae5 = state.FieldAE5,
            e8c = state.FieldE8C, ad7 = state.FieldAD7, flag551 = state.Field551;
        private readonly short idle = state.FieldEA6;
        public void Restore(PlayerCombatState value)
        {
            value.ActionState = action; value.AnimationId = Animation; value.AnimationFrame = Frame;
            value.Position = position; value.Velocity = velocity; value.CrawlSurfaceNormal = normal; value.ForwardDirection = forward; value.UpDirection = up;
            value.CollisionFlags = collision; value.ElapsedTicks = elapsed; value.HeadingTurnTicksRemaining = headingTicks; value.FloorCameraState = floorCamera;
            value.IsCrawling = crawl; value.HeldObjectPresent = held; value.Field8E8 = wall; value.Field8E9 = ceiling;
            value.FieldAE4 = ae4; value.FieldAE5 = ae5; value.FieldE8C = e8c; value.FieldAD7 = ad7; value.Field551 = flag551; value.FieldEA6 = idle;
        }
    }
    private static IEnumerable<Node> Descendants(Node owner)
    {
        foreach (Node child in owner.GetChildren())
        { yield return child; foreach (Node nested in Descendants(child)) yield return nested; }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native walking: {evidence}"); }
}
