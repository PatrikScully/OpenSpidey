using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerAirborneAttackConfiguration
{
    public required Func<bool> SwingPresent { get; init; }
    public required Action<bool> ReleaseSwing { get; init; }
    public required Action CreateSmashAttackTrails { get; init; }
    public required Action<PlayerWorldHit> BeginZip { get; init; }
}

/// <summary>Actual airborne attack gates over the shared player aim, source world contact, inventory and currentCWeb.</summary>
public sealed class GodotPlayerAirborneAttackSelection : IPlayerSmashAttackSelectionHost, IPlayerCeilingSmashSelectionHost, IPlayerStraightZipSelectionHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerFireWeb fire;
    private readonly GodotPlayerLookaroundAim lookaround;
    private readonly PlayerFollowCamera camera;
    private readonly GodotActorWorldQuery world;
    private readonly GodotPlayerMeleeSession session;
    private readonly GodotPlayerAirborneAttackConfiguration configuration;
    private GodotActorWorldHit? lastWorldContact;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Airborne attack selection requires the shared posed melee player.");
    public PlayerSmashAttackState Smash { get; }
    public PlayerStraightZipState Zip { get; }
    public uint Clock => fire.Clock;
    public bool SwingPresent => configuration.SwingPresent();
    public byte SwingHeldLatch => lookaround.State.SwingHeldLatch;

    public GodotPlayerAirborneAttackSelection(GodotPlayer player, GodotPlayerFireWeb fire, GodotPlayerLookaroundAim lookaround,
        PlayerFollowCamera camera, GodotActorWorldQuery world, GodotPlayerMeleeSession session,
        GodotPlayerAirborneAttackConfiguration configuration)
    {
        this.player = player; this.fire = fire; this.lookaround = lookaround; this.camera = camera;
        this.world = world; this.session = session; this.configuration = configuration;
        ArgumentNullException.ThrowIfNull(configuration.SwingPresent); ArgumentNullException.ThrowIfNull(configuration.ReleaseSwing);
        ArgumentNullException.ThrowIfNull(configuration.CreateSmashAttackTrails); ArgumentNullException.ThrowIfNull(configuration.BeginZip);
        Smash = new(Melee.State);
        Zip = new(Melee.State, fire.State, (player.Inventory ?? throw new InvalidOperationException("Airborne zip requires actual player supplies.")).State);
    }

    public bool CheckJumpingSmashKick() => PlayerAirborneAttackSelection.CheckSmash(Smash, this);
    public bool CheckCeilingJumpingSmashPunch() => PlayerAirborneAttackSelection.CheckCeilingSmash(Smash, this);
    public void OrientToNormal(bool useTarget, FixedVector3 target) => Melee.OrientToNormal(useTarget, target);
    public bool CheckJumpingR1ZipWeb()
    {
        Zip.TargetPosition = fire.ExplicitAimPosition; Zip.SurfaceNormal = fire.ExplicitAimSurfaceNormal;
        bool accepted = PlayerStraightZipSelection.Check(Zip, this);
        if (accepted) { fire.ExplicitAimPosition = Zip.TargetPosition; fire.ExplicitAimSurfaceNormal = Zip.SurfaceNormal; }
        return accepted;
    }

    public void SetCameraFocusHook(int hook) => camera.SetFocusHook(hook);
    public void ReleaseSwing(bool createSwingBack) => configuration.ReleaseSwing(createSwingBack);
    public void SetTargetTorsoAngle(FixedVector3 point) => Melee.SetTargetTorsoAngle(point);
    public void CreateSmashAttackTrails() => configuration.CreateSmashAttackTrails();
    public void SelectAndRunAnimation(ushort animation) => SelectAndRunAnimation(animation, 0);
    public void SelectAndRunAnimation(ushort animation, int firstFrame)
    { Melee.SelectAndResetAnimationSounds(animation); Melee.RunAnimation(animation, firstFrame, -1); }

    public PlayerStraightZipContact? TraceWorld(FixedVector3 start, FixedVector3 end)
    {
        lastWorldContact = world.Trace(start, end, includeEnvironmentalActors: true);
        if (lastWorldContact is not { } hit) return null;
        var normal = GodotImpactWebNativeHost.Normal(hit);
        return new(session.ResolveWorldContact(hit), hit.Position, new(normal.X, normal.Y, normal.Z), hit.Triangle.PackedMetadata);
    }
    public bool CheckZipAvailability(PlayerStraightZipContact contact, int range) =>
        PlayerZipWebRules.IsAvailable(new(player.Combat.State.Position, player.Combat.State.UpDirection,
            player.Combat.State.CrawlSurfaceNormal, player.Combat.State.ActionState, PlayerTerrainController.SurfaceClearance), WorldContact(contact), range);
    public WebStrandOwner CreateCurrentStrand() => new(fire);
    public int FireWeb(PlayerFireWebRequest request)
    {
        // DA0/DC0 writes precede the actual FireWeb call and remain shared with manual web/aim actions.
        fire.ExplicitAimPosition = Zip.TargetPosition; fire.ExplicitAimSurfaceNormal = Zip.SurfaceNormal;
        return fire.FireWeb(request);
    }
    public void BeginZip(PlayerStraightZipContact contact) => configuration.BeginZip(WorldContact(contact));

    /// <summary>Source4B8346 current-web render end, after any genuine swing adjustment to these posed hand positions.</summary>
    public void UpdateCurrentWeb(FixedVector3 leftHand, FixedVector3 rightHand)
    {
        if (fire.State.CurrentStrand is not { } strand) return;
        FixedVector3 end = strand.HookIndex switch { 0 => rightHand, 1 => leftHand, _ => leftHand + (rightHand - leftHand) / 2 };
        strand.SetFirePosition(end, fire.CameraPositionWholeUnits, fire.Random);
    }

    private PlayerWorldHit WorldContact(PlayerStraightZipContact contact)
    {
        GodotActorWorldHit hit = lastWorldContact ?? throw new InvalidOperationException("Straight zip must retain its genuine accepted world ray.");
        if (!ReferenceEquals(session.ResolveWorldContact(hit), contact.Identity) || hit.Position != contact.Position)
            throw new InvalidOperationException("Straight zip contact no longer matches the original current world hit.");
        bool supports = hit.Collider is ImportedCollisionBody body && (body.SourceFlags & 0x100) != 0;
        return new(new(contact.Position, contact.Normal, PlayerLookaroundCameraSetup.SourceLineLength(player.Combat.State.Position, hit.Position),
            contact.FaceFlags, supports), hit.InstanceIndex, hit.ModelIndex, hit.Triangle.FaceIndex, hit.SourceAsset, hit.ModelChecksum, hit.SupportId);
    }
}
