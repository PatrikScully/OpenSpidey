using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>DisplayGLineList/PCGfx_DrawLine additive gradients, projected two-pixel caps and actual ballistic lifetime.</summary>
public partial class GodotPlayerImpactLines : Node3D
{
    private sealed record Entry(PlayerImpactLineParticle Appearance, LineParticleMotion Motion);
    private readonly List<Entry> particles = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly StandardMaterial3D material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new(1, 1, 1, 128 / 255f),
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
    };
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => particles.Count;
    public ushort? NearDepth { get; set; }
    public ushort FarDepth { get; set; } = 8048;
    internal ImmediateMesh RenderMesh => mesh;
    internal StandardMaterial3D RenderMaterial => material;
    internal IReadOnlyList<LineParticleMotion> Motions => particles.Select(entry => entry.Motion).ToArray();
    public GodotPlayerImpactLines() => TopLevel = true;
    public override void _Ready() => AddChild(new MeshInstance3D { Name = "OriginalGLineParticleQuads", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    public void Emit(PlayerImpactLineParticle particle, LineParticleMotion motion)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (particle.CodeFlags != 0x02000000 || particle.VerticalAcceleration != motion.VerticalAcceleration)
            throw new NotSupportedException("Source additive line appearance must match its owned particle motion.");
        var entry = new Entry(particle, motion);
        particles.Insert(0, entry);
        SourceBitCreated?.Invoke(new(motion, motion.Advance,
            () => !disposed && particles.Contains(entry) && motion.Alive,
            () => { particles.Remove(entry); Rebuild(); }));
        Rebuild();
    }
    public void Advance(uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (Entry entry in particles) entry.Motion.Advance(clock);
        particles.RemoveAll(entry => !entry.Motion.Alive);
        Rebuild();
    }
    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        Vector2 pixelScale = new(viewport.Y / 240 * .625f, viewport.Y / 240);
        int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
            (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2) * 4096 * CameraProjection.OriginalAspect);
        int clipMin = NearDepth ?? (ushort)((((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1);
        foreach (Entry entry in particles)
        {
            if (!Project(entry.Motion.Head, out Vector2 head, out float headDepth) || !Project(entry.Motion.Tail, out Vector2 tail, out float tailDepth)) continue;
            PlayerImpactLineCorner[] corners = PlayerImpactLineGeometry.Build(head.X, head.Y, tail.X, tail.Y);
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            foreach (int index in new[] { 0, 1, 2, 0, 3, 2 })
            {
                PlayerImpactLineCorner corner = corners[index];
                mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(corner.Head ? entry.Appearance.StartRgb : entry.Appearance.EndRgb, 0, brightness));
                mesh.SurfaceAddVertex(camera.ProjectPosition(new(corner.X, corner.Y), corner.Head ? headDepth : tailDepth));
            }
            mesh.SurfaceEnd();
        }
        bool Project(FixedVector3 position, out Vector2 screen, out float adjustedDepth)
        {
            Vector3 world = PsxCoordinates.FixedWorldPosition(position);
            float depth = -camera.GlobalBasis.Z.Dot(world - camera.GlobalPosition);
            FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
            var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20, rotation.M01, rotation.M11, rotation.M21,
                rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
            int sourceDepth = FixedRotation.Transform(view, (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
            screen = default; adjustedDepth = 0;
            if (depth <= 0 || sourceDepth < clipMin || sourceDepth > FarDepth) return false;
            // Original DisplayGLineList subtracts this float bias, then PCGfx_DrawLine adds its distinct constant.
            float inverseDepth = 1 / (depth * 100);
            float biased = inverseDepth - 7.0710726f;
            float restored = biased + 7.0710678f;
            if (restored <= 0) return false;
            adjustedDepth = 1 / restored / 100;
            Vector2 logical = camera.UnprojectPosition(world) / pixelScale;
            screen = new Vector2(unchecked((short)GameWord.TruncateToInt32(logical.X)),
                unchecked((short)GameWord.TruncateToInt32(logical.Y))) * pixelScale;
            return true;
        }
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true; particles.Clear(); mesh.Dispose(); material.Dispose();
    }
}
