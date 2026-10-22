using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Gameplay;

public sealed class GodotSubwayLevelConfiguration
{
    public required PkrArchive Archive { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required MissionCountdown Countdown { get; init; }
    public required PlayerModuleHook PlayerHook { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> ScreenOffsetY { get; init; }
    public required Func<int> WideScreenValue { get; init; }
    public required Func<ushort> EndLevelNode { get; init; }
    public required Action<ushort> SendPulse { get; init; }
    public required Func<GodotPlayerBrightness?> PlayerBrightness { get; init; }
}

internal readonly record struct SubwayProgressDraw(int Texture, SubwayProgressPiece Piece, float Depth);

/// <summary>Genuine l5a5lsc module0/1: subway-car wiggle and progress bar over shared panel countdown.</summary>
public partial class GodotSubwayLevelModule : Node, ISubwayProgressHost, ISubwayCarWiggleHost
{
    private GodotSubwayLevelConfiguration configuration = null!;
    private readonly ImageTexture?[] textures = new ImageTexture?[4];
    private readonly DecodedTexture?[] sourceTextures = new DecodedTexture?[4];
    private readonly List<SubwayProgressDraw> draws = [];
    private readonly int[] springVelocities = new int[8];
    private readonly int[] springFlags = new int[8];
    private SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor>? modules;
    private GodotScriptActor[] environment = [];
    private GodotLizManActor[] baddies = [];
    private GodotPowerUpActor[] powerUps = [];
    private bool loaded, disposed;
    internal bool SourceModuleLoaded => loaded;
    internal int LiveTextures => textures.Count(texture => texture is not null);
    internal int TextureLookups { get; private set; }
    internal int WiggleCalls { get; private set; }
    internal int DisplayCalls { get; private set; }
    internal int ClearCalls { get; private set; }
    internal IReadOnlyList<SubwayProgressDraw> Draws => draws;

    public static GodotSubwayLevelModule Create(GodotSubwayLevelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.Archive);
        ArgumentNullException.ThrowIfNull(configuration.Repository);
        ArgumentNullException.ThrowIfNull(configuration.Registry);
        ArgumentNullException.ThrowIfNull(configuration.Countdown);
        ArgumentNullException.ThrowIfNull(configuration.PlayerHook);
        ArgumentNullException.ThrowIfNull(configuration.Clock);
        ArgumentNullException.ThrowIfNull(configuration.ScreenOffsetY);
        ArgumentNullException.ThrowIfNull(configuration.WideScreenValue);
        ArgumentNullException.ThrowIfNull(configuration.EndLevelNode);
        ArgumentNullException.ThrowIfNull(configuration.SendPulse);
        ArgumentNullException.ThrowIfNull(configuration.PlayerBrightness);
        return new() { Name = "SubwayLevelModule", configuration = configuration };
    }

    public void Register(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> registry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(registry);
        if (modules is not null) throw new InvalidOperationException("The source subway module is already registered.");
        registry.Register("l5a5lsc", module =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            module.SetClear(ClearSourceModule);
            module.SetParameterlessUserFunction(0, Wiggle);
            module.SetParameterlessUserFunction(1, Display);
            configuration.Countdown.DisplayEnabled = configuration.Countdown.Running = 0;
            configuration.Countdown.LastUpdateTick = configuration.Clock();
            Array.Clear(springVelocities);
            Array.Clear(springFlags);
            configuration.PlayerHook.Set("l5a5lsc", 1);
            loaded = true;
        });
        modules = registry;
    }

