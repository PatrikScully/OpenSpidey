using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerFireWebConfiguration
{
    public required Func<uint> Clock { get; init; }
    public required Func<FixedVector3> CameraPositionWholeUnits { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<int, int, int> ActuatorTime { get; init; }
    public required Func<ushort, FixedVector3, int, int> PlayPositionSound { get; init; }
    public required Action<FixedVector3, ActorAngles, int> CreateFireImpactWeb { get; init; }
    public required Action<FixedVector3, ActorAngles, int, Func<int, int>> EmitKnottedSplat { get; init; }
}

/// <summary>Genuine posed player web release, target virtual calls and shared source inventory/effects.</summary>
public sealed class GodotPlayerFireWeb : IPlayerFireWebHost, IPlayerWebAttachHost, IPlayerWebDripHost,
    IWebStrandOwnerHost
{
    private sealed class Target(GodotScriptActor actor, GodotActorWebContacts contacts) : IPlayerFireWebTarget
    {
        public GodotScriptActor Native => actor;
        public object Identity => actor;
        public ushort Type => checked((ushort)actor.SourceActorType);
        public FixedVector3 Position => actor.SourcePosition;
        public uint BehaviorFlags
        {
            get => actor switch
            {
                GodotThugActor thug => thug.Actor.BehaviorFlags,
                GodotHostageActor hostage => hostage.Actor.BehaviorFlags,
                _ => throw new NotSupportedException("A source switch is not a CBaddy web target.")
            };
            set
            {
                if (actor is GodotThugActor thug) thug.Actor.BehaviorFlags = value;
                else if (actor is GodotHostageActor hostage) hostage.Actor.BehaviorFlags = value;
                else throw new NotSupportedException("A source switch has no CBaddy web flags.");
            }
        }
        public IReadOnlyList<FixedVector3>? ActiveTrapStrandEndpoints => contacts.ActiveTrapEndpoints(actor);
    }

    private readonly GodotPlayer player;
    private readonly GodotPlayerMeleeDirectory directory;
    private readonly GodotPlayerMeleeSession session;
    private readonly GodotActorWorldQuery world;
    private readonly GodotImpactWebProjectiles projectiles;
    private readonly GodotWebStrands strands;
    private readonly GodotActorWebContacts contacts;
    private readonly GodotPlayerImpactLines drips;
    private readonly GodotPlayerFireWebConfiguration configuration;

    public PlayerFireWebState State { get; }
    public bool HasExplicitAim
    {
        get => Melee.State.WebAttackStarted != 0; // Existing melee field name maps source8ED, the explicit-aim flag.
        set => Melee.State.WebAttackStarted = value ? (byte)1 : (byte)0;
    }
    public FixedVector3 ExplicitAimPosition { get; set; }
    public FixedVector3 ExplicitAimSurfaceNormal { get; set; }
    public ActorAngles ExplicitAimNormal
    {
        get => new(unchecked((short)ExplicitAimSurfaceNormal.X), unchecked((short)ExplicitAimSurfaceNormal.Y),
            unchecked((short)ExplicitAimSurfaceNormal.Z));
        set => ExplicitAimSurfaceNormal = new(value.X, value.Y, value.Z);
    }
    public ActorAngles TrajectoryAngles { get; set; }
    public uint Clock => configuration.Clock();
    public FixedVector3 Position => player.Controller.Position;
    public FixedVector3 ForwardDirection => player.Controller.Orientation.Forward;
    public FixedVector3 CameraPositionWholeUnits => configuration.CameraPositionWholeUnits();
    public IPlayerFireWebTarget? AimTarget => Melee.State.AimTarget is { } target ? Wrap(target.Identity) : null;
    public int WebShot => Melee.State.WebAttackMode;
    public int PlayerWebShot => WebShot;
    public object PlayerIdentity => player;
    public ushort Animation => player.Combat.State.AnimationId;
    public byte FireWebbing => Inventory.State.FireWebbing;
    public bool VibrationEnabled => player.Combat.VibrationEnabled;
    public bool AnimationFinished => player.Model.Playback.IsFinished;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Live FireWeb requires the player's original melee/pose owner.");
    private GodotPlayerInventory Inventory => player.Inventory ?? throw new InvalidOperationException("Live FireWeb requires the player's original inventory owner.");

    public GodotPlayerFireWeb(GodotPlayer player, GodotPlayerMeleeDirectory directory,
        GodotPlayerMeleeSession session, GodotActorWorldQuery world, GodotImpactWebProjectiles projectiles,
        GodotWebStrands strands, GodotActorWebContacts contacts, GodotPlayerImpactLines drips,
        GodotPlayerFireWebConfiguration configuration)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        State = new(player.Combat.State);
        this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        this.strands = strands ?? throw new ArgumentNullException(nameof(strands));
        this.contacts = contacts ?? throw new ArgumentNullException(nameof(contacts));
        this.drips = drips ?? throw new ArgumentNullException(nameof(drips));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>CPlayer walks its direct WebList after animation/EveryFrame and before reading action input.</summary>
    public void AdvanceOwnersBeforeInput() => strands.AdvanceActors(Clock, CameraPositionWholeUnits);
    public void UpdateAttach() => PlayerWebAttachBehavior.Step(Melee.State, this);
    public void SwitchCurrentWebToBlob()
    {
        State.CurrentStrand?.SwitchToBlob();
        State.CurrentStrand = null;
    }
    public int FireWeb(PlayerFireWebRequest request) => PlayerFireWebBehavior.Fire(State, request, this);
    public void CreateWebDrips(bool firstHand, bool secondHand) => PlayerWebDrips.Create(firstHand, secondHand, this, Random);
    public bool CheckJump() => Melee.CheckJump();
    public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Melee.PlaySingleAnimation(animation, firstFrame, lastFrame);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public int Random(int bound) => configuration.Random(bound);
    public bool DecreaseWebbing(int amount) => Inventory.DecreaseWebbing(amount);
    public FixedVector3 GetPlayerHookPosition(int hook) => Melee.GetPlayerHookPosition(hook);
    public PlayerKickSwitchSelection SelectTargetSwitch(int maximumDistance, int minimumFacing, int weight, int facingWeight) =>
        Melee.SelectTargetSwitch(maximumDistance, minimumFacing, weight, facingWeight);

    public PlayerFireWebContact? TraceEnvironment(FixedVector3 start, FixedVector3 end)
    {
        world.LineOfSightCheck = true;
        GodotActorWorldHit? hit;
        try { hit = world.Trace(start, end, includeEnvironmentalActors: true); }
        finally { world.LineOfSightCheck = false; }
        if (hit is null) return null;
        object identity = session.ResolveWorldContact(hit);
        GodotSwitchActor? control = FindSwitch(identity);
        return new(hit.Position, GodotImpactWebNativeHost.Normal(hit), hit.Triangle.PackedMetadata,
            control is null ? null : Wrap(control));
    }
    public bool SwitchEnabled(IPlayerFireWebTarget target) => RequireNative(target) is GodotSwitchActor control && control.State.Mode != 0;
    public void FlickSwitch(IPlayerFireWebTarget target)
    {
        if (RequireNative(target) is not GodotSwitchActor control) throw new InvalidDataException("FireWeb switch target lost its original407 owner.");
        control.Flick();
    }
    public bool TrapWeb(IPlayerFireWebTarget target) => contacts.TrapWeb(RequireNative(target));
    public void AddTrapWebEffect(IPlayerFireWebTarget target, byte fireVariant) => contacts.AddTrap(RequireNative(target), fireVariant);
    public int TugWeb(IPlayerFireWebTarget target) => contacts.TugWeb(RequireNative(target)) ? 1 : 0;
    public void CreateImpactWeb(PlayerImpactWebRequest request) => projectiles.Emit(request, Clock);
    public void CreateFireImpactWeb(FixedVector3 position, ActorAngles direction, int damage) => configuration.CreateFireImpactWeb(position, direction, damage);
    public int PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => configuration.PlayPositionSound(sound, position, pitch);
    public void PlaySound(ushort sound, int volume, int pitch) => player.Combat.PlaySound(sound, volume, pitch);
    public int ActuatorTime(int controller, int actuator) => configuration.ActuatorTime(controller, actuator);
    public void ActivateActuator(int controller, int duration, int actuator, int strength) =>
        player.Combat.ActuatorOn(controller, unchecked((ushort)duration), actuator, strength);
    public void EmitLine(PlayerImpactLineParticle appearance, LineParticleMotion motion) => drips.Emit(appearance, motion);

    public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => directory.MakeHandle(target?.Identity);
    public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => directory.Recover(ref handle) is { } actor ? Wrap(actor) : null;
    public void AttachOwner(WebStrandOwner owner) => strands.Attach(owner);
    public void DetachOwner(WebStrandOwner owner) { State.DetachStrand(owner); strands.Detach(owner); }
    public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) =>
        configuration.EmitKnottedSplat(position, normal, fireVariant, random);
    public void ReplaceYankContact(IWebStrandTarget target, int strandCount) => contacts.ReplaceYank(RequireNative(target), strandCount);

    private Target Wrap(object identity)
    {
        if (identity is not GodotScriptActor actor || !directory.Contains(actor))
            throw new InvalidDataException("Player web target is outside the live source body/control directory.");
        if (actor is not (GodotThugActor or GodotHostageActor or GodotSwitchActor))
            throw new NotSupportedException("Player web target has no proven source virtual/contact adapter.");
        return new(actor, contacts);
    }
    private static GodotScriptActor RequireNative(IWebStrandTarget target) => target is Target typed ? typed.Native :
        throw new InvalidDataException("Player web target was not created by the genuine native target adapter.");
    public GodotScriptActor ResolveNativeTarget(IWebStrandTarget target) => RequireNative(target);
    private GodotSwitchActor? FindSwitch(object identity) => directory.SourceControls.FirstOrDefault(control =>
        ReferenceEquals(control.State.OffItem?.Identity, identity) || ReferenceEquals(control.State.OnItem?.Identity, identity));
}
