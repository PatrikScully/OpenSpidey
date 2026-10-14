using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original mechanism scripts drive a model and its source collision geometry at the source update rate.</summary>
public partial class GodotPlatformActor : GodotScriptActor, IActorScriptHost, IPlayerSupportActor,
    IGodotEnvironmentalActor, IPlatformDamageHost, IGodotSourceActorVisibility
{
    private GodotActorRegistry registry = null!;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Environment;
    private Node3D? collision;
    private MeshInstance3D? mesh;
    private PsxAssetLibrary? modelLibrary;
    private PsxMaterialLibrary? modelMaterials;
    private ArrayMesh? modelMesh;
    private readonly List<Shape3D> collisionShapes = [];
    private bool resourcesDisposed;
    private ushort bodyFlags = 0x216;
    private bool trodden;
    public PlatformMotion Motion { get; } = new();
    public int UpdatesSinceTrodden { get; private set; }
    public uint? ModelChecksum { get; private set; }
    public int ModelIndex { get; private set; } = -1;
    public bool GravityEnabled { get; private set; }
    public bool ResourcesDisposed => resourcesDisposed;
    public MeshInstance3D? SourceMesh => mesh;
    public PsxAssetLibrary SourceModelLibrary => modelLibrary ??
        throw new InvalidOperationException("The platform has not selected an original model.");
    public PsxAssetLibrary? SourceVisibilityBank => modelLibrary;
    public ushort SourceVisibilityFlags => Variables.ItemFlags;
    public void SetSourceVisibilityFlags(ushort flags) { Variables.ItemFlags = flags; ApplyTransform(); }
    public ushort BodyFlags => (ushort)(bodyFlags |
        (Lifecycle.Phase == ActorLifePhase.Suspended ? 1 : 0) |
        (Lifecycle.Phase == ActorLifePhase.PendingDeletion ? 0x80 : 0));
    public ActorCollisionBody CollisionSnapshot => new(this, 402, SourcePosition,
        Variables.CollisionRadius, BodyFlags, Variables.Health);
    /// <summary>Temporarily excludes the real body from source contact queries (CBody flag 100).</summary>
    public void SetCollisionExcluded(bool excluded)
    {
        if (excluded) bodyFlags |= 0x100;
        else bodyFlags &= unchecked((ushort)~0x100);
    }
    public PlayerKickObject KickObject => new(this, 402, Motion.Position, 0);
    public override FixedVector3 SourcePosition => Motion.Position;
    public ActorBodyMotion SupportMotion => Motion.Body;
    public PlayerSupportBody SupportState => new(GetInstanceId(), Motion.Velocity,
        Lifecycle.Phase is ActorLifePhase.Dead or ActorLifePhase.PendingDeletion);
    public uint SoundHandle { get; private set; }
    public short SoundUpdateInterval { get; private set; } = -1;
    private SoundChannelController Sounds => registry.Sounds ?? throw new InvalidOperationException("Platform sounds are not connected.");

    internal static GodotPlatformActor Create(TriggerNode node, GodotActorRegistry registry)
    {
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != 402) throw new InvalidDataException("A platform requires source actor type 402.");
        var actor = new GodotPlatformActor { Name = $"Platform{node.Id}", registry = registry, Definition = definition };
        actor.ScriptState = new() { ActorType = 402, NodeId = node.Id, Active = true };
        // CBody and CBaddy retain body flag2; the platform constructor clears item flag2.
        actor.Lifecycle = new(!definition.ClearsBodyFlag2, startsSuspended: false);
        if (definition.ClearsBodyFlag2) actor.bodyFlags &= unchecked((ushort)~2);
        actor.Motion.Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        actor.Motion.Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
        actor.Variables = new(actor.ScriptState, new VariableWorld(actor, registry))
            { Position = actor.Motion.Position, ItemFlags = 0x111 };
        actor.Variables.Write(0x2125, new(new byte[] { 0, 0, 32, 0 }));
        actor.Runtime = new(new(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
        return actor;
    }

    internal override void Begin() => ApplyTransform();

    internal override void Advance()
    {
        SourceUpdates++;
        bodyFlags &= unchecked((ushort)~4); // CBody::EveryFrame consumes the initial timing seed.
        Motion.AdvanceScale(2);
        Motion.Acceleration = default;
        if (!trodden) UpdatesSinceTrodden = unchecked(UpdatesSinceTrodden + 1);
        Runtime.AdvancePlatform(registry.Environment);
        Motion.UpdateDestination(Variables.Attributes[0]);
        if (GravityEnabled) Motion.Acceleration += registry.Gravity;
        Motion.Body.ScriptWaitTicks = ScriptState.WaitTicks;
        AdvanceSourcePhysics(2, registry.Environment.LevelId);
        Variables.Position = Motion.Position;
        ApplyTransform();
        if (SoundUpdateInterval != -1 && registry.SourceUpdates % SoundUpdateInterval == 0)
            Sounds.UpdatePosition(SoundHandle, Motion.Position, registry.SoundListener());
        trodden = false;
    }

    private void AdvanceSourcePhysics(int ticks, int levelId)
    {
        Motion.Integrate(ticks, Variables.Attributes[2], levelId);
        ScriptState.WaitTicks = Motion.Body.ScriptWaitTicks;
    }

    public void NotifyTrodden() { trodden = true; UpdatesSinceTrodden = 0; }

    public bool Hit(ActorHit hit) => PlatformDamage.Hit(Variables, hit, this);
    void IPlatformDamageHost.Die() => Retire();
    int IPlatformDamageHost.Random(int range) => registry.RandomSource(range);
    void IPlatformDamageHost.PlayBreakSound(int sound) =>
        Sounds.PlayAt((uint)sound, Motion.Position, registry.SoundListener());
    void IPlatformDamageHost.Shatter(bool checkSeen, bool markProcessed) =>
        registry.ShatterPlatform(this, checkSeen, markProcessed);

    public short ReadVariable(ushort variable, ActorScriptCursor operands) => variable == 0x2200
        ? unchecked((short)UpdatesSinceTrodden) : Variables.Read(variable, operands);

    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    {
        switch (variable)
        {
            case 0x2123:
                GravityEnabled = operands.ReadWord() != 0;
                break;
            case 0x2124: SelectModel(operands.ReadWord()); break;
            case 0x2134:
                Motion.Velocity = new FixedVector3(operands.ReadScriptValue(ReadVariable),
                    operands.ReadScriptValue(ReadVariable), operands.ReadScriptValue(ReadVariable)) << 12;
                break;
            case 0x2137: Motion.Angles = ReadAngles(operands); break;
            case 0x2127: Motion.AngularVelocity = ReadAngles(operands); break;
            case 0x2128: Motion.AngularAcceleration = ReadAngles(operands); break;
            default:
                Variables.Write(variable, operands);
                Motion.Position = Variables.Position;
                break;
        }
    }

    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotActorLinkEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorDirectEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if (GodotActorSpeechCommands.TryExecute(registry, opcode, operands, ScriptState, environment, SourcePosition)) return true;
        if (opcode == 0x42B9 && GodotActorPlayerScriptCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Motion.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        switch (opcode)
        {
            case 0x42B5:
                return ActorEnvironmentChunkCommand.TryExecute(opcode, operands, registry.EnvironmentChunks ??
                    throw new InvalidOperationException("Source environment chunks require the actual scene chunk producer."));
            case 0x4280:
                bool continued = ActorTimedMotionCommands.Execute(ScriptState, Motion.Body,
                    operands, ReadVariable, environment, ticks => AdvanceSourcePhysics(ticks, environment.LevelId));
                Variables.Position = Motion.Position;
                return continued;
            case 0x428E: case 0x4290:
                // These inherited commands read a raw word, without variable redirection.
                Sounds.Play(ActorScriptSoundCode.Global(operands.ReadWord(), opcode == 0x428E));
                return true;
            case 0x4205: Retire(); return true;
            case 0x4203: Variables.ItemFlags &= unchecked((ushort)~1); return true;
            case 0x4204: Variables.ItemFlags |= 1; return true;
            case 0x4220: Motion.MoveTo(ReadPosition(operands)); return true;
            case 0x4221:
                ushort nodeId = operands.ReadWord();
                if ((nodeId & 0x2000) != 0) nodeId = unchecked((ushort)ReadVariable(nodeId, operands));
                if (nodeId >= registry.Triggers.Nodes.Count) throw new InvalidDataException($"Missing platform destination {nodeId}.");
                TriggerPosition position = TriggerPlacement.Read(registry.Triggers.Nodes[nodeId]).Position;
                Motion.MoveTo(new FixedVector3(position.X, position.Y, position.Z) << 12);
                return true;
            case 0x4222: Motion.MoveTo(Motion.Position + ReadPosition(operands)); return true;
            case 0x4226: Motion.Velocity = default; return true;
            case 0x4300:
                if (trodden) return true;
                operands.Seek(operands.Position - 2);
                return false;
            case 0x4301:
                registry.Player?.Controller.ReleaseSupport(GetInstanceId());
                Variables.ItemFlags |= 1;
                Retire();
                registry.ShatterPlatform(this, false, false);
                return false;
            case 0x4304:
                short percent = unchecked((short)operands.ReadWord());
                short duration = unchecked((short)operands.ReadWord());
                for (int axis = 0; axis < 3; axis++) Motion.ChangeScale(axis, percent, duration);
                return true;
            case 0x4305:
                if (operands.ReadWord() != 0) Variables.ItemFlags |= 8;
                else Variables.ItemFlags &= unchecked((ushort)~8);
                return true;
            case 0x4306: case 0x4307: case 0x4308:
                Motion.ChangeScale(opcode - 0x4306, unchecked((short)operands.ReadWord()), unchecked((short)operands.ReadWord()));
                return true;
            case 0x4507:
                if (SoundHandle != 0) Sounds.Stop(SoundHandle);
                SoundHandle = Sounds.Play(unchecked((uint)(short)operands.ReadWord()));
                SoundUpdateInterval = -1;
                return true;
            case 0x4508: case 0x450B:
                if (SoundHandle != 0) Sounds.Stop(SoundHandle);
                uint sound = unchecked((uint)(short)operands.ReadWord());
                short interval = unchecked((short)operands.ReadWord());
                if (interval == 0) throw new InvalidDataException("Platform sound interval cannot be zero.");
                SoundUpdateInterval = interval;
                SoundHandle = Sounds.PlayAt(opcode == 0x450B ? sound | 0x8000u : sound, Motion.Position, registry.SoundListener());
                return true;
            case 0x4509:
                if (SoundHandle != 0) Sounds.Stop(SoundHandle);
                SoundHandle = 0;
                return true;
            default: throw new NotSupportedException($"Platform effect 0x{opcode:X4} has not been ported.");
        }
    }

    private ActorAngles ReadAngles(ActorScriptCursor operands) => new(operands.ReadScriptValue(ReadVariable),
        operands.ReadScriptValue(ReadVariable), operands.ReadScriptValue(ReadVariable));
    private static FixedVector3 ReadPosition(ActorScriptCursor operands) => new FixedVector3(
        unchecked((int)operands.ReadAlignedDword()), unchecked((int)operands.ReadAlignedDword()), unchecked((int)operands.ReadAlignedDword())) << 12;

    private void SelectModel(int index)
    {
        ObjectDisposedException.ThrowIf(resourcesDisposed, this);
        PsxAssetLibrary library = registry.ObjectLibrary();
        if (index < 0 || index >= library.Scene.Models.Count) throw new InvalidDataException($"Platform model index {index} exceeds its library.");
        PsxModel model = library.Scene.Models[index];
        var materials = new PsxMaterialLibrary(registry.ObjectTextures());
        ArrayMesh? geometry = null;
        MeshInstance3D? replacementMesh = null;
        Node3D? replacementCollision = null;
        Shape3D[] shapes = [];
        bool installed = false;
        try
        {
            geometry = PsxGeometryBuilder.BuildModel(model, library.Scene.ColorTable, materials);
            replacementMesh = new MeshInstance3D { Name = "SourceModel", Mesh = geometry };
            // Identity source instance keeps collision vertices local to this actor rather than baking its initial placement.
            var scene = new PsxScene(library.Scene.Version,
                [new(0, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, (ushort)index, 0)],
                library.Scene.Models, library.Scene.ColorTable);
            replacementCollision = PsxWorldCollisionBuilder.Build(scene, library.AssetPath);
            shapes = replacementCollision.GetChildren().OfType<CollisionObject3D>().SelectMany(body => body.GetChildren().OfType<CollisionShape3D>())
                .Select(child => child.Shape).OfType<Shape3D>().ToArray();
            registry.ClaimObjectModel(library.AssetPath, index);
            ReleaseModel();
            if ((model.Flags & 0x10) != 0) Variables.ItemFlags |= 0x20;
            else Variables.ItemFlags &= unchecked((ushort)~0x20);
            ModelIndex = index; modelLibrary = library; ModelChecksum = model.Checksum;
            modelMesh = geometry; modelMaterials = materials; mesh = replacementMesh; collision = replacementCollision;
            collisionShapes.AddRange(shapes); installed = true;
            AddChild(mesh); AddChild(collision); ApplyTransform();
        }
        finally
        {
            if (!installed)
            {
                if (replacementMesh is not null) { replacementMesh.Mesh = null; replacementMesh.Free(); }
                replacementCollision?.Free(); geometry?.Dispose();
                foreach (Shape3D shape in shapes) shape.Dispose();
                materials.Dispose();
            }
        }
    }

    private void ApplyTransform()
    {
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(Motion.Angles.X, Motion.Angles.Y, Motion.Angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(rotation);
        transform.Origin = PsxCoordinates.FixedWorldPosition(Motion.Position);
        Transform = transform;
        Visible = (Variables.ItemFlags & 1) == 0;
        if (mesh is not null && Motion.ScalingEnabled)
            mesh.Scale = new Vector3(Motion.Scale[0], Motion.Scale[1], Motion.Scale[2]) / 4096;
        if (collision is null) return;
        if (Motion.ScalingEnabled) collision.Scale = new Vector3(Motion.Scale[0], Motion.Scale[1], Motion.Scale[2]) / 4096;
        foreach (Node child in collision.GetChildren())
            if (child is ImportedCollisionBody body)
            {
                body.SourceRotation = rotation;
                body.SourceFlags = Variables.ItemFlags;
                body.SupportId = GetInstanceId();
                body.ProcessMode = (Variables.ItemFlags & 0x21) == 0 ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
                // ProcessMode alone does not remove physics bodies; disable their shapes as well.
                foreach (Node shape in body.GetChildren())
                    if (shape is CollisionShape3D collisionShape) collisionShape.Disabled = (Variables.ItemFlags & 0x21) != 0;
            }
    }

    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire()
    {
        if ((bodyFlags & 0x40) != 0) return;
        if ((ScriptState.BehaviorFlags & 0x4000) == 0 && ScriptState.TryMarkDeathPulseSent()) SendDeathPulse();
        Variables.ItemFlags |= 1;
        bodyFlags |= 0x40;
        Lifecycle.Kill();
        ApplyTransform();
    }
    /// <summary>Inherited CBody::Die leaves platform visibility and script-completion pulses unchanged.</summary>
    public void DieSourceBody()
    { if ((bodyFlags & 0x40) == 0) { bodyFlags |= 0x40; Lifecycle.Kill(); } }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || resourcesDisposed) return;
        resourcesDisposed = true;
        try { if (SoundHandle != 0 && registry?.Sounds is { } sounds) sounds.Stop(SoundHandle); }
        finally { ReleaseModel(); }
    }
    private void ReleaseModel()
    {
        if (mesh is not null) { mesh.Mesh = null; if (ReferenceEquals(mesh.GetParent(), this)) RemoveChild(mesh); mesh.Free(); mesh = null; }
        if (collision is not null) { if (ReferenceEquals(collision.GetParent(), this)) RemoveChild(collision); collision.Free(); collision = null; }
        modelMesh?.Dispose(); modelMesh = null;
        foreach (Shape3D shape in collisionShapes) shape.Dispose(); collisionShapes.Clear();
        modelMaterials?.Dispose(); modelMaterials = null;
    }
    internal (PsxMaterialLibrary Materials, Resource[] Resources) CaptureOwnedResources()
    {
        ObjectDisposedException.ThrowIf(resourcesDisposed, this);
        if (modelMaterials is null) throw new InvalidOperationException("The genuine platform has not selected an OBJ model.");
        var resources = new List<Resource>();
        resources.AddRange(collisionShapes);
        if (modelMesh is not null)
        {
            resources.Add(modelMesh);
            for (int index = 0; index < modelMesh.GetSurfaceCount(); index++)
            {
                Material material = modelMesh.SurfaceGetMaterial(index); resources.Add(material);
                if (material is StandardMaterial3D { AlbedoTexture: { } texture }) resources.Add(texture);
            }
        }
        return (modelMaterials, resources.DistinctBy(resource => resource.GetInstanceId()).ToArray());
    }

    private sealed class VariableWorld(GodotPlatformActor actor, GodotActorRegistry registry) : IActorScriptVariableWorld
    {
        public int Difficulty => registry.Difficulty;
        public int MovieInputPolls => registry.MovieInputPolls;
        public FixedVector3? PlayerPosition => registry.PlayerPosition;
        public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
        public int Random(int range) => registry.Random(range);
        public ushort FirstLink(ushort nodeId) => registry.FirstLink(nodeId);
        public void SelectModel(uint checksum)
        {
            actor.SelectModel(registry.ObjectLibrary().FindModel(checksum));
            actor.Variables.ItemFlags &= unchecked((ushort)~1);
        }
    }
}
