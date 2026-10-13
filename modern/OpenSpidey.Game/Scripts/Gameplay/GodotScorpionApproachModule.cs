using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Gameplay;

public sealed class GodotScorpionApproachConfiguration
{
    public required PkrArchive Archive { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required MissionCountdown Countdown { get; init; }
    public required ScorpionApproachState State { get; init; }
    public required PlayerModuleHook PlayerHook { get; init; }
    public required GodotSpeechPlayer Speech { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> ScreenOffsetY { get; init; }
    public required Func<bool> AlternateStory { get; init; }
    public required Action<ushort> SendPulse { get; init; }
}

internal readonly record struct ScorpionProgressDraw(int Texture, ScorpionProgressPiece Piece, float Depth);

/// <summary>Genuine l2a1lsc module0/1 over shared panel/speech state, authored nodes and lazily loaded original world textures.</summary>
public partial class GodotScorpionApproachModule : Node, IScorpionApproachCountdownHost, IScorpionApproachProgressHost
{
    private GodotScorpionApproachConfiguration configuration = null!;
    private readonly ImageTexture?[] textures = new ImageTexture?[5];
    private readonly DecodedTexture?[] sourceTextures = new DecodedTexture?[5];
    private readonly List<ScorpionProgressDraw> draws = [];
    private SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor>? modules;
    private PlayerTerrainController? boundController;
    private bool loaded, disposed;
    internal bool ResourcesDisposed => disposed;
    internal int LiveTextures => textures.Count(texture => texture is not null);
    internal int TextureLookups { get; private set; }
    internal int SourceHookVisits { get; private set; }
    internal int MonitorCalls { get; private set; }
    internal int DisplayCalls { get; private set; }
    internal int ClearCalls { get; private set; }
    internal int AllocatedPolys { get; private set; }
    internal int SubmittedPrimitives { get; private set; }
    internal bool PlayerHookBound => boundController is not null;
    internal IReadOnlyList<ScorpionProgressDraw> Draws => draws;
    internal ImageTexture? SourceTexture(int index) => textures[index];
    internal DecodedTexture? OriginalTexture(int index) => sourceTextures[index];

    public static GodotScorpionApproachModule Create(GodotScorpionApproachConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(configuration.Archive);
        ArgumentNullException.ThrowIfNull(configuration.Repository); ArgumentNullException.ThrowIfNull(configuration.Registry);
        ArgumentNullException.ThrowIfNull(configuration.Countdown); ArgumentNullException.ThrowIfNull(configuration.State);
        ArgumentNullException.ThrowIfNull(configuration.PlayerHook); ArgumentNullException.ThrowIfNull(configuration.Speech);
        ArgumentNullException.ThrowIfNull(configuration.Clock); ArgumentNullException.ThrowIfNull(configuration.ScreenOffsetY);
        ArgumentNullException.ThrowIfNull(configuration.AlternateStory); ArgumentNullException.ThrowIfNull(configuration.SendPulse);
        return new() { Name = "ScorpionApproachModule", configuration = configuration };
    }

    /// <summary>Registration is pre-player; actual189 load alone publishes functions and installs the process player hook.</summary>
    public void Register(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> registry)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(registry);
        if (modules is not null) throw new InvalidOperationException("The source Scorpion module is already registered.");
        registry.Register("l2a1lsc", module =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            module.SetClear(ClearSourceModule);
            module.SetParameterlessUserFunction(0, Monitor);
            module.SetParameterlessUserFunction(1, Display);
            configuration.PlayerHook.Set("l2a1lsc", 1);
            loaded = true;
        });
        modules = registry;
    }

