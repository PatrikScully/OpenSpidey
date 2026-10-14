using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.PowerUps;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Actual CPowerUp CBody: a source PowerUp-list identity, imported items model, and owned protected shadow/flat bits.</summary>
public partial class GodotPowerUpActor : GodotScriptActor, IPowerUpHost, IPlayerCollectiblePowerUp, IActorScriptVariableWorld,
    IGodotSourceActorVisibility
{
    private GodotActorRegistry registry = null!;
    private GodotPowerUpConfiguration configuration = null!;
    private TriggerPowerUpDefinition pickupDefinition = null!;
    private bool adopted;
    private bool disposed;
    private PsxAssetLibrary? itemsBank;
    private MeshInstance3D? modelNode;
    private ArrayMesh? modelMesh;
    private PsxMaterialLibrary? materialLibrary;
    private readonly HashSet<StandardMaterial3D> materials = [];
    private readonly List<(Godot.Collections.Array Arrays, Color[] Colors, StandardMaterial3D Material)> surfaces = [];
    private readonly Dictionary<StandardMaterial3D, SourceMaterialSnapshot> originalMaterials = [];
    private (bool Enabled, uint Rgb)? lastTint;
    private GodotActorShadow? shadow;
    private GodotPowerUpSprite? pickupBit;
    public PowerUpState State { get; } = new();
    public ushort BodyFlags { get => State.BodyFlags; set => State.BodyFlags = value; }
    public ushort Type => State.Type;
    public ushort Radius => State.Radius;
    FixedVector3 IPlayerCollectiblePowerUp.Position => State.Position;
    public override FixedVector3 SourcePosition => State.Position;
    public override ushort SourceNodeId => State.NodeId;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.PowerUp;
    public byte FailedCollectionFlash { get => State.FailedCollectionFlash; set => State.FailedCollectionFlash = value; }
    public int ModelIndex { get; private set; } = -1;
    public PsxAssetLibrary? SourceVisibilityBank => itemsBank;
    public ushort SourceVisibilityFlags => State.ItemFlags;
    public void SetSourceVisibilityFlags(ushort flags) { State.ItemFlags = flags; Synchronize(); }
    internal MeshInstance3D? RenderModel => modelNode;
    internal GodotActorShadow? SourceShadow => shadow;
    internal GodotPowerUpSprite? SourcePickupBit => pickupBit;
    public bool ResourcesDisposed => disposed;

    internal static GodotPowerUpActor Create(TriggerPowerUpDefinition definition, ushort type,
        GodotActorRegistry registry, GodotPowerUpConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        if (registry.Player is { } player && !ReferenceEquals(configuration.Collection, player.PowerUpCollection))
            throw new ArgumentException("The scene pickup factory must use the actual player's health/jingle fields.", nameof(configuration));
        var actor = new GodotPowerUpActor { Name = $"OriginalPowerUp{definition.NodeId}", registry = registry,
            configuration = configuration, pickupDefinition = definition,
            Definition = new(definition.NodeId, definition.NodeKind, unchecked((short)type), definition.Links, [], definition.Position,
                new(0, 0, 0), definition.PositionOffset, ReadOnlyMemory<byte>.Empty),
            ScriptState = new() { ActorType = unchecked((short)type), NodeId = definition.NodeId }, Lifecycle = new(true, false) };
        try
        {
            registry.ActorRemoved += actor.OnSourceRemoved;
            registry.ActorSuspended += actor.OnSourceSuspended;
            registry.ActorResumed += actor.OnSourceResumed;
            registry.ActorDistanceMeasured += actor.OnDistanceMeasured;
            actor.Variables = new(actor.ScriptState, actor);
            FixedVector3 position = new(unchecked(definition.Position.X << 12), unchecked(definition.Position.Y << 12), unchecked(definition.Position.Z << 12));
            PowerUpBehavior.Construct(actor.State, type, position, default, definition.Flags, definition.Lifetime, definition.SignalNode, actor);
            PowerUpBehavior.SetNode(actor.State, definition.NodeId);
            actor.Synchronize(); return actor;
        }
        catch
        {
            if (actor.adopted && registry.Owns(actor)) registry.DestroyImmediately(actor);
            else if (GodotObject.IsInstanceValid(actor)) actor.Free();
            throw;
        }
    }
    internal override void Begin() => Synchronize();
    internal override void Advance()
    {
        State.BodyFlags = State.Timing.PrepareClock(Clock, State.BodyFlags);
        PowerUpBehavior.Update(State, this); SourceUpdates++;
        Synchronize(); UpdateShadow();
    }
    public byte TakeEffect() { byte result = PowerUpBehavior.TakeEffect(State, this); Synchronize(); return result; }
    public void Die() { PowerUpBehavior.Die(State, this); Synchronize(); }
    public int LogicFrame => configuration.LogicFrame();
    public uint Clock => configuration.SourceClock();
    public int LevelId => registry.Environment.LevelId;
    public int PlayerMaxHealth => RequirePlayer().Combat.State.MaxHealth;
    public bool SpeechBusy => configuration.SpeechBusy();
    public CampaignProgress Progress => configuration.Progress;
    public int Random(int range) => registry.RandomSource(range);
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    public FixedVector3? PlayerPosition => registry.PlayerPosition;
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
    public ushort FirstLink(ushort nodeId) => registry.FirstLink(nodeId);
    public int GroundHeight(FixedVector3 position, int above, int below)
    {
        var from = position with { Y = unchecked(position.Y - (above << 12)) };
        var to = position with { Y = unchecked(position.Y + (below << 12)) };
        return configuration.World.Trace(from, to)?.Contact.Position.Y ?? -1;
    }
    public void ShadowOn()
    {
        // Source ShadowOn only sets bit8; allocation belongs to the post-AI UpdateShadow phase.
        if ((State.BodyFlags & 8) == 0) throw new InvalidOperationException("The source ShadowOn boundary lost body bit8.");
    }
    public void KillShadow()
    {
        if (shadow is not { } owned) return;
        shadow = null; configuration.SourceBitRemoved(owned); owned.State.Kill(); owned.Free();
    }
    private void UpdateShadow()
    {
        if ((State.BodyFlags & 8) == 0) { KillShadow(); return; }
        if (shadow is null)
        {
            shadow = GodotActorShadow.Create(configuration.ShadowTexture); AddChild(shadow);
            GodotActorShadow owned = shadow;
            configuration.SourceBitCreated(SourceParticleList.Quad, new(owned, _ => { },
                () => !disposed && ReferenceEquals(shadow, owned), KillShadow));
        }
        shadow.State.Update(State.BodyFlags, State.ShadowPosition, State.ShadowScale, State.ShadowDistance, State.ShadowThreshold);
        shadow.Rebuild();
    }
    public void InitializeItemsBank()
    {
        if (!configuration.Items.AssetPath.EndsWith("items.psx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original pickup InitItem must select the genuine items bank.");
        // InitItem writes the CItem region before the type switch, including non-model type18.
        itemsBank = configuration.Items;
    }
    public void SelectModel(uint checksum)
    {
        if (modelNode is not null) throw new InvalidOperationException("Original pickup constructor selected a model twice.");
        PsxAssetLibrary library = itemsBank ?? throw new InvalidOperationException("Original pickup model selection requires its actual InitItem region.");
        ModelIndex = library.FindModel(checksum); PsxModel model = library.Scene.Models[ModelIndex];
        foreach (uint key in model.Faces.Select(face => face.TextureChecksum).OfType<uint>().Distinct())
            if (!library.Textures.Any(texture => texture.Checksum == key)) throw new InvalidDataException($"Original items texture {key:X8} is absent.");
        materialLibrary = new(library.Textures);
        modelMesh = PsxGeometryBuilder.BuildModel(model, library.Scene.ColorTable, materialLibrary) ??
            throw new InvalidDataException("Original items model has no visible source faces.");
        StandardMaterial3D untextured = materialLibrary.GetMaterial(null);
        materials.Add(untextured); originalMaterials.TryAdd(untextured, SourceMaterialSnapshot.Of(untextured));
        for (int index = 0; index < modelMesh.GetSurfaceCount(); index++)
            if (modelMesh.SurfaceGetMaterial(index) is StandardMaterial3D material)
            {
                materials.Add(material); originalMaterials.TryAdd(material, SourceMaterialSnapshot.Of(material));
                Godot.Collections.Array arrays = modelMesh.SurfaceGetArrays(index);
                surfaces.Add((arrays, arrays[(int)Mesh.ArrayType.Color].AsColorArray(), material));
            }
        modelNode = new() { Name = "OriginalItemsModel", Mesh = modelMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(modelNode);
    }
    public void AttachToPowerUpList() { registry.Adopt(this); adopted = true; }
    public void SendNodePulses(ushort node)
    {
        if (node != pickupDefinition.NodeId) throw new InvalidDataException("Original pickup pulse lost its authored node.");
        foreach (ushort linked in pickupDefinition.Links) registry.ReceivePulse(linked);
    }
    public void SendSignal(ushort node) => configuration.SendSignal(node);
    public void MarkDead() => Lifecycle.Kill();
    public void EmitGlow(GlowFlashParameters parameters) => configuration.Glows.Emit(parameters, registry.RandomSource);
    public void CreatePickupBit(uint tint)
    {
        if (pickupBit is not null) throw new InvalidOperationException("Original pickup already owns a protected flat bit.");
        pickupBit = GodotPowerUpSprite.Create(configuration.TrainingPickupTexture(), tint); AddChild(pickupBit);
        GodotPowerUpSprite owned = pickupBit;
        configuration.SourceBitCreated(SourceParticleList.Flat, new(owned, _ => { },
            () => !disposed && ReferenceEquals(pickupBit, owned), DeletePickupBit));
    }
    public void SetPickupBitPosition(FixedVector3 position)
    { (pickupBit ?? throw new InvalidOperationException("Original non-model pickup lost its protected bit.")).SourcePosition = position; pickupBit.Rebuild(); }
    public void DeletePickupBit()
    {
        if (pickupBit is not { } owned) return;
        pickupBit = null; configuration.SourceBitRemoved(owned); owned.Free();
    }
    public void DeleteGlow() => throw new InvalidOperationException("Original CPowerUp never assigned its optional pGlow pointer in the supported constructor/AI paths.");
    public bool IncreaseHealth(int amount) => PlayerPowerUpResources.IncreaseHealth(RequirePlayer().Combat.State,
        RequireInventory().State, configuration.Collection, amount, Clock);
    public bool IncreaseWebbing(int amount) => RequireInventory().IncreaseWebbing(amount);
    public bool SetFireWebbing() => RequireInventory().SetFireWebbing();
    public bool SetArmor(bool active) => configuration.SetArmor(active);
    public void IncreasePickupRevision(int amount)
    { var inventory = RequireInventory().State; inventory.PickupRevision = unchecked(inventory.PickupRevision + amount); }
    public void PlaySound(ushort sound, int volume, int pitch) => configuration.PlaySound(sound, volume, pitch);
    public void PlaySpeech(int bank, int slot, int priority) => configuration.PlaySpeech(bank, slot, priority);
    private OpenSpidey.Game.Player.GodotPlayer RequirePlayer() => registry.Player ?? throw new InvalidOperationException("Original pickup effects require the genuine live player owner.");
    private OpenSpidey.Game.Player.GodotPlayerInventory RequireInventory() => RequirePlayer().Inventory ?? throw new InvalidOperationException("Original pickup effects require the shared player inventory.");
    private void Synchronize()
    {
        Variables.Position = State.Position; Variables.Health = State.Health; Variables.ItemFlags = State.ItemFlags; Variables.CollisionRadius = State.Radius;
        var rotation = FixedJointTransform.FromJoint(new(0, State.Yaw, 0, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(rotation); transform.Origin = PsxCoordinates.FixedWorldPosition(State.Position); Transform = transform;
        if (modelNode is not null)
        {
            modelNode.Visible = (State.ItemFlags & 0x8001) == 0 && Lifecycle.Phase != ActorLifePhase.Suspended;
            bool tinted = (State.ItemFlags & 0x480) == 0x400;
            if (lastTint != (tinted, State.Tint))
            {
                modelMesh!.ClearSurfaces();
                foreach (var surface in surfaces)
                {
                    Color[] colors = tinted ? surface.Colors.Select(color => new Color(Channel(color.R, (byte)State.Tint),
                        Channel(color.G, (byte)(State.Tint >> 8)), Channel(color.B, (byte)(State.Tint >> 16)), color.A)).ToArray() : surface.Colors;
                    surface.Arrays[(int)Mesh.ArrayType.Color] = colors;
                    modelMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surface.Arrays);
                    modelMesh.SurfaceSetMaterial(modelMesh.GetSurfaceCount() - 1, surface.Material);
                }
                lastTint = (tinted, State.Tint);
            }
            float? translucency = SourceItemTranslucency.Alpha(State.ItemFlags);
            foreach (StandardMaterial3D material in materials) SourceItemTranslucency.Apply(material, originalMaterials[material], translucency);
        }
        if ((State.BodyFlags & 0x40) != 0) Lifecycle.Kill();
    }
    private float Channel(float original, byte tint)
    {
        int source = Math.Clamp((int)Math.Round(original * 255f), 0, 255);
        float gamma = (float)Math.Pow(tint / 255f, LevelId == 0x1501 ? .4f : .7f);
        int channel = (int)((double)Math.Min(1f, gamma) * 255.0);
        return ((source * channel) >> 8) / 255f;
    }
    private void OnSourceRemoved(GodotScriptActor actor)
    {
        if (ReferenceEquals(actor, this)) DestroyOwnedState();
    }
    private void OnDistanceMeasured(GodotScriptActor actor, uint distance)
    { if (ReferenceEquals(actor, this)) State.PlayerDistance = distance; }
    private void OnSourceSuspended(GodotScriptActor actor)
    {
        if (!ReferenceEquals(actor, this)) return;
        PowerUpBehavior.DeleteStuff(State, this); State.BodyFlags |= 1; Synchronize();
    }
    private void OnSourceResumed(GodotScriptActor actor)
    {
        if (!ReferenceEquals(actor, this)) return;
        State.BodyFlags &= 0xFFFE; Synchronize();
    }
    private void DestroyOwnedState()
    {
        if (disposed) return;
        PowerUpBehavior.DeleteStuff(State, this); disposed = true;
        if (modelNode is not null) modelNode.Mesh = null;
        modelMesh?.Dispose(); modelMesh = null;
        foreach (var surface in surfaces) surface.Arrays.Dispose(); surfaces.Clear();
        materialLibrary?.Dispose(); materialLibrary = null;
        materials.Clear(); originalMaterials.Clear();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        registry.ActorRemoved -= OnSourceRemoved;
        registry.ActorSuspended -= OnSourceSuspended;
        registry.ActorResumed -= OnSourceResumed;
        registry.ActorDistanceMeasured -= OnDistanceMeasured;
        DestroyOwnedState();
    }
}
