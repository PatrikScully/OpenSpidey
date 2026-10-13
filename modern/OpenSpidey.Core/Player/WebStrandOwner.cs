using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IWebStrandTarget
{
    object Identity { get; }
    ushort Type { get; }
    uint BehaviorFlags { get; set; }
    IReadOnlyList<FixedVector3>? ActiveTrapStrandEndpoints { get; }
}
public interface IWebStrandOwnerHost
{
    object PlayerIdentity { get; }
    int PlayerWebShot { get; }
    PlayerKickHandle StoreTarget(IWebStrandTarget? target);
    IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle);
    void AttachOwner(WebStrandOwner owner);
    void DetachOwner(WebStrandOwner owner);
    void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random);
    void ReplaceYankContact(IWebStrandTarget target, int strandCount);
}

/// <summary>Actual CWeb constructor, Fire/SetFirePos and complete blob contraction and direct player-list cleanup path.</summary>
public sealed class WebStrandOwner : IDisposable
{
    private readonly IWebStrandOwnerHost host;
    private PlayerKickHandle targetHandle;
    private IWebStrandSnapHost? snapHost;
    private bool disposed;
    public ushort Type => 1;
    public uint BodyFlags { get; private set; } = 0x16;
    public int Mode { get; private set; }
    public ushort Age { get; private set; }
    public uint LastUpdateTick { get; private set; }
    public int ElapsedTicks { get; private set; }
    public int AccumulatedTicks { get; private set; }
    public ushort HookIndex { get; set; }
    public int FireVariant { get; set; }
    public FixedVector3 FirePosition { get; private set; }
    public FixedVector3 TargetPosition { get; private set; }
    public KnottedWebStrand? Strand { get; private set; }
    public event Action<KnottedWebStrand>? StrandCreated;
    public WebChain? Chain { get; private set; }
    public PlayerKickHandle TargetHandle => targetHandle;
    public ActorLifePhase Phase => (BodyFlags & 0x40) != 0 ? ActorLifePhase.Dead : ActorLifePhase.Active;

