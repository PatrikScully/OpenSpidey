using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.UI;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerLookaroundTargetConfiguration
{
    public required Func<uint> Clock { get; init; }
    public required Action<PlayerWorldHit> BeginZip { get; init; }
    public required Action<PlayerSwingWebAvailabilityState> BeginSwing { get; init; }
}

/// <summary>Actual steady lookaround targets over source baddy/switch order and imported world faces, sharing explicit fire aim and the real8EA owner.</summary>
public sealed class GodotPlayerLookaroundTargets : IPlayerLookaroundTargetHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerFireWeb fire;
    private readonly GodotPlayerLookaroundCamera camera;
    private readonly GodotPlayerMeleeDirectory directory;
    private readonly GodotPlayerMeleeSession session;
    private readonly GodotActorWorldQuery world;
    private readonly GodotScreenTarget screenTarget;
    private readonly GodotPlayerLookaroundTargetConfiguration configuration;
    private GodotActorWorldHit? lastWorldHit;
    private readonly Dictionary<GodotSwitchActor, SwitchTarget> controls = [];
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Native lookaround targeting requires the genuine posed melee player.");
    public PlayerLookaroundTargetState State { get; }
    public PlayerSwingWebAvailabilityState Swing { get; } = new();
    public uint Clock => configuration.Clock();
    public short EffectiveHeading => camera.EffectiveHeading;

    public GodotPlayerLookaroundTargets(GodotPlayer player, GodotPlayerFireWeb fire, GodotPlayerLookaroundCamera camera,
        GodotPlayerMeleeDirectory directory, GodotPlayerMeleeSession session, GodotActorWorldQuery world,
        GodotScreenTarget screenTarget, GodotPlayerLookaroundTargetConfiguration configuration)
    {
        this.player = player; this.fire = fire; this.camera = camera; this.directory = directory;
        this.session = session; this.world = world; this.screenTarget = screenTarget; this.configuration = configuration;
        ArgumentNullException.ThrowIfNull(configuration.Clock); ArgumentNullException.ThrowIfNull(configuration.BeginZip); ArgumentNullException.ThrowIfNull(configuration.BeginSwing);
        State = new(camera.State, Melee.State);
    }

    public void Update(PlayerLookaroundCameraSetupResult setup)
    {
        if (!setup.SelectTarget) return;
        State.TargetPosition = fire.ExplicitAimPosition;
        State.SurfaceNormal = fire.ExplicitAimSurfaceNormal;
        PlayerLookaroundTargetSelection.Step(State, setup, player.Controller.Orientation.Basis, this);
        fire.ExplicitAimPosition = State.TargetPosition;
        fire.ExplicitAimSurfaceNormal = State.SurfaceNormal;
        Melee.AutoAim.Target = State.TorsoTarget;
        Melee.State.AimTarget = State.TorsoTarget;
        Melee.Kick.TorsoTarget = State.TorsoTarget is { } target ? new(target.Identity, target.Type, target.Position, 0) : null;
    }

    public PlayerLookaroundSphereHit? TraceBaddySphere(FixedVector3 start, FixedVector3 end)
    {
        ActorLaserBody[] bodies = directory.SourceBodies.Select(body => new ActorLaserBody(body.Identity, body.Position, body.Radius, body.BodyFlags)).ToArray();
        ActorSphereContact? contact = ActorLaserCollision.CheckBodies(start, end, bodies, radiusScale: 4096, ignoreRadius: session.IgnoreSphereRadius);
        return contact is null ? null : new(directory.RequireBody(contact.Body.Identity), unchecked((ushort)contact.Body.BodyFlags), contact.Position);
    }
    public PlayerLookaroundWorldContact? TraceTargetWorld(FixedVector3 start, FixedVector3 end)
    {
        world.LineOfSightCheck = true;
        try
        {
            lastWorldHit = world.Trace(start, end, includeEnvironmentalActors: true);
            if (lastWorldHit is not { } hit) return null;
            var normal = GodotImpactWebNativeHost.Normal(hit);
            return new(session.ResolveWorldContact(hit), hit.Position, new(normal.X, normal.Y, normal.Z), hit.Triangle.PackedMetadata,
                PlayerLookaroundCameraSetup.SourceLineLength(start, hit.Position));
        }
        finally { world.LineOfSightCheck = false; }
    }
    public PlayerLookaroundSwitchContact ResolveSwitch(object itemIdentity)
    {
        foreach (GodotSwitchActor control in directory.SourceControls)
        {
            if (!ReferenceEquals(control.State.OffItem?.Identity, itemIdentity) && !ReferenceEquals(control.State.OnItem?.Identity, itemIdentity)) continue;
            if (!controls.TryGetValue(control, out SwitchTarget? target)) controls.Add(control, target = new(control));
            return new(target, (int)control.State.Mode);
        }
        throw new InvalidDataException("An actual switch-marked target face has no owning source407 control.");
    }
    public bool CheckZipAvailability(PlayerLookaroundWorldContact contact, int range)
    {
        PlayerCombatState combat = player.Combat.State;
        return PlayerZipWebRules.IsAvailable(new(combat.Position, combat.UpDirection, combat.CrawlSurfaceNormal,
            combat.ActionState, PlayerTerrainController.SurfaceClearance), WorldContact(contact), range);
    }
    public bool CheckSwingAvailability(PlayerLookaroundTargetState state, PlayerLookaroundWorldContact contact)
    {
        Swing.TargetPosition = state.TargetPosition; Swing.PlaneNormal = state.SurfaceNormal;
        bool result = PlayerSwingWebAvailability.Check(Swing, new(player.Controller.Position, contact.Position,
            unchecked((short)contact.Normal.Y), contact.Distance, contact.FaceFlags, player.Combat.State.ActionState, player.Combat.State.Field8E9 != 0));
        state.TargetPosition = Swing.TargetPosition; state.SurfaceNormal = Swing.PlaneNormal;
        return result;
    }
    public void BeginZip(PlayerLookaroundWorldContact contact) => configuration.BeginZip(WorldContact(contact));
    public void BeginSwing() => configuration.BeginSwing(Swing);
    public void SelectAndRunAnimation(ushort animation)
    { Melee.SelectAndResetAnimationSounds(animation); Melee.RunAnimation(animation, 0, -1); }
    public void SetTargetTorsoAngle(short wanted, bool fast) => Melee.SetTargetTorsoAngle(wanted, fast);
    public void ExitLookaroundMode() => camera.ExitLookaroundMode();
    public void SetTargetVisible(bool visible) => screenTarget.SetTargetVisible(visible);
    public void SetScreenTarget(FixedVector3 position, ushort radius, short angle) => screenTarget.SetTarget(position, radius, angle);
    private PlayerWorldHit WorldContact(PlayerLookaroundWorldContact contact)
    {
        GodotActorWorldHit hit = lastWorldHit ?? throw new InvalidOperationException("Source zip eligibility requires its actual current world contact.");
        if (!ReferenceEquals(session.ResolveWorldContact(hit), contact.Identity) || hit.Position != contact.Position)
            throw new InvalidOperationException("Lookaround zip target differs from its genuine imported world hit.");
        bool supports = hit.Collider is ImportedCollisionBody body && (body.SourceFlags & 0x100) != 0;
        return new(new(contact.Position, contact.Normal, contact.Distance, contact.FaceFlags, supports),
            hit.InstanceIndex, hit.ModelIndex, hit.Triangle.FaceIndex, hit.SourceAsset, hit.ModelChecksum, hit.SupportId);
    }
    private sealed class SwitchTarget(GodotSwitchActor actor) : IPlayerMeleeTarget
    {
        public object Identity => actor;
        public FixedVector3 Position => actor.SourcePosition;
        public ushort Type => 407;
        public ushort ItemFlags => actor.Variables.ItemFlags;
        public IPlayerMeleeTarget? Next => null; // Actual source407 belongs to ControlList, not the baddy list.
    }
}
