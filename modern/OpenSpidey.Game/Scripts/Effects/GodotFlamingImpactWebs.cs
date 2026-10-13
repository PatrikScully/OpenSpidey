using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

public interface IGodotFlamingWebCollisionHost
{
    ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end);
    object? CheckObjectCollision(FixedVector3 start, FixedVector3 end);
    void HitBody(object identity, ActorHit hit);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void FlickEligibleSwitch(object identity);
    int GetGroundY(FixedVector3 position);
}

/// <summary>Original flaming impact-web flat list, actual CMotionBlur trails and CFireySpark pixel list.</summary>
public partial class GodotFlamingImpactWebs : Node3D, IFlamingImpactWebHost
{
    private abstract class FlatEntry
    {
        public abstract bool Alive { get; }
        public abstract FixedVector3 Position { get; }
        public abstract short Scale { get; }
        public abstract short Angle { get; }
        public abstract int Frame { get; }
        public abstract uint CodeRgb { get; }
        public abstract uint PostScale { get; }
        public abstract void Advance(uint clock);
    }
    private sealed class ShotEntry(FlamingImpactWeb state, GodotFlamingImpactWebs owner) : FlatEntry
    {
        public FlamingImpactWeb State => state;
        public override bool Alive => state.Alive;
        public override FixedVector3 Position => state.Position;
        public override short Scale => state.Scale;
        public override short Angle => state.Angle;
        public override int Frame => state.Frame;
        public override uint CodeRgb => state.CodeRgb;
        public override uint PostScale => state.PostScale;
        public override void Advance(uint clock) => state.Advance(clock, owner, owner.random);
    }
    private sealed class TrailEntry(FlamingWebTrailRequest request, byte frameCount) : FlatEntry, IFlamingWebTrail
    {
        private short angle;
        public SpriteRingParticleMotion Motion { get; } = new(new(request.Position, request.Velocity,
            request.AnimationIndex, request.Scale, request.ScaleDecay, request.ColourDecay, 0), frameCount);
        public override bool Alive => Motion.Alive;
        public override FixedVector3 Position => Motion.Position;
        public override short Scale => Motion.Scale;
        public override short Angle => angle;
        short IFlamingWebTrail.Angle { get => angle; set => angle = value; }
        public override int Frame => Motion.Frame;
        public override uint CodeRgb => Motion.CodeRgb;
        public override uint PostScale => Motion.PostScale;
        public override void Advance(uint clock) => Motion.Advance();
    }

