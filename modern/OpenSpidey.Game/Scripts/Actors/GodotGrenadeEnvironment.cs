using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

public interface IGodotGrenadeEnvironmentHost
{
    IProjectileBlastEnvironment? Head { get; }
}

/// <summary>Actual current G-region EnviroList, sharing canonical static and claimed switch identities.</summary>
public sealed class GodotGrenadeEnvironment : IGodotGrenadeEnvironmentHost
{
    private readonly Func<ImportedLevelWorld> currentWorld;
    private readonly PsxLibraryRepository repository;
    private readonly Func<string, ReadOnlyMemory<byte>> readOriginal;
    private readonly GodotPlayerMeleeSession session;
    private readonly IEnvironmentCrateHost crateHost;
    private readonly GodotEnvironmentVisibility? visibility;
    private readonly EnvironmentCrateDestruction crates = new();
    private ImportedLevelSource? source;
    private GodotGrenadeEnvironmentItem[] items = [];

    public GodotGrenadeEnvironment(Func<ImportedLevelWorld> currentWorld, PsxLibraryRepository repository,
        Func<string, ReadOnlyMemory<byte>> readOriginal, GodotPlayerMeleeSession session,
        IEnvironmentCrateHost crateHost, GodotEnvironmentVisibility? visibility = null)
    {
        this.currentWorld = currentWorld ?? throw new ArgumentNullException(nameof(currentWorld));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.readOriginal = readOriginal ?? throw new ArgumentNullException(nameof(readOriginal));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.crateHost = crateHost ?? throw new ArgumentNullException(nameof(crateHost));
        this.visibility = visibility;
    }

    public IProjectileBlastEnvironment? Head
    {
        get { SynchronizeSource(); return items.FirstOrDefault(); }
    }
    public IReadOnlyList<GodotGrenadeEnvironmentItem> SourceItems
    {
        get { SynchronizeSource(); return items; }
    }

    private void SynchronizeSource()
    {
        ImportedLevelSource? next = currentWorld().Sources.SingleOrDefault(source =>
            source.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase));
        if (next is null)
        {
            source = null;
            items = [];
            return;
        }
        PsxAssetLibrary bank = repository.RequireLoaded(next.AssetPath);
        if (!ReferenceEquals(bank.Scene, next.Scene))
            throw new InvalidDataException("ENV blast traversal requires the imported source's actual loaded bank generation.");
        if (ReferenceEquals(next, source)) return;
        ReadOnlySpan<byte> bytes = readOriginal(next.AssetPath).Span;
        if (bytes.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != next.Scene.Instances.Count ||
            bytes.Length < checked(12 + next.Scene.Instances.Count * 36))
            throw new InvalidDataException("ENV blast traversal requires the original bounded instance records.");
        var result = new GodotGrenadeEnvironmentItem[next.Scene.Instances.Count];
        for (int index = 0; index < result.Length; index++)
        {
            int offset = 12 + index * 36;
            ushort model = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 22)..]);
            if (model != next.Scene.Instances[index].ModelIndex)
                throw new InvalidDataException("ENV raw instance model differs from its decoded source owner.");
            var position = new FixedVector3(BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 4)..]),
                BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 8)..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 12)..]));
            var angles = new ActorAngles(BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 16)..]),
                BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 18)..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 20)..]));
            result[index] = new(this, next, bank, index, position, angles,
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]));
        }
        for (int index = 0; index + 1 < result.Length; index++) result[index].Next = result[index + 1];
        source = next;
        items = result;
    }

    internal bool Owns(GodotGrenadeEnvironmentItem item) => ReferenceEquals(item.Source, source) &&
        (uint)item.InstanceIndex < items.Length && ReferenceEquals(items[item.InstanceIndex], item) &&
        repository.Loaded.TryGetValue(item.Bank.AssetPath, out PsxAssetLibrary? bank) && ReferenceEquals(bank, item.Bank) &&
        currentWorld().Sources.Any(live => ReferenceEquals(live, item.Source));

    internal GodotSwitchEnvironmentItem? ClaimedItem(GodotGrenadeEnvironmentItem item) =>
        session.SourceSwitchEnvironments.SelectMany(environment => environment.Items).SingleOrDefault(native =>
            native.SourceAsset.Equals(item.Source.AssetPath, StringComparison.OrdinalIgnoreCase) &&
            native.SourceInstanceIndex == item.InstanceIndex);

    internal object Identity(GodotGrenadeEnvironmentItem item) =>
        ClaimedItem(item)?.State.Identity ?? session.ResolveStaticItem(item.Source, item.InstanceIndex);

    internal ushort? SharedItemFlags(GodotGrenadeEnvironmentItem item) =>
        visibility?.SourceItemFlags(item.Source, item.InstanceIndex);
    internal FixedVector3? SharedPosition(GodotGrenadeEnvironmentItem item) => visibility is not null && visibility.SourceItemPrepared(item.Source, item.InstanceIndex)
        ? visibility.SourceItemPosition(item.Source, item.InstanceIndex) : null;
    internal ActorAngles? SharedAngles(GodotGrenadeEnvironmentItem item) => visibility?.SourceItemAngles(item.Source, item.InstanceIndex);
    internal ushort? SharedModelFlags(GodotGrenadeEnvironmentItem item) => visibility?.SourceModelFlags(item.Source, item.InstanceIndex);

    internal void Hit(GodotGrenadeEnvironmentItem item, ushort damage)
    {
        // CBullet::GiveScaledDamageToEnviro passes a null face. Ordinary damage
        // cannot shatter arbitrary nearby terrain, including off-screen items.
        EnvironmentImpactAction action = EnvironmentImpactPolicy.Evaluate(true, item.ItemFlags,
            Owns(item), item.ModelFlags, false, damage);
        if (action == EnvironmentImpactAction.DestroyCrate) crates.Destroy(item, crateHost);
    }

    internal void Hide(GodotGrenadeEnvironmentItem item)
    {
        if (!Owns(item)) throw new InvalidOperationException("Cannot mutate an unloaded ENV item generation.");
        if (visibility is not null)
        {
            visibility.SetItemVisibility(item.Source, item.InstanceIndex, false);
            return;
        }
        if (ClaimedItem(item) is { } claimed)
        {
            claimed.State.ItemFlags |= 1;
            claimed.Synchronize();
            return;
        }
        ImportedLevelWorld world = currentWorld();
        string nodeName = System.IO.Path.GetFileNameWithoutExtension(item.Source.AssetPath);
        foreach (Node3D group in world.Geometry.GetChildren().OfType<Node3D>().Where(group =>
            group.Name.ToString().Equals(nodeName, StringComparison.OrdinalIgnoreCase)))
            foreach (ImportedSceneMesh mesh in group.GetChildren().OfType<ImportedSceneMesh>().Where(mesh => mesh.SourceInstanceIndex == item.InstanceIndex))
                mesh.Visible = false;
        foreach (Node3D group in world.Collision.GetChildren().OfType<Node3D>())
            foreach (CollisionObject3D collider in group.GetChildren().OfType<CollisionObject3D>())
            {
                bool matches = collider switch
                {
                    ImportedCollisionBody sourceBody => sourceBody.SourceAsset == item.Source.AssetPath && sourceBody.InstanceIndex == item.InstanceIndex,
                    ImportedTriggerSurface trigger => trigger.SourceAsset == item.Source.AssetPath && trigger.InstanceIndex == item.InstanceIndex,
                    _ => false
                };
                if (!matches) continue;
                collider.CollisionLayer = 0;
                if (collider is ImportedCollisionBody body) body.SourceFlags |= 1;
            }
    }
}

