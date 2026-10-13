using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual CSmokeTrail owners and protected animated CRibbonBit children in the shared source particle lists.</summary>
public partial class GodotSmokeTrails : Node3D
{
    private readonly List<SmokeTrail> trails = [];
    private readonly List<SmokeTrailSegment> segments = [];
    private readonly Dictionary<uint, ImageTexture> textures = [];
    private readonly Dictionary<uint, StandardMaterial3D> materials = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private SpriteAnimationFrame[] frames = [];
    private bool disposed;
    public event Action<SourceParticleRegistration>? OwnerCreated;
    public event Action<SourceParticleRegistration>? SegmentCreated;
    public event Action<object>? BitRemoved;
    public int Count => trails.Count;
    public int SegmentCount => segments.Count;
    public ushort? NearDepth { get; set; }
    public ushort FarDepth { get; set; } = 8048;
    internal IReadOnlyList<SmokeTrail> Trails => trails;
    internal IReadOnlyList<SmokeTrailSegment> Segments => segments;
    internal IReadOnlyList<SpriteAnimationFrame> SourceFrames => frames;
    internal IReadOnlyDictionary<uint, ImageTexture> SourceTextures => textures;
    internal ImmediateMesh RenderMesh => mesh;
    internal bool ResourcesDisposed => disposed;
    public GodotSmokeTrails() => TopLevel = true;

    public static GodotSmokeTrails Create(byte[] originalBits)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(originalBits).Single(value => value.Name.Equals("ribbon", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count > 255) throw new InvalidDataException("Original CFT4Bit ribbon frame count is one byte.");
        var result = new GodotSmokeTrails { Name = "OriginalSmokeTrails", frames = animation.Frames.ToArray() };
        try
        {
            IReadOnlyList<DecodedTexture> images = PsxTextureReader.Read(originalBits);
            foreach (uint checksum in result.frames.Select(frame => frame.TextureChecksum).Distinct())
            {
                DecodedTexture source = images.First(value => value.Checksum == checksum);
                using Image image = Image.CreateFromData(source.Width, source.Height, false, Image.Format.Rgba8, source.RgbaPixels);
                ImageTexture texture = ImageTexture.CreateFromImage(image); result.textures.Add(checksum, texture);
                result.materials.Add(checksum, new()
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
                    AlbedoColor = new(1, 1, 1, 128 / 255f), AlbedoTexture = texture,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
                });
            }
            return result;
        }
        catch { result.Release(); result.Free(); throw; }
    }

    public override void _Ready() => AddChild(new MeshInstance3D { Name = "OriginalLinkedRibbonSmoke", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    public SmokeTrail Emit(FixedVector3 position, int segmentCount, int red, int green, int blue)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var trail = new SmokeTrail(position, segmentCount, red, green, blue, checked((byte)frames.Length));
        trails.Insert(0, trail);
        OwnerCreated?.Invoke(new(trail, _ => trail.Move(), () => !disposed && trails.Contains(trail) && !trail.IsDead, () => Remove(trail)));
        for (int index = trail.Segments.Length - 1; index >= 0; index--)
        {
            SmokeTrailSegment segment = trail.Segments[index]; segments.Insert(0, segment);
            SegmentCreated?.Invoke(new(segment, _ => segment.Move(), () => !disposed && segments.Contains(segment), () => RemoveSegment(segment)));
        }
        Rebuild(); return trail;
    }
    public void Remove(SmokeTrail trail)
    {
        if (!trails.Contains(trail)) return;
        foreach (SmokeTrailSegment segment in trail.Segments) RemoveSegment(segment);
        trails.Remove(trail); trail.Dispose(); BitRemoved?.Invoke(trail); Rebuild();
    }
    private void RemoveSegment(SmokeTrailSegment segment)
    { if (segments.Remove(segment)) BitRemoved?.Invoke(segment); }

    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D(); if (camera is null) return;
        Vector2 size = GetViewport().GetVisibleRect().Size; if (size.X <= 0 || size.Y <= 0) return;
        Vector2 pixelScale = new(size.Y / 240 * .625f, size.Y / 240);
        int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
            (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
        ushort near = NearDepth ?? unchecked((ushort)((((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1));
        FixedVector3 cameraWhole = PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12;
        FixedJointTransform view = FixedRotation.TransposeRotation(PsxCoordinates.OriginalRotation(camera.GlobalBasis));
        var source = segments.Select(segment => new LinkedRibbonView(segment, segment.Flags, Project(segment.Start), Project(segment.End),
            segment.Scale, frames[segment.Frame].Width));
        foreach (LinkedRibbonQuad quad in LinkedRibbonProjection.Project(source, near, FarDepth))
        {
            var segment = (SmokeTrailSegment)quad.Identity;
            float worldDepth = -camera.GlobalBasis.Z.Dot(PsxCoordinates.FixedWorldPosition(segment.End) - camera.GlobalPosition);
            if (worldDepth <= 0) continue;
            SpriteAnimationFrame frame = frames[segment.Frame];
            LinkedRibbonCorner[] corners = [quad.First, quad.Second, quad.Third, quad.Fourth];
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, materials[frame.TextureChecksum]);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(segment.CodeBgr, 0, brightness));
            foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
            {
                LinkedRibbonCorner point = corners[index];
                mesh.SurfaceSetUV(new((index & 1) != 0 ? 1 : 0, (index & 2) != 0 ? 1 : 0));
                mesh.SurfaceAddVertex(camera.ProjectPosition(new Vector2(point.X, point.Y) * pixelScale, worldDepth));
            }
            mesh.SurfaceEnd();
        }
        LinkedRibbonPoint Project(FixedVector3 position)
        {
            int depth = FixedRotation.Transform(view, (position >> 12) - cameraWhole).Z;
            // PC40EAD6/40E9B5 tests source depth before reading projected screen words.
            // Clipped coordinates are never consumed by the ribbon projector or uploaded as vertices.
            if ((uint)depth < near || (uint)depth > FarDepth) return new(0, 0, depth);
            Vector2 xy = camera.UnprojectPosition(PsxCoordinates.FixedWorldPosition(position)) / pixelScale;
            return new(unchecked((short)(int)xy.X), unchecked((short)(int)xy.Y), depth);
        }
    }
    private void Release()
    {
        if (disposed) return;
        foreach (SmokeTrail trail in trails.ToArray()) Remove(trail);
        disposed = true; mesh.Dispose();
        foreach (StandardMaterial3D material in materials.Values) material.Dispose(); materials.Clear();
        foreach (ImageTexture texture in textures.Values) texture.Dispose(); textures.Clear();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
