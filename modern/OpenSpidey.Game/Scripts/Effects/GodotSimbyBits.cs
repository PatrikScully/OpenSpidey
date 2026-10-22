using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Effects;

/// <summary>Scene services the Simby slime bits call; bound lazily because shots only exist after the player census.</summary>
public sealed class GodotSimbyBitServices
{
    public required Func<GodotPlayer?> Player { get; init; }
    /// <summary>G_MIKE_CAMERA[0] position in whole units.</summary>
    public required Func<FixedVector3> CameraPosition { get; init; }
    public required Func<int, int> Random { get; init; }
    /// <summary>The shared actor query Web_GetGroundY traces through.</summary>
    public required Func<GodotActorWorldQuery> GroundWorld { get; init; }
    /// <summary>dword_5FBEF0 read by M3dColij_LineToSphere.</summary>
    public required Func<bool> IgnoreSphereRadius { get; init; }
    public required Action<uint, FixedVector3> PlaySound { get; init; }
    public required Func<GlowFlashParameters, GlowFlashState> CreateGlowFlash { get; init; }
    /// <summary>The scene's single source BulletList (0x56EFE4) the blobs attach to.</summary>
    public required Func<SourceActorList> Bullets { get; init; }
    /// <summary>InitItem("symblob") model 0.</summary>
    public required Func<ArrayMesh> BlobMesh { get; init; }
}

/// <summary>Original CSimbyShot/CSimbyDrop/CSimbyDropSplat/CSimbyShotSplat CQuadBits on the source Quad bit list.</summary>
public partial class GodotSimbyBits : Node3D, ISimbyBitHost
{
    private readonly List<SimbyQuadBit> bits = [];
    private readonly Dictionary<SimbyBlob, MeshInstance3D> blobs = new(ReferenceEqualityComparer.Instance);
    private ArrayMesh? blobMesh;
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly Dictionary<(SimbyBitTexture, int), (ImageTexture Texture, StandardMaterial3D Semi, StandardMaterial3D Opaque)> textures = [];
    private GodotActorWorldQuery? shotWorld;
    private bool disposed;
    public GodotSimbyBitServices? Services { get; set; }
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => bits.Count;
    public int BlobCount => blobs.Count;
    internal IReadOnlyList<SimbyQuadBit> Bits => bits;
    internal ImmediateMesh RenderMesh => mesh;
    public GodotSimbyBits() => TopLevel = true;

    public static GodotSimbyBits Create(byte[] bitsBytes, byte[] symbitsBytes)
    {
        var result = new GodotSimbyBits { Name = "OriginalSimbyBits" };
        result.Load(bitsBytes, "WebSplat", SimbyBitTexture.WebSplat, 1);
        result.Load(symbitsBytes, "SymDrop", SimbyBitTexture.SymDrop, 1);
        result.Load(symbitsBytes, "Splats", SimbyBitTexture.Splats, 3);
        return result;
    }

