using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cops;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Source306/320 CCop AI through CopBrain and the thug-family path/waypoint controllers.</summary>
public partial class GodotCopActor : GodotScriptActor, ICopHost, IGodotSourceBaddyBody, IActorScriptHost,
    IActorScriptVariableWorld, IActorHeightHost
{
    private GodotActorRegistry registry = null!;
    private GodotCopConfiguration configuration = null!;
    private CopBrain brain = null!;
    private ThugStateFlags stateFlags = CopState.CreateStateFlags();
    private readonly ActorBodyMotion playerBody = new();
    private bool begun;
    private bool destroyed;
    private FixedVector3[]? throwPoints;
    /// <summary>The pose-link records of 0x549264 (police) and 0x549344 (SWAT); both start with the same sixteen links.</summary>
    private const string FallPoseLinks = "000010000000ffff00000000000000000700000000000000000000000800070000000000000000000200070000000000000000000100020000000000000000000300010000000000000000000400030000000000000000000600070000000000000000000500060000000000000000000900050000000000000000000a00000000000000000000000b000a0000000000000000000c000b0000000000000000000d00000000000000000000000e000d0000000000000000000f000e000000000000000000";
    public CopState State { get; private set; } = null!;
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorPathController Path { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public FixedVector3 SourceTransformTranslation { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public int AttackClock => configuration.AttackClock();
    public int AnimationId => Model.ClipId;
    public int AnimationFrame => Model.Playback.Frame;
    public byte AnimationFinished { get => Model.Playback.Finished; set => Model.SetAnimationFinished(value); }
    public ushort ItemFlags
    {
        get => Variables.ItemFlags;
        set
        {
            Variables.ItemFlags = value;
            if ((value & 4) == 0 && Model.SourcePoseEnabled) Model.ApplySourceJointPose(false);
        }
    }
    public bool HasMessages => Messages.Head is not null;
    public uint RouteFlags { get => Route.Flags; set => Route.Flags = value; }
    public override FixedVector3 SourcePosition => Actor.Body.Position;
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    FixedVector3? IActorScriptVariableWorld.PlayerPosition => registry.PlayerPosition;
    public short PlayerSurfaceClearance => (short)PlayerTerrainController.SurfaceClearance;
    public ActorBodyMotion? PlayerBody => playerBody;
    public ThugPlayerState Player
    {
        get
        {
            GodotPlayer player = configuration.PlayerProvider() ?? throw new InvalidOperationException("Cop AI requires the scene player.");
            return new(player.Controller.Position, player.Combat.State.Health, player.ActorState.BodyFlags,
                player.ActorState.InvisibilityFlag, player.Combat.State.HeldObjectPresent);
        }
    }
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
    public int Random(int range) => configuration.Random(range);
    public ushort FirstLink(ushort nodeId) => registry.FirstLink(nodeId);
    public void SelectModel(uint checksum) => registry.SelectModel(checksum);
    public ActorBodyMotion Body => Actor.Body;
    public ushort MinorRadius => unchecked((ushort)State.MinorRadius);
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => Actor.BehaviorFlags;
    public short Health => State.Health;

    public static GodotCopActor Create(TriggerNode node, GodotActorRegistry registry, GodotCopConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType is not (306 or 320)) throw new NotSupportedException("Cop factory requires type 306 or 320.");
        int ordinal = configuration.SourceConstructionOrdinal();
        var actor = new GodotCopActor { Name = $"Cop{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, configuration.AttackClock(), ordinal);
            actor.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), UseLocalVelocity = true
            }) { BehaviorFlags = CopConstruction.InitialBehaviorFlags };
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.ScriptState = new() { ActorType = definition.ActorType, NodeId = node.Id, BehaviorFlags = actor.Actor.BehaviorFlags };
            actor.Variables = new(actor.ScriptState, actor) { Position = actor.Actor.Body.Position, Health = actor.State.Health,
                GroundOffset = actor.State.SurfaceClearance, CollisionRadius = (ushort)actor.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000 };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.World = new(actor, registry.Triggers);
            actor.Height = new(actor.Actor, actor) { Clearance = actor.State.SurfaceClearance, QueryInterval = actor.State.Field212 };
            actor.Path = new(actor.Actor, actor.World) { Radius = (ushort)actor.State.MinorRadius, Clearance = actor.State.SurfaceClearance };
            actor.Route = new(registry.Triggers, node.Id, actor.ParseWaypoint);
            actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
            actor.State.SetCopType(definition.ActorType);
            actor.Model = configuration.ModelFactory(actor.State.ModelName!);
            actor.Model.AdvanceAutomatically = false; actor.AddChild(actor.Model);
            CopConstruction.Apply(actor.State, () => { });
            actor.brain = new(actor.State, actor.Actor, actor.Procedures, configuration.CopCombat, actor, configuration.Random);
            actor.BuildTransform();
            actor.AddChild(new GodotNativeDestructionObserver { Name = "SourceCopDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin() { Runtime.Start(registry.Environment); begun = true; BuildTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        if (configuration.PlayerProvider() is { } player)
            playerBody.Position = player.Controller.Position;
        State.BodyFlags = FrameTiming.Prepare(configuration.Timer(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance(FrameTiming.ElapsedTicks);
        Messages.Cleanup();
        SyncScriptStorage();
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || configuration.PlayerProvider() is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public void BuildTransform()
    {
        ActorAngles angles = Actor.Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        SourceTransformTranslation = Actor.Body.Position >> 12;
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition(SourceTransformTranslation << 12);
        Transform = transform;
    }
    public void RunAnimation(ushort clip, int from = 0, int to = -1) => Model.RunClip(clip, from, to < 0 ? int.MaxValue : to);
    public void CycleAnimation(ushort clip, sbyte direction = 1) => Model.CycleClip(clip, direction);
    public int SetHeight(bool initial, int above, int below)
    {
        Height.RouteFlags = Route.Flags;
        Height.Clearance = State.SurfaceClearance;
        return Height.SetHeight(initial, above, below, FrameTiming.ElapsedTicks);
    }
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void ProcessMessages() => Messages.Cleanup(all: true);
    public int GroundHeight(FixedVector3 position, int above, int below, int flags) =>
        flags == 0 ? World.GroundHeight(position, above, below) : throw new NotSupportedException("Cop ground queries use flags0.");
    public bool GetNextWaypoint() => Route.Advance();
    public FixedVector3 WaypointPosition() => Route.Position();
    /// <summary>CBaddy 0x1A8 slot 0 is the last-seen player position written by PlayerIsVisible.</summary>
    public FixedVector3 GetPathPoint(int index) => index == 0 ? State.LastSeenPlayerPosition : Path.GetPoint(index);
    public bool LineOfSight(FixedVector3 from, FixedVector3 to)
    {
        FixedVector3 hit = default;
        return World.LineOfSight(from, to, 0, ref hit);
    }
    public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance, bool globalCollisionFlag) =>
        Path.PathCheck(from, to, false, clearance, default).Code;
    public bool AddPointToPath(FixedVector3 point, int argument)
    {
        bool added = Path.AddPointToPath(point, argument);
        State.PathPointIndex = Path.PathCount;
        return added;
    }
    public FixedVector3 GetWaypointNearTarget(FixedVector3 target, int radius, byte phase) =>
        ActorPathController.GetWaypointNearTarget(target, radius, phase);
    public void SendSignal() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void ApplyHit(byte flags, short damage) => Hit(new ActorHit(flags, 0, 0, unchecked((ushort)damage), default));
    public void WarnOtherCops()
    {
        foreach (GodotCopActor other in registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotCopActor>())
            if (!ReferenceEquals(other, this) && other.State.AlertTicks == 0) other.ReceiveMessage(7, this);
    }
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) =>
        configuration.PlayPositionSound(sound, position, pitch);
    public void MakeSpriteRing(FixedVector3 offset) =>
        throw new NotSupportedException("CCop::MakeSpriteRing (0x403FC0) is not ported to Godot effects yet.");
    public int DrawLaserSite(bool checkPlayer)
    {
        // Full CCop::DrawLaserSiteThingie (0x42BB50) waits on hooks/tracers; smoke and idle AI use a miss.
        State.HasLaser = false;
        return 0;
    }
    public void ClearLaser() => State.HasLaser = false;
    public void ApplyPlayerHit(byte flags, short damage)
    {
        if (configuration.PlayerProvider() is { } player)
            player.Combat.Hit(new ActorHit(flags, 0, 0, unchecked((ushort)damage), default));
    }
    public void SendSelfMessage(int code) => ReceiveMessage(code, null);
    public void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask)
    {
        // Full CAIProc_MonitorAttack needs hook collision; melee AI advances without the hit sweep for now.
    }
    public ActorBodyMotion? RecoverAcknowledgeTarget() => PlayerBody;
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    private void ParseWaypoint(ActorScriptCursor script)
    {
        SyncScriptStorage();
        Runtime.ParseAt(script, registry.Environment);
        Path.CurrentWaypoint = Route.CurrentNode;
    }
    public bool Grab(FixedVector3 destination) => CopGrabEntry.TryGrab(State, Actor, stateFlags, destination, AddPointToPath);
    public bool Hit(ActorHit hit) { bool handled = brain.Hit(hit); SyncScriptStorage(); return handled; }
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });
    public void MarkDead() => Lifecycle.Kill();
    public void NotifyKill(int actorType) => configuration.Session.NotifyKill(actorType);
    public IThugTrapWeb? RecoverTrapWeb() => configuration.RecoverTrapWeb(this);
    public int PlayerWebComboValue => configuration.Session.PlayerWebComboValue;
    public int PlayerActionState => unchecked((int)RequirePlayer().Controller.Action);
    public short PlayerYaw => RequirePlayer().Controller.Orientation.Heading;
    public bool GrabUpdate(ref FixedVector3 position, ref short yaw) => configuration.GrabUpdate(ref position, ref yaw);
    private GodotPlayer RequirePlayer() => configuration.PlayerProvider() ?? throw new InvalidOperationException("Cop AI requires the scene player.");
    public void SetPlayerWebCombo(int duration, int amount) => configuration.Session.SetPlayerWebCombo(duration, amount);
    public int PlayerAnimationId => RequirePlayer().Model.ClipId;
    public CopState? FindClosest(int actorType)
    {
        int best = 10656; GodotCopActor? found = null;
        foreach (GodotCopActor candidate in registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotCopActor>())
        {
            if (ReferenceEquals(candidate, this) || (actorType != 0 && candidate.State.Type != actorType)) continue;
            int distance = ThugPerception.ApproximateHorizontalDistance(Actor.Body.Position, candidate.Actor.Body.Position);
            if (distance >= best) continue;
            best = distance; found = candidate;
        }
        return found?.State;
    }
    public void SendMessage(CopState target, int code)
    {
        foreach (GodotCopActor other in registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotCopActor>())
            if (ReferenceEquals(other.State, target)) { other.ReceiveMessage(code, this); return; }
    }
    public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, bool globalCollisionFlag, out FixedVector3 hit)
    {
        ActorPathCheckResult result = Path.PathCheck(from, to, true, clearance, default);
        hit = result.HitPoint;
        return result.Code;
    }
    // The Godot cop never creates the CSuper 0xCC shadow object, so there is nothing to delete.
    public void KillShadow() { }
    public IThugInjuryBody? StruckGameObject(int first, int second) => configuration.Effects.StruckGameObject(this, first, second);
    public void ElasticCollision(IThugInjuryBody target, FixedVector3 velocity, int duration) =>
        configuration.Effects.ElasticCollision(this, target, velocity, duration);
    public void PlayGlobalSound(ushort sound, int volume, int pan) => configuration.Effects.PlayGlobalSound(sound, volume, pan);
    public void ApplyFallPose(bool police)
    {
        Model.InitializeSourceJoints(ActorPoseLinkPacket.Read(Convert.FromHexString(FallPoseLinks), 16));
        if ((ItemFlags & 4) != 0) BuildTransform();
        Model.ApplySourceJointPose((ItemFlags & 4) != 0);
    }
    public bool HasJoints => Model.HasSourceJoints;
    public short JointOnePitch
    {
        get => Model.GetSourceJoint(1).RotationX;
        set => Model.SetSourceJoint(1, Model.GetSourceJoint(1) with { RotationX = value });
    }
    public FixedVector3 GetThrowPoint(int index) => (throwPoints ?? throw new InvalidOperationException("Source mpThrowPoints is null."))[index];
    public void SetThrowPoints(IReadOnlyList<FixedVector3> points)
    {
        if (points.Count != 8) throw new ArgumentException("Source GetWhippedLikeTheWhoreYouAre consumes eight precomputed throw points.", nameof(points));
        throwPoints = points.ToArray();
    }
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, SourceNodeId, opcode, operands)) return true;
        if (opcode == 0x470C)
        {
            State.SetParamByIndex(operands.ReadScriptValue(ReadVariable), operands.ReadScriptValue(ReadVariable));
            Variables.Damage = unchecked((ushort)State.Damage); return true;
        }
        if (opcode == 0x470F) { Actor.BehaviorFlags |= 0x02000000; return true; }
        if (Route.TryExecuteCommand(opcode, operands)) return true;
        throw new NotSupportedException($"Cop {Definition.NodeId}: source script command 0x{opcode:X4} is not connected.");
    }
    public short ReadVariable(ushort variable, ActorScriptCursor operands) { SyncScriptStorage(); return Variables.Read(variable, operands); }
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    {
        SyncScriptStorage(); Variables.Write(variable, operands);
        Actor.Body.Position = Variables.Position; State.Health = Variables.Health;
        State.MinorRadius = Variables.CollisionRadius; State.SurfaceClearance = Variables.GroundOffset;
        State.SetParamByIndex(9, Variables.Damage);
    }
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() { SendDeathPulse(); MarkSourceBodyDead(); ItemFlags |= 1; MarkDead(); }
    private void SyncScriptStorage()
    {
        Variables.Position = Actor.Body.Position; Variables.Health = State.Health;
        Variables.CollisionRadius = unchecked((ushort)State.MinorRadius);
        Variables.GroundOffset = State.SurfaceClearance;
    }
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true;
        registry.ActorRemoved -= OnSourceRemoved;
        configuration.CopCombat.Release(State);
        Messages.Cleanup(all: true);
    }
}
