using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

public sealed class GodotWebDomeConfiguration
{
    public required GodotPlayer Player { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required GodotPlayerMeleeDirectory Baddies { get; init; }
    public required GodotActorSessionDirectory Actors { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required GodotFireEmbers Embers { get; init; }
    public required GodotItemBurstFragments Fragments { get; init; }
    public required GodotGlowFlash Glows { get; init; }
    public required PlayerFollowCamera Camera { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<bool> LowMemory { get; init; }
}

/// <summary>Real MiscList dome/piece/ring bodies and NonRendered shock bits, sharing the scene's source phase owners.</summary>
public partial class GodotWebDomes : Node3D, IWebDomeHost, IWebDomeShockHost
{
    private sealed record DomeHandle(WeakReference<WebDome> Dome, uint Validation);
    private sealed class Entry(object identity, GodotWebDomeModel model, Func<ushort> flags,
        Action<ushort> setFlags, Func<FixedVector3> position, Action advance, Action delete)
    {
        public object Identity => identity;
        public GodotWebDomeModel Model => model;
        public Func<ushort> Flags => flags;
        public Action<ushort> SetFlags => setFlags;
        public Func<FixedVector3> Position => position;
        public Action Advance => advance;
        public Action Delete => delete;
        public uint LastClock;
        public int ElapsedTicks, AccumulatedTicks;
    }
    private sealed class Target(GodotScriptActor actor, Func<IWebDomeShockTarget?> next) : IWebDomeShockTarget
    {
        public object Identity => actor;
        public FixedVector3 Position => actor.SourcePosition;
        public ushort Type => unchecked((ushort)actor.SourceActorType);
        public IWebDomeShockTarget? Next => next();
        public ushort BodyFlags
        {
            get => actor switch
            {
                GodotThugActor thug => unchecked((ushort)thug.State.BodyFlags),
                GodotHostageActor hostage => unchecked((ushort)hostage.BodyFlags),
                GodotManipulationDebris debris => debris.State.BodyFlags,
                GodotManipulationActor manipulation => manipulation.State.BodyFlags,
                GodotPlatformActor platform => platform.BodyFlags,
                _ => throw new NotSupportedException("Dome shock target has no genuine source body metadata.")
            };
            set
            {
                switch (actor)
                {
                    case GodotThugActor thug: thug.State.BodyFlags = value; break;
                    case GodotHostageActor hostage: hostage.BodyFlags = value; break;
                    case GodotManipulationDebris debris: debris.State.BodyFlags = value; break;
                    case GodotManipulationActor manipulation: manipulation.State.BodyFlags = value; break;
                    case GodotPlatformActor platform: platform.SetCollisionExcluded((value & 0x100) != 0); break;
                    default: throw new NotSupportedException("Dome shock target has no genuine source flag100 owner.");
                }
            }
        }
    }
    private GodotWebDomeConfiguration configuration = null!;
    private readonly Dictionary<object, Entry> entries = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<WebDome, uint> domes = [];
    private readonly List<WebDomeShockWave> shocks = [];
    private WebDomeRing? ring;
    private uint validation;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int DomeCount => domes.Count;
    public int FireDomeCount => domes.Keys.Count(dome => dome.FireVariant != 0);
    public int ShockCount => shocks.Count;
    internal IReadOnlyList<WebDome> Domes => domes.Keys.ToArray();
    internal WebDomeRing? CurrentRing => ring;
    internal IReadOnlyList<WebDomeShockWave> Shocks => shocks;
    internal IReadOnlyList<GodotWebDomeModel> Models => entries.Values.Select(entry => entry.Model).ToArray();
    internal GodotWebDomeModel ModelFor(object identity) => entries[identity].Model;
    internal (uint LastClock, int ElapsedTicks, int AccumulatedTicks) Timing(object identity)
    { Entry entry = entries[identity]; return (entry.LastClock, entry.ElapsedTicks, entry.AccumulatedTicks); }
    /// <summary>Init_KillAll observes the actual live CBody protection flag before calling its deleting destructor.</summary>
    internal ushort SourceBodyFlags(object identity) => entries.TryGetValue(identity, out Entry? entry)
        ? entry.Flags() : throw new InvalidOperationException("Dome source-body metadata requires its actual registered identity.");
    /// <summary>Destroy one captured Misc/Suspended identity through the same genuine destructor as its AI death path.</summary>
    internal bool DestroySourceBody(object identity)
    {
        if (!entries.TryGetValue(identity, out Entry? entry)) return false;
        entry.Delete();
        return true;
    }
    public int FireModelVertexCount => configuration.Repository.Load("firedome").Scene.Models[1].Vertices.Count;
    public bool LowMemory => configuration.LowMemory();
    private SourceActorList Misc => configuration.Registry.SourceList(ActorUpdateGroup.Miscellaneous);

    public static GodotWebDomes Create(GodotWebDomeConfiguration configuration) =>
        new() { Name = "OriginalSourceWebDomes", configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)) };
    public PlayerKickHandle Spawn(byte fireVariant)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        WebDomePlayer player = RecoverPlayer() ?? throw new InvalidOperationException("CDome construction needs its actual allocated CPlayer owner.");
        var dome = new WebDome(player, fireVariant, this);
        return new(new DomeHandle(new(dome), domes[dome]), domes[dome]);
    }
    public WebDome? Recover(ref PlayerKickHandle handle)
    {
        if (handle.Target is DomeHandle token && token.Validation == handle.Validation &&
            token.Dome.TryGetTarget(out WebDome? dome) && domes.TryGetValue(dome, out uint actual) && actual == token.Validation) return dome;
        handle = handle with { Target = null }; return null;
    }
    public bool Burst(ref PlayerKickHandle handle)
    {
        WebDome? dome = Recover(ref handle);
        if (dome is null) return false;
        BurstSourceDome(dome); return true;
    }
    internal void BurstSourceDome(WebDome dome)
    {
        if (!domes.ContainsKey(dome)) throw new InvalidOperationException("Dome burst requires its live source identity.");
        dome.Burst(configuration.Random); Synchronize(dome);
    }
    public WebDomePlayer? RecoverPlayer() => !disposed && GodotObject.IsInstanceValid(configuration.Player) && !configuration.Player.IsQueuedForDeletion()
        ? new(configuration.Player.Controller.Position, PlayerTerrainController.SurfaceClearance,
            unchecked((short)configuration.Player.Combat.State.AnimationFrame)) : null;
    public void AttachDome(WebDome dome)
    {
        if (++validation == 0) throw new InvalidOperationException("Source dome validation counter exhausted.");
        domes.Add(dome, validation);
        try
        {
            Attach(dome, dome.ModelName, dome.ModelIndex, () => dome.BodyFlags, flags => dome.BodyFlags = flags,
                () => dome.Position, () =>
                {
                    dome.AdvanceActor(configuration.Random); Synchronize(dome);
                    foreach (WebDomePiece? piece in dome.Pieces) if (piece is not null) Synchronize(piece);
                }, dome.Dispose);
        }
        catch { domes.Remove(dome); throw; }
    }
    public void DetachDome(WebDome dome) { domes.Remove(dome); Detach(dome); }
    public void AttachPiece(WebDomePiece piece) => Attach(piece, piece.ModelName, piece.ModelIndex,
        () => piece.BodyFlags, flags => piece.BodyFlags = flags, () => piece.Position,
        () => { piece.AdvanceActor(); Synchronize(piece); }, () => DetachPiece(piece));
    public void DetachPiece(WebDomePiece piece) => Detach(piece);
    public FixedVector3 FireModelVertexWorldPosition(WebDome dome, int index)
    {
        System.Numerics.Vector3 vertex = configuration.Repository.Load("firedome").Scene.Models[1].Vertices[index].Position;
        // Actual Dome angles remain0; its ember transform uses raw SVECTOR coordinates and ignores display scale.
        return ((dome.Position >> 12) + new FixedVector3((short)vertex.X, (short)vertex.Y, (short)vertex.Z)) << 12;
    }
    public void EmitFirePulse(FixedVector3 position, int scale, Func<int, int> random) => configuration.Embers.Emit(position, scale, random);
    public void ShatterTrappedPiece(WebDomePiece piece, int first, int second) => configuration.Fragments.Burst(
        configuration.Repository.Load(piece.ModelName), piece.ModelIndex, piece.Position, default, first, second);
    public void EmitShockWave(int fireVariant, Func<int, int> random)
    {
        var shock = new WebDomeShockWave(configuration.Player.Controller.Position, fireVariant, this, random);
        shocks.Insert(0, shock);
        SourceBitCreated?.Invoke(new(shock, _ => shock.AdvanceBits(),
            () => !disposed && shocks.Contains(shock) && shock.Alive, () => shocks.Remove(shock)));
    }
    public void EmitRing(FixedVector3 position, int fireVariant)
    {
        if (ring is { } previous) Detach(previous); // Actual CDomeRing constructor directly deletes the previous singleton.
        PsxAssetLibrary bank = configuration.Repository.Load(fireVariant != 0 ? "firering" : "ring");
        var sourceVertices = bank.Scene.Models[0].Vertices.Select(vertex => new FixedVector3((short)vertex.Position.X,
            (short)vertex.Position.Y, (short)vertex.Position.Z)).ToArray();
        var created = new WebDomeRing(position, fireVariant, sourceVertices); ring = created;
        Attach(created, created.ModelName, 0, () => created.BodyFlags, flags => created.BodyFlags = flags,
            () => created.Position, () =>
            { created.AdvanceActor(); entries[created].Model.Deform(created.Vertices); Synchronize(created); }, () => Detach(created));
    }
    public IWebDomeShockTarget? BaddyHead => Head(configuration.Baddies.SourceBodies.Select(body => body.Native).ToArray(), 0);
    public IWebDomeShockTarget? EnvironmentalHead => Head(configuration.Registry.SourceActors(ActorUpdateGroup.Environment), 0);
    private static IWebDomeShockTarget? Head(IReadOnlyList<GodotScriptActor> list, int index) => index < list.Count
        ? new Target(list[index], () => Head(list, index + 1)) : null;
    public void ShakeCamera(FixedVector3 position, int magnitude) => configuration.Camera.Shake(position, magnitude);
    public void SetFlaming(IWebDomeShockTarget target, int frames, byte red, byte green, byte blue) =>
        throw new NotSupportedException($"Dome fire contact type{target.Type} needs its genuine original SetFlaming actor virtual.");
    public void EmitGlow(FixedVector3 position, int size, byte red, byte green, byte blue, int growth, int first, int second)
    {
        if (LowMemory) return;
        configuration.Glows.Emit(new(position, growth, red, green, blue, 2, red, green, blue, 20,
            second, 0, 0, size, first != 0 ? size : 0, first != 0 ? size / 2 : size,
            first != 0 ? size / 2 : 0, 0, 0), configuration.Random);
    }
    public void Hit(IWebDomeShockTarget target, ActorHit hit) => configuration.Actors.ApplyHit(target.Identity, hit);

