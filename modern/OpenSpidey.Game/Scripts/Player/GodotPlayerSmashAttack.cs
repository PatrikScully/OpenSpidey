using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Player;

/// <summary>Actual smash action and its two posed foot trails, using shared player fields and source actor contacts.</summary>
public sealed class GodotPlayerSmashAttack : IPlayerSmashAttackHost, IPlayerSmashContactHost, IPlayerSmashFrameHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerMeleeDirectory directory;
    private readonly GodotPlayerMeleeSession session;
    private readonly GodotSmokeTrails trails;
    private readonly Func<uint> clock;
    private readonly SmokeTrail?[] currentTrails = new SmokeTrail?[2];
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Smash attacks require the genuine posed melee player.");
    public PlayerSmashAttackState State { get; }
    public uint Clock => clock();
    public bool FirstTrailPresent => currentTrails[0] is not null;
    public uint TrailColour { get; }
    internal IReadOnlyList<SmokeTrail?> CurrentTrails => currentTrails;

    public GodotPlayerSmashAttack(GodotPlayer player, PlayerSmashAttackState state, GodotPlayerMeleeDirectory directory,
        GodotPlayerMeleeSession session, GodotSmokeTrails trails, Func<uint> clock)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(directory); ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(trails);
        this.player = player; State = state; this.directory = directory; this.session = session; this.trails = trails; this.clock = clock;
        ArgumentNullException.ThrowIfNull(clock);
        if (!ReferenceEquals(state.Melee, Melee.State))
            throw new ArgumentException("Smash entry, action and posed player must share one melee state.", nameof(state));
        // Player constructor field580 selects the source foot-ribbon colour from the current suit.
        TrailColour = player.Combat.CurrentSuit switch
        {
            1 or 4 or 7 => 0x402020,
            2 or 8 or 9 => 0x200000,
            3 => 0x404040,
            5 => 0x403030,
            _ => 0x202040
        };
    }

    public void Update() => PlayerSmashAttackBehavior.Step(State, player.Model.Playback.IsFinished, this);
    public void ResolveContacts(FixedVector3 previousPosition) => PlayerSmashAttackContacts.Resolve(State, previousPosition, this);
    public void CreateSmashAttackTrails()
    {
        for (int slot = 0; slot < currentTrails.Length; slot++)
        {
            if (currentTrails[slot] is not null) continue;
            currentTrails[slot] = trails.Emit(Melee.GetPlayerHookPosition(slot + 5), 4,
                unchecked((byte)TrailColour), unchecked((byte)(TrailColour >> 8)), unchecked((byte)(TrailColour >> 16)));
            SetTrailPresent(slot, true);
        }
    }
    /// <summary>Source buildPose updates trail endpoints after applying the original player joints.</summary>
    public void UpdateTrails()
    {
        for (int slot = 0; slot < currentTrails.Length; slot++)
            currentTrails[slot]?.SetPosition(Melee.GetPlayerHookPosition(slot + 5));
        trails.Rebuild();
    }
    public void FadeSmashAttackTrails() { FadeSmokeTrail(0); FadeSmokeTrail(1); }
    public void FadeSmokeTrail(int slot)
    {
        if ((uint)slot >= currentTrails.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        if (currentTrails[slot] is { } trail) trail.FadeAway = 1;
        currentTrails[slot] = null; SetTrailPresent(slot, false);
    }
    private void SetTrailPresent(int slot, bool present)
    {
        if (slot == 0) State.Melee.Combat.FirstSmokeTrailActive = present;
        else State.Melee.Combat.SecondSmokeTrailActive = present;
    }
    public void OrientToNormal(bool targetPresent, FixedVector3 target) => Melee.OrientToNormal(targetPresent, target);
    public void PlaySingleAnimation(ushort animation) => Melee.PlaySingleAnimation(animation, 0, -1);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public PlayerSmashSphereContact? TraceBaddies(FixedVector3 start, FixedVector3 end, object? ignoredTarget)
    {
        ActorLaserBody[] bodies = directory.SourceBodies.Select(body => new ActorLaserBody(body.Identity, body.Position, body.Radius, body.BodyFlags)).ToArray();
        ActorSphereContact? contact = ActorLaserCollision.CheckBodies(start, end, bodies, ignoredTarget, 4096, session.IgnoreSphereRadius);
        return contact is null ? null : new(directory.RequireBody(contact.Body.Identity), contact.Position);
    }
    public int GetDamage(int baseDamage) => Melee.Controller.GetDamage(baseDamage);
    public void HitTarget(IPlayerMeleeTarget target, ActorHit hit) => Melee.HitTarget(target, hit);
    public void PlayPositionSound(ushort sound, FixedVector3 position) => Melee.PlayPositionSound(sound, position, 0);
}
