using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerLookaroundSphereHit(IPlayerMeleeTarget Target, ushort BodyFlags, FixedVector3 Position);
public readonly record struct PlayerLookaroundWorldContact(object Identity, FixedVector3 Position, FixedVector3 Normal,
    uint FaceFlags, int Distance);
public readonly record struct PlayerLookaroundSwitchContact(IPlayerMeleeTarget Target, int Mode);

public sealed class PlayerLookaroundTargetState(PlayerLookaroundTransitionState transition, PlayerMeleeState melee)
{
    public PlayerLookaroundTransitionState Transition { get; } = transition;
    public PlayerMeleeState Melee { get; } = melee;
    public IPlayerMeleeTarget? TorsoTarget { get; set; }
    public FixedVector3 TargetPosition { get; set; }
    public FixedVector3 SurfaceNormal { get; set; }
    public FixedVector3 ZipOrigin { get; set; }
    public FixedVector3 SwingFacingTarget { get; set; }
    public uint ReticleColour { get; set; }
    public PlayerLookaroundWorldContact? ZipContact { get; set; }
}

public interface IPlayerLookaroundTargetHost
{
    uint Clock { get; }
    short EffectiveHeading { get; }
    PlayerLookaroundSphereHit? TraceBaddySphere(FixedVector3 start, FixedVector3 end);
    PlayerLookaroundWorldContact? TraceTargetWorld(FixedVector3 start, FixedVector3 end);
    PlayerLookaroundSwitchContact ResolveSwitch(object itemIdentity);
    bool CheckZipAvailability(PlayerLookaroundWorldContact contact, int range);
    bool CheckSwingAvailability(PlayerLookaroundTargetState state, PlayerLookaroundWorldContact contact);
    void BeginZip(PlayerLookaroundWorldContact contact);
    void BeginSwing();
    void SelectAndRunAnimation(ushort animation);
    void SetTargetTorsoAngle(short wanted, bool fast);
    void ExitLookaroundMode();
    void SetTargetVisible(bool visible);
    void SetScreenTarget(FixedVector3 position, ushort radius, short angle);
}

/// <summary>Actual SetupLookaroundCamera steady target/switch/zip/swing selection after its camera-placement slice.</summary>
public static class PlayerLookaroundTargetSelection
{
    public static void Step(PlayerLookaroundTargetState state, PlayerLookaroundCameraSetupResult setup,
        FixedJointTransform bodyRotation, IPlayerLookaroundTargetHost host)
    {
        if (!setup.SelectTarget) return;
        PlayerLookaroundTransitionState transition = state.Transition;
        PlayerLookaroundState angles = transition.Angles;
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        FixedJointTransform look = PlayerLookaroundCameraSetup.MultiplyRotation(bodyRotation, setup.RawLookRotation);
        FixedVector3 farPoint = setup.Anchor - (new FixedVector3(look.M02, look.M12, look.M22) << 12);
        transition.ReticleType = 1;
        state.TorsoTarget = null;
        if (host.TraceBaddySphere(setup.Anchor, farPoint) is { } sphere)
        {
            farPoint = sphere.Position;
            if ((sphere.BodyFlags & 0x10) != 0) state.TorsoTarget = sphere.Target;
        }
        PlayerLookaroundWorldContact? trace = host.TraceTargetWorld(setup.Anchor, farPoint);
        if (trace is { } contact)
        {
            state.SurfaceNormal = contact.Normal; state.TargetPosition = contact.Position;
            state.TorsoTarget = null; state.ReticleColour = 0x202080;
            if ((contact.FaceFlags & 0x2000000) != 0)
            {
                PlayerLookaroundSwitchContact control = host.ResolveSwitch(contact.Identity);
                if (control.Mode != 0)
                {
                    state.TorsoTarget = control.Target; state.ReticleColour = 0x802020;
                    angles.ActivateTarget = 0; return;
                }
            }
            bool zip = host.CheckZipAvailability(contact, 2048);
            if (zip) state.ReticleColour = 0x208020;
            bool swing = host.CheckSwingAvailability(state, contact);
            if (zip && !swing && angles.ActivateTarget != 0 && combat.ActionState == 1)
            {
                state.SurfaceNormal = contact.Normal;
                state.ZipOrigin = combat.Position; state.ZipContact = contact;
                combat.ActionState = 0x40000; melee.WebAttackStarted = 1;
                host.BeginZip(contact);
                host.SelectAndRunAnimation(combat.IsCrawling ? (ushort)0x104 : (ushort)0xFA);
                host.SetTargetTorsoAngle(unchecked((short)(host.EffectiveHeading + angles.Yaw)), false);
                host.ExitLookaroundMode(); angles.ActivateTarget = 0; return;
            }
            if (swing)
            {
                state.ReticleColour = 0x208020;
                if (angles.ActivateTarget != 0 && combat.ActionState == 1)
                {
                    combat.ActionState = 0x100; melee.WebAttackStarted = 1;
                    host.BeginSwing(); host.SelectAndRunAnimation(0x111);
                    if (!combat.IsCrawling)
                    {
                        host.SetTargetTorsoAngle(unchecked((short)(host.EffectiveHeading + angles.Yaw)), false);
                        state.SwingFacingTarget = default;
                    }
                    else state.SwingFacingTarget = contact.Position;
                    combat.IsCrawling = false;
                    host.ExitLookaroundMode(); angles.ActivateTarget = 0; return;
                }
            }
        }
        else
        {
            state.TargetPosition = farPoint;
            if (state.TorsoTarget is { } target)
            {
                transition.ReticleType = 0; host.SetTargetVisible(true);
                host.SetScreenTarget(target.Position, 24, unchecked((short)(32 * (host.Clock & 127))));
                state.TargetPosition = target.Position; angles.ActivateTarget = 0; return;
            }
            state.ReticleColour = 0x202080;
        }
        angles.ActivateTarget = 0;
    }
}
