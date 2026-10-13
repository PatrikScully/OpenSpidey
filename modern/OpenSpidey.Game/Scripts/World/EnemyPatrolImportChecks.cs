using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Import;
using SourceVector = System.Numerics.Vector3;

namespace OpenSpidey.Game.World;

/// <summary>Isolated original L1A1 patrol evidence, without registering incomplete combat actors.</summary>
internal partial class EnemyPatrolImportChecks : Node3D
{
    private readonly List<PatrolActor> actors = [];
    private Node3D levelGeometry = null!;
    private PsxScene sourceLevel = null!;
    private GodotActorWorldQuery world = null!;
    private int physicsFrames;
    public bool Finished { get; private set; }
    public int SourceUpdates { get; private set; }

    // Independent values read from the original L1A1_T.trg, not produced by the route controller.
    private static readonly (ushort Node, TriggerPosition Start, ushort FirstNode, TriggerPosition FirstPoint)[] Expected =
    [
        (9, new(-18937, 1242, -19681), 7, new(-19019, 1242, -19778)),
        (10, new(-17199, -422, -14734), 2, new(-17005, -422, -14732)),
        (16, new(-18183, 2534, -27104), 15, new(-18160, 2534, -26780)),
        (19, new(-18872, 3402, -23663), 21, new(-18839, 3402, -23747))
    ];

    public static EnemyPatrolImportChecks Create(TriggerFile triggers, byte[] enemyModelBytes,
        Node3D levelGeometry, PsxScene sourceLevel)
    {
        var check = new EnemyPatrolImportChecks
        {
            Name = "OriginalEnemyPatrolVerification", levelGeometry = levelGeometry, sourceLevel = sourceLevel
        };
        try
        {
            check.world = new(check, triggers);
            PsxScene modelScene = PsxSceneReader.Read(enemyModelBytes);
            IAnimationPoseSource animations = PsxMatrixAnimationReader.Read(enemyModelBytes)
                ?? throw new InvalidDataException("The original henchman matrix animation data is absent.");
            if (animations.Parents.Count != 15 || animations.ClipCount != 41)
                throw new InvalidDataException("L1A1 type 312 patrol requires the actual 15-part, 41-clip henchman model.");
            IReadOnlyList<DecodedTexture> textures = PsxTextureReader.Read(enemyModelBytes);
            TriggerActorDefinition[] definitions = triggers.Nodes.Where(node => node.TypeCode is 1 or 7)
                .Select(TriggerActorDefinition.Read)
                .Where(definition => definition.ActorType == 312 && definition.InitializeOnLoad).ToArray();
            if (!definitions.Select(definition => definition.NodeId).Order().SequenceEqual(Expected.Select(value => value.Node).Order()))
                throw new InvalidDataException("The original L1A1 startup thug factory records changed.");
            foreach (var expected in Expected)
            {
                TriggerNode node = triggers.Nodes[expected.Node];
                TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
                if (definition.Position != expected.Start || definition.Links[0] != expected.FirstNode ||
                    TriggerPlacement.Read(triggers.Nodes[expected.FirstNode]).Position != expected.FirstPoint ||
                    !definition.BehaviorData.Span[..10].SequenceEqual(new byte[] { 0, 0x21, 90, 0, 3, 0x21, 10, 0, 0, 0x41 }))
                    throw new InvalidDataException($"Enemy node {expected.Node}: source position, first waypoint or startup script changed.");
                ImportedAnimatedModel model = ImportedAnimatedModel.Build(modelScene, textures, animations, playerPartOrder: false);
                model.AdvanceAutomatically = false;
                model.Name = "SourceHenchman";
                var spatial = new Node3D { Name = $"OriginalThug{expected.Node}" };
                spatial.AddChild(model);
                check.AddChild(spatial);
                check.actors.Add(new PatrolActor(check, triggers, definition, expected.FirstNode, expected.FirstPoint, spatial, model));
            }
            return check;
        }
        catch { check.Free(); throw; }
    }

