using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source324 CSimby: constructor 0x4A7870, AI 0x4AE3D0 Initialize/Emerge/MoveFreely leaves.</summary>
public partial class GodotSimbyActor : GodotScriptActor, ISimbyConstructionHost, ISimbyHost, IGodotSourceBaddyBody, IActorScriptHost,
    IActorHeightHost, IActorPathHost, IActorAttackMonitorHost
{
    private readonly ActorBodyMotion playerBody = new();
    private readonly List<GodotSimbyBaseActor> symBases = [];
    private FixedJointTransform[] hookPose = [];
    private GodotActorRegistry registry = null!;
    private GodotSimbyConfiguration configuration = null!;
    private SimbyBrain brain = null!;
    private TriggerNode sourceNode = null!;
    private bool begun;
    private bool destroyed;
    public SimbyState State { get; private set; } = null!;
    public SimbyStateFlags StateFlags { get; } = new();
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorBodyMotion Body => Actor.Body;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public ActorPoseLinkPacket PoseLinks { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ActorPathController Path { get; private set; } = null!;
    public FixedVector3 LastLineHitNormal { get; private set; }
    public uint PlayerDistance { get; private set; }
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public int AnimationId => Model.ClipId;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public int ElapsedTicks => FrameTiming.ElapsedTicks;
    public int AttackClock => configuration.AttackClock();
    public FixedVector3 PlayerPosition
    {
        get => registry.PlayerPosition ?? default;
        set => throw new NotSupportedException("CSimby GettingGrabbed carrying the player needs a CPlayer position host.");
    }
    public ushort PlayerMinorRadius => RequirePlayer().ActorState.MinorRadius;
    public int AnimationFrame => Model.Playback.Frame;
    public byte HeightQueryInterval { set { State.HeightQueryInterval = value; Height.QueryInterval = value; } }
    /// <summary>The 0x164 matrix is rotation only; DoStretch never runs while ItemFlags bit 0x200 scales it.</summary>
    public FixedJointTransform TransformBasis => SourceTransformBasis;
    public int CeilingSetupLow => 555819296;
    public int CeilingSetupHigh => 543;
    public int SupportVelocityY => Height.Support?.Velocity.Y ??
        throw new InvalidOperationException("CSimby DropDown read field_2A4 velocity without a support body.");
    public ThugMessage? FirstMessage => Messages.Head;
    public int PlayerAnimationId => RequirePlayer().Model.ClipId;
    public int PlayerActionState
    {
        get => unchecked((int)RequirePlayer().Controller.Action);
        set => throw new NotSupportedException("CSimby GettingGrabbed throw needs a CPlayer field_E1C host.");
    }
    public short PlayerYaw => RequirePlayer().Controller.Orientation.Heading;
    public FixedVector3 PlayerVelocity { set => throw new NotSupportedException("CSimby GettingGrabbed throw needs a CPlayer velocity host."); }
    public int DomeCount => RequireEffects().DomeCount;
    /// <summary>CBaddy field_1FC, written by the script variable path.</summary>
    public ushort AttackDamage => Variables.Damage;
    public short PlayerHealth => registry.Player?.Combat.State.Health ?? 0;
    public byte PlayerSpecialState => 0;
    public byte PlayerInputLock => 0;
    public bool HasProcedures => Procedures.Count != 0;
    public bool HasSupportBody => Height.Support is not null;
    public int IdleSetupLow => 513;
    public int IdleSetupHigh => 0;
    public int CurrentWaypoint => Route.CurrentNode;
    public ushort WaypointFlags { get => unchecked((ushort)Route.Flags); set => Route.Flags = value; }
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => State.BehaviorFlags;
    public short Health => State.Health;

    public static GodotSimbyActor Create(TriggerNode node, GodotActorRegistry registry, GodotSimbyConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != SimbyState.Type) throw new NotSupportedException("Simby factory requires source324.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotSimbyActor
        {
            Name = $"Simby{node.Id}", registry = registry, configuration = configuration, Definition = definition, sourceNode = node
        };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                AngularDamping = new(1, 1, 1)
            });
            actor.ScriptState = new() { ActorType = SimbyState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry)
            {
                Position = actor.Body.Position, GroundOffset = actor.State.SurfaceClearance,
                CollisionRadius = actor.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000
            };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.World = new(actor, registry.Triggers);
            actor.Height = new(actor.Actor, actor) { Clearance = actor.State.SurfaceClearance };
            actor.Route = new(registry.Triggers, node.Id, actor.ParseWaypoint);
            actor.State.BindCombatFlags(actor.Actor);
            actor.Path = new(actor.Actor, actor);
            actor.brain = new(actor.State, configuration.World, actor.StateFlags, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "SimbyDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            registry.ActorDistanceMeasured += actor.OnDistanceMeasured;
            SimbyConstruction.Construct(actor.State, configuration.World, actor, configuration.AttackClock());
            actor.BuildSourceTransform();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    public void InitializeModel(string name)
    {
        Model = configuration.ModelFactory(State);
        Model.AdvanceAutomatically = false;
        AddChild(Model);
        Lighting = new(Model, new SourceModelLight(
            new(4096, 0, 0, 0, 4096, 0, 0, 0, 4096, 0, 0, 0),
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 0, 0, 0), configuration.LightingState);
    }
    public void AttachToSourceBaddyList() { }
    public void ReadPosePacket()
    {
        PoseLinks = configuration.PosePacket(Model.SourcePartCount);
        Model.InitializeSourceJoints(PoseLinks);
    }
    public void ParseAuthoredScript()
    {
        Runtime = new(new ActorScriptCursor(Definition.BehaviorData,
            checked(sourceNode.SourceOffset + Definition.ObjectDataOffset + 18)), this);
        Runtime.Start(registry.Environment);
    }
    public int NextRandom(int bound) => configuration.Random(bound);
    public void EnsureVertexWibbler() => configuration.EnsureVertexWibbler();

    internal override void Begin()
    {
        begun = true;
        BuildSourceTransform();
    }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        // CSimby::AI holds dword_5FBEE0 (enemy collision masks) for its whole update.
        World.CollisionCheckMode = 1;
        try { brain.Advance(); }
        finally { World.CollisionCheckMode = 0; }
        Variables.Position = Body.Position;
        Variables.CollisionRadius = State.MinorRadius;
        Variables.GroundOffset = State.SurfaceClearance;
        Variables.Health = State.Health;
        ScriptState.BehaviorFlags = State.BehaviorFlags;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public void SynchronizeSourceLighting() => Lighting.Synchronize(SourceTransformBasis, ItemFlags);
    public void BuildTransform() => BuildSourceTransform();
    private void BuildSourceTransform()
    {
        ActorAngles angles = Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        // M3d_BuildTransform applies the uniform 0x28..0x2C scale while ItemFlags bit 0x200 is set (slink/climb).
        if ((ItemFlags & 0x200) != 0) transform.Basis = transform.Basis.Scaled(Vector3.One * (State.Scale / 4096f));
        transform.Origin = PsxCoordinates.FixedWorldPosition((Body.Position >> 12) << 12);
        Transform = transform;
    }

    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("CSimby ground queries use flags0.");
        return World.GroundHeight(position, above, below);
    }
    public int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport)
    {
        int ground = World.GetGroundHeight(position, above, below, out ActorBodyMotion? support);
        hasSupport = support is not null;
        return ground;
    }
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    public int SetHeight(bool initial, int above, int below) =>
        Height.SetHeight(initial, above, below, ElapsedTicks);
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void CycleAnimation(int animation) => Model.CycleClip(animation);
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void CleanUpMessages() => Messages.Cleanup();
    public void ProcessProcedures() => Procedures.Execute(ElapsedTicks);
    /// <summary>CBaddy::DoPhysics; mode 1 spends at most the field_1F8 flight budget this update.</summary>
    public void DoPhysics(int mode)
    {
        int elapsed = ElapsedTicks;
        if (mode != 0)
        {
            elapsed = State.Field1F8 > ElapsedTicks ? ElapsedTicks : State.Field1F8;
            if (elapsed == 0) return;
            State.Field1F8 -= elapsed;
        }
        Body.Integrate(elapsed);
    }
    public void RunTimer(ref int timer)
    {
        timer = unchecked(timer - ElapsedTicks);
        if (timer < 0) timer = 0;
    }
    public bool TryAdvanceWaypoint()
    {
        bool advanced = Route.Advance();
        State.RouteFlags = unchecked((ushort)Route.Flags);
        return advanced;
    }
    public FixedVector3 WaypointPosition(int node) => World.WaypointPosition(node);
    public void SpawnEmergeEffect() { }
    // SFX_PlayPos stays unwired: routing it through the thug sound channels leaves s_slide1.wav PCM referenced at
    // story teardown (l7a3), an audio-backend retention bug outside the Simby port.
    public void PlayPositionalSound(int id) { }
    public int Random(int bound) => configuration.Random(bound);
    // CSimbySlimeBase quads are not rendered yet; the brain owns field_3D8 presence.
    public void CreateSlimeQuad(FixedVector3 position, int flags, bool scaleUp) { }
    public void DestroySlimeQuad() { }
    public void ReleaseSlimeQuad() { }
    public void UpdateSlimeQuad(FixedVector3 position, bool grounded, byte red, byte green, byte blue) { }
    public int UpdateGroundSupport(FixedVector3 position, int above, int below)
    {
        int ground = World.GetGroundHeight(position, above, below, out ActorBodyMotion? support);
        Height.Support = support;
        return ground;
    }
    public void AttachMoveTo(FixedVector3 target, int speed, int turnRate, int tolerance, uint mask) =>
        Procedures.AttachMove(new ActorMoveToProcedure(Actor, target, speed, turnRate, tolerance, mask));
    public void AttachLookAtPlayer(uint mask, int rate, int tolerance) =>
        Procedures.Attach(ActorLookAtProcedure.ForBody(Actor, RecoverPlayerBody, mask, rate, tolerance));
    public void AttachLookAtPoint(FixedVector3 point, uint mask, int rate, int tolerance) =>
        Procedures.Attach(ActorLookAtProcedure.ForPoint(Actor, point, mask, rate, tolerance));
    public void AttachLookAtYaw(int yaw, uint mask, int rate, int tolerance) =>
        Procedures.Attach(ActorLookAtProcedure.ForYaw(Actor, yaw, mask, rate, tolerance));
    public void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask) =>
        Procedures.Attach(new ActorAttackMonitorProcedure(Actor, this, startFrame, hookMask, frames, completionMask));
    public void AttachStateSwitchMessage(int message) =>
        Procedures.Attach(new SimbyStateSwitchMessageProcedure(State, message, code => ReceiveMessage(code, this)));
    public FixedVector3 DynamicHookPosition(ActorHook hook)
    {
        if (hookPose.Length != Model.SourcePartCount) hookPose = new FixedJointTransform[Model.SourcePartCount];
        for (int part = 0; part < hookPose.Length; part++) hookPose[part] = Model.GetSourcePartPose(part);
        // CItem MATRIX.t is the whole-unit body position M3d_BuildTransform cached.
        return ActorHookPosition.Evaluate(Body.Position, Body.Position >> 12, SourceTransformBasis, hookPose, hook);
    }
    public FixedVector3 GetActorHookPosition(ActorHook hook) => DynamicHookPosition(hook);
    public void ApplyPose()
    {
        for (int joint = 0; joint < State.Joints.Length; joint++) Model.SetSourceJoint(joint, State.Joints[joint]);
        BuildSourceTransform();
        Model.ApplySourceJointPose(true);
    }
    public bool CollideWithPlayer(FixedVector3 from, FixedVector3 to, int collisionScale, out ActorHook hitHook) =>
        RequirePlayer().Combat.CollideWithPlayer(from, to, collisionScale, out hitHook);
    /// <summary>CSimby vtable 0x53C0B4 slot 6 is nullsub_10 (retn 8): a Simby's landed blow draws no impact rings.</summary>
    public void CreateCombatImpactEffect(FixedVector3 position, int flags) { }
    public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) => RequireEffects().PlayPositionSound(sound, position, volume);
    public void HitPlayer(ActorHit hit) => RequirePlayer().Combat.Hit(hit);
    public void Victorious() { }
    // field_104 (trap web) is only set by CSimby::Hit (0x4A8AB0), which is not ported; Mem_RecoverPointer returns null.
    public bool TrapWebBurning() => false;
    public void BurstTrapWeb() { }
    /// <summary>CPlayer::CanITalkRightNow (0x4B9730).</summary>
    public bool PlayerCanTalk() => (RequirePlayer().Combat.State.ActionState & 0x800080) == 0;
    /// <summary>Redbook_XAPlayPos at the player, then CBody::AttachXA on the player.</summary>
    public bool PlayAndAttachPlayerXA(int bank, int slot, int volume)
    {
        if (!configuration.TryPlayPositionSpeech(bank, slot, PlayerPosition, volume)) return false;
        RequirePlayer().Combat.SpeechAttachment.Attach(configuration.SourceClock(), bank, slot);
        return true;
    }
    public void SpawnShot(FixedVector3 position) => RequireBits().SpawnShot(position, World);
    public void NotifyPlayerKill() => RequireEffects().NotifyKill(SimbyState.Type);
    /// <summary>CBaddy::Die(0) (0x404320).</summary>
    public void Die()
    {
        if ((State.BodyFlags & 0x40) != 0) return;
        if ((State.CombatFlags & 0x4000) == 0) SendDeathPulse();
        State.BodyFlags |= 0x40;
        ItemFlags |= 1;
        Lifecycle.Kill();
    }
    /// <summary>Simby_SplattyDeath (0x4A7050): hides this body (mFlags |= 1) before bursting it into blobs and drops.</summary>
    public void SplattyDeath()
    {
        ItemFlags |= 1;
        RequireBits().SplattyDeath(Body.Position, part => DynamicHookPosition(new ActorHook(0, 0, 0, unchecked((ushort)part))));
    }
    public void SpawnFireDeath() => throw new NotSupportedException("CSimbyFireDeath (0x4A3640) is not ported yet.");
    public int SpawnSymBase(FixedVector3 position, ActorAngles angles, bool following)
    {
        GodotSimbyBaseActor symBase = GodotSimbyBaseActor.Spawn(this, position, angles, registry, configuration.SymBaseMesh);
        symBase.State.Following = following ? 1 : 0;
        symBases.Add(symBase);
        return symBases.Count;
    }
    public void StartSymBaseFollowing(int handle, int scale) { SimbyBaseState symBase = SymBase(handle); symBase.Following = 1; symBase.TargetScale = scale; }
    public void SetSymBaseTarget(int handle, int scale) => SymBase(handle).TargetScale = scale;
    public void ReleaseSymBase(int handle) { SimbyBaseState symBase = SymBase(handle); symBase.Following = 0; symBase.TargetScale = 0; }
    public void StopSymBaseFollowing(int handle) => SymBase(handle).Following = 0;
    public void DeleteSymBase(int handle) => SymBaseActor(handle).Delete();
    public void SetSymBaseItemScale(int handle, short scale)
    {
        SimbyBaseState symBase = SymBase(handle);
        symBase.ScaleX = scale; symBase.ScaleY = scale; symBase.ScaleZ = scale;
    }
    public void SetSymBaseAngleX(int handle, short angle) { SimbyBaseState symBase = SymBase(handle); symBase.Angles = symBase.Angles with { X = angle }; }
    // field_338/33C hold raw CSimbyBase pointers; a base that already deleted itself keeps its (unused) state object.
    private GodotSimbyBaseActor SymBaseActor(int handle) => handle > 0 && handle <= symBases.Count ? symBases[handle - 1]
        : throw new InvalidOperationException($"CSimby sym_base handle {handle} was never created.");
    private SimbyBaseState SymBase(int handle) => SymBaseActor(handle).State;
    public int CreateRibbon(int faces, byte red, byte green, byte blue, int width) => throw RibbonMissing();
    public void SetRibbonPoint(int handle, int index, FixedVector3 position) => throw RibbonMissing();
    public void SetRibbonPointY(int handle, int index, int y) => throw RibbonMissing();
    public void SetRibbonWidth(int handle, int index, int width) => throw RibbonMissing();
    public void DeleteRibbon(int handle) => throw RibbonMissing();
    public int ClipFrameCount(int clip) => Model.ReadFrameCount(clip);
    public int WebGroundY(FixedVector3 position) => RequireBits().WebGroundY(position);
    public void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int flags) =>
        RequireBits().SpawnDrop(position, velocity, groundY, flags);
    /// <summary>CPlayer_GrabUpdate (0x4BB810), the same player owner the thugs' GettingGrabbed uses (type324 hook8, 55/32).</summary>
    public bool GrabUpdate(ref FixedVector3 position, ref short yaw) =>
        (RequirePlayer().Melee ?? throw new InvalidOperationException("CPlayer_GrabUpdate needs the player's melee services."))
            .ZipDive.GrabUpdate(ref position, ref yaw);
    public void SetPlayerAi(SimbyPlayerAi ai) => throw new NotSupportedException($"SpideyAI {ai} player hook is not ported yet.");
    public void RestorePlayerAi() => throw new NotSupportedException("Restoring the player's AI after a Simby throw is not ported yet.");
    public void PlayPlayerSingleAnimation(int animation) => throw new NotSupportedException("CSimby throw needs CPlayer::PlaySingleAnim.");
    public void CreatePlayerImpactEffect(FixedVector3 position, int flags) =>
        (RequirePlayer().Melee ?? throw new InvalidOperationException("CPlayer::CreateCombatImpactEffect (0x4C6E10) needs the player's melee services."))
            .CreateCombatImpactEffect(position, flags);
    private ActorBodyMotion? RecoverPlayerBody()
    {
        if (registry.Player is not { } player || !GodotObject.IsInstanceValid(player)) return null;
        playerBody.Position = player.Controller.Position;
        playerBody.Velocity = player.Controller.Velocity;
        playerBody.Angles = new(0, player.Controller.Orientation.Heading, 0);
        return playerBody;
    }
    private GodotSimbyBits RequireBits() => configuration.Bits ??
        throw new InvalidOperationException("CSimby shots and drops need the scene's Simby Quad bit owner.");
    private GodotPlayer RequirePlayer() => registry.Player ?? throw new InvalidOperationException("CSimby needs the live player (MechList).");
    private IGodotThugNativeEffects RequireEffects() => configuration.Effects ??
        throw new InvalidOperationException("CSimby sound and kill boundaries need the scene's native effect services.");
    private static NotSupportedException RibbonMissing() => new("CSimpleTexturedRibbon (0x40A460, display 0x40AA00) is not ported yet.");
    private void OnDistanceMeasured(GodotScriptActor actor, uint distance) { if (ReferenceEquals(actor, this)) PlayerDistance = distance; }

    public bool LineOfSight(FixedVector3 from, FixedVector3 to, out FixedVector3 hit)
    {
        hit = default;
        return LineOfSight(from, to, 0, ref hit);
    }
    public bool LineToItem(FixedVector3 start, FixedVector3 end, out int hitY)
    {
        GodotActorWorldHit? contact = World.Trace(start, end, includeEnvironmentalActors: true);
        hitY = contact?.Position.Y ?? 0;
        return contact is not null;
    }
    public bool CheckObjectCollision(FixedVector3 from, FixedVector3 to)
    {
        if (registry.Player is { } player)
        {
            var body = new ActorLaserBody(player, PlayerPosition, player.ActorState.MinorRadius, player.ActorState.BodyFlags);
            if (ActorLaserCollision.CheckBodies(from, to, [body], excluded: this,
                    ignoreRadius: configuration.IgnoreSphereRadius()) is not null) return true;
        }
        World.LineOfSightCheck = true;
        try { return World.TraceEnvironmental(from, to) is not null; }
        finally { World.LineOfSightCheck = false; }
    }
    public ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int groundRange, FixedVector3 initialHit)
    {
        Path.Radius = State.MinorRadius;
        Path.Clearance = State.SurfaceClearance;
        // field_29C: CSimby::AI Initialize stores the settled body Y there.
        Path.StoredBodyY = State.ShadowPosition.Y;
        return Path.PathCheck(from, to, wantHitPoint, groundRange, initialHit);
    }
    int IActorPathHost.CollisionCheckMode { get => World.CollisionCheckMode; set => World.CollisionCheckMode = value; }
    public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, ref FixedVector3 hit)
    {
        if (World.LineOfSight(from, to, flags, ref hit)) return true;
        // The shared Utils SLineInfo keeps the normal of the most recent blocked line (word_6B4C50).
        if (World.LastHit is { } contact)
        {
            ActorAngles normal = GodotImpactWebNativeHost.Normal(contact);
            LastLineHitNormal = new(normal.X, normal.Y, normal.Z);
        }
        return false;
    }
    int IActorPathHost.GroundHeight(FixedVector3 position, int above, int below) => World.GroundHeight(position, above, below);
    public bool BumpedIntoSpidey(int radius) => false;
    public bool Hit(ActorHit hit) => brain.Hit(hit) != 0;
    public void SendSelfMessage(int code) => ReceiveMessage(code, this);
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });
    public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
    public void WriteVariable(ushort variable, ActorScriptCursor operands) => Variables.Write(variable, operands);
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, SourceNodeId, opcode, operands)) return true;
        if (GodotActorLinkEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorDirectEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotEnvironmentChunkCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorScreenFlashCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Body.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if (GodotActorSpeechCommands.TryExecute(registry, opcode, operands, ScriptState, environment, SourcePosition)) return true;
        if (opcode == 0x42B9 && GodotActorPlayerScriptCommands.TryExecute(registry, opcode, operands)) return true;
        // CBaddy 0x4601/0x4602/0x4603: field_218 |= 0x100000/0x200000/0x400000 (0x4601 also mFlags |= 1).
        if (opcode == 0x4601) { State.CombatFlags |= 0x100000; ItemFlags |= 1; return true; }
        if (opcode == 0x4602) { State.CombatFlags |= 0x200000; return true; }
        if (opcode == 0x4603) { State.CombatFlags |= 0x400000; return true; }
        if (opcode == 0x470F) { State.BehaviorFlags |= 0x02000000; return true; }
        if (Route.TryExecuteCommand(opcode, operands)) return true;
        if (opcode == 0x4227)
        {
            ushort interval = operands.ReadWord();
            State.RouteFlags |= 4;
            State.HeightQueryInterval = unchecked((byte)interval);
            State.HeightQueryElapsed = 0;
            Height.QueryInterval = unchecked((byte)interval);
            Height.QueryElapsed = 0;
            return true;
        }
        throw new NotSupportedException($"CSimby actor script requires an unported opcode{opcode:X4}.");
    }
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => throw new NotSupportedException("CSimby script has no proven player-ceiling dependency.");
    public void FlashCompass() => throw new NotSupportedException("CSimby script has no proven compass dependency.");
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() => throw new NotSupportedException("CSimby must retain its native body resources.");
    private void ParseWaypoint(ActorScriptCursor cursor) => Runtime = new(cursor, this);
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved; registry.ActorDistanceMeasured -= OnDistanceMeasured;
        configuration.World.ReleaseTargetSlot(State);
        configuration.BossGlobal.ClearIf(this);
        SimbyReactions.ClearAttackData(State, configuration.World);
        configuration.World.Count = Math.Max(0, configuration.World.Count - 1);
        Messages.Cleanup(all: true);
    }
}
