using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public interface IWebDomeShockTarget
{
    object Identity { get; }
    FixedVector3 Position { get; }
    ushort Type { get; }
    ushort BodyFlags { get; set; }
    IWebDomeShockTarget? Next { get; }
}
public interface IWebDomeShockHost
{
    IWebDomeShockTarget? BaddyHead { get; }
    IWebDomeShockTarget? EnvironmentalHead { get; }
    bool LowMemory { get; }
    void ShakeCamera(FixedVector3 position, int magnitude);
    void SetFlaming(IWebDomeShockTarget target, int frames, byte red, byte green, byte blue);
    void EmitGlow(FixedVector3 position, int size, byte red, byte green, byte blue, int growth, int first, int second);
    void Hit(IWebDomeShockTarget target, ActorHit hit);
}

/// <summary>Actual non-rendered CDomeShockWave: source radius140 visits, hit flags100 and both live body lists.</summary>
public sealed class WebDomeShockWave
{
    private readonly IWebDomeShockHost host;
    private readonly int[] phases = new int[16];
    public FixedVector3 Position { get; }
    public FixedVector3 ColourAmplitude { get; private set; } = new(240, 240, 240);
    public int FireVariant { get; }
    public int Radius { get; private set; }
    public int Height { get; private set; }
    public bool Alive { get; private set; } = true;
    public IReadOnlyList<int> Phases => phases;

    public WebDomeShockWave(FixedVector3 playerPosition, int fireVariant, IWebDomeShockHost host, Func<int, int> random)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        Position = playerPosition with { Y = unchecked(playerPosition.Y + 204800) };
        FireVariant = fireVariant;
        ResetHitFlags(host.BaddyHead); ResetHitFlags(host.EnvironmentalHead);
        for (int index = 0; index < phases.Length; index++) phases[index] = random(4096);
        host.ShakeCamera(Position, 1);
    }
    public void AdvanceBits()
    {
        if (!Alive) return;
        Radius = unchecked(Radius + 140);
        if (Radius > 2000) Height = unchecked(Height + 100);
        ColourAmplitude -= new FixedVector3(16, 16, 16);
        if (ColourAmplitude.X <= 0 && ColourAmplitude.Y <= 0 && ColourAmplitude.Z <= 0) Alive = false;
        // The original performs both hit walks even on the update that called Die.
        HitTargets(host.BaddyHead, true); HitTargets(host.EnvironmentalHead, false);
    }
    private static void ResetHitFlags(IWebDomeShockTarget? head)
    { for (IWebDomeShockTarget? target = head; target is not null; target = target.Next) target.BodyFlags &= 0xFEFF; }
    private void HitTargets(IWebDomeShockTarget? head, bool baddy)
    {
        for (IWebDomeShockTarget? target = head; target is not null; target = target.Next)
        {
            ushort flags = target.BodyFlags;
            if ((flags & 0x10) == 0 || (flags & 0x100) != 0 || target.Type == 401) continue;
            int height = unchecked(Position.Y - target.Position.Y) >> 12;
            if (height <= -300 || height >= 300 || unchecked((int)ActorLifecycle.Distance(Position, target.Position)) > Radius) continue;
            if (FireVariant != 0 && target.Type == 324) host.SetFlaming(target, 20, 255, 123, 0);
            else if (baddy && !host.LowMemory) host.EmitGlow(target.Position, 70, 128, 128, 128, 5, 0, 100);
            ushort damage = Radius > 500 ? (ushort)0 : unchecked((ushort)(200 - unchecked(200 * Radius) / 500));
            FixedVector3 direction = FixedVectorMath.Normalize(((target.Position - Position) with { Y = 0 }) >> 12);
            host.Hit(target, new(30, 0, FireVariant != 0 ? 22 : 21, damage, direction, 512, 15));
            target.BodyFlags |= 0x100;
        }
    }
}