    public override void _Ready()
    {
        if (Engine.PhysicsTicksPerSecond != 60 || !levelGeometry.IsInsideTree())
            throw new InvalidOperationException("Original patrol checks require imported level collision and the 60 Hz native clock.");
        ProcessPhysicsPriority = 15;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Finished || ++physicsFrames % 2 != 0) return;
        try
        {
            SourceUpdates++;
            foreach (PatrolActor actor in actors) actor.Advance(SourceUpdates);
            if (SourceUpdates < 128) return;
            foreach (PatrolActor actor in actors) actor.VerifyFinished();
            Finished = true;
            SetPhysicsProcess(false);
            GD.Print("Verified original L1A1 thug nodes 9/10/16/19: 128 updates at 30 Hz, animation before AI with two elapsed ticks, " +
                "original startup scripts, real imported floor faces, first-waypoint arrivals, persistent visible henchman meshes and changing source poses.");
        }
        catch (Exception exception)
        {
            SetPhysicsProcess(false);
            GD.PushError($"Original enemy patrol verification failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private int Ground(FixedVector3 position, int above, int below)
    {
        int height = world.GetGroundHeight(position, above, below, out ActorBodyMotion? support);
        if (height == -1) return -1;
        GodotActorWorldHit hit = world.LastHit ?? throw new InvalidDataException("Native enemy ground has no original face.");
        if (!levelGeometry.IsAncestorOf(hit.Collider) || support is not null)
            throw new InvalidDataException("The original patrol floor query hit a replacement floor or unsupported moving actor.");
        return height;
    }

    private void VerifySourceFloor(FixedVector3 position, int nativeHeight, string evidence, int above = 300, int below = 600)
    {
        // Reconstruct the nearest downward face plane from original instance/vertex data, independently
        // of Godot's shape and ray result. The first patrol legs lie on their original flat roof floors.
        double x = position.X / 4096.0, z = position.Z / 4096.0;
        double start = position.Y / 4096.0 - above, end = position.Y / 4096.0 + below;
        double? nearest = null;
        foreach (PsxInstance instance in sourceLevel.Instances)
        {
            if (!OriginalCollisionFilter.IsInstanceEnabled(instance.Flags)) continue;
            var rotation = FixedJointTransform.FromJoint(new((short)instance.Angles.X,
                (short)instance.Angles.Y, (short)instance.Angles.Z, 0, 0, 0));
            PsxModel model = sourceLevel.Models[instance.ModelIndex];
            foreach (CollisionTriangle triangle in CollisionGeometry.Read(model))
            {
                if (!OriginalCollisionFilter.Accepts(triangle.PackedMetadata, CollisionQuery.Player)) continue;
                Point a = Transform(triangle.A), b = Transform(triangle.B), c = Transform(triangle.C);
                double denominator = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
                if (denominator == 0) continue;
                double first = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / denominator;
                double second = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / denominator;
                if (first < 0 || second < 0 || first + second > 1) continue;
                double normalY = rotation.M10 * triangle.FixedNormal.X + rotation.M11 * triangle.FixedNormal.Y + rotation.M12 * triangle.FixedNormal.Z;
                if (normalY >= 0) continue;
                double height = first * a.Y + second * b.Y + (1 - first - second) * c.Y;
                if (height < start || height > end || nearest is { } current && height >= current) continue;
                nearest = height;
            }
            Point Transform(SourceVector vertex) => new(
                (rotation.M00 * (double)vertex.X + rotation.M01 * (double)vertex.Y + rotation.M02 * (double)vertex.Z + instance.Position.X) / 4096,
                (rotation.M10 * (double)vertex.X + rotation.M11 * (double)vertex.Y + rotation.M12 * (double)vertex.Z + instance.Position.Y) / 4096,
                (rotation.M20 * (double)vertex.X + rotation.M21 * (double)vertex.Y + rotation.M22 * (double)vertex.Z + instance.Position.Z) / 4096);
        }
        if (nearest is null && nativeHeight == -1) return;
        if (nativeHeight == -1) throw new InvalidDataException($"{evidence}: native query missed an original floor plane.");
        if (nearest is not { } expected) throw new InvalidDataException($"{evidence}: no original floor triangle covers the source position.");
        float meters = (float)(-expected / 100);
        // Native collision coordinates use float. Allow its representable endpoint spacing and fixed-int truncation,
        // rather than a gameplay-unit tolerance that could hide a misplaced actor or substitute floor.
        double quantization = Math.Abs(MathF.BitIncrement(meters) - meters) * 409600.0 * 4 + 1;
        if (Math.Abs(nativeHeight - expected * 4096) > quantization)
            throw new InvalidDataException($"{evidence}: native floor {nativeHeight} differs from original triangle plane {expected * 4096} (float bound {quantization}).");
    }

    private readonly record struct Point(double X, double Y, double Z);

    private sealed class PatrolActor : IThugPatrolHost, IThugInitializationHost, IActorHeightHost, IActorScriptHost, IActorScriptVariableWorld
    {
        private readonly EnemyPatrolImportChecks owner;
        private readonly TriggerFile triggers;
        private readonly TriggerActorDefinition definition;
        private readonly ushort firstNode;
        private readonly FixedVector3 firstPoint;
        private readonly Node3D spatial;
        private readonly ImportedAnimatedModel model;
        private readonly ActorProcedureState actor;
        private readonly ActorProcedureList procedures = new();
        private readonly ThugState thug;
        private readonly ThugStateFlags stateFlags = new();
        private readonly ActorWaypointRoute route;
        private readonly ActorHeightController height;
        private readonly ActorScriptRuntime runtime;
        private readonly ActorScriptVariables variables;
        private readonly FixedJointTransform[] previousPose = new FixedJointTransform[15];
        private bool placed;
        private bool arrived;
        private bool moved;
        private bool animated;
        private int animationUpdates;
        private int sourceUpdates;
        public ActorScriptState ScriptState { get; }

        public PatrolActor(EnemyPatrolImportChecks owner, TriggerFile triggers, TriggerActorDefinition definition,
            ushort firstNode, TriggerPosition firstPoint, Node3D spatial, ImportedAnimatedModel model)
        {
            this.owner = owner; this.triggers = triggers; this.definition = definition;
            this.firstNode = firstNode;
            this.firstPoint = new FixedVector3(firstPoint.X, firstPoint.Y, firstPoint.Z) << 12;
            this.spatial = spatial; this.model = model;
            thug = new(definition.NodeId, 0x101);
            thug.SetThugType(definition.ActorType);
            actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), UseLocalVelocity = true
            }) { BehaviorFlags = thug.BehaviorFlags };
            height = new(actor, this) { Clearance = thug.SurfaceClearance, QueryInterval = thug.Field212 };
            ScriptState = new() { ActorType = definition.ActorType, NodeId = definition.NodeId };
            variables = new(ScriptState, this) { Position = actor.Body.Position };
            route = new(triggers, definition.NodeId, ParseWaypoint);
            runtime = new(new(definition.BehaviorData, checked(triggers.Nodes[definition.NodeId].SourceOffset + definition.ObjectDataOffset + 18)), this);
            runtime.Start(new(LevelId: 0x101));
            if (variables.Health != 90 || variables.Damage != 10 || thug.ModelName != "henchman" || thug.SurfaceClearance != 100)
                throw new InvalidDataException("Original enemy startup did not configure its source model, health, damage and floor clearance.");
            ApplyTransform();
        }

