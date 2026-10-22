using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.Gameplay;

public sealed class GodotTrainingConfiguration
{
    public required PkrArchive Archive { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required MissionCountdown Countdown { get; init; }
    public required TrainingResults Results { get; init; }
    public required PlayerModuleHook PlayerHook { get; init; }
    public required CampaignProgress Progress { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> ScreenOffsetY { get; init; }
    public required Func<int> KillCount { get; init; }
    public required Action<ushort> SendPulse { get; init; }
}

/// <summary>Genuine training module (0x4DDC60): post-AI monitor slot0, SpideyDied slot1 and the shared panel countdown.</summary>
public partial class GodotTrainingModule : Node, ITrainingMonitorHost
{
    private readonly record struct TextDraw(int X, int Y, byte[] Text, int Scale, byte Justification);
    private GodotTrainingConfiguration configuration = null!;
    private GodotBitmapText font = null!;
    private readonly List<TextDraw> draws = [];
    private int textScale = 256;
    private byte textJustification = 1;
    private bool loaded, disposed;
    public TrainingState State { get; } = new();
    internal bool SourceModuleLoaded => loaded;
    internal int MonitorCalls { get; private set; }
    internal int TextDraws => draws.Count;

    public static GodotTrainingModule Create(GodotTrainingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(configuration.Archive);
        ArgumentNullException.ThrowIfNull(configuration.Registry); ArgumentNullException.ThrowIfNull(configuration.Countdown);
        ArgumentNullException.ThrowIfNull(configuration.Results); ArgumentNullException.ThrowIfNull(configuration.PlayerHook);
        ArgumentNullException.ThrowIfNull(configuration.Progress); ArgumentNullException.ThrowIfNull(configuration.Clock);
        ArgumentNullException.ThrowIfNull(configuration.ScreenOffsetY); ArgumentNullException.ThrowIfNull(configuration.KillCount);
        ArgumentNullException.ThrowIfNull(configuration.SendPulse);
        var result = new GodotTrainingModule { Name = "TrainingModule", configuration = configuration };
        result.font = GodotBitmapText.Create(OriginalBitmapFontReader.Read(configuration.Archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt"));
        // Panel_Display's in-game Mess colour; the HUD font uses the same preset.
        result.font.State.Red = result.font.State.Green = result.font.State.Blue = 128;
        result.font.Visible = false; result.AddChild(result.font);
        return result;
    }

    public override void _Ready() => font.SetProcess(false);

    public void Register(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> registry)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(registry);
        registry.Register(TrainingMonitor.ModuleName, module =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            module.SetClear(ClearSourceModule);
            module.SetParameterlessUserFunction(0, Monitor);
            module.SetParameterlessUserFunction(1, () => TrainingMonitor.SpideyDied(State));
            TrainingMonitor.Initialize(State, configuration.Countdown, configuration.PlayerHook, configuration.Registry.Environment.LevelId);
            loaded = true;
        });
    }

    public void Monitor()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        MonitorCalls++; draws.Clear();
        TrainingMonitor.Monitor(State, configuration.Results, configuration.Countdown, this);
    }

    /// <summary>Submits the text the last source monitor call drew; the module has no separate render loop.</summary>
    public void DrawOn(CanvasItem canvas, HudViewportTransform viewport)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(canvas);
        foreach (TextDraw draw in draws)
        {
            font.State.Scale = draw.Scale * 8;
            font.State.HorizontalAlignment = draw.Justification switch { 0 => BitmapHorizontalAlignment.Centre,
                1 => BitmapHorizontalAlignment.Left, 2 => BitmapHorizontalAlignment.Right, _ => font.State.HorizontalAlignment };
            font.ShowText(draw.X, draw.Y, draw.Text, HudHorizontalAnchor.Center);
            font.DrawOn(canvas, viewport);
        }
    }

    private GodotPlayer? Player => configuration.Registry.Player;
    private GodotPlayer RequirePlayer() => Player ?? throw new InvalidOperationException("Training reads a missing current player.");
    public bool PlayerPresent => Player is not null;
    public short PlayerHealth => RequirePlayer().Combat.State.Health;
    public int PlayerFieldE18 => RequirePlayer().Combat.State.FieldE18;
    public int PlayerField568 => RequirePlayer().Combat.State.SwitchCounter;
    public int PlayerField5D0 => (RequirePlayer().Inventory ??
        throw new InvalidOperationException("Training reads the player's pickup counter before its inventory exists.")).State.PickupRevision;
    public uint Clock => configuration.Clock();
    public int KillCount => configuration.KillCount();
    public uint CostumeUnlockFlags
    {
        get => configuration.Progress.CostumeUnlockFlags;
        set => configuration.Progress.CostumeUnlockFlags = value;
    }
    public int ScreenOffsetY => configuration.ScreenOffsetY();
    public int NodeCount => configuration.Registry.Triggers.Nodes.Count;
    public ushort NodeKind(int node) => configuration.Registry.Triggers.Nodes[node].TypeCode;
    public FixedVector3 NodePosition(int node)
    {
        TriggerPosition source = TriggerPlacement.Read(configuration.Registry.Triggers.Nodes[node]).Position;
        return new FixedVector3(source.X, source.Y, source.Z) << 12;
    }
    public void SendPulse(int node) => configuration.SendPulse(checked((ushort)node));
    public void SetTextScale(int scale) => textScale = scale;
    public void SetTextJustification(byte justification) => textJustification = justification;
    public void DrawText(int x, int y, ReadOnlyMemory<byte> text, int argument, uint depth)
        => draws.Add(new(x, y, text.ToArray(), textScale, textJustification));
    public void EndTraining() => throw new NotSupportedException(
        "PShell_EndTrainingInit (0x47B720) needs the training record box, name entry and score tables of the shell port.");

    private void ClearSourceModule()
    {
        TrainingMonitor.Clear(configuration.Countdown, Clock);
        draws.Clear(); loaded = false;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true; draws.Clear();
    }
}
