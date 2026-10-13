using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Nonanimated source families expose their actual selected bank, flag word and immediate native synchronization.</summary>
public interface IGodotSourceActorVisibility
{
    PsxAssetLibrary? SourceVisibilityBank { get; }
    ushort SourceVisibilityFlags { get; }
    void SetSourceVisibilityFlags(ushort flags);
}

/// <summary>Canonical141/192/209 and scene-camera visibility over actual ENV items and active original world lists.</summary>
public sealed class GodotVisibilityBoxHost : IVisibilityBoxHost
{
    private readonly GodotActorRegistry registry;
    private readonly GodotEnvironmentVisibility environment;
    private readonly PsxLibraryRepository repository;

    public GodotVisibilityBoxHost(GodotActorRegistry registry, GodotEnvironmentVisibility environment, PsxLibraryRepository repository)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public IEnumerable<IVisibilityBoxItem> SourceItems(SourceVisibilityList list)
    {
        if (list == SourceVisibilityList.StaticEnvironment) return environment.SourceItems;
        ActorUpdateGroup group = list switch
        {
            SourceVisibilityList.Baddy => ActorUpdateGroup.Enemy,
            SourceVisibilityList.EnvironmentBody => ActorUpdateGroup.Environment,
            SourceVisibilityList.PowerUp => ActorUpdateGroup.PowerUp,
            _ => throw new ArgumentOutOfRangeException(nameof(list))
        };
        return registry.SourceList(group).SourceOrder.OfType<GodotScriptActor>()
            .Where(registry.Owns).Select(actor => new ActorItem(actor, repository));
    }

    private sealed class ActorItem(GodotScriptActor actor, PsxLibraryRepository repository) : IVisibilityBoxItem
    {
        public FixedVector3 SourcePosition => actor.SourcePosition;
        public bool RegionUsable
        {
            get
            {
                if (actor is IGodotSourceActorVisibility source)
                    return source.SourceVisibilityBank is { } bank &&
                        repository.Loaded.TryGetValue(bank.AssetPath, out PsxAssetLibrary? current) && ReferenceEquals(current, bank);
                ImportedAnimatedModel model = AnimatedModel();
                return repository.Loaded.Values.Any(bank => ReferenceEquals(bank.Scene, model.SourceScene));
            }
        }
        public ushort ItemFlags => actor switch
        {
            GodotThugActor thug => thug.ItemFlags,
            GodotHostageActor hostage => hostage.ItemFlags,
            GodotBlackCatActor cat => cat.ItemFlags,
            IGodotSourceActorVisibility source => source.SourceVisibilityFlags,
            _ => throw Unported()
        };
        public void SetVisible(bool visible)
        {
            ushort flags = EnvironmentVisibilityPolicy.SetVisible(ItemFlags, visible);
            switch (actor)
            {
                case GodotThugActor thug: thug.ItemFlags = flags; break;
                case GodotHostageActor hostage: hostage.ItemFlags = flags; hostage.Variables.ItemFlags = flags; break;
                case GodotBlackCatActor cat: cat.ItemFlags = flags; break;
                case IGodotSourceActorVisibility source: source.SetSourceVisibilityFlags(flags); return;
                default: throw Unported();
            }
            AnimatedModel().Visible = (flags & 0x8001) == 0;
        }
        private ImportedAnimatedModel AnimatedModel() => actor switch
        {
            GodotThugActor thug => thug.Model,
            GodotHostageActor hostage => hostage.Model,
            GodotBlackCatActor cat => cat.Model,
            _ => throw Unported()
        };
        private NotSupportedException Unported() => new($"Source actor{actor.SourceActorType} has no canonical model-bank/visibility adapter.");
    }
}
