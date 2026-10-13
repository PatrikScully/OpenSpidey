using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual paired CKnottedWeb polylines and original protected WebKnot sprites.</summary>
public partial class GodotWebStrands : Node3D
{
    private readonly List<WebStrandOwner> owners = [];
    private readonly List<KnottedWebStrand> sourceStrands = [];
    private readonly Dictionary<KnottedWebStrand, (KnottedWebFragment Fragment, Func<FixedVector3> Camera)> brokenStrands = [];
    private readonly Dictionary<KnottedWebStrand, (ReleasedSwingWeb Web, Func<FixedVector3> Player, Func<FixedVector3> Camera)> releasedSwings = [];
    private Func<uint>? sourceClock;
    private Func<int, int>? sourceRandom;
    private Action<FixedVector3, int, Func<int, int>>? sourceFirePulse;
    private readonly ImmediateMesh lineMesh = new(), knotMesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly StandardMaterial3D lineMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true
    };
    private readonly StandardMaterial3D transparentLineMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new(1, 1, 1, 128 / 255f),
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
    };
    private StandardMaterial3D knotMaterial = null!;
    private ImageTexture texture = null!;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public int Count => owners.Count;
    public event Action<SourceParticleRegistration>? PolylineEmitted;
    public event Action<object>? PolylineRemoved;
    public event Action<SourceParticleRegistration>? KnotEmitted;
    public event Action<WebStrandKnot>? KnotRemoved;
    internal ImmediateMesh LineMesh => lineMesh;
    internal ImmediateMesh KnotMesh => knotMesh;
    internal SpriteAnimationFrame SourceFrame => frame;
    internal ImageTexture SourceTexture => texture;
    internal StandardMaterial3D SourceTransparentLineMaterial => transparentLineMaterial;
    internal StandardMaterial3D SourceOpaqueLineMaterial => lineMaterial;
    internal IReadOnlyList<KnottedWebStrand> SourceStrands => sourceStrands;
    internal IReadOnlyList<WebStrandOwner> Owners => owners;
    internal IReadOnlyList<KnottedWebFragment> Fragments => brokenStrands.Values.Select(value => value.Fragment).ToArray();
    internal IReadOnlyList<ReleasedSwingWeb> ReleasedSwings => releasedSwings.Values.Select(value => value.Web).ToArray();
    public GodotWebStrands() => TopLevel = true;
    public static GodotWebStrands Create(byte[] bitsBytes)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(bitsBytes).Single(a => a.Name.Equals("WebKnot", StringComparison.OrdinalIgnoreCase));
        SpriteAnimationFrame frame = animation.Frames[0];
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).Single(t => t.Checksum == frame.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var result = new GodotWebStrands { Name = "OriginalKnottedWebs", frame = frame, texture = ImageTexture.CreateFromImage(image) };
        result.knotMaterial = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            AlbedoColor = new(1, 1, 1, 128 / 255f), AlbedoTexture = result.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
        };
        return result;
    }
    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "OriginalGPolyLinePairs", Mesh = lineMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Name = "OriginalProtectedWebKnotSprites", Mesh = knotMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
    public void Attach(WebStrandOwner owner)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (owners.Contains(owner)) throw new InvalidOperationException("Source CWeb owner is already attached.");
        owners.Insert(0, owner); owner.StrandCreated += AttachStrand;
        if (owner.Strand is { } strand) AttachStrand(strand);
        Rebuild();
    }
    public void Detach(WebStrandOwner owner)
    {
        ObjectDisposedException.ThrowIf(disposed, this); owner.StrandCreated -= AttachStrand; owners.Remove(owner); Rebuild();
    }
    public void ConfigureSourcePhase(Func<uint> clock, Func<int, int> random,
        Action<FixedVector3, int, Func<int, int>> emitFirePulse)
    {
        sourceClock = clock ?? throw new ArgumentNullException(nameof(clock));
        sourceRandom = random ?? throw new ArgumentNullException(nameof(random));
        sourceFirePulse = emitFirePulse ?? throw new ArgumentNullException(nameof(emitFirePulse));
    }
    public void AttachStrand(KnottedWebStrand strand)
        => AttachStrand(strand, tick => MoveBit(strand), () => strand.Alive, strand.Dispose);

    public KnottedWebFragment EmitFragment(KnottedWebFragmentRequest request, Func<int, int> random,
        Func<FixedVector3, int> groundY, Func<FixedVector3> cameraWhole)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(cameraWhole);
        var fragment = new KnottedWebFragment(request, random, groundY);
        brokenStrands.Add(fragment.Strand, (fragment, cameraWhole));
        AttachStrand(fragment.Strand, tick => fragment.AdvanceBits(tick, cameraWhole(),
                sourceRandom ?? throw new InvalidOperationException("Broken web requires the shared source random stream."),
                sourceFirePulse ?? throw new InvalidOperationException("Broken web requires the genuine fire relocation.")),
            () => fragment.Alive, fragment.Dispose);
        return fragment;
    }

    public ReleasedSwingWeb EmitReleasedSwing(WebSwingAnchor swing, Func<FixedVector3> playerPosition,
        Func<FixedVector3> cameraWhole)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(playerPosition); ArgumentNullException.ThrowIfNull(cameraWhole);
        var web = ReleasedSwingWeb.Create(swing.Strand, swing.RenderEndOffset, swing.FireVariant, lowMemory: false, cameraWhole(),
            sourceRandom ?? throw new InvalidOperationException("Released swing requires the shared source random stream."))!;
        releasedSwings.Add(web.Strand, (web, playerPosition, cameraWhole));
        AttachStrand(web.Strand, tick => web.Move(tick, playerPosition(), cameraWhole(),
                sourceRandom ?? throw new InvalidOperationException("Released swing requires the shared source random stream."),
                sourceFirePulse ?? throw new InvalidOperationException("Released swing requires the genuine fire relocation.")),
            () => web.Alive, web.Dispose);
        return web;
    }

    private void AttachStrand(KnottedWebStrand strand, Action<uint> move, Func<bool> alive, Action destroy)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sourceStrands.Contains(strand)) throw new ArgumentException("Knotted bit is already attached.", nameof(strand));
        sourceStrands.Insert(0, strand);
        strand.Disposed += () => DetachStrand(strand);
        PolylineEmitted?.Invoke(new(strand, move, alive, destroy));
        // Actual 4F8E20 creates flat knots before the protected inner CGPolyLine.
        foreach (WebStrandKnot knot in strand.Knots)
            KnotEmitted?.Invoke(new(knot, SourceBaseBitMove, () => !strand.IsDisposed, () => KnotRemoved?.Invoke(knot)));
        PolylineEmitted?.Invoke(new(strand.InnerPolyline, SourceBaseBitMove,
            () => !strand.IsDisposed, () => PolylineRemoved?.Invoke(strand.InnerPolyline)));
    }
    private void DetachStrand(KnottedWebStrand strand)
    {
        if (!sourceStrands.Remove(strand)) return;
        brokenStrands.Remove(strand);
        releasedSwings.Remove(strand);
        // Actual 4F9060 deletes the inner line first, then each knot, then the base line.
        PolylineRemoved?.Invoke(strand.InnerPolyline);
        foreach (WebStrandKnot knot in strand.Knots) KnotRemoved?.Invoke(knot);
        PolylineRemoved?.Invoke(strand);
    }
    public void MoveBit(KnottedWebStrand strand)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!sourceStrands.Contains(strand)) throw new ArgumentException("Knotted bit is outside its source owner.", nameof(strand));
        strand.AdvanceBits((sourceClock ?? throw new InvalidOperationException("Shared knotted bit phase requires its source clock."))(),
            sourceRandom ?? throw new InvalidOperationException("Shared knotted bit phase requires the source random stream."),
            sourceFirePulse ?? throw new InvalidOperationException("Shared knotted bit phase requires its genuine fire relocation."));
    }
    // Plain CFlatBit knots and the protected CGPolyLine inherit the PC empty Move slot4015B0.
    private static void SourceBaseBitMove(uint sourceClock) { }
    public void AdvanceActors(uint sourceClock, FixedVector3 cameraWhole)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (WebStrandOwner owner in owners.ToArray()) owner.AdvanceActor(sourceClock, cameraWhole);
        Rebuild();
    }
    public void AdvanceBits(uint clock, Func<int, int> random, Action<FixedVector3, int, Func<int, int>> emitFirePulse)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (KnottedWebStrand strand in sourceStrands.ToArray())
            if (strand.Alive)
            {
                if (releasedSwings.TryGetValue(strand, out var released))
                    released.Web.Move(clock, released.Player(), released.Camera(), random, emitFirePulse);
                else if (brokenStrands.TryGetValue(strand, out var broken))
                    broken.Fragment.AdvanceBits(clock, broken.Camera(), random, emitFirePulse);
                else strand.AdvanceBits(clock, random, emitFirePulse);
            }
        Rebuild();
    }
    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        lineMesh.ClearSurfaces(); knotMesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        Vector2 pixelScale = new(viewport.Y / 240 * .625f, viewport.Y / 240);
        foreach (KnottedWebStrand strand in sourceStrands)
        {
            if (!strand.Alive) continue;
            // Protected inner line is created after the owner and precedes it in G_GPOLYLINE_LIST.
            Polyline(strand.InnerStart, strand.InnerStartColour, strand.InnerEnds, strand.InnerColours);
            Polyline(strand.Start, strand.StartColour, strand.Ends, strand.Colours);
            foreach (WebStrandKnot knot in strand.Knots.Reverse())
            {
                Vector3 position = PsxCoordinates.FixedWorldPosition(knot.Position);
                float depth = -camera.GlobalBasis.Z.Dot(position - camera.GlobalPosition);
                if (depth <= 0) continue;
                Vector2 logical = camera.UnprojectPosition(position) / pixelScale;
                ThugPingPixel[]? corners = ThugPingProjection.Project(frame, 176, 0, (int)(depth * 100), (int)logical.X, (int)logical.Y, knot.PostScale);
                if (corners is null) continue;
                knotMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, knotMaterial);
                knotMesh.SurfaceSetColor(GodotGlowFlash.SourceColour(knot.CodeRgb, 0, brightness));
                foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
                {
                    knotMesh.SurfaceSetUV(new((index & 1) != 0 ? .99f : .01f, (index & 2) != 0 ? .99f : .01f));
                    Vector2 pixel = (new Vector2(corners[index].X, corners[index].Y) + logical - new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                    knotMesh.SurfaceAddVertex(camera.ProjectPosition(pixel, depth));
                }
                knotMesh.SurfaceEnd();
            }
        }
        void Polyline(FixedVector3 start, uint colour, IReadOnlyList<FixedVector3> ends, IReadOnlyList<uint> colours)
        {
            for (int i = 0; i < ends.Count; i++)
            {
                Vector3 a = PsxCoordinates.FixedWorldPosition(start), b = PsxCoordinates.FixedWorldPosition(ends[i]);
                float depthA = -camera.GlobalBasis.Z.Dot(a - camera.GlobalPosition), depthB = -camera.GlobalBasis.Z.Dot(b - camera.GlobalPosition);
                if (depthA > 0 && depthB > 0)
                {
                    Vector2 first = camera.UnprojectPosition(a), second = camera.UnprojectPosition(b);
                    PlayerImpactLineCorner[] corners = PlayerImpactLineGeometry.Build(first.X, first.Y, second.X, second.Y);
                    lineMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, (colour & 0x02000000) != 0 ? transparentLineMaterial : lineMaterial);
                    foreach (int index in new[] { 0, 1, 2, 0, 3, 2 })
                    {
                        PlayerImpactLineCorner corner = corners[index];
                        lineMesh.SurfaceSetColor(GodotGlowFlash.SourceColour(corner.Head ? colour : colours[i], 0, brightness));
                        lineMesh.SurfaceAddVertex(camera.ProjectPosition(new(corner.X, corner.Y), corner.Head ? depthA : depthB));
                    }
                    lineMesh.SurfaceEnd();
                }
                start = ends[i]; colour = colours[i];
            }
        }
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        foreach (WebStrandOwner owner in owners.ToArray()) owner.Dispose();
        foreach (KnottedWebStrand strand in sourceStrands.ToArray()) strand.Dispose();
        disposed = true; owners.Clear(); lineMesh.Dispose(); knotMesh.Dispose(); lineMaterial.Dispose(); transparentLineMaterial.Dispose(); knotMaterial.Dispose(); texture?.Dispose();
    }
}
