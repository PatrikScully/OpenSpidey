using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class GodotActorWorldQueryImportChecks
{
    private sealed record Ray(FixedVector3 From, FixedVector3 To, bool Clear, GodotActorWorldHit? Hit);
    private sealed record GroundSample(FixedVector3 Position, int Above, int Below, int Height);
    private sealed class Recorder(GodotActorWorldQuery world) : IActorPathHost
    {
        public List<Ray> Rays { get; } = [];
        public List<GroundSample> Ground { get; } = [];
        public int CollisionCheckMode { get => world.CollisionCheckMode; set => world.CollisionCheckMode = value; }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, ref FixedVector3 hit)
        {
            bool clear = world.LineOfSight(from, to, flags, ref hit);
            Rays.Add(new(from, to, clear, world.LastHit));
            return clear;
        }
        public int GroundHeight(FixedVector3 position, int above, int below)
        {
            int height = world.GroundHeight(position, above, below);
            Ground.Add(new(position, above, below, height));
            return height;
        }
        public FixedVector3 WaypointPosition(int node) => world.WaypointPosition(node);
    }

    public static void VerifyPatrolCapsule(GodotActorWorldQuery world, ActorProcedureState actor,
        FixedVector3 destination, ushort node, Action<FixedVector3, int, int, int, string> verifyGround)
    {
        // Compiled original VectorNormal and Q12 operations, evaluated at the four original actor/first-waypoint X/Z pairs.
        int[] expected = node switch
        {
            9 => [-77367652, -80378776, -78100124, -81245288, 614400, 0],
            10 => [-70754229, -60353614, -69345355, -60339122, 0, 614400],
            16 => [-74499318, -111324359, -74361610, -109384505, 614400, 0],
            19 => [-77411987, -96637748, -77052269, -97553612, 614400, 0],
            _ => throw new InvalidDataException("Unknown original patrol capsule fixture.")
        };
        var recorder = new Recorder(world);
        var path = new ActorPathController(actor, recorder) { Radius = 150, Clearance = 100, StoredBodyY = actor.Body.Position.Y };
        ActorPathCheckResult result = path.PathCheck(actor.Body.Position, destination, true, 55);
        Ray center = recorder.Rays[0];
        if (center.From.X != expected[0] || center.From.Z != expected[1] || center.To.X != expected[2] || center.To.Z != expected[3] ||
            center.From.Y != actor.Body.Position.Y || center.To.Y != actor.Body.Position.Y || recorder.Rays.Count != 3)
            throw new InvalidDataException($"Enemy {node}: native capsule rays lost the original compiled route endpoints.");
        if (recorder.Rays[1].From.X != expected[0] + (expected[4] >> 1) || recorder.Rays[1].From.Z != expected[1] + (expected[5] >> 1) ||
            recorder.Rays[2].From.X != expected[0] - (expected[4] >> 1) || recorder.Rays[2].From.Z != expected[1] - (expected[5] >> 1))
            throw new InvalidDataException("Native capsule side rays lost the original half-radius offsets.");
        VerifyResult(recorder, result, verifyGround, $"Enemy {node}, original first-destination capsule");
        GD.Print($"Verified original enemy {node} capsule: 3 real native rays, {recorder.Ground.Count} source ground samples, result {result.Code}.");
    }

    public static void VerifyLedge(GodotActorWorldQuery world, ActorProcedureState actor,
        Action<FixedVector3, int, int, int, string> verifyGround)
    {
        FixedVector3 start = actor.Body.Position;
        FixedVector3 floor = start with { Y = unchecked(start.Y + (100 << 12)) };
        (int X, int Z)[] directions = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)];
        foreach (var direction in directions)
            for (int distance = 250; distance <= 16000; distance += 250)
            {
                FixedVector3 offset = new FixedVector3(direction.X * distance, 0, direction.Z * distance) << 12;
                FixedVector3 sample = floor + offset;
                int height = world.GroundHeight(sample, 55, 55);
                if (height != -1) continue;
                verifyGround(sample, 55, 55, height, "Original roof ledge");
                var recorder = new Recorder(world);
                var path = new ActorPathController(actor, recorder) { Radius = 150, Clearance = 100, StoredBodyY = start.Y };
                ActorPathCheckResult result = path.PathCheck(start, start + offset, true, 55);
                VerifyResult(recorder, result, verifyGround, "Original roof ledge capsule");
                if (result.Code == 0) throw new InvalidDataException("A real original roof gap was accepted as a supported path.");
                GD.Print($"Verified original roof ledge at source X/Z=({sample.X >> 12},{sample.Z >> 12}): " +
                    $"native ground -1, capsule result {result.Code}, {recorder.Rays.Count} real obstacle rays and {recorder.Ground.Count} ground samples.");
                return;
            }
        throw new InvalidDataException("No genuine original roof boundary was found for native ledge verification.");
    }

    private static void VerifyResult(Recorder recorder, ActorPathCheckResult result,
        Action<FixedVector3, int, int, int, string> verifyGround, string evidence)
    {
        foreach (GroundSample sample in recorder.Ground)
            verifyGround(sample.Position, sample.Above, sample.Below, sample.Height, evidence);
        if (recorder.Rays.All(ray => ray.Clear))
        {
            int expected = recorder.Ground.Any(sample => sample.Height == -1) ? 1 : 0;
            if (recorder.Ground.Count == 0 || result.Code != expected)
                throw new InvalidDataException($"{evidence}: clear native capsule skipped genuine source ground-gap samples.");
        }
        else
        {
            if (result.Code is not (2 or 4) || recorder.Rays.Where(ray => !ray.Clear).Any(ray => ray.Hit is null ||
                ray.Hit.SourceAsset is null || !OriginalCollisionFilter.Accepts(ray.Hit.Triangle.PackedMetadata, CollisionQuery.Player)))
                throw new InvalidDataException($"{evidence}: blocked native capsule has no actual accepted original face contact.");
        }
    }

    public static async Task VerifyPlatformAsync(Node3D owner, GodotActorRegistry sessionActors)
    {
        var registry = new GodotActorRegistry
        {
            Name = "ActorGroundSupportVerification", Triggers = sessionActors.Triggers,
            ObjectLibrary = sessionActors.ObjectLibrary, ObjectTextures = sessionActors.ObjectTextures,
            ClaimObjectModel = sessionActors.ClaimObjectModel, Environment = sessionActors.Environment,
            Sounds = sessionActors.Sounds, SoundListener = sessionActors.SoundListener
        };
        owner.AddChild(registry);
        registry.SetPhysicsProcess(false);
        GodotPlatformActor? platform = null;
        registry.Spawned += actor => platform = actor as GodotPlatformActor;
        try
        {
            registry.Spawn(sessionActors.Triggers.Nodes[170]);
            if (platform is null) throw new InvalidDataException("Original pressure platform 170 did not spawn for actor support verification.");
            platform.Advance();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            var query = new GodotActorWorldQuery(owner, sessionActors.Triggers);
            var bodies = platform.GetNode<Node3D>("WorldCollision").GetChildren().OfType<ImportedCollisionBody>();
            bool verified = false;
            foreach (ImportedCollisionBody body in bodies)
                foreach (CollisionTriangle triangle in body.SourceTriangles)
                {
                    if (triangle.FixedNormal.Y >= 0) continue;
                    Vector3 center = body.GlobalTransform * PsxCoordinates.WorldPosition((triangle.A + triangle.B + triangle.C) / 3);
                    FixedVector3 point = PsxCoordinates.OriginalWorldPosition(center);
                    int ground = query.GetGroundHeight(point, 10, 10, out ActorBodyMotion? support);
                    if (ground == -1 || query.LastHit?.SupportId != platform.GetInstanceId()) continue;
                    if (support is null || support.Position != platform.Motion.Position || support.Velocity != platform.Motion.Velocity)
                        throw new InvalidDataException("Native actor ground lost the actual pressure mechanism's body-motion owner.");
                    platform.Motion.Velocity = new(4096, -8192, 0);
                    query.GetGroundHeight(point, 10, 10, out ActorBodyMotion? refreshed);
                    if (!ReferenceEquals(support, refreshed) || support.Velocity != platform.Motion.Velocity)
                        throw new InvalidDataException("Native actor ground replaced or failed to refresh its supporting body's source snapshot.");
                    var grounded = new ActorProcedureState(new ActorBodyMotion { Position = point with { Y = point.Y - (100 << 12) } });
                    var heightController = new ActorHeightController(grounded, query) { Clearance = 100 };
                    if (heightController.SetHeight(true, 10, 10, 2) != 2 || !ReferenceEquals(heightController.Support, support) ||
                        heightController.GroundHeight != ground - 8192 || grounded.Body.Position.Y != ground - 8192 - (100 << 12))
                        throw new InvalidDataException("Recovered actor height did not add its real moving support's vertical source velocity and snap at the support threshold.");
                    FixedVector3 rayStart = point with { Y = point.Y - (10 << 12) }, rayEnd = point with { Y = point.Y + (10 << 12) };
                    FixedVector3 hit = default;
                    if (query.LineOfSight(rayStart, rayEnd, 0, ref hit) || query.LastHit?.SupportId != platform.GetInstanceId())
                        throw new InvalidDataException("Native actor LOS skipped its included original environmental platform.");
                    query.LineOfSight(rayStart, rayEnd, 0x100000, ref hit);
                    if (query.LastHit?.SupportId == platform.GetInstanceId())
                        throw new InvalidDataException("Native actor LOS ignored source environmental-object exclusion.");
                    await ActorLaserImportChecks.VerifyQueryPolicyAsync((Node3D)owner, sessionActors.Triggers, platform);
                    platform.Lifecycle.Kill();
                    query.GetGroundHeight(point, 10, 10, out refreshed);
                    if (query.LastHit?.SupportId == platform.GetInstanceId() || refreshed is not null && ReferenceEquals(refreshed, support))
                        throw new InvalidDataException("Native actor ground retained a dead mechanism's collision or support owner.");
                    verified = true;
                    break;
                }
            if (!verified) throw new InvalidDataException("No actual original pressure-platform face supported a native actor ground query.");
            GD.Print("Verified native actor query ownership: original platform 170 ground, stable live source-velocity snapshot, " +
                "environment inclusion/exclusion and nearest accepted hit after dead-support rejection.");
        }
        finally { registry.QueueFree(); }
    }
}
