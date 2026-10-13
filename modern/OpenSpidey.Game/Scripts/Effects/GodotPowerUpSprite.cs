using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original protected type18 CFlatBit using the current level's named TrainingPick-Up_01 texture.</summary>
public partial class GodotPowerUpSprite : Node3D
{
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public FixedVector3 SourcePosition { get; set; }
    public uint CodeRgb { get; private set; }
    public ushort SourceSpriteScale => 400;
    public bool Protected => true;
    public bool Attached => !disposed;
    internal ImmediateMesh RenderMesh => mesh;
    internal SpriteAnimationFrame SourceFrame => frame;
    public GodotPowerUpSprite() => TopLevel = true;
    public static GodotPowerUpSprite Create(DecodedTexture pixels, uint tint)
    {
        if (pixels.Width != 128 || pixels.Height != 128) throw new InvalidDataException("The genuine TrainingPick-Up_01 rectangle must be128 by128.");
        byte width = unchecked((byte)(pixels.Width - 1)), height = unchecked((byte)(pixels.Height - 1));
        var result = new GodotPowerUpSprite { Name = "OriginalProtectedPickupFlat", CodeRgb = 0x2E000000 | (tint & 0xFFFFFF),
            frame = new(unchecked((sbyte)(width / -2)), unchecked((sbyte)(height / -2)), width, height, pixels.Checksum) };
        using var image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.RgbaPixels);
        result.texture = ImageTexture.CreateFromImage(image);
        result.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            AlbedoColor = new(1, 1, 1, 128 / 255f), AlbedoTexture = result.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
        };
        result.AddChild(new MeshInstance3D { Name = "SourceFlatTextureRectangle", Mesh = result.mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return result;
    }
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
        int near = (((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1;
        Vector3 center = PsxCoordinates.FixedWorldPosition(SourcePosition);
        float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
        int depth = FixedRotation.Transform(FixedRotation.TransposeRotation(PsxCoordinates.OriginalRotation(camera.GlobalBasis)),
            (SourcePosition >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
        if (worldDepth <= 0 || depth < near || depth > 8048) return;
        Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
        ThugPingPixel[]? corners = ThugPingProjection.Project(frame, SourceSpriteScale, 0, depth, (int)logical.X, (int)logical.Y, 0x10001000);
        if (corners is null) return;
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(CodeRgb, 0, brightness));
        foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
        {
            Vector2 pixel = (new Vector2(corners[corner].X, corners[corner].Y) + logical -
                new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
            mesh.SurfaceSetUV(new((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f));
            mesh.SurfaceAddVertex(camera.ProjectPosition(pixel, worldDepth));
        }
        mesh.SurfaceEnd();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true; mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