    public WebStrandOwner(IWebStrandOwnerHost host)
    {
        this.host = host; host.AttachOwner(this);
    }
    public void Fire(FixedVector3 hook, FixedVector3 targetPosition, IWebStrandTarget? target, bool splat,
        ActorAngles normal, FixedVector3 cameraWhole, Func<int, int> random)
    {
        CheckAlive(); FirePosition = hook; TargetPosition = targetPosition;
        if (splat) host.EmitKnottedSplat(targetPosition, normal, FireVariant, random);
        if (Strand is null)
        {
            targetHandle = host.StoreTarget(target);
            Strand = new(TargetPosition, FirePosition, random);
            StrandCreated?.Invoke(Strand);
            Strand.SetEndpoints(TargetPosition, FirePosition);
            Strand.Side = KnottedWebStrand.FacingCamera(TargetPosition, FirePosition, cameraWhole);
            Strand.CacheRestPositions();
            Strand.FireVariant = FireVariant;
            int shot = target is not null && ReferenceEquals(target.Identity, host.PlayerIdentity) ? 2 : host.PlayerWebShot;
            if (shot == 1) Strand.FlickerKnots = 1;
            else if (shot == 2)
            {
                if (target is not null) host.ReplaceYankContact(target, 16);
                Strand.TriggerPulse = 1;
            }
        }
        Mode = 1;
    }
    public void SetFirePosition(FixedVector3 position, FixedVector3 cameraWhole, Func<int, int> random)
    {
        CheckAlive(); FirePosition = position;
        IWebStrandTarget? target = host.RecoverTarget(ref targetHandle);
        if (target?.ActiveTrapStrandEndpoints is { Count: > 0 } endpoints) TargetPosition = endpoints[random(endpoints.Count)];
        KnottedWebStrand strand = RequireStrand();
        strand.SetEndpoints(TargetPosition, FirePosition);
        strand.Side = KnottedWebStrand.FacingCamera(TargetPosition, FirePosition, cameraWhole);
        strand.CacheRestPositions();
    }
    public void SwitchToBlob()
    {
        CheckAlive(); Mode = 3;
        IWebStrandTarget? target = host.RecoverTarget(ref targetHandle);
        if (target is not null && target.Type != 401) target.BehaviorFlags &= ~8u;
    }
    public void SwitchToSnap(FixedVector3 direction, IReadOnlyList<FixedVector3>? path,
        IWebStrandSnapHost snapHost, FixedVector3 cameraWhole, Func<int, int> random)
    {
        CheckAlive(); ArgumentNullException.ThrowIfNull(snapHost);
        KnottedWebStrand strand = RequireStrand();
        strand.ClearPulse();
        IWebStrandTarget? target = host.RecoverTarget(ref targetHandle);
        if (target is not null)
        {
            if (target.Type != 50 && snapHost.TugImpulse(target, TargetPosition, direction, path))
                snapHost.SetFirstContactDetails();
            else { BreakIntoBits(snapHost, cameraWhole, random); return; }
        }
        this.snapHost = snapHost;
        Mode = 2;
        FixedVector3[] points = new[] { strand.Start }.Concat(strand.Ends).ToArray();
        Chain = new(points, KnottedWebFragment.Distance(points[0], points[1]), 24576, 2400, 2048);
        if (target is not null)
        {
            if (GameWord.Absolute(unchecked(snapHost.PlayerPosition.Y - snapHost.SourcePosition(target).Y)) < 122880)
                Chain.SetGroundY(unchecked(snapHost.PlayerPosition.Y + 491520));
        }
    }
    public void BreakIntoBits(IWebStrandSnapHost snapHost, FixedVector3 cameraWhole, Func<int, int> random)
    {
        CheckAlive();
        if (Strand is { } strand)
        {
            FixedVector3 start = strand.Start, end = strand.Ends[^1];
            FixedVector3 side = KnottedWebStrand.FacingCamera(start, end, cameraWhole);
            int distance = KnottedWebFragment.Distance(start, end), count = 6;
            if (distance / count < 100) count = distance / 100;
            if (count == 0) count = 1;
            FixedVector3 step = (end - start) / count;
            end = start + step;
            for (int index = 0; index < count; index++)
            {
                FixedVector3 firstVelocity = side * (random(10) + 10), secondVelocity = side * (random(10) + 10);
                if (random(2) != 0) { firstVelocity *= -1; secondVelocity *= -1; }
                firstVelocity = firstVelocity with { Y = unchecked(firstVelocity.Y - (random(20) << 12)) };
                secondVelocity = secondVelocity with { Y = unchecked(secondVelocity.Y - (random(20) << 12)) };
                snapHost.EmitBrokenStrand(new(start, end, firstVelocity, secondVelocity), random);
                if ((index & 1) != 0)
                {
                    GlowFlashState glow = snapHost.EmitSnapGlow(new(end, 5, 255, 255, 255, 0,
                        128, 128, 128, 0, 9, 0, 1, 12, 40, 6, 20, 1, 1), random);
                    glow.Angle = unchecked((ushort)random(4096));
                }
                start += step; end += step;
            }
        }
        BodyFlags |= 0x40;
    }
    public ActorUpdateAction AdvanceActor(uint sourceClock, FixedVector3 cameraWhole)
    {
        CheckAlive();
        // CPlayer::AI walks WebList directly: source Body40 deletes before calling CWeb::AI.
        if ((BodyFlags & 0x40) != 0) { Dispose(); return ActorUpdateAction.Delete; }
        AdvanceClock(sourceClock);
        if (Mode == 2)
        {
            Age = unchecked((ushort)(Age + ElapsedTicks));
            if (Age > 64) { Mode = 4; Strand?.Dispose(); Strand = null; Chain = null; }
            else if (host.RecoverTarget(ref targetHandle) is { } target)
            {
                WebChain chain = Chain ?? throw new InvalidOperationException("Source snap web requires its loose chain.");
                chain.Move((snapHost ?? throw new InvalidOperationException("Source snap web requires its target host.")).TargetAttachment(target));
                KnottedWebStrand strand = RequireStrand();
                strand.SetPolylineStart(chain.Points[0].Position);
                for (int index = 0; index < strand.SegmentCount; index++) strand.SetPolylinePoint(index, chain.Points[index + 1].Position);
            }
        }
        else if (Mode == 3)
        {
            Age = unchecked((ushort)(Age + ElapsedTicks));
            if (Age > 16) { Mode = 4; Strand?.Dispose(); Strand = null; }
            else
            {
                FixedVector3 delta = ((TargetPosition - FirePosition) >> 12) * GameAngle.Heading(Age << 6).Cosine;
                FixedVector3 start = TargetPosition - delta;
                KnottedWebStrand strand = RequireStrand();
                strand.SetEndpoints(start, TargetPosition);
                strand.Side = KnottedWebStrand.FacingCamera(start, TargetPosition, cameraWhole);
                strand.CacheRestPositions();
            }
        }
        else if (Mode == 4) BodyFlags |= 0x40;
        return ActorUpdateAction.RunBehavior;
    }
    private void AdvanceClock(uint sourceClock)
    {
        // CWeb::AI calls CBody::EveryFrame itself; its clock is independent of the player's elapsed time.
        if ((BodyFlags & 4) != 0)
        {
            ElapsedTicks = 2;
            BodyFlags &= 0xFFFB;
            AccumulatedTicks = 0;
        }
        else
        {
            int elapsed = unchecked((int)(sourceClock - LastUpdateTick));
            if (elapsed < 0) throw new InvalidOperationException("Source web clock moved backwards.");
            ElapsedTicks = Math.Min(elapsed, 6);
        }
        LastUpdateTick = sourceClock;
        AccumulatedTicks = unchecked(AccumulatedTicks + ElapsedTicks);
    }
    public void AdvanceBits(uint clock, Func<int, int> random, Action<FixedVector3, int, Func<int, int>> emitFirePulse)
    {
        CheckAlive(); Strand?.AdvanceBits(clock, random, emitFirePulse);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; host.DetachOwner(this); Strand?.Dispose(); Strand = null; Chain = null;
    }
    private KnottedWebStrand RequireStrand() => Strand ?? throw new InvalidOperationException("Original CWeb path requires its created strand.");
    private void CheckAlive() => ObjectDisposedException.ThrowIf(disposed, this);
}