    private readonly List<FlatEntry> flatBits = [];
    private readonly List<FireSpark> sparks = [];
    private readonly ImmediateMesh flatMesh = new(), pixelMesh = new();
    private readonly List<ImageTexture> textures = [];
    private readonly List<StandardMaterial3D> materials = [];
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly StandardMaterial3D pixelMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        AlbedoColor = new(1, 1, 1, 128 / 255f)
    };
    private SpriteAnimationFrame[] frames = [];
    private IGodotFlamingWebCollisionHost collision = null!;
    private Func<int, int> random = null!;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceFlatBitCreated;
    public event Action<SourceParticleRegistration>? SourcePixelBitCreated;
    public int ProjectileCount => flatBits.Count(bit => bit is ShotEntry && bit.Alive);
    public int TrailCount => flatBits.Count(bit => bit is TrailEntry && bit.Alive);
    public int SparkCount => sparks.Count(spark => spark.Alive);
    public ushort FarDepth { get; set; } = 8048;
    internal IReadOnlyList<FlamingImpactWeb> Projectiles => flatBits.OfType<ShotEntry>().Select(entry => entry.State).ToArray();
    internal IReadOnlyList<SpriteRingParticleMotion> Trails => flatBits.OfType<TrailEntry>().Select(entry => entry.Motion).ToArray();
    internal IReadOnlyList<FireSpark> Sparks => sparks;
    internal IReadOnlyList<SpriteAnimationFrame> SourceFrames => frames;
    internal ImmediateMesh FlatMesh => flatMesh;
    internal ImmediateMesh PixelMesh => pixelMesh;
    internal IReadOnlyList<ImageTexture> SourceTextures => textures;

    public GodotFlamingImpactWebs() => TopLevel = true;

    public static GodotFlamingImpactWebs Create(byte[] fireBitsBytes, IGodotFlamingWebCollisionHost collision, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(collision); ArgumentNullException.ThrowIfNull(random);
        SpriteAnimation animation = SpriteAnimationReader.Read(fireBitsBytes).Single(animation =>
            animation.Name.Equals("fireimp", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count is 0 or > byte.MaxValue) throw new InvalidDataException("Original fireimp animation requires its original byte-sized frame list.");
        DecodedTexture[] decoded = PsxTextureReader.Read(fireBitsBytes).ToArray();
        var result = new GodotFlamingImpactWebs
        {
            Name = "OriginalFlamingImpactWebs", frames = animation.Frames.ToArray(), collision = collision, random = random
        };
        try
        {
            foreach (SpriteAnimationFrame frame in result.frames)
            {
                DecodedTexture pixels = decoded.First(texture => texture.Checksum == frame.TextureChecksum);
                using var image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.RgbaPixels);
                ImageTexture texture = ImageTexture.CreateFromImage(image); result.textures.Add(texture);
                result.materials.Add(new()
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
                    VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
                    AlbedoColor = new(1, 1, 1, 128 / 255f), AlbedoTexture = texture,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
                });
            }
            return result;
        }
        catch { result.Release(); result.Free(); throw; }
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "OriginalFireimpProjectilesAndBlurs", Mesh = flatMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Name = "OriginalFireySparkPixels", Mesh = pixelMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }

    public FlamingImpactWeb Emit(FixedVector3 position, ActorAngles direction, int damage, uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var state = new FlamingImpactWeb(position, direction, damage, clock, this, random);
        var entry = new ShotEntry(state, this);
        flatBits.Insert(0, entry);
        RegisterFlat(entry, state);
        Rebuild(); return state;
    }
    public void MoveFlatBits(uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (FlatEntry bit in flatBits.ToArray()) if (bit.Alive) bit.Advance(clock);
        Rebuild();
    }
    public void MovePixelBits()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (FireSpark spark in sparks) spark.AdvanceBits();
        Rebuild();
    }
    public void RemoveDeadBits()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        flatBits.RemoveAll(bit => !bit.Alive); sparks.RemoveAll(spark => !spark.Alive); Rebuild();
    }
    // Actual PC411B30 walks PixelList before FlatBitList: newborn impact sparks wait for its next call.
    public void AdvanceBits(uint clock) { MovePixelBits(); MoveFlatBits(clock); RemoveDeadBits(); }

    public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end) => collision.TraceInitialEnvironment(start, end);
    public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end) => collision.CheckObjectCollision(start, end);
    public void HitBody(object identity, ActorHit hit) => collision.HitBody(identity, hit);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => collision.PlayPositionSound(sound, position, pitch);
    public void FlickEligibleSwitch(object identity) => collision.FlickEligibleSwitch(identity);
    public int GetGroundY(FixedVector3 position) => collision.GetGroundY(position);
    public IFlamingWebTrail CreateTrail(FlamingWebTrailRequest request)
    {
        if (request.AnimationIndex != 16) throw new NotSupportedException("Flaming web trail requires original fireimp quick animation16.");
        var trail = new TrailEntry(request, checked((byte)frames.Length)); flatBits.Insert(0, trail);
        RegisterFlat(trail, trail.Motion);
        return trail;
    }
    public void CreateSpark(FireSparkRequest request, Func<int, int> random)
    {
        var spark = new FireSpark(request, random);
        sparks.Insert(0, spark);
        SourcePixelBitCreated?.Invoke(new(spark, _ => spark.AdvanceBits(),
            () => !disposed && sparks.Contains(spark) && spark.Alive,
            () => { sparks.Remove(spark); Rebuild(); }));
    }
    private void RegisterFlat(FlatEntry entry, object identity) => SourceFlatBitCreated?.Invoke(new(identity, entry.Advance,
        () => !disposed && flatBits.Contains(entry) && entry.Alive,
        () => { flatBits.Remove(entry); Rebuild(); }));

    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        flatMesh.ClearSurfaces(); pixelMesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        Vector2 pixelScale = new(size.Y / 240 * .625f, size.Y / 240);
        int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
            (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
        int near = (((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1;
        foreach (FlatEntry bit in flatBits)
        {
            if (!bit.Alive || bit.Scale == 0) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(bit.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = SourceDepth(camera, bit.Position);
            if (worldDepth <= 0 || depth < near || depth > FarDepth) continue;
            if ((uint)bit.Frame >= frames.Length) throw new InvalidDataException("Original fireimp frame is outside its owned sprite table.");
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frames[bit.Frame], unchecked((ushort)bit.Scale),
                bit.Angle, depth, (int)logical.X, (int)logical.Y, bit.PostScale);
            if (corners is null) continue;
            flatMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, materials[bit.Frame]);
            flatMesh.SurfaceSetColor(GodotGlowFlash.SourceColour(bit.CodeRgb, 0, brightness));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                Vector2 pixel = (new Vector2(corners[corner].X, corners[corner].Y) + logical -
                    new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                flatMesh.SurfaceSetUV(new((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f));
                flatMesh.SurfaceAddVertex(camera.ProjectPosition(pixel, worldDepth));
            }
            flatMesh.SurfaceEnd();
        }
        foreach (FireSpark spark in sparks)
        {
            if (!spark.Alive) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(spark.Position);
            float depth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            if (depth <= 0) continue;
            float inverseDepth = 1 / (depth * 100), biased = inverseDepth - 7.0710726f, restored = biased + 7.0710678f;
            if (restored <= 0) continue;
            float renderDepth = 1 / restored / 100;
            Vector2 origin = camera.UnprojectPosition(center), extent = pixelScale * spark.DisplayWidth;
            pixelMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, pixelMaterial);
            // DisplayPixelList uses DrawQuad2D's raw RGB, which bypasses DrawQPoly's halve/bias transform.
            pixelMesh.SurfaceSetColor(new(brightness[spark.Red] / 255f, brightness[spark.Green] / 255f, brightness[spark.Blue] / 255f));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
                pixelMesh.SurfaceAddVertex(camera.ProjectPosition(origin + new Vector2(
                    (corner & 1) != 0 ? extent.X : 0, (corner & 2) != 0 ? extent.Y : 0), renderDepth));
            pixelMesh.SurfaceEnd();
        }
    }

    private static int SourceDepth(Camera3D camera, FixedVector3 position) =>
        FixedRotation.Transform(FixedRotation.TransposeRotation(PsxCoordinates.OriginalRotation(camera.GlobalBasis)),
            (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
    private void Release()
    {
        if (disposed) return;
        disposed = true; flatBits.Clear(); sparks.Clear(); flatMesh.Dispose(); pixelMesh.Dispose(); pixelMaterial.Dispose();
        foreach (StandardMaterial3D material in materials) material.Dispose();
        foreach (ImageTexture texture in textures) texture.Dispose();
        materials.Clear(); textures.Clear();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
