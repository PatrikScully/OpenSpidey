using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Game.UI;

internal sealed record GodotBossHealthBarTexture(DecodedTexture Source, ImageTexture Native);

/// <summary>First named textures from the already loaded encounter bank, plus the genuine bits Sp3 frame. Owns only native uploads.</summary>
internal sealed class GodotScorpionHealthBarResources : IDisposable
{
    private readonly Dictionary<string, GodotBossHealthBarTexture> portraits = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GodotBossHealthBarTexture> uploads = [];
    private readonly PsxLibraryRepository libraries;
    private bool hasPresenter;
    public PsxAssetLibrary EncounterBank { get; }
    public PsxAssetLibrary BitsBank { get; }
    public GodotBossHealthBarTexture PipTexture { get; private set; } = null!;
    public SpriteAnimationFrame PipFrame { get; private set; }
    public bool IsDisposed { get; private set; }
    internal IReadOnlyList<GodotBossHealthBarTexture> UploadedTextures => uploads;

    private GodotScorpionHealthBarResources(PsxLibraryRepository libraries)
    {
        this.libraries = libraries;
        EncounterBank = libraries.RequireLoaded("L2A2_G");
        BitsBank = libraries.RequireLoaded("bits");
    }

    public static GodotScorpionHealthBarResources Create(PkrArchive archive, PsxLibraryRepository libraries)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(libraries);
        var resources = new GodotScorpionHealthBarResources(libraries);
        try
        {
            byte[] manifest = archive.Read("data/l2a2_g.tex");
            foreach (string name in new[] { "scorpion", "boss", "jonah", "scorpion_wounded" })
            {
                uint checksum = NamedTextureTable.FindFirstSourceName(manifest, name);
                DecodedTexture source = resources.EncounterBank.Textures.First(texture => texture.Checksum == checksum);
                resources.portraits.Add(name, resources.Upload(source));
            }
            resources.PipFrame = SpriteAnimationReader.Read(archive.Read("data/bits.psx"))
                .Single(animation => animation.Name.Equals("Sp", StringComparison.OrdinalIgnoreCase)).Frames[3];
            DecodedTexture pip = resources.BitsBank.Textures.First(texture => texture.Checksum == resources.PipFrame.TextureChecksum);
            resources.PipTexture = resources.Upload(pip);
            libraries.LibraryUnloaded += resources.OnLibraryUnloaded;
            return resources;
        }
        catch { resources.Dispose(); throw; }
    }

    public GodotBossHealthBarTexture FindTexture(string name)
    {
        RequireActive();
        return portraits.TryGetValue(name, out GodotBossHealthBarTexture? texture) ? texture
            : throw new KeyNotFoundException($"The Scorpion panel has no original cached texture named {name}.");
    }

    internal void AttachPresenter()
    {
        RequireActive();
        if (hasPresenter) throw new InvalidOperationException("The native boss panel resources already have a presentation owner.");
        hasPresenter = true;
    }

    internal void RequireActive() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    private GodotBossHealthBarTexture Upload(DecodedTexture source)
    {
        using Image image = Image.CreateFromData(source.Width, source.Height, false, Image.Format.Rgba8, source.RgbaPixels);
        var texture = new GodotBossHealthBarTexture(source, ImageTexture.CreateFromImage(image));
        uploads.Add(texture);
        return texture;
    }

    private void OnLibraryUnloaded(PsxAssetLibrary bank)
    {
        if (ReferenceEquals(bank, EncounterBank) || ReferenceEquals(bank, BitsBank)) Dispose();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        libraries.LibraryUnloaded -= OnLibraryUnloaded;
        foreach (GodotBossHealthBarTexture texture in uploads) texture.Native.Dispose();
        portraits.Clear();
    }
}
