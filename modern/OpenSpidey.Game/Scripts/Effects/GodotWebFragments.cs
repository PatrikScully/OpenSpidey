using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original CWebFrag paired CGLines, source floor physics and PCGfx_DrawLine additive caps.</summary>
public partial class GodotWebFragments : Node3D
{
    private readonly List<WebFragment> fragments = [];
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
    public event Action<object>? SourceBitRemoved;
    public int Count => fragments.Count;
    public ushort? NearDepth { get; set; }
    public ushort FarDepth { get; set; } = 8048;
    internal ImmediateMesh RenderMesh => mesh;
    internal StandardMaterial3D RenderMaterial => material;
    internal IReadOnlyList<WebFragment> Fragments => fragments;
    public GodotWebFragments() => TopLevel = true;
    public override void _Ready() => AddChild(new MeshInstance3D { Name = "OriginalWebFragmentPairedLines", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    public void Emit(ImpactWebFragmentRequest request, Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var fragment = new WebFragment(request, random);
        fragments.Insert(0, fragment);
        // Source CGLine owner attaches before its protected companion is constructed.
        SourceBitCreated?.Invoke(new(fragment, _ => fragment.AdvanceBits(),
            () => !disposed && fragments.Contains(fragment) && fragment.Alive,
            () =>
            {
                fragment.DestroyCompanion();
                SourceBitRemoved?.Invoke(fragment.CompanionIdentity);
                fragments.Remove(fragment); Rebuild();
            }));
        SourceBitCreated?.Invoke(new(fragment.CompanionIdentity, _ => { },
            () => !disposed && fragments.Contains(fragment) && fragment.CompanionAlive,
            () => { fragment.DestroyCompanion(); Rebuild(); }));
        Rebuild();
    }
    public void AdvanceBits()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (WebFragment fragment in fragments) fragment.AdvanceBits();
        fragments.RemoveAll(fragment => !fragment.Alive); Rebuild();
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
        foreach (WebFragment fragment in fragments)
        {
            // Companion CGLine attaches after its owner, so it precedes the owner in the source list.
            if (fragment.CompanionAlive) Draw(fragment.CompanionStart, fragment.CompanionEnd, fragment.CodeRgb0, fragment.CodeRgb1);
            Draw(fragment.Start, fragment.End, fragment.CodeRgb0, fragment.CodeRgb1);
        }
        void Draw(FixedVector3 start, FixedVector3 end, uint startColour, uint endColour)
        {
            if (!Project(start, out Vector2 head, out float headDepth) || !Project(end, out Vector2 tail, out float tailDepth)) return;
            PlayerImpactLineCorner[] corners = PlayerImpactLineGeometry.Build(head.X, head.Y, tail.X, tail.Y);
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            foreach (int index in new[] { 0, 1, 2, 0, 3, 2 })
            {
                PlayerImpactLineCorner corner = corners[index];
                mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(corner.Head ? startColour : endColour, 0, brightness));
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
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true; fragments.Clear(); mesh.Dispose(); material.Dispose();
    }
}