/// <summary>One original static CItem record; floating-point render coordinates never replace its source position.</summary>
public sealed class GodotGrenadeEnvironmentItem : IProjectileBlastEnvironment, IEnvironmentCrate
{
    private readonly GodotGrenadeEnvironment owner;
    private readonly FixedVector3 sourcePosition;
    private readonly ActorAngles sourceAngles;
    private ushort itemFlags;
    internal GodotGrenadeEnvironmentItem(GodotGrenadeEnvironment owner, ImportedLevelSource source,
        PsxAssetLibrary bank, int index, FixedVector3 position, ActorAngles angles, ushort flags)
    {
        this.owner = owner;
        Source = source;
        Bank = bank;
        InstanceIndex = index;
        sourcePosition = position;
        sourceAngles = angles;
        itemFlags = flags;
        // M3dInit_ParsePSX propagates these model flags into static item flags.
        if ((ModelFlags & 0x10) != 0) itemFlags |= 0x20;
        if ((ModelFlags & 0x20) != 0 || !PsxRenderPolicy.IsModelVisible(Bank.Scene.Models[ModelIndex])) itemFlags |= 0x1000;
    }
    public ImportedLevelSource Source { get; }
    public PsxAssetLibrary Bank { get; }
    public int InstanceIndex { get; }
    public int ModelIndex => Source.Scene.Instances[InstanceIndex].ModelIndex;
    public ActorAngles Angles => owner.SharedAngles(this) ?? owner.ClaimedItem(this)?.SourceAngles ?? sourceAngles;
    public ushort ModelFlags => owner.SharedModelFlags(this) ?? Bank.Scene.Models[ModelIndex].Flags;
    public ushort ItemFlags => owner.SharedItemFlags(this) ?? owner.ClaimedItem(this)?.State.ItemFlags ?? itemFlags;
    public FixedVector3 Position => owner.SharedPosition(this) ?? owner.ClaimedItem(this)?.State.Position ?? sourcePosition;
    public uint ModelChecksum => Bank.Scene.Models[ModelIndex].Checksum ??
        throw new InvalidDataException("ENV item has no original model/command-point checksum.");
    public object Identity => owner.Identity(this);
    public GodotGrenadeEnvironmentItem? Next { get; internal set; }
    public IProjectileBlastEnvironment? NextEnvironment => Next;
    public void HitEnvironment(ushort damage) => owner.Hit(this, damage);
    public void MarkShattered()
    {
        owner.Hide(this);
        itemFlags |= 1;
    }
}
