using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

public interface IGodotPlayerWebContactHost
{
    IReadOnlyList<FixedVector3>? ActiveTrapEndpoints(GodotScriptActor actor);
    void AddTrap(GodotScriptActor actor, byte fireVariant);
    void ReplaceYank(GodotScriptActor actor, int count);
    void BurstYank(GodotScriptActor actor);
    bool TrapWeb(GodotScriptActor actor);
    bool TugWeb(GodotScriptActor actor);
}

/// <summary>Actor-owned genuine trap/yank contacts, source hook geometry and CTrapWebEffect bit scheduling.</summary>
public partial class GodotActorWebContacts : Node3D, ITrapWebEffectHost, IGodotPlayerWebContactHost
{
    private readonly List<TrapWebEffect> effects = [];
    private readonly Dictionary<(GodotScriptActor Actor, byte Type), WeakReference<TrapWebEffect>> contacts = [];
    private readonly Dictionary<GodotScriptActor, Target> targets = [];
    private readonly List<TrapWebTriangle> quadOrder = [];
    private readonly Dictionary<TrapWebTriangle, TrapWebEffect> quadOwners = [];
    private readonly ImmediateMesh lineMesh = new(), triangleMesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly StandardMaterial3D lineMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, AlbedoColor = new(1, 1, 1, 128 / 255f),
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true
    };
    private StandardMaterial3D triangleMaterial = null!;
    private ImageTexture texture = null!;
    private GodotPlayerMeleeDirectory directory = null!;
    private Func<GodotThugActor, IReadOnlyList<ActorPartBox>> thugBoxes = null!;
    private Func<int, int> random = null!;
    private Func<FixedVector3, int> groundY = null!;
    private Action<FixedVector3, int> emitFirePulse = null!;
    private Action firstContact = null!;
    private GodotWebFragments fragments = null!;
    private readonly Dictionary<GodotScriptActor, GodotNativeDestructionObserver> lifetimeObservers = [];
    private bool disposed;
    public int Count => effects.Count;
    public event Action<SourceParticleRegistration>? Emitted;
    public event Action<TrapWebEffect>? Removed;
    public event Action<SourceParticleRegistration>? LineEmitted;
    public event Action<TrapWebLine>? LineRemoved;
    public event Action<SourceParticleRegistration>? QuadEmitted;
    public event Action<TrapWebTriangle>? QuadRemoved;
    internal ImmediateMesh LineMesh => lineMesh;
    internal ImmediateMesh TriangleMesh => triangleMesh;
    internal IReadOnlyList<TrapWebEffect> Effects => effects;
    internal ImageTexture SourceTexture => texture;
    public GodotActorWebContacts() => TopLevel = true;

    public static GodotActorWebContacts Create(byte[] bitsBytes, GodotPlayerMeleeDirectory directory,
        Func<GodotThugActor, IReadOnlyList<ActorPartBox>> thugBoxes, GodotWebFragments fragments,
        Func<int, int> random, Func<FixedVector3, int> groundY, Action<FixedVector3, int> emitFirePulse,
        Action firstContact)
    {
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).Single(t => t.Checksum == 0x35023093);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var result = new GodotActorWebContacts
        {
            Name = "OriginalActorWebContacts", texture = ImageTexture.CreateFromImage(image),
            directory = directory ?? throw new ArgumentNullException(nameof(directory)),
            thugBoxes = thugBoxes ?? throw new ArgumentNullException(nameof(thugBoxes)),
            fragments = fragments ?? throw new ArgumentNullException(nameof(fragments)),
            random = random ?? throw new ArgumentNullException(nameof(random)),
            groundY = groundY ?? throw new ArgumentNullException(nameof(groundY)),
            emitFirePulse = emitFirePulse ?? throw new ArgumentNullException(nameof(emitFirePulse)),
            firstContact = firstContact ?? throw new ArgumentNullException(nameof(firstContact))
        };
        result.triangleMaterial = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, AlbedoColor = new(1, 1, 1, 128 / 255f),
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, AlbedoTexture = result.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
        };
        return result;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "OriginalTrapGPolyLines", Mesh = lineMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Name = "OriginalTrapWebTriangles", Mesh = triangleMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
    public TrapWebEffect? Contact(GodotScriptActor actor, byte type)
    {
        CheckAlive();
        if (!contacts.TryGetValue((actor, type), out WeakReference<TrapWebEffect>? pointer)) return null;
        if (pointer.TryGetTarget(out TrapWebEffect? effect) && !effect.IsDisposed) return effect;
        contacts.Remove((actor, type)); return null;
    }
    public IThugTrapWeb? RecoverContact(GodotScriptActor actor, bool yank) => Contact(actor, yank ? (byte)1 : (byte)0);
    public IReadOnlyList<FixedVector3>? ActiveTrapEndpoints(GodotScriptActor actor) =>
        Contact(actor, 0) is { StrandCount: > 0 } effect ? effect.Points.Skip(1).Take(effect.StrandCount).ToArray() : null;
    public void AddTrap(GodotScriptActor actor, byte fireVariant)
    {
        TrapWebEffect? effect = Contact(actor, 0);
        if (effect is null) { effect = CreateContact(actor, 0); effect.FireVariant = fireVariant; firstContact(); }
        for (int index = 0; index < 8; index++) effect.AddAnotherStrand();
        Rebuild();
    }
    public void ReplaceYank(GodotScriptActor actor, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Contact(actor, 1)?.Burst();
        TrapWebEffect effect = CreateContact(actor, 1);
        for (int index = 0; index < count; index++) effect.AddAnotherStrand();
        Rebuild();
    }
    public void BurstYank(GodotScriptActor actor) => Contact(actor, 1)?.Burst();
    public bool TrapWeb(GodotScriptActor actor) => ActorWebReactions.TrapWeb(Flags(actor), Health(actor), new ReactionHost(this, actor));
    public bool TugWeb(GodotScriptActor actor) => ActorWebReactions.TugWeb(Flags(actor), Health(actor), new ReactionHost(this, actor));

    private TrapWebEffect CreateContact(GodotScriptActor actor, byte type)
    {
        Target target = TargetFor(actor);
        TrapWebEffect[] before = effects.ToArray();
        try { return new(target, type, this, random); }
        catch { foreach (TrapWebEffect added in effects.Except(before).ToArray()) added.Dispose(); throw; }
    }
    private Target TargetFor(GodotScriptActor actor)
    {
        CheckAlive();
        if (!directory.Contains(actor)) throw new ArgumentException("Trap web target requires its allocated source actor generation.", nameof(actor));
        if (actor is not (GodotThugActor or GodotHostageActor))
            throw new NotSupportedException($"Source web contact actor family {actor.GetType().Name} is not ported.");
        if (targets.TryGetValue(actor, out Target? existing)) return existing;
        var target = new Target(actor, actor is GodotThugActor thug ? thugBoxes(thug) : null);
        targets.Add(actor, target);
        var observer = new GodotNativeDestructionObserver { Name = "SourceWebContactLifetime" };
        observer.Destroyed = () =>
        {
            // Actual CSuper destructor deletes both recovered attached effects before freeing the actor.
            foreach (byte type in new byte[] { 0, 1 })
                if (contacts.TryGetValue((actor, type), out WeakReference<TrapWebEffect>? pointer) &&
                    pointer.TryGetTarget(out TrapWebEffect? effect)) effect.Dispose();
            targets.Remove(actor); contacts.Remove((actor, 0)); contacts.Remove((actor, 1));
            lifetimeObservers.Remove(actor);
        };
        actor.AddChild(observer);
        lifetimeObservers.Add(actor, observer);
        return target;
    }
    public PlayerKickHandle StoreTarget(ITrapWebEffectTarget target) => directory.MakeHandle(target.Identity);
    public ITrapWebEffectTarget? RecoverTarget(ref PlayerKickHandle handle) =>
        directory.Recover(ref handle) is { } actor ? TargetFor(actor) : null;
    public bool ContactExists(ITrapWebEffectTarget target, byte type) => Contact((GodotScriptActor)target.Identity, type) is not null;
    public void BindContact(ITrapWebEffectTarget target, byte type, TrapWebEffect effect) => contacts[( (GodotScriptActor)target.Identity, type)] = new(effect);
    public void ClearContact(ITrapWebEffectTarget target, byte type) => contacts.Remove(((GodotScriptActor)target.Identity, type));
    public void AttachEffect(TrapWebEffect effect)
    {
        effects.Insert(0, effect);
        Emitted?.Invoke(new(effect, tick => MoveBit(effect, tick), () => effect.Alive, () => RemoveBit(effect)));
    }
    public void DetachEffect(TrapWebEffect effect)
    {
        effects.Remove(effect);
        Removed?.Invoke(effect);
    }
    public void CreateLine(TrapWebLine line) => LineEmitted?.Invoke(new(line, SourceBaseBitMove,
        () => !line.Owner.IsDisposed, () => DeleteLine(line)));
    public void DeleteLine(TrapWebLine line) => LineRemoved?.Invoke(line);
    public void CreateStrand(TrapWebEffect effect, TrapWebTriangle strand)
    {
        quadOrder.Insert(0, strand); quadOwners.Add(strand, effect);
        QuadEmitted?.Invoke(new(strand, SourceBaseBitMove, () => quadOwners.ContainsKey(strand), () => DeleteStrand(strand)));
    }
    public void DeleteStrand(TrapWebTriangle strand)
    { quadOrder.Remove(strand); quadOwners.Remove(strand); QuadRemoved?.Invoke(strand); }
    // Actual CGPolyLine and CQuadBit inherit the original empty CBit::Move virtual.
    private static void SourceBaseBitMove(uint sourceClock) { }
    public int GroundY(FixedVector3 position) => groundY(position);
    public void CreateFragment(ImpactWebFragmentRequest request, Func<int, int> random) => fragments.Emit(request, random);
    public void EmitFirePulse(FixedVector3 position, int scale) => emitFirePulse(position, scale);

    public void AdvanceBits()
    {
        MoveBits();
        RemoveDeadBits();
    }
    public void MoveBits()
    {
        CheckAlive();
        foreach (TrapWebEffect effect in effects.ToArray())
            if (effect.Alive) effect.AdvanceBits();
        Rebuild();
    }
    public void MoveBit(TrapWebEffect effect, uint sourceClock)
    {
        CheckAlive();
        if (!effects.Contains(effect)) throw new ArgumentException("Trap bit is outside its source owner.", nameof(effect));
        if (effect.Alive) effect.AdvanceBits();
    }
    public void RemoveBit(TrapWebEffect effect)
    {
        CheckAlive();
        if (!effects.Contains(effect)) throw new ArgumentException("Trap bit is outside its source owner.", nameof(effect));
        effect.Dispose();
    }
    public void RemoveDeadBits()
    {
        CheckAlive();
        foreach (TrapWebEffect effect in effects.Where(effect => !effect.Alive).ToArray()) effect.Dispose();
        Rebuild();
    }
    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        lineMesh.ClearSurfaces(); triangleMesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        foreach (TrapWebEffect effect in effects)
        {
            FixedVector3 start = effect.Start; uint colour = effect.StartColour;
            for (int index = 0; index < effect.HookCount; index++)
            {
                DrawLine(start, effect.Points[index + 1], colour, effect.Colours[index]);
                start = effect.Points[index + 1]; colour = effect.Colours[index];
            }
        }
        foreach (TrapWebTriangle strand in quadOrder)
        {
            triangleMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, triangleMaterial);
            triangleMesh.SurfaceSetColor(GodotGlowFlash.SourceColour(strand.Tint, 0, brightness));
            // Original quad corners C and D coincide; draw its nondegenerate A/B/C triangle.
            foreach (int corner in new[] { 0, 1, 2 })
            {
                triangleMesh.SurfaceSetUV(new((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f));
                triangleMesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(corner switch { 0 => strand.A, 1 => strand.B, _ => strand.C }));
            }
            triangleMesh.SurfaceEnd();
        }
        void DrawLine(FixedVector3 from, FixedVector3 to, uint headColour, uint tailColour)
        {
            Vector3 first = PsxCoordinates.FixedWorldPosition(from), last = PsxCoordinates.FixedWorldPosition(to);
            float firstDepth = -camera.GlobalBasis.Z.Dot(first - camera.GlobalPosition), lastDepth = -camera.GlobalBasis.Z.Dot(last - camera.GlobalPosition);
            if (firstDepth <= 0 || lastDepth <= 0) return;
            Vector2 head = camera.UnprojectPosition(first), tail = camera.UnprojectPosition(last);
            PlayerImpactLineCorner[] corners = PlayerImpactLineGeometry.Build(head.X, head.Y, tail.X, tail.Y);
            lineMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, lineMaterial);
            foreach (int index in new[] { 0, 1, 2, 0, 3, 2 })
            {
                PlayerImpactLineCorner corner = corners[index];
                lineMesh.SurfaceSetColor(GodotGlowFlash.SourceColour(corner.Head ? headColour : tailColour, 0, brightness));
                lineMesh.SurfaceAddVertex(camera.ProjectPosition(new(corner.X, corner.Y), corner.Head ? firstDepth : lastDepth));
            }
            lineMesh.SurfaceEnd();
        }
    }
    private static uint Flags(GodotScriptActor actor) => actor switch
    { GodotThugActor thug => thug.Actor.BehaviorFlags, GodotHostageActor hostage => hostage.Actor.BehaviorFlags,
        _ => throw new NotSupportedException("Web acceptance requires an actual CBaddy actor family.") };
    private static short Health(GodotScriptActor actor) => actor switch
    { GodotThugActor thug => thug.State.Health, GodotHostageActor hostage => hostage.State.Health,
        _ => throw new NotSupportedException("Web acceptance requires actual source health.") };
    private sealed class ReactionHost(GodotActorWebContacts owner, GodotScriptActor actor) : IActorWebReactionHost
    {
        public bool TryCreateMessage(int code)
        {
            if (actor is GodotThugActor thug) thug.ReceiveMessage(code, null);
            else if (actor is GodotHostageActor hostage) hostage.ReceiveMessage(code, null);
            else throw new NotSupportedException("Web message recipient has no source message queue.");
            return true;
        }
        public void BurstYankContact() => owner.BurstYank(actor);
    }
    private sealed class Target(GodotScriptActor actor, IReadOnlyList<ActorPartBox>? boxes) : ITrapWebEffectTarget
    {
        private readonly FixedJointTransform[] pose = new FixedJointTransform[boxes?.Count ?? 0];
        public object Identity => actor;
        public FixedVector3 Position => actor.SourcePosition;
        // Neither supported actor family overrides these inherited CSuper constructor bytes.
        public byte UpperExtent => 100;
        public byte LowerExtent => 94;
        public ushort ItemFlags
        {
            get => actor is GodotThugActor thug ? thug.ItemFlags : ((GodotHostageActor)actor).ItemFlags;
            set { if (actor is GodotThugActor thug) thug.ItemFlags = value; else ((GodotHostageActor)actor).ItemFlags = value; }
        }
        public uint TintColor
        {
            get => actor is GodotThugActor thug ? thug.State.TintColor : ((GodotHostageActor)actor).State.TintColor;
            set { if (actor is GodotThugActor thug) thug.State.TintColor = value; else ((GodotHostageActor)actor).State.TintColor = value; }
        }
        public bool Collide(FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook)
        {
            if (actor is GodotHostageActor hostage) return hostage.CollideWithPartBoxes(start, end, scale, out hook);
            var thug = (GodotThugActor)actor;
            for (int part = 0; part < pose.Length; part++) pose[part] = thug.Model.GetSourceAnimationPartPose(part);
            ActorAngles angles = thug.Actor.Body.Angles;
            FixedJointTransform basis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
            return ActorPartCollision.TryCollide(Position, basis, pose, boxes!, thug.Model.SourceHiddenParts, start, end, scale, out hook);
        }
        public FixedVector3 HookPosition(ActorHook hook) => actor is GodotThugActor thug
            ? thug.GetDynamicHookPosition(hook) : ((GodotHostageActor)actor).GetDynamicHookPosition(hook);
        public void BuildTransform()
        { if (actor is GodotThugActor thug) thug.BuildTransform(); else ((GodotHostageActor)actor).BuildTransform(); }
    }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(disposed, this);
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        foreach (TrapWebEffect effect in effects.ToArray()) effect.Dispose();
        foreach (GodotNativeDestructionObserver observer in lifetimeObservers.Values.ToArray())
        {
            if (!GodotObject.IsInstanceValid(observer)) continue;
            observer.Destroyed = null;
            observer.GetParent()?.RemoveChild(observer);
            observer.Free();
        }
        lifetimeObservers.Clear();
        disposed = true; contacts.Clear(); targets.Clear(); lineMesh.Dispose(); triangleMesh.Dispose();
        lineMaterial.Dispose(); triangleMaterial?.Dispose(); texture?.Dispose();
    }
}
