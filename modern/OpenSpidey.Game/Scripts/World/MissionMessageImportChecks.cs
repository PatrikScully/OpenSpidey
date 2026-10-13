using Godot;
using System.Buffers.Binary;
using System.Text;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Source checkpoint140 ownership, actual executable text and real bitmap simple-message display/lifetime.</summary>
internal static class MissionMessageImportChecks
{
    public static void Verify(Node3D owner, PkrArchive archive, ReadOnlyMemory<byte> originalExe)
    {
        var viewport = new SubViewport { Name = "OriginalMissionMessageProof", Size = new(640, 480),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            OriginalMissionMessageData data = OriginalMissionMessageData.Read(originalExe);
            Assert(data.CheckpointText.Span.SequenceEqual("Checkpoint"u8),
                "The genuine original checkpoint text comes through stored pointer54B88C, with its source language bytes.");
            OriginalBitmapFont font = OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt");
            GodotMissionMessages messages = GodotMissionMessages.Create(data, font); viewport.AddChild(messages);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
            RestartPoint[] points = triggers.Nodes.Where(node => node.TypeCode == 8).Select(RestartPoint.Read).ToArray();
            RestartPoint first = points[0], second = points.First(point => !point.Name.Equals(first.Name, StringComparison.OrdinalIgnoreCase));
            var checkpoint = new MissionCheckpointSession(triggers, new MissionCheckpointSave("initial"), messages);
            checkpoint.Select(first.Name); checkpoint.Select(second.Name);
            Assert(checkpoint.Current?.NodeId == second.NodeId && checkpoint.Save.RestartPointName == second.Name &&
                messages.Messages.SourceMessages.Count == 1 && messages.Messages.SourceMessages[0].Text.Span.SequenceEqual(data.CheckpointText.Span) &&
                messages.Messages.SourceMessages[0].Program is null && messages.Text.GlyphDrawCount == 0,
                "Actual changed source checkpoint selection composes message-clear/create/save-name ownership; PC SMessage has no invented rendered toast.");
            SimpleMissionMessage simple = messages.ShowSimpleMessage(data.CheckpointText, 256, 30, 125);
            Assert(simple.Scale == 200 && simple.Age == 0 && simple.FadeFactor == 0 && messages.Text.State.Scale == 1600 &&
                messages.Text.GlyphDrawCount == 20 && messages.Messages.LastSimpleMessageX == 256 &&
                messages.Messages.LastSimpleMessageWidth == messages.Text.Layout.Width(data.CheckpointText.Span),
                "Genuine simple messages use source scale200, font Q12 scale1600, actual glyph metrics, head placement and foreground/shadow glyphs.");
            messages.UpdateSourceTimer(100);
            Assert(simple.Age == 2 && simple.FadeFactor == 273 && messages.Text.GlyphDrawCount == 20,
                "Actual PC first-clock update supplies two ticks and thirty-tick fade data, while the original font draw remains fully present.");
            BitmapGlyphDraw draw = messages.Text.Layout.Draw(256, 30, data.CheckpointText.Span, 8, 4).First();
            byte[] brightness = Enumerable.Range(0, 256).Select(value => (byte)Math.Min(value * 2, 255)).ToArray();
            GodotBitmapGlyphQuad normal = GodotBitmapText.Project(draw, font.Glyphs[draw.GlyphIndex], new(640, 480),
                HudHorizontalAnchor.Center, HudVerticalAnchor.Top, brightness)!.Value;
            GodotBitmapGlyphQuad wide = GodotBitmapText.Project(draw, font.Glyphs[draw.GlyphIndex], new(1280, 480),
                HudHorizontalAnchor.Center, HudVerticalAnchor.Top, brightness)!.Value;
            Assert(normal.Destination.Size == wide.Destination.Size && wide.Destination.Position.X - normal.Destination.Position.X == 320 &&
                normal.Colour == new Color(192 / 255f, 192 / 255f, 1) && normal.Depth == 4,
                "Real source glyph output keeps PC RGB96/96/128 brightness, explicit depth4 and proportional centered widescreen placement.");
            for (int redraw = 0; redraw < 10; redraw++) messages.Rebuild();
            Assert(simple.Age == 2 && messages.Messages.SourceMessages[0].Time == 0 && messages.Messages.SourceMessages[0].TotalTime == 0,
                "Native presentation cannot age simple or checkpoint messages outside the original source update phase.");
            messages.UpdateSourceTimer(314);
            Assert(messages.Messages.SourceSimpleMessages.Count == 0 && messages.Messages.SourceMessages.Count == 1 && messages.Text.GlyphDrawCount == 0,
                "Source duration+90 retires the simple message while retaining its separate PC checkpoint SMessage.");
            messages.ShowSimpleMessage("A"u8.ToArray(), 64, 100, 125);
            messages.ClearSimpleMessages();
            Assert(messages.Messages.SourceSimpleMessages.Count == 0 && messages.Messages.SourceMessages.Count == 1 && messages.Text.GlyphDrawCount == 0,
                "Source ClearTextMessages clears only actual simple records and their native glyphs.");
            const string action = "openspidey_mission_text_verification";
            Assert(!InputMap.HasAction(action), "The native keyboard fixture must not replace an existing input action.");
            InputMap.AddAction(action);
            try
            {
                using var key = new InputEventKey { PhysicalKeycode = Key.J };
                InputMap.ActionAddEvent(action, key);
                var controls = new GodotMissionControlNames(mask => mask == 0x40 ? action : null);
                MissionControlName binding = controls.Read(0x40);
                Assert(binding.IsBound && binding.Name == "J" && !controls.Read(0).IsBound,
                    "The production text binding adapter reads actual Godot input-map physical keys and preserves unbound source action0.");
                byte[] message = Encoding.Latin1.GetBytes("Press AB to pick Objects up");
                int words = (message.Length + 2) / 2;
                byte[] arguments = new byte[words * 2 + 6]; message.CopyTo(arguments, 0);
                BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2), 64);
                BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2 + 2), 30);
                BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2 + 4), 125);
                var handlers = MissionTextCommandHandler.CreateHandlers(messages, data, controls).ToDictionary(handler => handler.Opcode);
                handlers[198].Execute(new(triggers.Nodes[0], 0), new(198, 0, arguments));
                SimpleMissionMessage tutorial = messages.Messages.SourceSimpleMessages.Single();
                int length = tutorial.Text.Span.IndexOf((byte)0);
                Assert(tutorial.Text.Span[..length].SequenceEqual("Press J to pick Objects up"u8) &&
                    messages.Text.GlyphDrawCount > 0 && tutorial.X == 64 && tutorial.Y == 30 && tutorial.Duration == 125,
                    "Actual source198 pattern/action data, real current keyboard mapping and native archived glyphs compose a genuine pickup tutorial.");
                handlers[218].Execute(new(triggers.Nodes[0], 0), new(218, 0, ReadOnlyMemory<byte>.Empty));
                Assert(messages.Messages.SourceSimpleMessages.Count == 0 && messages.Messages.SourceMessages.Count == 1 && messages.Text.GlyphDrawCount == 0,
                    "Source218 command dispatch clears actual native tutorial output while preserving the checkpoint record.");
            }
            finally { InputMap.EraseAction(action); }
            messages.DeleteAllMessages();
            Assert(messages.Messages.SourceSimpleMessages.Count == 0 && messages.Messages.SourceMessages.Count == 0,
                "Original DeleteAll owns both message lists without leaving a checkpoint record behind.");
            GD.Print("Verified native mission messages: actual checkpoint data pointer, real140 saved-name ownership, separate PC SMessage/Simple lists, original font/scale/brightness, centered widescreen geometry and exact source clock/removal.");
        }
        finally { viewport.Free(); }
        static void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native mission messages: {evidence}"); }
    }
}