    private void Attach(object identity, string bank, ushort modelIndex, Func<ushort> flags, Action<ushort> setFlags,
        Func<FixedVector3> position, Action advance, Action delete)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        GodotWebDomeModel model = GodotWebDomeModel.Create(configuration.Repository.Load(bank), modelIndex);
        var entry = new Entry(identity, model, flags, setFlags, position, advance, delete);
        try { AddChild(model); entries.Add(identity, entry); Misc.Attach(identity, () => Advance(entry)); Synchronize(identity); }
        catch
        {
            entries.Remove(identity); Misc.Detach(identity); configuration.Registry.SuspendedSourceList.Detach(identity);
            if (ReferenceEquals(identity, ring)) ring = null;
            if (model.GetParent() == this) RemoveChild(model);
            model.Release(); model.Free(); throw;
        }
    }
    private void Advance(Entry entry)
    {
        if (disposed || !entries.TryGetValue(entry.Identity, out Entry? attached) || !ReferenceEquals(attached, entry)) return;
        ushort flags = entry.Flags();
        if ((flags & 0x40) != 0)
        { if ((flags & 0x80) != 0) entry.Delete(); else entry.SetFlags((ushort)(flags | 0x80)); return; }
        uint distance = ActorLifecycle.Distance(configuration.Player.Controller.Position, entry.Position());
        if ((flags & 2) != 0 && distance > configuration.Registry.SuspensionDistance)
        {
            Misc.Detach(entry.Identity); entry.SetFlags((ushort)(flags | 1)); entry.Model.Visible = false;
            configuration.Registry.SuspendedSourceList.Attach(entry.Identity, () => Resume(entry)); return;
        }
        uint clock = configuration.Clock();
        if ((flags & 4) != 0)
        { entry.SetFlags((ushort)(flags & 0xFFFB)); entry.ElapsedTicks = 2; entry.AccumulatedTicks = 0; }
        else { entry.ElapsedTicks = unchecked((int)(clock - entry.LastClock)); if (entry.ElapsedTicks > 6) entry.ElapsedTicks = 6; }
        entry.LastClock = clock; entry.AccumulatedTicks = unchecked(entry.AccumulatedTicks + entry.ElapsedTicks);
        entry.Advance();
    }
    private void Resume(Entry entry)
    {
        if (disposed || !entries.TryGetValue(entry.Identity, out Entry? attached) || !ReferenceEquals(attached, entry)) return;
        if ((entry.Flags() & 2) == 0 || ActorLifecycle.Distance(configuration.Player.Controller.Position, entry.Position()) > configuration.Registry.SuspensionDistance) return;
        configuration.Registry.SuspendedSourceList.Detach(entry.Identity);
        entry.SetFlags((ushort)(entry.Flags() & 0xFFFE)); Misc.Attach(entry.Identity, () => Advance(entry)); Synchronize(entry.Identity);
    }
    private void Synchronize(object identity)
    {
        Entry entry = entries[identity];
        (FixedVector3 position, FixedVector3 scale, ushort flags) = identity switch
        {
            WebDome dome => (dome.Position, dome.Scale, dome.ItemFlags),
            WebDomePiece piece => (piece.Position, new FixedVector3(4096, 4096, 4096), piece.ItemFlags),
            WebDomeRing sourceRing => (sourceRing.Position, new FixedVector3(4096, 4096, 4096), sourceRing.ItemFlags),
            _ => throw new InvalidOperationException("Unknown genuine dome model owner.")
        };
        entry.Model.Synchronize(position, scale, flags); if ((entry.Flags() & 1) != 0) entry.Model.Visible = false;
    }
    private void Detach(object identity)
    {
        if (!entries.Remove(identity, out Entry? entry)) return;
        Misc.Detach(identity); configuration.Registry.SuspendedSourceList.Detach(identity);
        if (ReferenceEquals(identity, ring)) ring = null;
        RemoveChild(entry.Model); entry.Model.Release(); entry.Model.Free();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        foreach (WebDome dome in domes.Keys.ToArray()) dome.Dispose();
        foreach (object identity in entries.Keys.ToArray()) Detach(identity);
        shocks.Clear(); disposed = true;
    }
}
