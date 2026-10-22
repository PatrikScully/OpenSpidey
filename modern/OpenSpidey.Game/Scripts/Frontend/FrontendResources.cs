using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Frontend;

namespace OpenSpidey.Game.Frontend;

/// <summary>PShell_Initialise 0x48D790 loads: shell/icons/vmu/control PSX, the three menu fonts and the menu SFX group.</summary>
public sealed class FrontendResources : IDisposable
{
    /// <summary>PShell_DrawHighlight 0x47A590 title bar texture.</summary>
    public const uint TitleBarTexture = 0xE90B5F6E;
    private static readonly string[] ShellPsx = ["data/shell.psx", "data/Icons.PSX", "data/vmu.psx", "data/control.PSX"];
    private readonly Dictionary<uint, ImageTexture> textures = [];
    private readonly Dictionary<string, SpriteAnimation> animations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageTexture> bitmaps = new(StringComparer.OrdinalIgnoreCase);
    public PkrArchive Archive { get; }
    public byte[] Executable { get; }
    public OriginalFrontendData Data { get; }
    public IReadOnlyList<string> BadWords { get; }
    public IReadOnlyList<string> GoodWords { get; }
    public OriginalBitmapFont NormalFont { get; }
    public OriginalBitmapFont SmallFont { get; }
    public OriginalBitmapFont BigFont { get; }
    /// <summary>gAnimTable[15]: nine menubox border frames from data/shell.psx.</summary>
    public IReadOnlyList<SpriteAnimationFrame> MenuBoxFrames { get; }
    public IReadOnlyList<OriginalMenuBoxSlice> MenuBoxSlices => Data.MenuBoxSlices;

    private FrontendResources(string directory)
    {
        Archive = new(System.IO.Path.Combine(directory, "data.pkr"));
        Executable = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "SpideyPC.exe"));
        Data = OriginalFrontendData.Read(Executable);
        (BadWords, GoodWords) = OriginalNameEntry.ReadWordLists(Executable);
        NormalFont = Font("sp_fnt00.fnt");
        SmallFont = Font("sp_fnt02.fnt");
        BigFont = Font("font_big.fnt");
        SpriteAnimation? menuBox = null;
        foreach (string path in ShellPsx)
        {
            byte[] bytes = Archive.Read(path);
            foreach (SpriteAnimation animation in SpriteAnimationReader.Read(bytes))
            {
                animations.TryAdd(animation.Name, animation);
                if (animation.Name.Equals("menubox", StringComparison.OrdinalIgnoreCase)) menuBox = animation;
            }
            foreach (DecodedTexture texture in PsxTextureReader.Read(bytes))
            {
                if (textures.ContainsKey(texture.Checksum)) continue;
                using Image image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
                textures.Add(texture.Checksum, ImageTexture.CreateFromImage(image));
            }
        }
        if (menuBox is null)
        {
            byte[] bits = Archive.Read("data/bits.psx");
            foreach (DecodedTexture texture in PsxTextureReader.Read(bits))
            {
                if (textures.ContainsKey(texture.Checksum)) continue;
                using Image image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
                textures.Add(texture.Checksum, ImageTexture.CreateFromImage(image));
            }
            foreach (SpriteAnimation animation in SpriteAnimationReader.Read(bits))
                if (animation.Name.Equals("menubox", StringComparison.OrdinalIgnoreCase)) menuBox = animation;
        }
        if (menuBox is null || menuBox.Frames.Count < 9)
            throw new InvalidDataException("data/bits.psx must expose nine menubox border frames.");
        MenuBoxFrames = menuBox.Frames;
    }

    public static FrontendResources Load()
    {
        string directory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS")
            ?? throw new InvalidOperationException("Supply your original game folder with --assets.");
        return new(directory);
    }

    private OriginalBitmapFont Font(string name) => OriginalBitmapFontReader.Read(Archive.Read("data/" + name), name);

    public ImageTexture Texture(uint checksum) => textures.TryGetValue(checksum, out ImageTexture? texture)
        ? texture : throw new InvalidDataException($"Shell texture 0x{checksum:X8} is not in the shell PSX files.");

    /// <summary>Spool_FindAnim over the shell PSX files.</summary>
    public SpriteAnimation Animation(string name) => animations.TryGetValue(name, out SpriteAnimation? animation)
        ? animation : throw new InvalidDataException($"Shell animation {name} is not in the shell PSX files.");

    public ImageTexture Frame(string animation, int frame) => Texture(Animation(animation).Frames[frame].TextureChecksum);

    /// <summary>Sprite2 BMP loader for 512x240 shell screens.</summary>
    public ImageTexture Bitmap(string name)
    {
        if (bitmaps.TryGetValue(name, out ImageTexture? cached)) return cached;
        using var image = new Image();
        Error error = image.LoadBmpFromBuffer(Archive.Read("data/" + name));
        if (error != Error.Ok) throw new InvalidDataException($"Could not decode original {name}: {error}.");
        var texture = ImageTexture.CreateFromImage(image);
        bitmaps.Add(name, texture);
        return texture;
    }

    public void Dispose()
    {
        foreach (ImageTexture texture in textures.Values) texture.Dispose();
        foreach (ImageTexture texture in bitmaps.Values) texture.Dispose();
        textures.Clear();
        bitmaps.Clear();
        Archive.Dispose();
    }
}