    private void Load(byte[] bytes, string name, SimbyBitTexture slot, int frames)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(bytes).SingleOrDefault(candidate =>
            candidate.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidDataException($"gAnimTable[{(int)slot}] needs the original {name} sprite animation.");
        if (animation.Frames.Count < frames) throw new InvalidDataException($"Original {name} lacks the frames the Simby bits use.");
        IReadOnlyList<DecodedTexture> decoded = PsxTextureReader.Read(bytes);
        for (int frame = 0; frame < frames; frame++)
        {
            // CQuadBit::SetTexture keeps only the frame's texture page; the quad maps the whole texture.
            DecodedTexture pixels = decoded.First(texture => texture.Checksum == animation.Frames[frame].TextureChecksum);
            using var image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.RgbaPixels);
            ImageTexture texture = ImageTexture.CreateFromImage(image);
            textures[(slot, frame)] = (texture, Material(texture, true), Material(texture, false));
        }
    }

    private static StandardMaterial3D Material(ImageTexture texture, bool semiTransparent) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = semiTransparent ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        AlbedoColor = new(1, 1, 1, semiTransparent ? 128 / 255f : 1),
        AlbedoTexture = texture,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
    };

    public override void _Ready() => AddChild(new MeshInstance3D
    {
        Name = "OriginalSimbyQuadBits", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    /// <summary>new CSimbyShot from CSimby::Shoot, inside CSimby::AI's collision masks.</summary>
    public void SpawnShot(FixedVector3 hand, GodotActorWorldQuery actorWorld)
    {
        ArgumentNullException.ThrowIfNull(actorWorld);
        shotWorld = actorWorld;
        try { Attach(new SimbyShot(hand, this)); }
        finally { shotWorld = null; }
    }

    public int WebGroundY(FixedVector3 position) => GroundY(position).Y;

    /// <summary>Simby_SplattyDeath after the caller hid the Simby (mFlags |= 1).</summary>
    public void SplattyDeath(FixedVector3 simbyPosition, Func<int, FixedVector3> partHookPosition) =>
        SimbySplattyDeath.Run(simbyPosition, partHookPosition, this);

    /// <summary>InitItem("symblob") (0x460020 model index 0).</summary>
    public static ArrayMesh BuildBlobMesh(PsxLibraryRepository libraries)
    {
        ArgumentNullException.ThrowIfNull(libraries);
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(SimbyBlob.ModelName));
        if (library.Scene.Models.Count == 0) throw new InvalidDataException("Original symblob assets require model 0.");
        return PsxGeometryBuilder.BuildModel(library.Scene.Models[0], library.Scene.ColorTable, new PsxMaterialLibrary(library.Textures))
            ?? throw new InvalidDataException("Original symblob model has no visible faces.");
    }

    private void Attach(SimbyQuadBit bit)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // A constructor that already called Die leaves a bit the next list pass deletes unmoved.
        bits.Insert(0, bit);
        SourceBitCreated?.Invoke(new(bit, _ => bit.Move(this),
            () => !disposed && bits.Contains(bit) && !bit.Dead,
            () => { bits.Remove(bit); Rebuild(); }));
        Rebuild();
    }

    private GodotSimbyBitServices RequireServices() =>
        Services ?? throw new InvalidOperationException("Simby bits need the bound player, camera and effect services.");
    private GodotPlayer RequirePlayer() =>
        RequireServices().Player() ?? throw new InvalidOperationException("CSimbyShot needs the live player (dword_6A9038).");

    public bool PlayerPresent => RequireServices().Player() is { } player && GodotObject.IsInstanceValid(player);
    public FixedVector3 PlayerPosition => RequirePlayer().Controller.Position;
    public FixedVector3 CameraPosition => RequireServices().CameraPosition();
    public int Random(int bound) => RequireServices().Random(bound);

    public SimbyLineHit? LineToItem(FixedVector3 start, FixedVector3 end)
    {
        GodotActorWorldQuery world = shotWorld ?? throw new InvalidOperationException("M3dZone_LineToItem only runs inside the CSimbyShot constructor.");
        GodotActorWorldHit? hit;
        world.LineOfSightCheck = true;
        // Mode 0 skips the EnvironmentalObjectList pass (sub_4527C0); the ctor asserts the hit is a static EnviroList item.
        try { hit = world.Trace(start, end, includeEnvironmentalActors: false); }
        finally { world.LineOfSightCheck = false; }
        if (hit is null) return null;
        var normal = GodotImpactWebNativeHost.Normal(hit);
        return new(hit.Position, normal.X, normal.Y, normal.Z);
    }

    /// <summary>Web_GetGroundY's 5000-unit ray; the item's mVel.vy when its mFlags carry 0x100.</summary>
    public SimbyGround GroundY(FixedVector3 position)
    {
        FixedVector3 end = position with { Y = unchecked(position.Y + 0x1388000) };
        GodotActorWorldHit? hit = RequireServices().GroundWorld().Trace(position, end);
        if (hit is null) return new(end.Y, null);
        if (hit.Collider is not ImportedCollisionBody body || (body.SourceFlags & 0x100) == 0) return new(hit.Position.Y, null);
        if (hit.SupportId is not { } id || GodotObject.InstanceFromId(id) is not IGodotEnvironmentalActor actor)
            throw new NotSupportedException("A Simby drop landed on a CBody item with no recovered body-motion owner.");
        return new(hit.Position.Y, actor.SupportMotion.Velocity.Y);
    }

    public FixedVector3? LineToPlayer(FixedVector3 from, FixedVector3 to)
    {
        GodotPlayer player = RequirePlayer();
        var body = new ActorLaserBody(player, player.Controller.Position, player.ActorState.MinorRadius, player.ActorState.BodyFlags);
        return ActorLaserCollision.CheckBodies(from, to, [body], radiusScale: 4096,
            ignoreRadius: RequireServices().IgnoreSphereRadius())?.Position;
    }

    public void HitPlayer(ActorHit hit) => RequirePlayer().Combat.Hit(hit);
    public void PlaySound(int sound, FixedVector3 position) => RequireServices().PlaySound(unchecked((uint)sound), position);
    public GlowFlashState CreateGlowFlash(GlowFlashParameters parameters) => RequireServices().CreateGlowFlash(parameters);
    public void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int itemVelocityY) =>
        Attach(new SimbyDrop(position, velocity, groundY, itemVelocityY, this));
    public void SpawnShotSplat(FixedVector3 position, FixedVector3 normal) => Attach(new SimbyShotSplat(position, normal, this));
    public void SpawnDropSplat(FixedVector3 position, int itemVelocityY) => Attach(new SimbyDropSplat(position, itemVelocityY, this));

    public void SpawnBlob(FixedVector3 position, FixedVector3 velocity, int groundY)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        GodotSimbyBitServices services = RequireServices();
        var blob = new SimbyBlob(position, velocity, groundY, services.Random);
        blobMesh ??= services.BlobMesh();
        var mesh = new MeshInstance3D { Name = "SymBlob", Mesh = blobMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
        AddChild(mesh);
        blobs.Add(blob, mesh);
        PlaceBlob(blob, mesh);
        services.Bullets().Attach(blob, () => AdvanceBlob(blob));
    }

    private void AdvanceBlob(SimbyBlob blob)
    {
        if (!blobs.TryGetValue(blob, out MeshInstance3D? mesh)) return;
        blob.Move(RequireServices().Random);
        if (!blob.Dead) { PlaceBlob(blob, mesh); return; }
        RemoveBlob(blob);
    }

    private void RemoveBlob(SimbyBlob blob)
    {
        if (!blobs.Remove(blob, out MeshInstance3D? mesh)) return;
        Services?.Bullets().Detach(blob);
        mesh.QueueFree();
    }

    /// <summary>CItem transform rebuild: angle matrix scaled per axis by 0x28..0x2C (mFlags 0x200).</summary>
    private static void PlaceBlob(SimbyBlob blob, MeshInstance3D mesh)
    {
        Transform3D transform = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(blob.AngleX, blob.AngleY, blob.AngleZ, 0, 0, 0)));
        transform.Basis *= Basis.FromScale(new Vector3(blob.ScaleX, blob.ScaleY, blob.ScaleZ) / 4096f);
        transform.Origin = PsxCoordinates.FixedWorldPosition((blob.Position >> 12) << 12);
        mesh.Transform = transform;
    }

    public override void _Process(double delta) => Rebuild();

    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        foreach (SimbyQuadBit bit in bits)
        {
            if (bit.Dead) continue;
            var (_, semi, opaque) = textures[(bit.Texture, bit.TextureFrame)];
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, bit.SemiTransparent ? semi : opaque);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(bit.ColourCode, 0, brightness));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                mesh.SurfaceSetUV(new((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f));
                mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(bit.Corners[corner]));
            }
            mesh.SurfaceEnd();
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (SimbyBlob blob in blobs.Keys.ToArray()) RemoveBlob(blob);
        bits.Clear();
        mesh.Dispose();
        foreach (var (texture, semi, opaque) in textures.Values) { semi.Dispose(); opaque.Dispose(); texture.Dispose(); }
        textures.Clear();
    }
}
