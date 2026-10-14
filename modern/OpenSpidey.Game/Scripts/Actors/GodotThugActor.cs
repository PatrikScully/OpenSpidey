using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Actor-owned source thug AI, procedures, animation and actual player combat/world queries.</summary>
public partial class GodotThugActor : GodotScriptActor, IThugBrainHost, IThugCombatParticipant,
    IActorScriptHost, IActorScriptVariableWorld, IActorHeightHost, IActorAttackMonitorHost,
    IThugSpitHost, IThugFrameSoundHost, IActorSpriteRingHost, IThugTorsoAimHost,
    IThugConversationHost, IThugMessageHost, IThugMessageReactionHost, IActorLaserHost, IThugInjuryBody
{
    private GodotActorRegistry registry = null!;
    private GodotThugConfiguration configuration = null!;
    private ThugBrain brain = null!;
    private FixedJointTransform[] hookPose = [];
    private readonly ActorBodyMotion playerBody = new();
    private object? hostageHandle;
    private FixedVector3[]? throwPoints;
    private GodotThugActor? conversationPartner;
    private bool begun;
    private bool destroyed;
    public ThugState State { get; private set; } = null!;
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorPathController Path { get; private set; } = null!;
    public ThugStateFlags StateFlags { get; } = new();
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public ActorSpeechAttachment SpeechAttachment { get; } = new();
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public FixedVector3 SourceTransformTranslation { get; private set; }
    public GodotActorWorldHit? LastLaserHit { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public int Type => State.Type;
    public int AttackClock => configuration.AttackClock();
    public int AnimationId => Model.ClipId;
    public int AnimationFrame => Model.Playback.Frame;
    public byte AnimationFinished { get => Model.Playback.Finished; set => Model.SetAnimationFinished(value); }
    public int PlayerAnimationId => configuration.Player.Model.ClipId;
    public int PlayerAnimation => PlayerAnimationId;
    public int PlayerActionState => unchecked((int)configuration.Player.Controller.Action);
    public short PlayerYaw => configuration.Player.Controller.Orientation.Heading;
    public short PlayerHealth => configuration.Player.Combat.State.Health;
    public override FixedVector3 SourcePosition => Position;
    public FixedVector3 PlayerPosition => configuration.Player.Controller.Position;
    public short PlayerSurfaceClearance => (short)PlayerTerrainController.SurfaceClearance;
    public int PlayerUnavailableFlag => configuration.Player.ActorState.InvisibilityFlag;
    public bool PlayerHoldsObject => configuration.Player.Combat.State.HeldObjectPresent;
    public ThugPlayerState Player => new(PlayerPosition, PlayerHealth, configuration.Player.ActorState.BodyFlags,
        PlayerUnavailableFlag, PlayerHoldsObject);
    public bool HasFightOwner => configuration.Combat.FightOwner is not null;
    public bool HasMessages => Messages.Head is not null;
    public int DomeCount => configuration.Effects.DomeCount;
    public ushort AttackDamage => unchecked((ushort)State.Field1FC);
    public ActorAngles Angles => Actor.Body.Angles;
    public new FixedVector3 Position { get => Actor.Body.Position; set { Actor.Body.Position = value; Variables.Position = value; } }
    public ushort ItemFlags
    {
        get => Variables.ItemFlags;
        set
        {
            Variables.ItemFlags = value;
            if ((value & 4) == 0 && Model.SourcePoseEnabled) Model.ApplySourceJointPose(false);
        }
    }
    public bool HasJoints => Model.HasSourceJoints;
    public short JointOnePitch
    {
        get => Model.GetSourceJoint(1).RotationX;
        set => Model.SetSourceJoint(1, Model.GetSourceJoint(1) with { RotationX = value });
    }
    public short JointOneRoll
    {
        get => Model.GetSourceJoint(1).RotationZ;
        set => Model.SetSourceJoint(1, Model.GetSourceJoint(1) with { RotationZ = value });
    }
    public uint WaypointFlags { get => Route.Flags; set => Route.Flags = value; }
    public int PlayerWebComboValue => configuration.Effects.PlayerWebComboValue;
    public bool IsCurrentThugListOwner => ReferenceEquals(configuration.Combat.FightOwner, this);
    public int Difficulty => registry.Difficulty;
    public int DifficultyLevel => Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    FixedVector3? IActorScriptVariableWorld.PlayerPosition => registry.PlayerPosition;
    public object ActorIdentity => this;
    public object PlayerIdentity => configuration.Player;
    public FixedVector3 ActorPosition => Position;
    public IReadOnlyList<ActorLaserBody> PlayerBodies => new[] { new ActorLaserBody(configuration.Player, PlayerPosition,
        configuration.Player.ActorState.MinorRadius, configuration.Player.ActorState.BodyFlags) };
    public bool IgnoreSphereRadius => configuration.Effects.IgnoreSphereRadius;
    public bool LineOfSightCheck { get => World.LineOfSightCheck; set => World.LineOfSightCheck = value; }
    public IReadOnlyList<ThugConversationActor> SourceBaddyOrder => configuration.SourceBaddyOrder()
        .Select(actor => new ThugConversationActor(actor.State, actor.Actor, actor.StateFlags)).ToArray();

    public static GodotThugActor Create(TriggerNode node, GodotActorRegistry registry, GodotThugConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType is not (304 or 312))
            throw new NotSupportedException("The original thug factory requires source type 304 or 312.");
        int ordinal = configuration.SourceConstructionOrdinal();
        var result = new GodotThugActor { Name = $"Thug{node.Id}", registry = registry,
            configuration = configuration, Definition = definition };
        try
        {
            result.State = new(node.Id, registry.Environment.LevelId, configuration.AttackClock(),
                sourceConstructionOrdinal: ordinal);
            result.State.SetThugType(definition.ActorType);
            result.Model = configuration.ModelFactory(result.State.ModelName!);
            result.Model.AdvanceAutomatically = false;
            result.AddChild(result.Model);
            result.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), UseLocalVelocity = true
            }) { BehaviorFlags = result.State.BehaviorFlags };
            if (definition.ClearsBodyFlag2) result.State.BodyFlags &= ~2u;
            result.ScriptState = new() { ActorType = definition.ActorType, NodeId = node.Id,
                BehaviorFlags = result.Actor.BehaviorFlags };
            result.Variables = new(result.ScriptState, result) { Position = result.Position, Health = result.State.Health,
                Damage = (ushort)result.State.Field1FC, GroundOffset = result.State.SurfaceClearance,
                CollisionRadius = (ushort)result.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000 };
            result.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            result.World = new(result, registry.Triggers);
            result.Height = new(result.Actor, result) { Clearance = result.State.SurfaceClearance, QueryInterval = result.State.Field212 };
            result.Path = new(result.Actor, result.World) { Radius = (ushort)result.State.MinorRadius, Clearance = result.State.SurfaceClearance };
            result.Route = new(registry.Triggers, node.Id, result.ParseWaypoint);
            result.Runtime = new(new(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), result);
            result.brain = new(result.State, result.Actor, result.Procedures, result.Route, result.StateFlags, result, result.Random);
            result.hookPose = new FixedJointTransform[definition.ActorType == 304 ? 16 : 15];
            result.BuildTransform();
            result.AddChild(new GodotNativeDestructionObserver { Name = "SourceThugDestructor", Destroyed = result.DestroySourceOwnedState });
            registry.ActorRemoved += result.OnSourceRemoved;
            configuration.Effects.CreateShadow(result);
            return result;
        }
        catch
        {
            result.Free();
            throw;
        }
    }

    internal override void Begin()
    {
        Runtime.Start(registry.Environment);
        begun = true;
        BuildTransform();
    }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Timer(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance(FrameTiming.ElapsedTicks);
        Messages.Cleanup();
        SyncScriptStorage();
        configuration.Effects.UpdateAppearanceAndShadow(this);
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || configuration.SourcePlayer is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    private void OnSourceRemoved(GodotScriptActor actor)
    { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true;
        registry.ActorRemoved -= OnSourceRemoved;
        if (configuration is not null)
        {
            configuration.Combat.Remove(this);
            configuration.Effects.DeleteBarrelLine(this);
        }
        Messages.Cleanup(all: true);
    }
    public void BuildTransform()
    {
        ActorAngles angles = Actor.Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        SourceTransformTranslation = Position >> 12;
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition(SourceTransformTranslation << 12);
        Transform = transform;
    }
    public bool Hit(ActorHit hit)
    {
        bool accepted = ThugDamageBehavior.Hit(State, Actor, Procedures, StateFlags, this, hit, Random);
        SyncScriptStorage();
        return accepted;
    }
    private void SyncScriptStorage()
    {
        Variables.Position = Position;
        Variables.Health = State.Health;
        Variables.Damage = unchecked((ushort)State.Field1FC);
        Variables.CollisionRadius = unchecked((ushort)State.MinorRadius);
        Variables.GroundOffset = State.SurfaceClearance;
        ScriptState.BehaviorFlags = Actor.BehaviorFlags;
    }
    private void ParseWaypoint(ActorScriptCursor script)
    {
        SyncScriptStorage();
        Runtime.ParseAt(script, registry.Environment);
        Path.CurrentWaypoint = Route.CurrentNode;
    }
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void RunAnimation(ushort clip, int from = 0, int to = -1) => Model.RunClip(clip, from, to);
    public void CycleAnimation(ushort clip, sbyte direction = 1) => Model.CycleClip(clip, direction);
    public void ApplyPoseForType(int type)
    {
        Model.InitializeSourceJoints(OriginalThugModelPackets.PoseLinks(type));
        if ((ItemFlags & 4) != 0) BuildTransform();
        Model.ApplySourceJointPose((ItemFlags & 4) != 0);
    }
    public void ApplyMessagePose(int actorType) => ApplyPoseForType(actorType);
    public short GetJointRoll(int index) => Model.GetSourceJoint(index).RotationZ;
    public void SetJointRoll(int index, short roll) => Model.SetSourceJoint(index, Model.GetSourceJoint(index) with { RotationZ = roll });
    public void RotateTorsoToAimAtPlayer(FixedVector3 origin) => ThugTorsoAim.Step(State, Actor, origin, this);
    public int SetHeight(bool initial, int clearance, int range)
    {
        Height.RouteFlags = Route.Flags;
        Height.Clearance = State.SurfaceClearance;
        return Height.SetHeight(initial, clearance, range, FrameTiming.ElapsedTicks);
    }
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    public int FindGroundHeight(int above, int below) => World.GroundHeight(Position, above, below);
    public int GroundHeight(FixedVector3 position, int above, int below) => World.GroundHeight(position, above, below);
    public void StoreGroundPlacement(int bodyHeight, int groundHeight)
    {
        Height.GroundHeight = groundHeight;
        State.GroundReferenceY = bodyHeight;
        Path.StoredBodyY = bodyHeight;
    }
    private void SyncPath()
    {
        Path.PathCount = State.PathPointIndex;
        Path.CurrentWaypoint = Route.CurrentNode;
        Path.Radius = unchecked((ushort)State.MinorRadius);
        Path.Clearance = State.SurfaceClearance;
        Path.StoredBodyY = State.GroundReferenceY;
    }
    public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance) => CheckMessagePath(from, to, false, clearance, default).Code;
    public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit)
    {
        ActorPathCheckResult result = CheckMessagePath(from, to, true, clearance, default);
        hit = result.HitPoint;
        return result.Code;
    }
    public ActorPathCheckResult CheckMessagePath(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int clearance, FixedVector3 initialHit)
    { SyncPath(); return Path.PathCheck(from, to, wantHitPoint, clearance, initialHit); }
    public bool AddPointToPath(FixedVector3 point, int argument)
    {
        SyncPath(); bool added = Path.AddPointToPath(point, argument); State.PathPointIndex = Path.PathCount; return added;
    }
    public FixedVector3 GetPathPoint(int sourceIndex) => Path.GetPoint(sourceIndex);
    public FixedVector3 GetThrowPoint(int sourceIndex) => (throwPoints ?? throw new InvalidOperationException("Source mpThrowPoints is null."))[sourceIndex];
    public void SetThrowPoints(IReadOnlyList<FixedVector3> points)
    {
        if (points.Count != 8) throw new ArgumentException("Source WebThrow consumes eight precomputed throw points.", nameof(points));
        throwPoints = points.ToArray();
    }
    public FixedVector3 GetWaypointNearTarget(FixedVector3 target, int radius, byte phase) => ActorPathController.GetWaypointNearTarget(target, radius, phase);
    public void ReportChaseDepth(int depth) => GD.Print($"Thug {Definition.NodeId}: source chase depth {depth}.");
    public bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags)
    { FixedVector3 hit = default; return World.LineOfSight(from, to, unchecked((int)flags), ref hit); }
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => LineOfSight(from, to, 0);
    public bool MessageLineOfSight(FixedVector3 from, FixedVector3 to) => LineOfSight(from, to, 0);
    public int DistanceToPlayer(int cacheTolerance) => ThugPerception.DistanceToPlayer(State, Actor.Body, Player, AttackClock, cacheTolerance);
    public ActorBodyMotion? RecoverPlayerBody()
    {
        if (configuration.SourcePlayer is not { } player || !GodotObject.IsInstanceValid(player)) return null;
        playerBody.Position = PlayerPosition;
        playerBody.Velocity = player.Controller.Velocity;
        playerBody.Angles = new(0, PlayerYaw, 0);
        return playerBody;
    }
    public bool DetermineFightState() => ThugFightBehavior.Determine(State, Actor, this);
    void IThugCombatParticipant.DetermineFightState() => DetermineFightState();
    public void SetAnimationMode(int mode, bool playAnimation) => ThugAnimationBehavior.SetMode(State, mode, playAnimation, this);
    public void SetFightOwner() => configuration.Combat.FightOwner = this;
    public void ClearThugListOwner() => configuration.Combat.FightOwner = null;
    public int SetAttackFlags() => configuration.Combat.AssignAttackPosition(this, PlayerPosition, PlayerUnavailableFlag) ? 1 : 0;
    public void ClearAttackFlags() => configuration.Combat.ClearAttackOwnership(this);
    public void ClearAttackOwnership() => ClearAttackFlags();
    public void SetAttacker()
    {
        if (ReferenceEquals(configuration.Combat.PrimaryAttacker, this)) return;
        ClearAttackFlags();
        if (configuration.Combat.PrimaryAttacker is { } previous) configuration.Combat.ClearAttackOwnership(previous);
        configuration.Combat.PrimaryAttacker = this;
        State.AttackOwnershipFlags = 1;
    }
    public void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask) =>
        Procedures.Attach(new ActorAttackMonitorProcedure(Actor, this, startFrame, unchecked((uint)attackMask), frameCount, completionMask));
    public FixedVector3 GetDynamicHookPosition(ActorHook hook)
    {
        for (int part = 0; part < hookPose.Length; part++) hookPose[part] = Model.GetSourcePartPose(part);
        return ActorHookPosition.Evaluate(Position, SourceTransformTranslation, SourceTransformBasis, hookPose, hook);
    }
    public FixedVector3 GetActorHookPosition(ActorHook hook) => GetDynamicHookPosition(hook);
    public FixedVector3 GetDynamicHookPosition(short partX, short partY, short partZ, int offset) =>
        GetDynamicHookPosition(new(partX, partY, partZ, checked((ushort)offset)));
    public FixedVector3 GetHookPosition(int index) => GetDynamicHookPosition(OriginalThugModelPackets.Hooks(State.Type).Hooks[index]);
    public bool CollideWithPlayer(FixedVector3 from, FixedVector3 to, int scale, out ActorHook hit) => configuration.Player.Combat.CollideWithPlayer(from, to, scale, out hit);
    public void HitPlayer(ActorHit hit) => configuration.Player.Combat.Hit(hit);
    public void ApplyPlayerHit(uint flags, int damage) => HitPlayer(new(unchecked((byte)flags), 0, 0, unchecked((ushort)damage), default));
    public void ApplyHit(uint flags, int damage) => Hit(new(unchecked((byte)flags), 0, 0, unchecked((ushort)damage), default));
    public void CreateCombatImpactEffect(FixedVector3 position, int flags) => configuration.Effects.CreateCombatImpact(this, position, flags);
    public bool MonitorSpitPlease() => ThugSpitBehavior.Monitor(this, Random);
    public void EmitSpitParticle(ThugSpitParticle particle) => configuration.SpitParticles.Emit(particle, configuration.Timer());
    public void PlayAnimationSounds() => ThugFrameSounds.Update(State.Type, Model.Playback.ClipId, unchecked((ushort)AnimationFrame), Position, this, Random);
    public void PlayFrameSound(uint sound, FixedVector3 position, int pitchOffset) => configuration.Effects.PlayPositionSound(sound, position, pitchOffset);
    public void PlaySound(ushort sound) => PlayPositionSound(sound, Position, 0);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) => configuration.Effects.PlayPositionSound(sound, position, pitchOffset);
    public void PlayPositionalSound(ushort sound, FixedVector3 position, int pitchOffset) => PlayPositionSound(sound, position, pitchOffset);
    public void PlayGlobalSound(ushort sound, int volume, int pitchOffset) => configuration.Effects.PlayGlobalSound(sound, volume, pitchOffset);
    public void PlayRangedSound(ushort sound, int pitchOffset) => PlayPositionSound(sound, Position, pitchOffset);
    public void PlayMessageSound(uint sound, FixedVector3 position, int pitchOffset) => configuration.Effects.PlayPositionSound(sound, position, pitchOffset);
    public void PlayPlayerHitSound(ushort sound) => PlayPositionSound(sound, PlayerPosition, 0);
    public void Victorious() => PlayPositionSound(0x8024, Position, 0);
    public void MakeSpriteRing(FixedVector3 localOffset) => ActorSpriteRing.Create(Actor.Body, localOffset, this);
    public void EmitSprite(ActorSpriteRingParticle particle) => configuration.SpriteRings.Emit(particle);
    public int LaserCollision(ThugShotLine line, ref FixedVector3 direction, bool checkPlayer)
    {
        var sourceLine = new ActorLaserLine { Start = line.Start, End = line.End };
        int result = ActorLaserCollision.Check(State.Type, sourceLine, ref direction, checkPlayer, this, Random);
        line.Start = sourceLine.Start; line.End = sourceLine.End; LastLaserHit = World.LastHit;
        return result;
    }
    public ActorLaserContact? TraceZoned(FixedVector3 from, FixedVector3 to, bool includeEnvironment) => Contact(World.Trace(from, to, includeEnvironment));
    public ActorLaserContact? TraceEnvironmental(FixedVector3 from, FixedVector3 to) => Contact(World.TraceEnvironmental(from, to));
    private static ActorLaserContact? Contact(GodotActorWorldHit? hit) => hit is null ? null : new(hit.Collider, hit.Position);
    public void ClearLaser() => configuration.Effects.ClearLaser(this);
    public void DeleteLaser() => ClearLaser();
    public void SetLaser(FixedVector3 start, FixedVector3 end) => configuration.Effects.SetLaser(this, start, end);
    public void DrawTracer(ThugShotLine line, bool attachToPlayer, byte red, byte green, byte blue) =>
        configuration.Effects.DrawTracer(this, line, attachToPlayer ? configuration.Player : null, red, green, blue);
    public void DrawHostageTracer(ThugShotLine line, ActorProcedureState hostage, byte red, byte green, byte blue) =>
        configuration.Effects.DrawTracer(this, line, hostage, red, green, blue);
    public void DrawBarrelFlash(ThugShotLine line, byte red, byte green, byte blue) => configuration.Effects.DrawBarrelFlash(this, line, red, green, blue);
    public void DrawGlowFlash(FixedVector3 position, IReadOnlyList<int> arguments) => configuration.Effects.DrawGlowFlash(position, arguments);
    public void SpawnGrenade(ThugGrenadeRequest request) => configuration.Effects.SpawnGrenade(this, request);
    public void DoPhysics(int mode)
    {
        Actor.Body.UseLocalVelocity = (Actor.BehaviorFlags & 1) != 0;
        Actor.Body.LimitedMotionTicks = State.ActionCounter;
        Actor.Body.Integrate(FrameTiming.ElapsedTicks, limitElapsed: mode != 0);
        if (mode != 0) State.ActionCounter = Actor.Body.LimitedMotionTicks;
    }
    public void KillShadow() => configuration.Effects.KillShadow(this);
    public IThugInjuryBody? StruckGameObject(int first, int second) => configuration.Effects.StruckGameObject(this, first, second);
    public void ElasticCollision(IThugInjuryBody target, FixedVector3 velocity, int duration) => configuration.Effects.ElasticCollision(this, target, velocity, duration);
    public void CheckFallBack() => configuration.Effects.CheckFallBack(this);
    public void NotifyKill(int type) => configuration.Effects.NotifyKill(type);
    public bool Die(int stage) => ThugDeathLifecycle.Step(State, Actor, this, stage);
    public void MarkDead() { configuration.Combat.Remove(this); Lifecycle.Kill(); }
    public bool Grab(FixedVector3 destination) => ThugGrabEntry.TryGrab(State, Actor, StateFlags, destination, AddPointToPath);
    public bool GrabUpdate(ref FixedVector3 position, ref short yaw) => configuration.Effects.GrabUpdate(this, ref position, ref yaw);
    public void ApplyHitToBody(IThugInjuryBody body, ActorHit hit) => configuration.Effects.ApplyHitToBody(body, hit);
    public IThugTrapWeb? RecoverTrapWeb() => configuration.Effects.RecoverWeb(this, false);
    public void BurstTrapWeb() => RecoverTrapWeb()?.Burst();
    public void BurstTugWeb() => configuration.Effects.RecoverWeb(this, true)?.Burst();
    public void SetPlayerWebCombo(int duration, int amount) => configuration.Effects.SetPlayerWebCombo(duration, amount);
    public void PlayDialogue(int bank, int clip, FixedVector3 position, int attenuation)
    => configuration.Effects.PlayDialogue(this, bank, clip, position, attenuation);
    public ActorBodyMotion? RecoverConversationPartnerBody() => conversationPartner is { } partner && GodotObject.IsInstanceValid(partner) ? partner.Actor.Body : null;
    public void StoreConversationPartner(ThugState owner, ThugState partner)
    {
        IReadOnlyList<GodotThugActor> order = configuration.SourceBaddyOrder();
        GodotThugActor first = order.Single(actor => ReferenceEquals(actor.State, owner));
        first.conversationPartner = order.Single(actor => ReferenceEquals(actor.State, partner));
    }
    public void StrikeUpConversation() => ThugConversationBehavior.StrikeUpConversation(new(State, Actor, StateFlags), this, Random);
    public ThugState? FindClosest(int actorType)
    {
        int best = 10656; GodotThugActor? found = null;
        foreach (GodotThugActor candidate in configuration.SourceBaddyOrder())
        {
            if (ReferenceEquals(candidate, this) || (actorType != 0 && candidate.State.Type != actorType)) continue;
            int distance = ThugPerception.ApproximateHorizontalDistance(Position, candidate.Position);
            if (distance >= best) continue;
            best = distance; found = candidate;
        }
        return found?.State;
    }
    public void SendMessage(ThugState recipient, int code) =>
        configuration.Effects.SendMessage(this, configuration.SourceBaddyOrder().Single(actor => ReferenceEquals(actor.State, recipient)), code);
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) => Messages.Enqueue(new(code, sender) { Vector0 = vector });
    public void SendSelfMessage(int code) => ReceiveMessage(code, null);
    public void WarnOtherThugs()
    {
        ThugState? closest = FindClosest(304) ?? FindClosest(312);
        if (closest is { AlertTicks: 0 }) SendMessage(closest, 7);
    }
    public void ProcessMessages() => ThugMessageProcessor.Process(State, Actor, Procedures, Messages, this);
    public void GetReadyToShootHostage(ThugMessage message) => ThugMessageReactions.GetReadyToShootHostage(State, Actor, Procedures, message, this);
    public void RunToWhereTheActionIs(FixedVector3 position) => ThugMessageReactions.RunToWhereTheActionIs(State, Actor, position, this);
    public bool TryGetMessageSenderPosition(ThugMessage message, out FixedVector3 position)
    {
        ActorProcedureState? sender = configuration.Effects.RecoverActor(message.SenderHandle);
        position = sender?.Body.Position ?? default; return sender is not null;
    }
    public void TrySendToMessageSender(ThugMessage message, int code)
    {
        if (message.SenderHandle is { } handle && configuration.Effects.RecoverActor(handle) is not null)
            configuration.Effects.SendMessage(this, handle, code);
    }
    public void StoreHostageHandle(object? senderHandle) => hostageHandle = senderHandle;
    public ActorProcedureState? RecoverHostage() => configuration.Effects.RecoverActor(hostageHandle);
    public ActorBodyMotion? RecoverHostageBody(ActorProcedureState hostage) => configuration.Effects.RecoverBody(hostage);
    public void ReplyToStoredHostage(int code)
    {
        if (hostageHandle is { } handle && configuration.Effects.RecoverActor(handle) is not null)
            configuration.Effects.SendMessage(this, handle, code);
    }
    public void SendHostageMessage(ActorProcedureState hostage, int code) => configuration.Effects.SendMessage(this, hostage, code);
    public void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition) => configuration.Effects.SelectHostageCamera(actorPosition, hostagePosition);
    public void SendSignal() => configuration.Effects.SendSignal(this);
    public void SendDeathPulse()
    {
        if (!ScriptState.TryMarkDeathPulseSent()) return;
        foreach (ushort link in Definition.Links) registry.ReceivePulse(link);
    }
    public void Retire() { SendDeathPulse(); MarkDead(); }
    public short ReadVariable(ushort variable, ActorScriptCursor operands) { SyncScriptStorage(); return Variables.Read(variable, operands); }
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    {
        SyncScriptStorage(); Variables.Write(variable, operands);
        Position = Variables.Position; State.Health = Variables.Health;
        State.MinorRadius = Variables.CollisionRadius;
        State.SetParamByIndex(9, Variables.Damage);
        Model.PlaybackSpeed = Variables.AnimationSpeed;
        State.SurfaceClearance = Variables.GroundOffset;
        Height.Clearance = State.SurfaceClearance;
        Path.Clearance = State.SurfaceClearance;
    }
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, SourceNodeId, opcode, operands)) return true;
        if (GodotActorLinkEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorDirectEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotEnvironmentChunkCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorScreenFlashCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Actor.Body.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if (GodotActorSpeechCommands.TryExecute(registry, opcode, operands, ScriptState, environment, SourcePosition)) return true;
        if (opcode == 0x42B9 && GodotActorPlayerScriptCommands.TryExecute(registry, opcode, operands)) return true;
        if (Route.TryExecuteCommand(opcode, operands)) return true;
        // CBaddy::ExecuteCommand: 470F has no operands and sets body field2A8.
        if (opcode == 0x470F) { Actor.BehaviorFlags |= 0x02000000; return true; }
        if (opcode == 0x470C)
        {
            State.SetParamByIndex(operands.ReadScriptValue(ReadVariable), operands.ReadScriptValue(ReadVariable));
            Variables.Damage = unchecked((ushort)State.Field1FC); return true;
        }
        if (configuration.Effects.ExecuteScriptCommand(this, opcode, operands, environment)) return true;
        throw new NotSupportedException($"Thug {Definition.NodeId}: source script command 0x{opcode:X4} is not connected.");
    }
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public int Random(int range) => configuration.Random(range);
    public ushort FirstLink(ushort nodeId) => registry.FirstLink(nodeId);
    public void SelectModel(uint checksum) => registry.SelectModel(checksum);
}
