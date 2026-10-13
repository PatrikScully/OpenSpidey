using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Core.Animation;
using OpenSpidey.Game.Utilities;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Exercises the actual physics space, including one-sided faces, layers, trigger areas and a falling capsule.</summary>
public partial class CollisionImportChecks : Node3D
{
    private readonly Vector3 origin = new(5000, 0, 5000);
    private CharacterBody3D capsule = null!;
    private int ticks;
    private int assertions;
    private GodotPlayer[] surfacePlayers = [];
    private FixedVector3[] surfaceStartPositions = [];
    private int surfaceMovementStart = -1;
    private bool surfacePlayersVerified;
    private readonly List<PlayerZipRoundTripCheck> zipChecks = [];
    private PlayerJumpCycleCheck? jumpCheck;
    public Node3D? LevelGeometry { get; init; }
    public Func<ImportedAnimatedModel>? PlayerModelFactory { get; init; }
    public PlayerEffectTextures? PlayerEffects { get; init; }

    public override void _Ready()
    {
        var vertices = new List<PsxVertex>();
        var faces = new List<PsxFace>();
        var normals = new List<PsxNormal>();
        AddQuad(new(-100, 0, -100), new(100, 0, -100), new(-100, 0, 100), new(100, 0, 100), new(0, -4096, 0), 0x100000);
        AddQuad(new(-100, -200, -100), new(100, -200, -100), new(-100, -200, 100), new(100, -200, 100), new(0, 4096, 0), 0x100000);
        AddQuad(new(100, -200, -100), new(100, -200, 100), new(100, 0, -100), new(100, 0, 100), new(-4096, 0, 0), 0x100000);
        AddQuad(new(-100, -50, -100), new(100, -50, -100), new(-100, -50, 100), new(100, -50, 100), new(0, -4096, 0), 0x120000);
        AddQuad(new(-500, 0, -100), new(-300, 0, -100), new(-500, 0, 100), new(-300, 0, 100), new(0, -4096, 0), 0);
        AddQuad(new(-900, 0, -100), new(-700, 0, -100), new(-900, 0, 100), new(-700, 0, 100), new(0, -4096, 0), 0x300000);
        var model = new PsxModel(0, ushort.MaxValue, vertices.AsReadOnly(), faces.AsReadOnly()) { Normals = normals.AsReadOnly() };
        var rotatedModel = new PsxModel(0, ushort.MaxValue, vertices.Take(4).ToArray(),
            new[] { faces[0] with { CollisionWord = 0x900000 } }) { Normals = new[] { normals[0] } };
        var scene = new PsxScene(0x20006, new[] { new PsxInstance(0, System.Numerics.Vector3.Zero,
            System.Numerics.Vector3.Zero, 0, 0), new PsxInstance(0x100, new(400 * 4096, -100 * 4096, 0),
                new(0, 0, 1024), 1, 0) }, new[] { model, rotatedModel }, []);
        Node3D geometry = PsxWorldCollisionBuilder.Build(scene);
        geometry.Position = origin;
        AddChild(geometry);
        capsule = new CharacterBody3D { Position = origin + new Vector3(-0.3f, 0.3f, 0.3f),
            CollisionLayer = CollisionLayers.Actors, CollisionMask = CollisionLayers.PlayerTerrain, SafeMargin = 0.001f };
        capsule.AddChild(new CollisionShape3D { Position = new(0, 0.8f, 0), Shape = new CapsuleShape3D { Radius = 0.2f, Height = 1.6f } });
        AddChild(capsule);
        if (PlayerModelFactory is not null)
        {
            FixedVector3 wall = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0.04f, 1, 0));
            FixedVector3 ceiling = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 1.04f, 0));
            surfacePlayers = [CreateSurfacePlayer(wall, new(-4096, 0, 0)), CreateSurfacePlayer(ceiling, new(0, 4096, 0))];
            FixedVector3 floor = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 0.96f, 0));
            AddZipActor(floor, -(104 << 12), -(96 << 12), "native fixture");
            GodotPlayer jumping = GodotPlayer.Create(floor, 0, PlayerModelFactory(), new(true, new(false, 10, true)), PlayerEffects);
            jumping.SampleInput = () => default;
            jumping.SampleButtons = () => default;
            jumping.CameraHeading = () => 2048;
            AddChild(jumping);
            jumpCheck = new(jumping, Assert);
        }

        GodotPlayer CreateSurfacePlayer(FixedVector3 position, FixedVector3 normal)
        {
            GodotPlayer player = GodotPlayer.Create(position, 0, PlayerModelFactory!(), new(true, new(false, 10, true)), PlayerEffects);
            player.SampleInput = () => default;
            player.SampleButtons = () => default;
            player.CameraHeading = () => 2048;
            player.AttachToSurface(new(position, normal, 227));
            AddChild(player);
            return player;
        }

        void AddQuad(System.Numerics.Vector3 a, System.Numerics.Vector3 b, System.Numerics.Vector3 c,
            System.Numerics.Vector3 d, System.Numerics.Vector3 normal, uint packed)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { new PsxVertex(a, 0), new PsxVertex(b, 0), new PsxVertex(c, 0), new PsxVertex(d, 0) });
            faces.Add(new(0, new[] { (byte)first, (byte)(first + 1), (byte)(first + 2), (byte)(first + 3) }, 0, null, [],
                packed | (uint)normals.Count));
            normals.Add(new(normal, 0));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        try
        {
            ticks++;
            if (ticks == 3)
            {
                VerifyRays();
                VerifyPlayerQueries();
                VerifyCrawlQueries();
                VerifyTerrainController();
                VerifySurfaceController();
                VerifyJumpController();
                VerifyLevelGeometry();
                AddOriginalOverhangActor();
            }
            if (ticks < 3) return;
            VerifyScheduledSurfacePlayers();
            foreach (PlayerZipRoundTripCheck check in zipChecks) check.Advance();
            jumpCheck?.Advance();
            capsule.Velocity = capsule.IsOnFloor() ? Vector3.Zero : capsule.Velocity + Vector3.Down * 9.8f * (float)delta;
            capsule.MoveAndSlide();
            if (ticks < 60) return;
            if (surfacePlayers.Length != 0 && (!surfacePlayersVerified || zipChecks.Any(check => !check.Finished) || jumpCheck?.Finished == false))
            {
                if (ticks > 600) throw new InvalidDataException("Original surface players did not finish the scheduled crawl verification.");
                return;
            }
            Assert(capsule.IsOnFloor(), "Capsule lands on the imported floor.");
            Assert(Mathf.Abs(capsule.Position.Y - origin.Y) < 0.02f, "Capsule feet remain at floor height.");
            GD.Print($"Passed {assertions} actual Godot physics checks: floor, ceiling, wall, query layers, trigger area and capsule landing.");
            GetTree().Quit();
            SetPhysicsProcess(false);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
            SetPhysicsProcess(false);
        }
    }

    private void AddZipActor(FixedVector3 position, int ceilingY, int floorY, string context,
        Func<GodotPlayer, string, Task>? capture = null, bool requireVisibleKnots = false)
    {
        GodotPlayer player = GodotPlayer.Create(position, 0, PlayerModelFactory!(), new(true, new(false, 10, true)), PlayerEffects);
        player.SampleInput = () => default;
        player.SampleButtons = () => default;
        player.CameraHeading = () => 2048;
        AddChild(player);
        zipChecks.Add(new(player, ceilingY, floorY, context, Assert, capture, requireVisibleKnots));
    }

    private void AddOriginalOverhangActor()
    {
        if (LevelGeometry is null || PlayerModelFactory is null) return;
        var world = new GodotPlayerWorldQuery(this);
        foreach (ImportedCollisionBody body in SceneNodes.Descendants<ImportedCollisionBody>(LevelGeometry))
        {
            if ((body.CollisionLayer & CollisionLayers.PlayerTerrain) == 0) continue;
            var shape = (ConcavePolygonShape3D)body.GetChildren().OfType<CollisionShape3D>().Single().Shape;
            Vector3[] vertices = shape.GetFaces();
            for (int index = 0; index < body.SourceTriangles.Count; index++)
            {
                var triangle = body.SourceTriangles[index];
                FixedVector3 normal = PsxCoordinates.RotateOriginalNormal(triangle.FixedNormal, body.SourceRotation);
                if (normal.Y <= 3400 || (triangle.PackedMetadata & 0x40000) != 0) continue;
                Vector3 center = body.GlobalTransform * ((vertices[index * 3] + vertices[index * 3 + 1] + vertices[index * 3 + 2]) / 3);
                FixedVector3 below = PsxCoordinates.OriginalWorldPosition(center) + new FixedVector3(0, 16 << 12, 0);
                PlayerWorldHit? floor = world.Trace(below, below + new FixedVector3(0, 3072 << 12, 0));
                if (floor is not { } floorHit || floorHit.Contact.Normal.Y >= -2600) continue;
                FixedVector3 position = floorHit.Contact.Position with { Y = unchecked(floorHit.Contact.Position.Y - (96 << 12)) };
                PlayerWorldHit? ceiling = PlayerZipWebRules.FindStraightTarget(new(position, new(0, -4096, 0),
                    new(0, -4096, 0), 1, 96), world);
                if (ceiling is not { } ceilingHit || ceilingHit.Contact.Normal.Y <= 3400 || ceilingHit.Contact.Distance < 128) continue;
                FixedVector3 destination = ceilingHit.Contact.Position + ceilingHit.Contact.Normal * 96;
                AddZipActor(position, destination.Y, position.Y, "original level overhang",
                    string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("OPENSPIDEY_VERIFY_CAPTURES")) ? null : CaptureOverhang,
                    requireVisibleKnots: true);
                Assert(ceilingHit.SourceAsset is not null && floorHit.SourceAsset is not null,
                    "Original overhang and floor queries retain their imported source identities.");
                GD.Print($"Original zip overhang: {ceilingHit.SourceAsset}, instance {ceilingHit.InstanceIndex}, face {ceilingHit.FaceIndex}; " +
                    $"floor={floorHit.SourceAsset}, position={position}, ceiling={destination}.");
                return;
            }
        }
        throw new InvalidDataException("The selected level has no imported ceiling/floor pair for the original overhang zip check.");
    }

    private async Task CaptureOverhang(GodotPlayer player, string surface)
    {
        string directory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_VERIFY_CAPTURES")!;
        var camera = new Camera3D { Near = 0.05f, Far = 2000, Fov = 60, KeepAspect = Camera3D.KeepAspectEnum.Height };
        AddChild(camera);
        player.SetPhysicsProcess(false);
        try
        {
            Vector3 focus = player.Model.GlobalTransform * player.Model.PoseBounds.GetCenter();
            Vector3 destination = focus;
            float farthest = -1;
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.Tau / 8;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0.15f, Mathf.Cos(angle)) * 3.2f;
                Vector3 candidate = focus + offset;
                using var query = PhysicsRayQueryParameters3D.Create(focus, candidate, CollisionLayers.CameraTerrain);
                query.HitBackFaces = false;
                using Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (hit.Count != 0) candidate = hit["position"].AsVector3() - offset.Normalized() * 0.15f;
                float distance = candidate.DistanceSquaredTo(focus);
                if (distance <= farthest) continue;
                destination = candidate;
                farthest = distance;
            }
            camera.GlobalPosition = destination;
            camera.LookAt(focus);
            camera.Current = true;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            string path = System.IO.Path.Combine(directory, "original-overhang-" + surface + ".png");
            Error error = image.SavePng(path);
            if (error != Error.Ok) throw new IOException($"Could not save {surface} overhang capture: {error}.");
            GD.Print($"Saved original overhang capture: {path}");
        }
        finally
        {
            player.SetPhysicsProcess(true);
            camera.QueueFree();
        }
    }

    private void VerifyScheduledSurfacePlayers()
    {
        if (surfacePlayers.Length == 0 || surfacePlayersVerified) return;
        if (surfaceMovementStart < 0)
        {
            if (surfacePlayers.Any(player => player.SourceUpdates < 4 || player.Model.ClipId != 19 || !player.Controller.IsGrounded)) return;
            surfaceMovementStart = surfacePlayers[0].SourceUpdates;
            surfaceStartPositions = surfacePlayers.Select(player => player.Controller.Position).ToArray();
            Assert(surfacePlayers[0].Controller.OnWall && surfacePlayers[1].Controller.OnCeiling,
                "Original Godot player models finish attachment in the wall and ceiling crawl states.");
            foreach (GodotPlayer player in surfacePlayers)
                player.SampleInput = () => player.SourceUpdates < surfaceMovementStart + 8
                    ? new(true, new(-127, 0), false, false, false, false, false, false) : default;
            return;
        }
        if (surfacePlayers.Any(player => player.SourceUpdates < surfaceMovementStart + 60)) return;
        for (int index = 0; index < surfacePlayers.Length; index++)
        {
            GodotPlayer player = surfacePlayers[index];
            Assert(player.Controller.IsCrawling && player.Controller.IsGrounded && player.Controller.Action == PlayerAction.Idle &&
                player.Model.ClipId == 19 && player.Controller.Velocity == default,
                "Original surface actors finish moving and stopping at a supported crawl idle.");
            Assert(Mathf.Abs(player.Transform.Basis.Determinant() - 1) < 0.01f &&
                float.IsFinite(player.Position.X) && float.IsFinite(player.Position.Y) && float.IsFinite(player.Position.Z),
                "Scheduled surface actors preserve complete finite body axes.");
            player.Model.VerifyPlayback();
            Assert(!player.Model.AdvanceAutomatically, "Surface actor animation remains owned by its player controller.");
        }
        Assert(surfacePlayers[0].Controller.Position.Y < surfaceStartPositions[0].Y &&
            surfacePlayers[1].Controller.Position.Z > surfaceStartPositions[1].Z,
            "Cached-pose body hooks allow the original actor models to climb and crawl beneath a ceiling.");
        surfacePlayersVerified = true;
        GD.Print($"Verified scheduled original surface actors: {surfacePlayers[0].SourceUpdates} source updates, cached-pose hooks and skin playback.");
    }

    private void VerifyRays()
    {
        Hit(new(0, 1, 0), new(0, -1, 0), CollisionLayers.PlayerTerrain, Vector3.Up, false);
        Hit(new(0, 1, 0), new(0, 3, 0), CollisionLayers.PlayerTerrain, Vector3.Down, false);
        Hit(new(0, 1, 0), new(2, 1, 0), CollisionLayers.PlayerTerrain, Vector3.Left, false);
        Miss(new(0, -1, 0), new(0, 1, 0), CollisionLayers.PlayerTerrain,
            "One-sided floor does not catch a ray from below.");
        Miss(new(-4, 1, 0), new(-4, -1, 0), CollisionLayers.CameraTerrain,
            "Non-camera terrain is excluded from camera probes.");
        Hit(new(-4, 1, 0), new(-4, -1, 0), CollisionLayers.PlayerTerrain, Vector3.Up, false);
        Miss(new(-8, 1, 0), new(-8, -1, 0), CollisionLayers.PlayerTerrain,
            "Enemy-only terrain does not catch player probes.");
        Hit(new(-8, 1, 0), new(-8, -1, 0), CollisionLayers.EnemyTerrain, Vector3.Up, false);
        Hit(new(0, 1, 0), new(0, 0, 0), CollisionLayers.TriggerZones, Vector3.Up, true);
    }

    private void VerifyLevelGeometry()
    {
        if (LevelGeometry is null) return;
        ImportedCollisionBody[] bodies = SceneNodes.Descendants<ImportedCollisionBody>(LevelGeometry).ToArray();
        foreach (ImportedCollisionBody body in bodies)
        {
            var shape = (ConcavePolygonShape3D)body.GetChildren().OfType<CollisionShape3D>().Single().Shape;
            Vector3[] vertices = shape.GetFaces();
            Vector3 center = (vertices[0] + vertices[1] + vertices[2]) / 3;
            Vector3 normal = (vertices[0] - vertices[2]).Cross(vertices[0] - vertices[1]).Normalized();
            var excluded = new Godot.Collections.Array<Rid>(bodies.Where(other => other != body).Select(other => other.GetRid()));
            using var query = PhysicsRayQueryParameters3D.Create(center + normal * 0.02f,
                center - normal * 0.02f, CollisionLayers.EnemyTerrain, excluded);
            query.HitBackFaces = false;
            using Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            Assert(hit.Count != 0, $"Original level instance {body.InstanceIndex} participates in physics queries.");
            Assert(hit["collider"].AsGodotObject().GetInstanceId() == body.GetInstanceId(),
                "Physics hit retains the original collision body mapping.");
        }
        GD.Print($"Verified first-triangle ray hits for {bodies.Length} imported solid surface groups.");
    }

    private void VerifyPlayerQueries()
    {
        var world = new GodotPlayerWorldQuery(this);
        FixedVector3 start = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 1, 0));
        FixedVector3 end = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, -1, 0));
        PlayerWorldHit? candidate = world.Trace(start, end);
        Assert(candidate is not null, "Managed player query sees the native imported floor.");
        PlayerWorldHit hit = candidate!.Value;
        Assert(hit.Contact.Normal == new FixedVector3(0, -4096, 0), "Player query returns the original Q12 floor normal.");
        Assert(hit.Contact.Position.Y == 0 && hit.Contact.Distance == 100,
            "Player query converts floor position and whole-unit distance to original coordinates.");
        Assert(hit.InstanceIndex == 0 && hit.ModelIndex == 0 && hit.FaceIndex == 0 && hit.Contact.SurfaceFlags == 0x100000,
            "Player query preserves the hit's source face identity and packed metadata.");
        var frame = new PlayerGroundFrame(start, start, new(0, 4096, 0), 0, 96, false, new(1, 4, 1));
        PlayerGroundResponse landed = PlayerGroundResponder.Resolve(frame, hit.Contact, 4096, 4096, true);
        Assert(landed.Position.Y == -(96 << 12) && landed.Velocity.Y == 0 && (landed.CollisionFlags & 2) != 0,
            "Native floor hits drive the recovered managed landing response.");
        PlayerFallResult solved = PlayerGroundSolver.ResolveFall(frame, 4096, 2, world, new(false, 10, true));
        Assert(solved.Hit?.FaceIndex == 0 && solved.Ground is not null && solved.Movement.Position.Y == -(96 << 12) &&
            solved.Movement.Velocity.Y == 0, "The recovered fall-probe sequence lands through native Godot terrain queries.");
        FixedVector3 walkStart = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 0.96f, 0));
        FixedVector3 walkPosition = walkStart;
        for (int step = 0; step < 10; step++)
        {
            var walkingFrame = new PlayerTerrainFrame(walkPosition, new(8192, 4096, 0), default,
                new(31, 31, 31), 2, 96, 70, false);
            PlayerTerrainStep walking = PlayerTerrainMotion.Step(walkingFrame, world, new(true, new(false, 10, true)));
            Assert(walking.CollisionFlags == 2 && walking.Movement.Position.Y == -(96 << 12),
                "Combined player terrain movement stays grounded on the native floor.");
            walkPosition = walking.Movement.Position;
        }
        Assert(walkPosition.X == unchecked(walkStart.X + 81920), "Native terrain steps preserve the original horizontal movement arithmetic.");
        FixedVector3 nearWall = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0.4f, 0.96f, 0));
        PlayerTerrainStep blocked = PlayerTerrainMotion.Step(new(nearWall, new(8192, 4096, 0), default,
            new(31, 31, 31), 2, 96, 70, false), world, new(true, new(false, 10, true)));
        Assert(blocked.Movement.Position.X == nearWall.X && blocked.Sweep.BodyHit?.FaceIndex == 2 && blocked.CollisionFlags == 3,
            "The combined terrain step blocks horizontal travel against the native wall and retains ground contact.");
        PlayerTerrainStep upward = PlayerTerrainMotion.Step(new(walkStart, new(0, -8192, 0), default,
            new(31, 31, 31), 2, 96, 70, false), world, new(true, new(false, 10, true)));
        Assert(upward.Movement.Position.Y == walkStart.Y && upward.Movement.Velocity.Y == 0 && upward.CollisionFlags == 0x100 &&
            upward.CeilingHit?.FaceIndex == 1, "The combined terrain step cancels upward travel at the native ceiling.");
        Assert(world.Trace(start, start) is null, "Zero-length player queries do not invoke native ray intersection.");
        Assert(world.Trace(end, start) is null, "Managed player queries preserve one-sided floor rejection.");
        Assert(world.Trace(PsxCoordinates.OriginalWorldPosition(origin + new Vector3(-8, 1, 0)),
            PsxCoordinates.OriginalWorldPosition(origin + new Vector3(-8, -1, 0))) is null,
            "Managed player queries exclude enemy-only terrain.");
        PlayerWorldHit? ceiling = world.Trace(start, PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 3, 0)));
        Assert(ceiling?.Contact.Normal == new FixedVector3(0, 4096, 0) && ceiling?.FaceIndex == 1,
            "Managed player queries distinguish the original downward ceiling normal and face.");
        PlayerWorldHit? rotated = world.Trace(PsxCoordinates.OriginalWorldPosition(origin + new Vector3(5, 1, 0)),
            PsxCoordinates.OriginalWorldPosition(origin + new Vector3(3, 1, 0)));
        Assert(rotated is not null && rotated.Value.Contact.Normal == new FixedVector3(4095, 8, 0),
            "Rotated player contacts preserve the original quantized Q12 normal rather than a normalized geometric normal.");
        Assert(rotated?.InstanceIndex == 1 && rotated?.ModelIndex == 1 && rotated?.Contact.SupportsPlayer == true &&
            rotated?.Contact.SurfaceFlags == 0x900000, "Rotated contacts preserve source identity, support and brightness flags.");
    }

    private void VerifyCrawlQueries()
    {
        var world = new GodotPlayerWorldQuery(this);
        FixedVector3 wallPosition = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0.04f, 1, 0));
        var wall = new PlayerCrawlFrame(wallPosition, default, default, new(1, 1, 1), 2, 96,
            new(-4096, 0, 0), new(-4096, 0, 0), new(0, 0, 4096), new(0, 4096, 0), true);
        PlayerCrawlStep resting = PlayerCrawlMotion.Step(wall, world, position => position);
        Assert(resting.GroundHit?.FaceIndex == 2 && resting.SurfaceNormal == new FixedVector3(-4096, 0, 0) &&
            resting.CollisionFlags == 2 && resting.GroundContactGraceUpdates == 4,
            "Crawl support queries hit the native wall with its original surface normal.");
        FixedVector3 wallContact = resting.GroundHit!.Value.Contact.Position;
        Assert(resting.Movement.Position == wallContact + new FixedVector3(-(96 << 12), 0, 0),
            "Native wall support positions the crawler at the recovered horizontal clearance.");
        PlayerCrawlStep traveling = PlayerCrawlMotion.Step(wall with { Position = resting.Movement.Position,
            Velocity = new(0, 0, 65536) }, world, position => position);
        Assert(traveling.CollisionFlags == 2 && traveling.Movement.Velocity.Z == 32768 &&
            traveling.Movement.Position.Z > resting.Movement.Position.Z && traveling.GroundHit?.FaceIndex == 2,
            "The crawl adapter moves along an imported native wall and reacquires its support.");
        FixedVector3 ceilingPosition = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 1.04f, 0));
        var ceiling = wall with { Position = ceilingPosition, SurfaceNormal = new(0, 4096, 0),
            Up = new(0, 4096, 0), Right = new(4096, 0, 0), OnWall = false };
        PlayerCrawlStep overhead = PlayerCrawlMotion.Step(ceiling, world, position => position);
        Assert(overhead.GroundHit?.FaceIndex == 1 && overhead.CollisionFlags == 2 &&
            overhead.Movement.Position.Y == -(104 << 12) && overhead.SurfaceNormal == new FixedVector3(0, 4096, 0),
            "Native ceiling support keeps the crawler below the ceiling at its original clearance.");
        Assert(PlayerWallAttachmentRules.Resolve(new(wallPosition, 4, 1, resting.GroundHit, 96, 212), world) is null,
            "The native floor within 180 units prevents falling-to-wall attachment.");
        FixedVector3 highPosition = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 1.9f, 0));
        PlayerWorldHit? highWall = world.Trace(highPosition,
            PsxCoordinates.OriginalWorldPosition(origin + new Vector3(2, 1.9f, 0)));
        PlayerWallAttachment? attachment = PlayerWallAttachmentRules.Resolve(new(highPosition, 4, 1, highWall, 96, 212), world);
        Assert(attachment is not null && attachment.Value.Normal == new FixedVector3(-4096, 0, 0) &&
            attachment.Value.Position.X == wallContact.X - (96 << 12) && attachment.Value.AnimationId == 227,
            "A native wall contact farther above the floor selects the recovered attachment position and clip.");
    }

    private void VerifyTerrainController()
    {
        var animation = new PhysicsCheckAnimation();
        FixedVector3 start = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 0.96f, 0));
        var player = new PlayerTerrainController(start, 0, new GodotPlayerWorldQuery(this), animation,
            new(true, new(false, 10, true)), _ => 0);
        player.Step(default, 2048);
        Assert(player.IsGrounded && player.Position.Y == -(96 << 12),
            "The terrain controller initializes from the actual imported native floor.");
        var forward = new PlayerInputSample(true, new(-127, 0), false, false, false, false, false, false);
        for (int index = 0; index < 6; index++) player.Step(forward, 2048);
        Assert(player.IsGrounded && player.Position.Z > start.Z && player.Action == PlayerAction.GroundMovement &&
            animation.Playback.ClipId == 21, "Native terrain supports controlled walking and the start-to-walk follow-on.");
        player.Step(default, 2048);
        Assert(player.Action == PlayerAction.Idle && animation.Playback.ClipId is 11 or 12 or 13,
            "Native controlled walking chooses a stop clip after input release.");
        for (int index = 0; index < 20; index++) player.Step(default, 2048);
        Assert(player.IsGrounded && player.Velocity == default && animation.Playback.ClipId == 0,
            "Native stop recovery returns to a supported stationary idle.");
        for (int index = 0; index < 30; index++) player.Step(forward, 2048);
        Assert(player.Action == PlayerAction.Falling && player.Position.Y > -(96 << 12),
            "Leaving the native floor enters the falling controller rather than preserving fake ground.");
    }

    private void VerifySurfaceController()
    {
        var world = new GodotPlayerWorldQuery(this);
        var animation = new PhysicsCheckAnimation();
        FixedVector3 start = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0.04f, 1, 0));
        var player = new PlayerTerrainController(start, 0, world, animation,
            new(true, new(false, 10, true)), _ => 0) { BodyHook = position => position };
        player.AttachToSurface(new(start, new(-4096, 0, 0), 227));
        for (int index = 0; index < 4; index++) player.Step(default, 0);
        Assert(player.IsCrawling && player.OnWall && player.IsGrounded && animation.Playback.ClipId == 19,
            "The surface controller follows attachment to a crawl idle on the imported native wall.");
        FixedVector3 before = player.Position;
        var forward = new PlayerInputSample(true, new(-127, 0), false, false, false, false, false, false);
        for (int index = 0; index < 5; index++) player.Step(forward, 2048);
        Assert(player.Position.Y < before.Y && player.Position.X == before.X && player.IsGrounded &&
            animation.Playback.ClipId == 50 && player.Action == PlayerAction.GroundMovement,
            "Source crawl actions advance up a native wall while keeping its original clearance.");
        player.Step(default, 0);
        Assert(animation.Playback.ClipId == 55 && player.Action == PlayerAction.Idle,
            "Native wall walking selects the recovered crawl stop clip after input release.");
        for (int index = 0; index < 16; index++) player.Step(default, 0);
        Assert(player.Velocity == default && animation.Playback.ClipId == 19 && player.IsGrounded,
            "Native crawl stopping returns to supported idle and removes residual velocity.");
        FixedVector3 ceilingStart = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 1.04f, 0));
        var ceilingAnimation = new PhysicsCheckAnimation();
        var ceiling = new PlayerTerrainController(ceilingStart, 0, world, ceilingAnimation,
            new(true, new(false, 10, true)), _ => 0) { BodyHook = position => position };
        ceiling.AttachToSurface(new(ceilingStart, new(0, 4096, 0), 227));
        for (int index = 0; index < 4; index++) ceiling.Step(default, 2048);
        Assert(ceiling.OnCeiling && ceiling.IsGrounded && ceilingAnimation.Playback.ClipId == 19 &&
            ceiling.Position.Y == -(104 << 12), "The controller keeps a crawl idle beneath the imported native ceiling.");
        for (int index = 0; index < 5; index++) ceiling.Step(forward, 2048);
        Assert(ceiling.Position.Z > ceilingStart.Z && ceiling.Position.Y == -(104 << 12) &&
            ceiling.IsGrounded && ceilingAnimation.Playback.ClipId == 50,
            "Ceiling crawl actions move on the free horizontal plane through native support queries.");
    }

    private void VerifyJumpController()
    {
        var animation = new PhysicsCheckAnimation();
        var player = new PlayerTerrainController(PsxCoordinates.OriginalWorldPosition(origin + new Vector3(0, 0.96f, 0)),
            0, new GodotPlayerWorldQuery(this), animation, new(true, new(false, 10, true)), _ => 0) { BodyHook = at => at };
        player.Step(default, 2048);
        player.Step(default, 2048, buttons: new(PlayerButton.Jump, PlayerButton.Jump));
        Assert(player.Action == PlayerAction.JumpWindup && animation.Playback.ClipId == 210,
            "Native floor support starts the standing jump windup.");
        for (int update = 0; update < 30 && !player.IsCrawling; update++)
            player.Step(default, 2048, buttons: new(PlayerButton.Jump, PlayerButton.None));
        Assert(player.OnCeiling && player.Position.Y == -(104 << 12) && animation.Playback.ClipId == 227,
            "Held jumping attaches through native ceiling contact and its original clearance.");
        for (int update = 0; update < 5; update++) player.Step(default, 2048);
        player.Step(default, 2048, buttons: new(PlayerButton.Jump, PlayerButton.Jump));
        Assert(!player.IsCrawling && player.Action == PlayerAction.Falling && player.Orientation.Basis.M11 == 4096,
            "Jumping from native ceiling support restores free-fall orientation.");
    }

    private sealed class PhysicsCheckAnimation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Run(int clip, int from = 0, int to = -1) =>
            Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 4, from, to);
    }

    private void Hit(Vector3 from, Vector3 to, uint layer, Vector3 normal, bool area)
    {
        using Godot.Collections.Dictionary result = Ray(from, to, layer, area);
        Assert(result.Count != 0, $"Ray on layer {layer} hits imported geometry.");
        Assert(result["normal"].AsVector3().Dot(normal) > 0.99f, "Physics hit normal matches the original surface normal.");
        Assert(area ? result["collider"].AsGodotObject() is ImportedTriggerSurface :
            result["collider"].AsGodotObject() is ImportedCollisionBody, "Ray preserves the solid/trigger surface role.");
        int triangle = result["face_index"].AsInt32();
        int count = result["collider"].AsGodotObject() switch
        {
            ImportedCollisionBody body => body.SourceTriangles.Count,
            ImportedTriggerSurface trigger => trigger.SourceTriangles.Count,
            _ => 0
        };
        Assert(triangle >= 0 && triangle < count, "Physics face index maps back to original face metadata.");
    }

    private void Miss(Vector3 from, Vector3 to, uint layer, string message)
    {
        using Godot.Collections.Dictionary result = Ray(from, to, layer, area: false);
        Assert(result.Count == 0, message);
    }

    private Godot.Collections.Dictionary Ray(Vector3 from, Vector3 to, uint layer, bool area)
    {
        using var query = PhysicsRayQueryParameters3D.Create(from + origin, to + origin, layer);
        query.CollideWithAreas = area;
        query.CollideWithBodies = !area;
        query.HitBackFaces = false;
        return GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    private void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidDataException(message);
    }
}
