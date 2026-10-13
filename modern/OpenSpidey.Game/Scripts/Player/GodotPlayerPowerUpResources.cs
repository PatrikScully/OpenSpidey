using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Real player pickup resources and costarm HUD animation. Godot's hardware renderer follows the source high-graphics branch.</summary>
public sealed class GodotPlayerPowerUpResources : IPlayerArmorResourceHost, IPlayerArmorTextureHost
{
    private readonly GodotPlayer player;
    private readonly Func<int> difficulty;
    private readonly SpriteAnimation armorIcon;
    private readonly SpriteAnimation? suitIcon;
    private SpriteAnimation? selectedIcon;
    private readonly PlayerArmorTextureState textures;
    public PlayerPowerUpCollectionState Collection { get; }
    public IReadOnlyList<DecodedTexture> ArmorIconTextures { get; }
    public SpriteAnimation? ArmorHealthIcon => player.Combat.State.ArmorAnimationVariant != 0 ? selectedIcon : null;
    public SpriteAnimationFrame? HealthIcon => (ArmorHealthIcon ?? suitIcon)?.Frames[0];
    public IReadOnlyList<DecodedTexture> HealthIconTextures { get; }
    public int Difficulty => difficulty();

    public GodotPlayerPowerUpResources(GodotPlayer player, PkrArchive archive, PlayerPowerUpCollectionState collection, Func<int> difficulty)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(collection); ArgumentNullException.ThrowIfNull(difficulty);
        if (!ReferenceEquals(collection, player.PowerUpCollection)) throw new ArgumentException("Pickup resources must use this actual player's fields370/5E0.", nameof(collection));
        this.player = player; this.difficulty = difficulty; Collection = collection;
        byte[] source = archive.Read("data/costarm.psx");
        armorIcon = SpriteAnimationReader.Read(source).Single(animation => animation.Name.Equals("costarm", StringComparison.OrdinalIgnoreCase));
        ArmorIconTextures = PsxTextureReader.Read(source);
        foreach (SpriteAnimationFrame frame in armorIcon.Frames)
            if (!ArmorIconTextures.Any(texture => texture.Checksum == frame.TextureChecksum))
                throw new InvalidDataException("The original costarm HUD animation lost a source texture.");
        string? alternative = player.Combat.CurrentSuit switch
        {
            2 => "cost99", 3 or 9 => "costblk", 4 => "costcapt", 6 => "costbag", 7 => "costscar", 10 => "costpete", _ => null
        };
        var iconTextures = new List<DecodedTexture>(ArmorIconTextures);
        if (alternative is not null)
        {
            byte[] suitBytes = archive.Read($"data/{alternative}.psx");
            suitIcon = SpriteAnimationReader.Read(suitBytes).Single(animation => animation.Name.Equals(alternative, StringComparison.OrdinalIgnoreCase));
            iconTextures.AddRange(PsxTextureReader.Read(suitBytes));
        }
        HealthIconTextures = iconTextures.GroupBy(texture => texture.Checksum).Select(group => group.First()).ToArray();
        if (suitIcon is not null && suitIcon.Frames.Any(frame => !HealthIconTextures.Any(texture => texture.Checksum == frame.TextureChecksum)))
            throw new InvalidDataException("The original alternative health icon lost a source texture.");
        // Spidey_SwapSuitTextures remaps the original software renderer's packed texture IDs. Hardware mode does not execute it.
        textures = new() { LowGraphics = false, Suit = player.Combat.CurrentSuit };
    }
    public bool IncreaseHealth(int amount, uint clock) => PlayerPowerUpResources.IncreaseHealth(player.Combat.State,
        (player.Inventory ?? throw new InvalidOperationException("Pickup resources require the shared live inventory.")).State, Collection, amount, clock);
    public bool SetArmor(bool active) => PlayerPowerUpResources.SetArmor(player.Combat.State, active, this);
    /// <summary>Install the original constructor's independently proven5E9,57C, shared armor global and icon-pointer results.</summary>
    public void ApplyConstructorState(PlayerCarriedConstructorState constructor)
    {
        if (constructor.SwapArmourTextures) throw new InvalidOperationException("Native hardware construction cannot request the original software texture-ID swap.");
        ClearArmorHealthIcon();
        if (constructor.LoadArmourAnimation) LoadArmorHealthIcon();
        PlayerCombatState state = player.Combat.State;
        state.ArmorAnimationVariant = constructor.LoadArmourAnimation ? 1 : 0;
        state.ArmorActive = constructor.ArmourAnimationVariant != 0; // Existing carried policy name refers to source5E9, not the animation pointer.
        state.SuitArmorEnabled = constructor.ArmourOn;
        state.ArmorTexturesActive = constructor.ArmourTexturesActive != 0;
        state.ArmorAmount = constructor.ArmourAmount;
        textures.Processing = constructor.ArmourTextureProcessing != 0;
    }
    public void ClearArmorHealthIcon() => selectedIcon = null;
    public void LoadArmorHealthIcon() => selectedIcon = armorIcon;
    public void ProcessArmorTextures(bool active) => PlayerPowerUpResources.ProcessArmorTextures(textures, active, this);
    public void SwapSuitTextures(int sourceSuit, int targetSuit) =>
        throw new InvalidOperationException("The source software texture-ID swap cannot be selected by the native hardware renderer.");
}