    public void BindPlayer(GodotPlayer player)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(player);
        if (modules is null) throw new InvalidOperationException("The source Scorpion module has not been registered.");
        if (boundController is not null)
        {
            if (ReferenceEquals(boundController, player.Controller)) return;
            throw new InvalidOperationException("A level-script source hook cannot transfer between live player owners.");
        }
        boundController = player.Controller;
        boundController.AfterPlayerAi += RunPlayerHook;
    }

    private void RunPlayerHook()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SourceHookVisits++;
        configuration.PlayerHook.Run(modules!.CallParameterlessUserFunction);
    }
    public void Monitor()
    {
        ObjectDisposedException.ThrowIf(disposed, this); MonitorCalls++;
        ScorpionApproachCountdown.Monitor(configuration.Countdown, configuration.State, this);
    }
    public void BeginPresentationFrame()
    {
        ObjectDisposedException.ThrowIf(disposed, this); draws.Clear(); AllocatedPolys = SubmittedPrimitives = 0;
    }
    public void Display()
    {
        ObjectDisposedException.ThrowIf(disposed, this); DisplayCalls++;
        ScorpionApproachProgress.Display(configuration.Countdown, configuration.State, this);
    }
    /// <summary>Only the actual HUD drawing callback submits these captured source quads; this owner has no independent update/render loop.</summary>
    public void DrawOn(CanvasItem canvas, HudViewportTransform viewport)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(canvas);
        foreach (ScorpionProgressDraw draw in draws.OrderByDescending(draw => draw.Depth))
        {
            ScreenRectangle rectangle = viewport.Project(draw.Piece.Quad, HudHorizontalAnchor.Center);
            Vector2[] corners = [new(rectangle.X, rectangle.Y), new(rectangle.X + rectangle.Width, rectangle.Y),
                new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), new(rectangle.X, rectangle.Y + rectangle.Height)];
            Vector2[] uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
            uint packed = draw.Piece.Colour;
            DecodedTexture source = sourceTextures[draw.Texture] ?? throw new InvalidOperationException("A captured source HUD draw lost its real texture.");
            byte alpha = (source.Flags & 0x1000) != 0 ? (byte)128 : (byte)(packed >> 24);
            // Original PCGfx_DrawQPoly2D doubles default RGB; DXPOLY mode1 is SRCALPHA/INVSRCALPHA.
            Color colour = new(Math.Min(255, (byte)(packed >> 16) * 2) / 255f,
                Math.Min(255, (byte)(packed >> 8) * 2) / 255f, Math.Min(255, (byte)packed * 2) / 255f, alpha / 255f);
            Color[] colours = [colour, colour, colour, colour];
            canvas.DrawPolygon(corners, colours, uvs, textures[draw.Texture]);
            SubmittedPrimitives++;
        }
    }

    public int Difficulty => configuration.Registry.Difficulty;
    public uint Clock => configuration.Clock();
    public int NodeCount => configuration.Registry.Triggers.Nodes.Count;
    public ushort NodeKind(int node) => configuration.Registry.Triggers.Nodes[node].TypeCode;
    public FixedVector3 NodePosition(int node)
    {
        TriggerPosition source = TriggerPlacement.Read(configuration.Registry.Triggers.Nodes[node]).Position;
        return new FixedVector3(source.X, source.Y, source.Z) << 12;
    }
    public void SendPulse(int node) => configuration.SendPulse(checked((ushort)node));
    public void ReportMissingFailureNode() => throw new InvalidDataException("Original L2A1 Scorpion timeout pulse node was not found at(-419,-2730,-3826).");
    public int ScreenOffsetY => configuration.ScreenOffsetY();
    public bool AlternateStory => configuration.AlternateStory();
    public bool SpeechAllowed => configuration.Speech.Controller.Allowed;
    public bool SpeechPlaying => configuration.Speech.Controller.Busy;
    public int SpeechBank => configuration.Speech.Controller.Current?.Bank ?? 0;
    public int SpeechSlot => configuration.Speech.Controller.Current?.Slot ?? 0;
    public bool TexturePresent(int index) => textures[index] is not null;
    public void FindTexture(int index, string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (TexturePresent(index)) throw new InvalidOperationException("The original progress lookup is only for a null cached texture.");
        PsxAssetLibrary world = configuration.Repository.RequireLoaded("L2A1_G");
        byte[] manifest = configuration.Archive.Read("data/l2a1_g.tex");
        uint checksum = NamedTextureTable.FindFirst(manifest, name);
        DecodedTexture source = world.Textures.FirstOrDefault(texture => texture.Checksum == checksum)
            ?? throw new InvalidDataException($"Original first-match progress texture{name}/{checksum:X8} is absent from the loaded L2A1_G bank.");
        using Image image = Image.CreateFromData(source.Width, source.Height, false, Image.Format.Rgba8, source.RgbaPixels);
        ImageTexture uploaded = ImageTexture.CreateFromImage(image);
        sourceTextures[index] = source; textures[index] = uploaded; TextureLookups++;
    }
    public void PlaySpeech(int bank, int slot, int priority) => configuration.Speech.Play(bank, slot, priority);
    public ScorpionProgressPiece CreatePiece(int texture, int x, int y, int width, int height)
    { AllocatedPolys++; return ScorpionApproachProgress.CreatePanelPiece(x, y, width, height); }
    public void DrawPiece(int texture, ScorpionProgressPiece piece, float depth) => draws.Add(new(texture, piece, depth));

    private void ClearSourceModule()
    {
        ClearCalls++;
        ScorpionApproachCountdown.Clear(configuration.Countdown, Clock, ReleaseTexture);
        draws.Clear(); loaded = false;
    }
    private void ReleaseTexture(int index)
    { textures[index]?.Dispose(); textures[index] = null; sourceTextures[index] = null; }
    private void Release()
    {
        if (disposed) return;
        if (boundController is not null) boundController.AfterPlayerAi -= RunPlayerHook;
        boundController = null;
        try
        {
            if (loaded) modules!.Unload("l2a1lsc");
        }
        finally
        {
            disposed = true;
            for (int index = 0; index < textures.Length; index++) ReleaseTexture(index);
            draws.Clear();
        }
    }
    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }
}
