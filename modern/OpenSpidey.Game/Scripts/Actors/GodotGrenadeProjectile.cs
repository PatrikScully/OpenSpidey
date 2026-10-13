using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Genuine source grenade model, collision, protected glow/shadow and ordered explosion/damage adapters.</summary>
public partial class GodotGrenadeProjectile : Node3D, IGrenadeProjectileHost
{
    private GodotGrenadeConfiguration configuration = null!;
    private ArrayMesh? mesh;
    private readonly List<StandardMaterial3D> materials = [];
    private readonly List<ImageTexture> textures = [];
    private GlowState? glow;
    private GodotActorShadow? shadow;
    private bool attached;
    private bool disposed;
    public GrenadeProjectile State { get; private set; } = null!;
    public ActorLifecycle Lifecycle { get; } = new(false, false);
    public string SourceAsset { get; private set; } = "";
    public int SourceModelIndex { get; private set; } = -1;
    internal ArrayMesh? RenderMesh => mesh;
    internal GlowState? ProtectedGlow => glow;
    internal GodotActorShadow? SourceShadow => shadow;
    internal bool ResourcesDisposed => disposed;
    public int Timer => configuration.Timer();
    public int TotalTime => configuration.TotalTime();
    public int LevelId => configuration.LevelId();
    public byte ObjectFileRegion => configuration.ObjectFileRegion();
    public byte DefaultGrenadeRegion => configuration.DefaultGrenadeRegion();

    public GodotGrenadeProjectile() => TopLevel = true;

    public static GodotGrenadeProjectile Create(ThugGrenadeRequest request, GodotGrenadeConfiguration configuration)
    {
        if (request.TargetList != ThugProjectileTargetList.Baddies)
            throw new ArgumentOutOfRangeException(nameof(request), "Unknown source thug grenade target list.");
        return Create(request.Start, request.Target, request.Speed, request.Damage, request.Radius,
            GrenadeTargetList.Baddies, request.ModelChecksum, request.ExplodeOnContact, request.Flag, configuration);
    }

    public static GodotGrenadeProjectile Create(FixedVector3 start, FixedVector3 target, int speed, int damage, int radius,
        GrenadeTargetList targets, uint checksum, byte contact, byte flag, GodotGrenadeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var result = new GodotGrenadeProjectile { Name = "OriginalGrenade", configuration = configuration };
        try
        {
            result.State = new(start, target, speed, damage, radius, targets, checksum, contact, flag, result, configuration.Random);
            result.Synchronize();
            return result;
        }
        catch { result.Release(); result.Free(); throw; }
    }

