using Godot;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.UI;

/// <summary>Actual source message lists and original font glyphs; checkpoint SMessages retain their non-rendered PC lifetime.</summary>
public partial class GodotMissionMessages : CanvasLayer, IMissionCheckpointMessages, IMissionTextMessageHost
{
    public MissionMessages Messages { get; private set; } = null!;
    public GodotBitmapText Text { get; private set; } = null!;
    public ushort Sort { get; set; }

    public static GodotMissionMessages Create(OriginalMissionMessageData data, OriginalBitmapFont originalFont)
    {
        var owner = new GodotMissionMessages { Name = "OriginalMissionMessages", Messages = new(data) };
        try
        {
            owner.Text = GodotBitmapText.Create(originalFont);
            owner.AddChild(owner.Text);
            owner.Messages.Changed += owner.Rebuild;
            return owner;
        }
        catch { owner.Free(); throw; }
    }

    public void DeleteAllMessages() => Messages.DeleteAllMessages();
    public void ShowCheckpointMessage() => Messages.ShowCheckpointMessage();
    public void ClearSimpleMessages() => Messages.ClearSimpleMessages();
    public void UpdateSourceTimer(int timer) => Messages.Update(timer);

    public SimpleMissionMessage ShowSimpleMessage(ReadOnlyMemory<byte> text, uint x, uint y, uint duration)
    {
        // Mess_SimpleMessage sets scale200 before measuring the same actual active font.
        Text.State.Scale = 1600;
        return Messages.AddSimpleMessage(text, x, y, duration, Text.Layout.Width(text.Span));
    }

    public void Rebuild()
    {
        Text.Clear();
        Text.State.HorizontalAlignment = BitmapHorizontalAlignment.Left;
        BitmapTextColour colour = MissionMessagePresentation.Colour;
        Text.State.Red = colour.Red; Text.State.Green = colour.Green; Text.State.Blue = colour.Blue;
        float depth = MissionMessagePresentation.Depth(Sort);
        foreach (SimpleMissionMessage message in Messages.SourceSimpleMessages)
        {
            Text.State.Scale = MissionMessagePresentation.FontScale(message);
            // PC Mess_DrawText receives the fade operand but never uses it for its actual font draw.
            Text.AddText(unchecked((int)message.X), unchecked((int)message.Y), message.Text.Span,
                HudHorizontalAnchor.Center, HudVerticalAnchor.Top, depth: depth);
        }
    }
}
