using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Original463 rescue text/208 and L1A4 tutorial287, actual font measurements, native quads, real source bits and219 destruction.</summary>
internal static class MissionTextBoxImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, ReadOnlyMemory<byte> originalExe)
    {
        OriginalMissionMessageData data = OriginalMissionMessageData.Read(originalExe);
        OriginalBitmapFont font = OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt");
        TriggerFile source = TriggerFile.Read(archive.Read("data/l1a3_t.trg"));
        TriggerNode rescued = source.Nodes[463];
        IReadOnlyList<TriggerInstruction> program = TriggerCommandReader.Read(rescued);
        int checks = 0;
        Assert(program.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[] { 218, 219, 198, 208 }),
            "Untouched L1A3 rescue463 is the exact separate text-clear/box-clear/message/box program.");
        foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
        {
            var viewport = new SubViewport { Name = "OriginalTextBoxProof", Size = size, Disable3D = true,
                TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            var particles = new SourceParticleScheduler(); int timer = 100;
            GodotMissionMessages messages = GodotMissionMessages.Create(data, font); viewport.AddChild(messages);
            GodotMissionTextBoxes boxes = GodotMissionTextBoxes.Create(messages.Messages, particles, () => timer); messages.AddChild(boxes);
            var registry = new GodotActorRegistry { Name = "OriginalTextBoxCommandFactory", Triggers = source };
            viewport.AddChild(registry); registry.SetPhysicsProcess(false);
            var handlers = MissionTextCommandHandler.CreateHandlers(messages, data, new GodotMissionControlNames(PlayerControls.SourceActionName))
                .Concat(MissionTextBoxCommandHandler.CreateHandlers(boxes)).ToDictionary(handler => handler.Opcode);
            var mission = new MissionRuntime(source, registry, handlers.Values);
            var completed = new List<MissionTrace>(); mission.Trace += completed.Add;
            try
            {
                messages.ShowCheckpointMessage();
                mission.Queue(463); mission.ExecutePending();
                GodotMissionTextBox first = boxes.SourceOrder.Single();
                var message = messages.Messages.SourceSimpleMessages.Single();
                var definition = first.State.Definition;
                Assert(completed.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted)
                        .Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(program.Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset))) &&
                    messages.Messages.SourceMessages.Count == 1 && message.X == 140 && message.Y == 205 && message.Duration == 400 &&
                    definition.X == 133 && definition.Y == 192 && definition.Height == 15 && definition.Lifetime == 520 &&
                    definition.Width == messages.Text.Layout.Width(message.Text.Span) + 8 &&
                    definition is { Red: 0, Green: 0, Blue: 48 } && first.State.LastTimer == 100 && first.State.Age == 0 &&
                    particles.Identities(SourceParticleList.TextBox).Single() == first && !first.State.Dead && !first.State.Protected,
                    "Real463 executes every original instruction once;208 derives its actual font-measured width and initializes the same native TextBox-list bit with source RGB/age/timer/lifetime.");
                timer += 2; particles.Move(unchecked((uint)timer));
                Assert(first.State.Age == 2 && first.State.ElapsedTicks == 2 && first.State.DisplayQuad() == new HudSourceQuad(133, 198,
                    unchecked((short)(133 + definition.Width)), 199),
                    "Genuine source TextBox phase advances two timer ticks and emits the original signed16 opening quad, independently of simple-message aging.");
                short age = first.State.Age;
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Assert(first.State.Age == age && message.Age == 0 && boxes.SourceOrder.Count == 1,
                    "Native frame drawing cannot age source boxes/messages or consume their separate lifetime.");
                timer = 116; particles.Move(unchecked((uint)timer));
                Assert(first.State.Age == 16 && first.State.DisplayQuad() == new HudSourceQuad(133, 192,
                    unchecked((short)(133 + definition.Width)), 207),
                    "Original16-tick grow reaches the actual full15-pixel rectangle behind the message glyphs.");
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using (Image capture = viewport.GetTexture().GetImage())
                {
                    ScreenRectangle rectangle = new HudViewportTransform(size.X, size.Y).Project(first.State.DisplayQuad(), HudHorizontalAnchor.Center);
                    Color pixel = capture.GetPixel((int)(rectangle.X + 3), (int)(rectangle.Y + 2));
                    Assert(pixel.A > .5f && pixel.B > pixel.R && pixel.B > pixel.G && rectangle.Width == definition.Width * new HudViewportTransform(size.X, size.Y).ScaleX,
                        "Actual native alpha160 blue rectangle renders behind archived glyphs with the same intact centered widescreen transform as its measured text.");
                }
                Node parent = first.GetParent(); parent.RemoveChild(first); viewport.AddChild(first);
                Assert(!first.Disposed && GodotObject.IsInstanceValid(first) && particles.Identities(SourceParticleList.TextBox).Single() == first,
                    "Reparenting the allocated native CTextBox keeps its source bit identity, timing and owner instead of deleting on TreeExit.");
                handlers[208].Execute(new(rescued, 0), program.Single(instruction => instruction.Opcode == 208));
                GodotMissionTextBox second = boxes.SourceOrder[0];
                Assert(boxes.SourceOrder.SequenceEqual(new[] { second, first }) && second.State.Age == 0 && second.State.LastTimer == timer,
                    "Actual208 prepends a second native CTextBox at the shared source head, including a retained reparented older bit.");
                first.State.Protected = true;
                handlers[219].Execute(new(rescued, 0), program.Single(instruction => instruction.Opcode == 219));
                Assert(first.Disposed && second.Disposed && !GodotObject.IsInstanceValid(first) && !GodotObject.IsInstanceValid(second) &&
                    boxes.SourceOrder.Count == 0 && particles.Count == 0 && ReferenceEquals(messages.Messages.SourceSimpleMessages.Single(), message) &&
                    messages.Messages.SourceMessages.Count == 1 && messages.Text.GlyphDrawCount > 0,
                    "Original219 immediately destroys both native head entries, including protected/reparented bits, and preserves actual simple/checkpoint messages and glyphs.");
                handlers[208].Execute(new(rescued, 0), program.Single(instruction => instruction.Opcode == 208));
                GodotMissionTextBox timed = boxes.SourceOrder.Single();
                timer = unchecked(timed.State.LastTimer + timed.State.Definition.Lifetime); particles.Move(unchecked((uint)timer)); particles.RemoveDead();
                Assert(timed.State.Age == 520 && !timed.State.Dead && !timed.Disposed && particles.Count == 1,
                    "CTextBox death is strictly age>lifetime; exact age520 keeps its allocated native source bit for the final display phase.");
                timer++; particles.Move(unchecked((uint)timer));
                Assert(timed.State.Dead && !timed.Disposed && particles.Count == 1, "Source Move marks the expired box dead before the separate native cleanup phase.");
                particles.RemoveDead();
                Assert(timed.Disposed && !GodotObject.IsInstanceValid(timed) && particles.Count == 0 && messages.Messages.SourceSimpleMessages.Count == 1,
                    "Actual source bit cleanup releases only its expired CTextBox through Predelete, retaining message ownership.");
                TriggerFile fourth = TriggerFile.Read(archive.Read("data/l1a4_t.trg")); TriggerNode tutorial = fourth.Nodes[287];
                IReadOnlyList<TriggerInstruction> tutorialProgram = TriggerCommandReader.Read(tutorial);
                Assert(tutorialProgram.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[] { 218, 219, 198, 208, 139 }),
                    "Untouched L1A4 tutorial287 retains its real text/control-binding/box prefix and separate subsequent voice139 dependency.");
                foreach (TriggerInstruction instruction in tutorialProgram.Take(4)) handlers[instruction.Opcode].Execute(new(tutorial, 0), instruction);
                GodotMissionTextBox tutorialBox = boxes.SourceOrder.Single();
                Assert(tutorialBox.State.Definition is { X: 83, Y: 192, Height: 15, Lifetime: 520, Red: 48, Green: 0, Blue: 0 } &&
                    messages.Messages.SourceSimpleMessages.Single() is { X: 90, Y: 205, Duration: 480 } &&
                    tutorialBox.State.Definition.Width == messages.Messages.LastSimpleMessageWidth + 8 && messages.Text.GlyphDrawCount > 0,
                    "Actual authored tutorial198 uses genuine current input-map names and archived font widths before its208 red rectangle; no substitute text or assumed width is supplied.");
                messages.RemoveChild(boxes); viewport.AddChild(boxes);
                Assert(!boxes.ResourcesDisposed && !tutorialBox.Disposed && particles.Count == 1, "Native UI-owner reparenting preserves genuine source bit ownership.");
                boxes.Free();
                Assert(boxes.ResourcesDisposed && tutorialBox.Disposed && !GodotObject.IsInstanceValid(tutorialBox) && particles.Count == 0,
                    "Actual UI owner Predelete releases its source TextBox identities even after reparenting and leaves no scheduler registrations.");
            }
            finally { viewport.Free(); }
        }
        GD.Print($"Passed {checks} native text-box checks: real L1A3 rescue463/L1A4 tutorial287, original archived font measurements,208 timed source bits/grow quads/alpha, centered wide rendering,219 protected clear and exact reparent/expiry/destruction.");

        void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException("Native mission text boxes: " + evidence); checks++; }
    }
}