        public void Advance(int update)
        {
            sourceUpdates++;
            AnimationPlaybackState before = model.Playback;
            AnimationPlaybackState expected = ActorAnimationPlayback.Advance(before, 2).State;
            model.AdvanceTicks(2);
            animationUpdates++;
            if (model.Playback != expected || animationUpdates != sourceUpdates)
                throw new InvalidDataException("Enemy animation lost its two-tick source clock before AI.");
            model.VerifyCurrentPose();
            for (int part = 0; part < previousPose.Length; part++)
            {
                FixedJointTransform pose = model.GetPartPose(part);
                if (update > 1 && previousPose[part] != pose) animated = true;
                previousPose[part] = pose;
            }
            FixedVector3 beforeMovement = actor.Body.Position;
            if ((stateFlags.Check(thug.State) & 4) == 0) actor.Body.Integrate(2);
            procedures.Execute(2);
            if (animationUpdates != update || AnimationFrame != expected.Frame)
                throw new InvalidDataException("Thug AI read an animation frame before its original UpdateFrame stage.");
            switch (thug.State)
            {
                case 0:
                    ThugInitializationBehavior.Step(thug, route, this, update);
                    break;
                case 1:
                    ThugGuardBehavior.Step(thug, this, Random);
                    break;
                case 2:
                    ThugPatrolBehavior.Step(thug, actor, procedures, route, this, Random);
                    height.RouteFlags = route.Flags;
                    if (height.SetHeight(false, 100, 600, 2) == 0)
                        throw new InvalidDataException("An original unalerted patrol lost its real floor.");
                    break;
                default: throw new NotSupportedException($"Enemy patrol check reached combat state {thug.State}.");
            }
            if (actor.Body.Position.X != beforeMovement.X || actor.Body.Position.Z != beforeMovement.Z) moved = true;
            if (!arrived && placed && actor.Body.Position.X == firstPoint.X && actor.Body.Position.Z == firstPoint.Z)
            {
                int ground = owner.Ground(actor.Body.Position, 300, 600);
                if (ground == -1) throw new InvalidDataException("An original first waypoint has no imported ground face.");
                owner.VerifySourceFloor(actor.Body.Position, ground, $"Enemy {definition.NodeId}, first waypoint {firstNode}");
                owner.VerifySourceFloor(actor.Body.Position, height.GroundHeight,
                    $"Enemy {definition.NodeId}, cached ground at first waypoint {firstNode}");
                if (actor.Body.Position.Y + (thug.SurfaceClearance << 12) != height.GroundHeight)
                    throw new InvalidDataException("Patrol endpoint lost its exact source cached floor clearance.");
                arrived = true;
                GD.Print($"Original thug {definition.NodeId} reached waypoint {firstNode} at source update {update}: " +
                    $"source X/Z=({firstPoint.X >> 12},{firstPoint.Z >> 12}), body Y={actor.Body.Position.Y}, original ground Y={ground}.");
            }
            ApplyTransform();
            if (!spatial.IsVisibleInTree() || !model.IsVisibleInTree() || model.AdvanceAutomatically)
                throw new InvalidDataException("Original patrol lost its visible actor-owned model.");
        }