    public void BeginPresentationFrame()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        draws.Clear();
    }

    public void Wiggle()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        WiggleCalls++;
        CacheSceneActors();
        SubwayCarWiggle.Wiggle(this);
    }

    public void Display()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        DisplayCalls++;
        SubwayProgress.Display(configuration.Countdown, this);
    }

    public void DrawOn(CanvasItem canvas, HudViewportTransform viewport)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        foreach (SubwayProgressDraw draw in draws.OrderByDescending(draw => draw.Depth))
        {
            ScreenRectangle rectangle = viewport.Project(draw.Piece.Quad, HudHorizontalAnchor.Center);
            Vector2[] corners =
            [
                new(rectangle.X, rectangle.Y), new(rectangle.X + rectangle.Width, rectangle.Y),
                new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), new(rectangle.X, rectangle.Y + rectangle.Height)
            ];
            Vector2[] uvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
            uint packed = draw.Piece.Colour;
            DecodedTexture source = sourceTextures[draw.Texture] ?? throw new InvalidOperationException("A captured subway HUD draw lost its real texture.");
            byte alpha = (source.Flags & 0x1000) != 0 ? (byte)128 : (byte)(packed >> 24);
            Color colour = new(Math.Min(255, (byte)(packed >> 16) * 2) / 255f,
                Math.Min(255, (byte)(packed >> 8) * 2) / 255f, Math.Min(255, (byte)packed * 2) / 255f, alpha / 255f);
            Color[] colours = [colour, colour, colour, colour];
            canvas.DrawPolygon(corners, colours, uvs, textures[draw.Texture]);
        }
    }

    int ISubwayProgressHost.ScreenOffsetY => configuration.ScreenOffsetY();
    int ISubwayProgressHost.WideScreenValue => configuration.WideScreenValue();
    uint ISubwayProgressHost.Clock => configuration.Clock();
    ushort ISubwayProgressHost.EndLevelNode => configuration.EndLevelNode();
    bool ISubwayProgressHost.TexturePresent(int index) => textures[index] is not null;
    void ISubwayProgressHost.FindTexture(int index, string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (TexturePresent(index)) throw new InvalidOperationException("The original subway progress lookup is only for a null cached texture.");
        PsxAssetLibrary world = configuration.Repository.RequireLoaded("L5A5_G");
        byte[] manifest = configuration.Archive.Read("data/l5a5_g.tex");
        uint checksum = NamedTextureTable.FindFirst(manifest, name);
        DecodedTexture source = world.Textures.FirstOrDefault(texture => texture.Checksum == checksum)
            ?? throw new InvalidDataException($"Original first-match subway texture {name}/{checksum:X8} is absent from the loaded L5A5_G bank.");
        using Image image = Image.CreateFromData(source.Width, source.Height, false, Image.Format.Rgba8, source.RgbaPixels);
        ImageTexture uploaded = ImageTexture.CreateFromImage(image);
        sourceTextures[index] = source;
        textures[index] = uploaded;
        TextureLookups++;
    }
    void ISubwayProgressHost.SendPulse(ushort node) => configuration.SendPulse(node);
    SubwayProgressPiece ISubwayProgressHost.CreatePiece(int texture, int x, int y, int width, int height) =>
        SubwayProgress.CreatePanelPiece(x, y, width, height);
    void ISubwayProgressHost.DrawPiece(int texture, SubwayProgressPiece piece, float depth) =>
        draws.Add(new(texture, piece, depth));

    bool ISubwayCarWiggleHost.PlayerPresent => configuration.Registry.Player is not null;
    FixedVector3 ISubwayCarWiggleHost.PlayerPosition
    {
        get => configuration.Registry.Player?.Combat.State.Position
            ?? throw new InvalidOperationException("Subway wiggle requires the scene player.");
        set
        {
            GodotPlayer player = configuration.Registry.Player
                ?? throw new InvalidOperationException("Subway drowning relocation requires the scene player.");
            player.Combat.State.Position = value;
            player.Controller.ApplyCombatState();
        }
    }
    uint ISubwayCarWiggleHost.PlayerBrightness
    {
        get => configuration.PlayerBrightness()?.State.Current ?? 208;
        set
        {
            GodotPlayerBrightness? brightness = configuration.PlayerBrightness();
            if (brightness is null) return;
            brightness.State.Current = value;
            brightness.State.Adjust(unchecked((ushort)Math.Min(value, ushort.MaxValue)), 0);
        }
    }
    int ISubwayCarWiggleHost.Random(int range) => configuration.Registry.Random(range);
    int ISubwayCarWiggleHost.NodeCount => configuration.Registry.Triggers.Nodes.Count;
    ushort ISubwayCarWiggleHost.NodeKind(int node) => configuration.Registry.Triggers.Nodes[node].TypeCode;
    FixedVector3 ISubwayCarWiggleHost.NodePosition(int node)
    {
        TriggerPosition source = TriggerPlacement.Read(configuration.Registry.Triggers.Nodes[node]).Position;
        return new FixedVector3(source.X, source.Y, source.Z) << 12;
    }
    void ISubwayCarWiggleHost.SendPulse(int node) => configuration.SendPulse(checked((ushort)node));
    void ISubwayCarWiggleHost.ReportMissingDrowningNode() =>
        throw new InvalidDataException("No TRG_Drowning node");
    int ISubwayCarWiggleHost.EnvironmentCount => environment.Length;
    int ISubwayCarWiggleHost.ReadEnvironmentY(int index) => ReadEnvironmentPosition(index).Y;
    void ISubwayCarWiggleHost.WriteEnvironmentY(int index, int y)
    {
        FixedVector3 position = ReadEnvironmentPosition(index) with { Y = y };
        WriteEnvironmentPosition(index, position);
    }
    int ISubwayCarWiggleHost.EnvironmentSpringVelocity(int index) => springVelocities[index];
    void ISubwayCarWiggleHost.SetEnvironmentSpringVelocity(int index, int velocity) => springVelocities[index] = velocity;
    int ISubwayCarWiggleHost.EnvironmentSpringFlag(int index) => springFlags[index];
    void ISubwayCarWiggleHost.SetEnvironmentSpringFlag(int index, int flag) => springFlags[index] = flag;
    int ISubwayCarWiggleHost.BaddyCount => baddies.Length;
    short ISubwayCarWiggleHost.BaddyType(int index) => LizManState.Type;
    int ISubwayCarWiggleHost.BaddyAiState(int index) => baddies[index].State.State;
    void ISubwayCarWiggleHost.SetBaddyAiState(int index, int state, int substate)
    {
        baddies[index].State.State = state;
        baddies[index].State.Substate = substate;
    }
    int ISubwayCarWiggleHost.PowerUpCount => powerUps.Length;
    FixedVector3 ISubwayCarWiggleHost.PowerUpWorkingPosition(int index) => powerUps[index].State.WorkingPosition;
    void ISubwayCarWiggleHost.SetPowerUpWorkingPosition(int index, FixedVector3 position) =>
        powerUps[index].State.WorkingPosition = position;

    private bool TexturePresent(int index) => textures[index] is not null;

    private void CacheSceneActors()
    {
        environment = configuration.Registry.SourceActors(ActorUpdateGroup.Environment).ToArray();
        baddies = configuration.Registry.SourceActors(ActorUpdateGroup.Enemy).OfType<GodotLizManActor>().ToArray();
        powerUps = configuration.Registry.SourceActors(ActorUpdateGroup.PowerUp).OfType<GodotPowerUpActor>().ToArray();
    }

    private FixedVector3 ReadEnvironmentPosition(int index) => environment[index] switch
    {
        GodotPlatformActor platform => platform.Motion.Position,
        GodotScriptActor actor => actor.Variables.Position,
        _ => throw new InvalidOperationException("Subway wiggle requires a source enviro body.")
    };

    private void WriteEnvironmentPosition(int index, FixedVector3 position)
    {
        switch (environment[index])
        {
            case GodotPlatformActor platform:
                platform.Motion.Position = position;
                platform.Variables.Position = position;
                break;
            case GodotScriptActor actor:
                actor.Variables.Position = position;
                break;
            default:
                throw new InvalidOperationException("Subway wiggle requires a source enviro body.");
        }
    }

    private void ClearSourceModule()
    {
        ClearCalls++;
        SubwayProgress.Clear(configuration.Countdown, configuration.Clock(), ReleaseTexture);
        draws.Clear();
        Array.Clear(springVelocities);
        Array.Clear(springFlags);
        loaded = false;
    }

    private void ReleaseTexture(int index)
    {
        textures[index]?.Dispose();
        textures[index] = null;
        sourceTextures[index] = null;
    }

    private void Release()
    {
        if (disposed) return;
        try
        {
            if (loaded) modules!.Unload("l5a5lsc");
        }
        finally
        {
            disposed = true;
            for (int index = 0; index < textures.Length; index++) ReleaseTexture(index);
            draws.Clear();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