    /// <summary>One source bullet-list update. Dead nodes remain visible/recoverable until the owner deletes them.</summary>
    public void Advance()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        switch (Lifecycle.PrepareUpdate(0, 0))
        {
            case ActorUpdateAction.RunBehavior: State.Update(); Synchronize(); break;
            case ActorUpdateAction.Delete: Free(); break;
        }
    }

    public void Synchronize()
    {
        if (disposed || State is null) return;
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(State.Angles.X, State.Angles.Y, State.Angles.Z, 0, 0, 0));
        Basis basis = PsxCoordinates.CharacterTransform(rotation).Basis.Scaled(new Vector3(State.ModelScale.X, State.ModelScale.Y, State.ModelScale.Z) / 4096f);
        Transform = new(basis, PsxCoordinates.FixedWorldPosition(State.Position));
        // Like C3DExplosion, source CBody::Die never sets the render hide flag.
        Visible = (State.ItemFlags & 1) == 0;
        configuration.Glows.Rebuild();
    }

    public void AttachProjectile(GrenadeProjectile projectile)
    {
        State = projectile;
        attached = true;
        configuration.AttachProjectile(this);
    }

    public void LoadModel(uint checksum, byte region)
    {
        var library = configuration.ResolveRegion(region);
        int index = library.FindModel(checksum);
        SourceAsset = library.AssetPath;
        SourceModelIndex = index;
        var temporary = PsxGeometryBuilder.BuildModel(library.Scene.Models[index], library.Scene.ColorTable,
            new PsxMaterialLibrary(library.Textures)) ?? throw new InvalidDataException("Original grenade model has no source geometry.");
        try
        {
            mesh = new ArrayMesh();
            foreach (int surface in Enumerable.Range(0, temporary.GetSurfaceCount()))
            {
                Godot.Collections.Array arrays = temporary.SurfaceGetArrays(surface);
                Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
                byte[] brightness = GodotGlowFlash.SourceBrightness();
                for (int vertex = 0; vertex < colours.Length; vertex++)
                {
                    Color value = colours[vertex];
                    uint packed = (uint)Math.Clamp((int)Math.Round(value.R * 255), 0, 255) |
                        ((uint)Math.Clamp((int)Math.Round(value.G * 255), 0, 255) << 8) |
                        ((uint)Math.Clamp((int)Math.Round(value.B * 255), 0, 255) << 16);
                    colours[vertex] = GodotGlowFlash.SourceColour(packed, 0, brightness);
                }
                arrays[(int)Mesh.ArrayType.Color] = colours;
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                if (temporary.SurfaceGetMaterial(surface) is not StandardMaterial3D material)
                    throw new InvalidDataException("Original grenade mesh surface has no imported source material.");
                material.VertexColorIsSrgb = true;
                materials.Add(material);
                if (material.AlbedoTexture is ImageTexture texture && !textures.Contains(texture)) textures.Add(texture);
                mesh.SurfaceSetMaterial(surface, material);
            }
            AddChild(new MeshInstance3D { Name = "ChecksumSelectedOriginalGrenade", Mesh = mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        finally { temporary.Dispose(); }
    }

    public GrenadeSweep Sweep(FixedVector3 start, FixedVector3 end, bool lineOfSightCheck)
    {
        configuration.World.LineOfSightCheck = lineOfSightCheck;
        GodotActorWorldHit? hit;
        try { hit = configuration.World.Trace(start, end); }
        finally { configuration.World.LineOfSightCheck = false; }
        if (hit is null) return default;
        FixedJointTransform rotation = hit.Collider is ImportedCollisionBody body ? body.SourceRotation :
            PsxCoordinates.OriginalRotation(hit.Collider.GlobalBasis);
        FixedVector3 normal = PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal, rotation);
        int distance = GameWord.TruncateToInt32(PsxCoordinates.FixedWorldPosition(start).DistanceTo(PsxCoordinates.FixedWorldPosition(hit.Position)) * 100);
        return new(true, distance, hit.Position, normal);
    }

    public int GroundHeight(FixedVector3 position, int above, int below) => configuration.World.GroundHeight(position, above, below);
    public void SetShadow(bool enabled, FixedVector3 position, ushort scale, short distance)
    {
        if (!enabled) { if (shadow is not null) { shadow.Free(); shadow = null; } return; }
        if (shadow is null)
        {
            shadow = configuration.ShadowFactory();
            AddChild(shadow);
        }
        shadow.State.Update(State.BodyFlags, position, scale, distance, State.ShadowThreshold);
        shadow.Rebuild();
    }
    public void CreateGlow(int sectors, int bands) { glow = new(sectors, bands); configuration.Glows.Emit(glow); }
    private GlowState Glow => glow ?? throw new InvalidOperationException("Grenade glow has not been constructed.");
    public void SetGlowSkipTriangles(bool skip) => Glow.SkipTriangles = skip;
    public void SetGlowFringeWidth(int index, int width) => Glow.SetFringeWidth(index, unchecked((uint)width));
    public void SetGlowCentreRgb(byte red, byte green, byte blue) => Glow.SetCentreRgb(red, green, blue);
    public void SetGlowRgb(byte red, byte green, byte blue) => Glow.SetRgb(red, green, blue);
    public void SetGlowFringeRgb(int index, byte red, byte green, byte blue) => Glow.SetFringeRgb(index, red, green, blue);
    public void SetGlowAngle(int angle) => Glow.Angle = unchecked((ushort)angle);
    public void SetGlowRadius(int radius) => Glow.SetRadius(radius);
    public void SetGlowPosition(FixedVector3 position) => Glow.Position = position;
    public void SetGlowProtected(bool protect) => Glow.Protected = protect;
    public void PlaySound(uint sound, FixedVector3 position, int pitchOffset) => configuration.PlayPositionSound(sound, position, pitchOffset);
    public void GiveScaledDamageToObjects(GrenadeDamageRequest request) => configuration.ApplyObjectBlast(this, request);
    public void GiveScaledDamageToEnviro(FixedVector3 position, ushort damage, ushort radius) => configuration.ApplyEnvironmentBlast(this, position, damage, radius);
    public void CreateGrenadeExplosion(FixedVector3 position) => configuration.Explosions.Emit(position, configuration.Random);
    public void CreateGlowFlash(FixedVector3 position, IReadOnlyList<int> arguments) => configuration.Glows.Emit(GlowFlashParameters.FromSourceArguments(position, arguments), configuration.Random);
    public void MarkDead() => Lifecycle.Kill();
    public void DestroyGlow()
    {
        if (glow is null) return;
        if (!configuration.Glows.ResourcesDisposed) configuration.Glows.Remove(glow);
        glow = null;
    }
    public void DetachProjectile() { if (attached) { attached = false; configuration.DetachProjectile(this); } }
    private void Release()
    {
        if (disposed) return;
        disposed = true; // A scene-owner detach may synchronously exit this node again.
        State?.Dispose();
        if (shadow is not null && GodotObject.IsInstanceValid(shadow)) shadow.Free();
        shadow = null;
        mesh?.Dispose();
        foreach (StandardMaterial3D material in materials) material.Dispose();
        foreach (ImageTexture texture in textures) texture.Dispose();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