        public void VerifyFinished()
        {
            if (sourceUpdates != 128 || animationUpdates != 128 || !placed || !arrived || !moved || !animated ||
                model.GetNode<Node3D>("MatrixParts").GetChildren().OfType<MeshInstance3D>().All(mesh => mesh.Mesh is null))
                throw new InvalidDataException($"Enemy {definition.NodeId}: original movement, floor, waypoint or animated visible mesh evidence is incomplete.");
        }

        private void ApplyTransform()
        {
            spatial.Position = PsxCoordinates.FixedWorldPosition(actor.Body.Position);
            spatial.Basis = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(actor.Body.Angles.X,
                actor.Body.Angles.Y, actor.Body.Angles.Z, 0, 0, 0))).Basis;
        }

        private void ParseWaypoint(ActorScriptCursor script)
        {
            variables.Position = actor.Body.Position;
            runtime.ParseAt(script, new(LevelId: 0x101));
        }
        public FixedVector3 Position { get => actor.Body.Position; set { actor.Body.Position = value; variables.Position = value; } }
        public int FindGroundHeight(int above, int below) => owner.Ground(Position, above, below);
        public void StoreGroundPlacement(int bodyHeight, int groundHeight)
        {
            height.GroundHeight = groundHeight;
            owner.VerifySourceFloor(Position, groundHeight, $"Enemy {definition.NodeId}, initialization");
            if (bodyHeight != groundHeight - (100 << 12)) throw new InvalidDataException("Original enemy floor clearance changed.");
            GodotActorWorldQueryImportChecks.VerifyPatrolCapsule(owner.world, actor, firstPoint, definition.NodeId, VerifyGroundSample);
            if (definition.NodeId == 9) GodotActorWorldQueryImportChecks.VerifyLedge(owner.world, actor, VerifyGroundSample);
            placed = true;
        }
        private void VerifyGroundSample(FixedVector3 position, int above, int below, int nativeHeight, string evidence) =>
            owner.VerifySourceFloor(position, nativeHeight, evidence, above, below);
        public int SetHeight(bool initial, int clearance, int range)
        {
            int result = height.SetHeight(initial, clearance, range, 2);
            if (result == 0)
                throw new InvalidDataException("Source thug initialization failed its real height query.");
            return result;
        }
        public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support)
        {
            return owner.world.GetGroundHeight(position, above, below, out support);
        }
        public int AnimationId => model.Playback.ClipId;
        public int AnimationFrame => model.Playback.Frame;
        public byte AnimationFinished
        {
            get => model.Playback.Finished;
            set
            {
                if (value != model.Playback.Finished)
                    throw new NotSupportedException("This henchman check does not override the original model's animation finish state.");
            }
        }
        public void Neutralize() => actor.Neutralize(procedures);
        public void RunAnimation(ushort clip, int from = 0, int to = -1) => model.RunClip(clip, from, to);
        public void CycleAnimation(ushort clip, sbyte direction = 1) => model.CycleClip(clip, direction);
        public bool MonitorSpitPlease() => throw new NotSupportedException("The isolated unalerted patrol check has no native spit particle host.");
        public FixedVector3 GetPathPoint(int sourceIndex) => throw new NotSupportedException("The original unalerted patrol has no combat path points.");
        public void ClearAttackOwnership()
        {
            if (thug.AlertTicks != 0) throw new NotSupportedException("Combat attack ownership has not been connected in this isolated check.");
        }
        public bool DetermineFightState() => throw new NotSupportedException("Combat selection has not been connected in this isolated check.");
        public short ReadVariable(ushort variable, ActorScriptCursor operands) => variables.Read(variable, operands);
        public void WriteVariable(ushort variable, ActorScriptCursor operands)
        {
            variables.Write(variable, operands);
            actor.Body.Position = variables.Position;
        }
        public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
        {
            if (route.TryExecuteCommand(opcode, operands)) return true;
            if (opcode == 0x470C)
            {
                thug.SetParamByIndex(operands.ReadScriptValue(ReadVariable), operands.ReadScriptValue(ReadVariable));
                return true;
            }
            throw new NotSupportedException($"Original enemy script command 0x{opcode:X4} has not been connected in this isolated check.");
        }
        public ActorScriptCursor ScriptAtNode(ushort nodeId)
        {
            TriggerNode node = triggers.Nodes[nodeId];
            TriggerScriptFragment script = TriggerPlacement.Read(node).ReadScriptAfterAngles(node);
            return new(script.Data, script.SourceOffset);
        }
        public bool PlayerCeilingCheck(short x, short z) => throw new NotSupportedException("The isolated patrol has no player actor.");
        public void FlashCompass() => throw new NotSupportedException("The original unalerted enemy has no compass command.");
        public void SendDeathPulse() => throw new NotSupportedException("Enemy death is outside this isolated patrol check.");
        public void Retire() => throw new NotSupportedException("Enemy retirement is outside this isolated patrol check.");
        public int Difficulty => 1;
        public int MovieInputPolls => 0;
        public FixedVector3? PlayerPosition => null;
        public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => throw new NotSupportedException("The isolated patrol has no player sight target.");
        public int Random(int range)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
            return 0; // Deterministically exercise source clip 22 at waypoint pauses, not spit clip 21.
        }
        public ushort FirstLink(ushort nodeId)
        {
            IReadOnlyList<ushort> links = MissionRuntime.ReadLinks(triggers.Nodes[nodeId]);
            return links.Count == 0 ? (ushort)0 : links[0];
        }
        public void SelectModel(uint checksum) => throw new NotSupportedException("The original L1A1 thug startup does not select a checksum model.");
    }
}
