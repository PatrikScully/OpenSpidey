using System.Text;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Native l2a1lsc slots over real triggers, player, speech, five first-match textures and the actual panel presenter.</summary>
internal static class ScorpionApproachImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, string mediaDirectory)
    {
        SceneTree tree = owner.GetTree();
        var viewport = new SubViewport { Name = "OriginalScorpionApproachProof", Size = new(640, 480), OwnWorld3D = true,
            Disable3D = true, TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        var root = new Node3D { Name = "OriginalL2A1ModuleOwners" }; viewport.AddChild(root);
        var repository = new PsxLibraryRepository(archive);
        GodotScorpionApproachModule? module = null;
        GodotPlayerModuleHook? playerModules = null;
        GodotMissionTimerPresenter? presenter = null;
        GodotBitmapText? font = null;
        ImageTexture[] uploaded = [];
        int assertions = 0;
        try
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L2A1_T.trg"));
            TriggerNode autoexec = triggers.Nodes[0];
            TriggerInstruction load = TriggerCommandReader.Read(autoexec).Single(instruction => instruction.Opcode == 189 &&
                Encoding.Latin1.GetString(instruction.Arguments.Span).TrimEnd('\0').Equals("l2a1lsc", StringComparison.Ordinal));
            var countdown = new MissionCountdown();
            var state = new ScorpionApproachState();
            var hook = new PlayerModuleHook();
            var modules = new SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor>();
            var pulses = new List<ushort>();
            uint clock = 1000;
            bool alternate = false, playerPresent = true;
            var speech = new GodotSpeechPlayer { Name = "SharedScorpionSpeech", CacheDirectory = mediaDirectory };
            root.AddChild(speech); speech.SetPhysicsProcess(false);
            var registry = new GodotActorRegistry { Name = "OriginalL2A1Nodes", Triggers = triggers, Difficulty = 2,
                Environment = new(LevelId: 0x201), ProcessMode = Node.ProcessModeEnum.Disabled };
            root.AddChild(registry);
            var moduleOwner = new Node { Name = "OriginalLevelScriptOwner" }; root.AddChild(moduleOwner);
            module = GodotScorpionApproachModule.Create(new()
            {
                Archive = archive, Repository = repository, Registry = registry, Countdown = countdown, State = state,
                PlayerHook = hook, Speech = speech, Clock = () => clock, ScreenOffsetY = () => 0,
                AlternateStory = () => alternate, SendPulse = node =>
                {
                    Check(state.PrimaryWarnings[4] == 1, "Timeout must latch its original warning before sending the authored pulse.");
                    pulses.Add(node);
                }
            });
            moduleOwner.AddChild(module); module.Register(modules);
            var loader = new ModuleLoadCommandHandler(new ModuleLoader(modules));
            Check(!module.SourceModuleLoaded && modules.SourceOrder.Count == 0 &&
                hook.ModuleName is null && countdown.Running == 0 && repository.Loaded.Count == 0,
                "Registration must load no module, player, countdown, map or cached progress texture.");
            loader.Execute(new(autoexec, 0), load);
            loader.Execute(new(autoexec, 0), load);
            Check(module.SourceModuleLoaded && modules.SourceOrder.Count == 1 && modules.SourceOrder[0].Name == "l2a1lsc" &&
                hook.ModuleName == "l2a1lsc" && hook.FunctionMask == 1 && countdown.Running == 0 && module.LiveTextures == 0,
                "Untouched autoexec189 publishes real monitor0/display1 and the process mask1 exactly once, before the player and first AI.");

            // This private proof imports its genuine world explicitly; the production story proof separately verifies authored restart128 ordering.
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L2A1_G.psx"); root.AddChild(world);
            PsxAssetLibrary bank = repository.Load("spidey");
            var animations = bank.Animations as CompressedAnimationLibrary
                ?? throw new InvalidDataException("Original Scorpion proof player skeleton is absent.");
            ImportedAnimatedModel model = ImportedAnimatedModel.Build(bank.Scene, bank.Textures, animations, playerPartOrder: true);
            RestartPoint restart = RestartPoint.Read(triggers.Nodes[1]);
            GodotPlayer player = GodotPlayer.Create(new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12,
                restart.Orientation.Y, model, new(true, new(false, 10, true)));
            player.Name = "ActualPostAiPlayer"; root.AddChild(player); player.SetPhysicsProcess(false);
            player.Combat.SourceTimer = () => clock; registry.Player = player;
            int phase = 0, finishedContactFrames = 0, priorMonitors = 0, priorUpdates = 0;
            player.MissionContacts = new ContactProbe(() => phase = 1, () =>
            {
                Check(phase == 3 && player.SourceUpdates == priorUpdates + 1 && module.MonitorCalls == priorMonitors &&
                    player.Transform.Origin == PsxCoordinates.FixedWorldPosition(player.Controller.Position),
                    "The genuine native player completes movement, posed transform, source update and contact rearming before the module hook.");
                phase = 4; finishedContactFrames++;
            });
            player.Controller.BeforePlayerInput += _ => phase = 2;
            player.Controller.AfterPlayerMovement += _ =>
            {
                Check(phase == 2 && module.MonitorCalls == priorMonitors,
                    "The source post-movement callback still precedes the full native player AI hook.");
                phase = 3;
            };
            player.Controller.AfterPlayerAi += BeforeHook;
            playerModules = GodotPlayerModuleHook.Bind(player, hook, modules);
            Check(ReferenceEquals(GodotPlayerModuleHook.Bind(player, hook, modules), playerModules) &&
                player.GetChildren().OfType<GodotPlayerModuleHook>().Count() == 1,
                "Repeated same-player binding must preserve the sole generic source-hook owner and subscription.");
            player.Controller.AfterPlayerAi += AfterHook;
            bool rejectedDifferentOwner = false;
            try { GodotPlayerModuleHook.Bind(player, new PlayerModuleHook(), modules); }
            catch (InvalidOperationException) { rejectedDifferentOwner = true; }
            Check(rejectedDifferentOwner && playerModules.PlayerHookBound && ReferenceEquals(playerModules.BoundPlayerController, player.Controller) &&
                countdown.Running == 0, "Binding the actual controller must not run its source callback eagerly.");
            await owner.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            StepPlayer();
            Check(phase == 6 && finishedContactFrames == 1 && playerModules.SourceHookVisits == 1 && countdown.DisplayEnabled == 1 &&
                countdown.Running == 1 && countdown.RemainingTicks == 0x3174 && countdown.Rate == 4096 && countdown.LastUpdateTick == clock &&
                state.PrimaryWarnings.All(value => value == 0) && state.SecondaryWarnings.All(value => value == 0),
                "The first full player AI starts the shared difficulty2 countdown and resets both five-byte warning arrays at the actual clock.");
            clock += 2; StepPlayer();
            Check(playerModules.SourceHookVisits == 2 && countdown.RemainingTicks == 0x3174 && countdown.LastUpdateTick == 1000,
                "Monitor0 selects rate4096 but leaves countdown decay and its last clock to the genuine HUD update phase.");
            countdown.Update(clock, false);
            Check(countdown.RemainingTicks == 0x3172 && countdown.LastUpdateTick == clock,
                "The panel phase consumes the shared two-vblank delta once after the monitor; rendering cannot invent a second timer.");

            font = GodotBitmapText.Create(OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt"));
            font.Visible = false; root.AddChild(font); font.SetProcess(false);
            presenter = GodotMissionTimerPresenter.Create(archive, countdown, new MissionTimerDisplayState(), font, new()
            {
                LevelId = () => registry.Environment.LevelId, PlayerPresent = () => playerPresent,
                PlayerScriptGate = () => player.Combat.State.FieldE18,
                PlayerSynthesizedInput = () => player.Combat.State.SynthesizedInputActive,
                Random = _ => throw new InvalidDataException("L2A1 progress must not enter the ordinary timer random scan loop."),
                PlaySound = (_, _, _) => throw new InvalidDataException("L2A1 progress must not emit ordinary timer digit sounds."),
                CallModule = modules.CallParameterlessUserFunction,
                BeginModulePresentation = module.BeginPresentationFrame, DrawModulePresentation = module.DrawOn
            });
            var canvas = new ScorpionApproachProofCanvas { Presenter = presenter }; viewport.AddChild(canvas); canvas.AddChild(presenter);
            Array.Fill(state.PrimaryWarnings, (byte)1); Array.Fill(state.SecondaryWarnings, (byte)1);
            countdown.RemainingTicks = ScorpionApproachCountdown.Duration(registry.Difficulty);
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                viewport.Size = size;
                await Render();
                Check(module.LiveTextures == 5 && module.TextureLookups == 5 && module.Draws.Count == 23 &&
                    module.AllocatedPolys == 23 && module.SubmittedPrimitives == 23 && presenter.PrimitiveCount == 0,
                    "Actual timer slot1 must submit exactly two portraits, nineteen tiles and two end pieces through the genuine module, retaining its five cached textures.");
                Check(module.Draws[0].Texture == 0 && module.Draws[0].Piece.Quad == new HudSourceQuad(144, 11, 176, 37) &&
                    module.Draws[1].Texture == 1 && module.Draws[1].Piece.Quad == new HudSourceQuad(446, 11, 478, 37) &&
                    module.Draws.Skip(2).Take(19).Select((draw, index) => draw.Texture == 2 &&
                        draw.Piece.Quad == new HudSourceQuad((short)(138 + index * 18), 23, (short)(157 + index * 18), 33)).All(value => value),
                    "Original6554 width rounding, source portrait positions and all contiguous eighteen-pixel bar origins must reach the native draw owner.");
                byte[] manifest = archive.Read("data/l2a1_g.tex");
                PsxAssetLibrary geometry = repository.RequireLoaded("L2A1_G");
                uint[] firstChecksums = [0xD318F156, 0xF3376716, 0x0836621A, 0x8B31168B, 0x9270B05D];
                for (int index = 0; index < 5; index++)
                {
                    uint checksum = NamedTextureTable.FindFirst(manifest, ScorpionApproachProgress.TextureNames[index]);
                    DecodedTexture original = geometry.Textures.First(texture => texture.Checksum == checksum);
                    Check(checksum == firstChecksums[index] && ReferenceEquals(module.OriginalTexture(index), original),
                        "The actual source manifest's first matching texture identity must be retained, including repeated authored names.");
                    using Image pixels = module.SourceTexture(index)!.GetImage();
                    Check(pixels.GetWidth() == original.Width && pixels.GetHeight() == original.Height && pixels.GetData().SequenceEqual(original.RgbaPixels),
                        "Each native portrait/bar texture must upload the real source dimensions, palette and alpha pixels.");
                }
                uploaded = Enumerable.Range(0, 5).Select(index => module.SourceTexture(index)!).ToArray();
                using Image capture = viewport.GetTexture().GetImage();
                byte[] pixelsData = capture.GetData();
                var transform = new HudViewportTransform(size.X, size.Y);
                ScreenRectangle envelope = transform.Project(new ScreenRectangle(133, 11, 353, 26), HudHorizontalAnchor.Center);
                int visible = 0;
                for (int pixel = 0; pixel < pixelsData.Length; pixel += 4)
                {
                    if (pixelsData[pixel + 3] == 0) continue;
                    int offset = pixel / 4, x = offset % size.X, y = offset / size.X; visible++;
                    Check(x >= envelope.X - 2 && x <= envelope.X + envelope.Width + 2 && y >= envelope.Y - 2 && y <= envelope.Y + envelope.Height + 2,
                        "Every actual native progress pixel must remain inside the intact proportional source group at the viewport centre.");
                }
                Check(visible >= 512, "The genuine portraits and tiled Scorpion progress bar must draw visible pixels in both4:3 and widescreen.");
                if (capture.SavePng($"/tmp/openspidey-scorpion-{size.X}x{size.Y}.png") != Error.Ok)
                    throw new IOException("Could not save the original Scorpion approach HUD proof.");
                int displays = module.DisplayCalls;
                await Render();
                Check(module.DisplayCalls == displays + 1 && module.TextureLookups == 5 &&
                    Enumerable.Range(0, 5).All(index => ReferenceEquals(module.SourceTexture(index), uploaded[index])),
                    "A later actual HUD render invokes only display1 and reuses every genuine first-match cached native texture.");
            }

            int beforeGate = module.DisplayCalls;
            player.Combat.State.FieldE18 = 1; await Render();
            Check(module.DisplayCalls == beforeGate && module.Draws.Count == 0 && module.SubmittedPrimitives == 0,
                "The actual panel source201 script-control gate must suppress module1 without leaving stale progress quads.");
            player.Combat.State.FieldE18 = 0; player.Combat.State.SynthesizedInputActive = true; await Render();
            Check(module.DisplayCalls == beforeGate && module.Draws.Count == 0,
                "Synthesized player input uses the same genuine module presentation gate.");
            playerPresent = false; await Render();
            Check(module.DisplayCalls == beforeGate + 1 && module.Draws.Count == 23,
                "The original no-player panel branch still calls real display1 rather than requiring a fabricated player.");
            playerPresent = true; player.Combat.State.SynthesizedInputActive = false;

            VerifySpeechAndPortraits();
            countdown.RemainingTicks = 0; state.PrimaryWarnings[4] = 0;
            int failureNode = Enumerable.Range(1, triggers.Nodes.Count - 1).First(node => module.NodeKind(node) == 1 &&
                (module.NodePosition(node) >> 12) == new FixedVector3(-419, -2730, -3826));
            modules.CallParameterlessUserFunction("l2a1lsc", 0);
            modules.CallParameterlessUserFunction("l2a1lsc", 0);
            Check(pulses.SequenceEqual(new[] { checked((ushort)failureNode) }) && state.PrimaryWarnings[4] == 1,
                "Two real monitor0 calls send one pulse to the first ascending authored kind1 timeout node at the genuine position.");

            // Release the proof canvas's captured native draw commands before source module unloading destroys their textures.
            canvas.Present = false; await Render();
            uploaded = Enumerable.Range(0, 5).Select(index => module.SourceTexture(index)!).ToArray();
            byte[] primary = state.PrimaryWarnings.ToArray(), secondary = state.SecondaryWarnings.ToArray();
            uint remaining = countdown.RemainingTicks, rate = countdown.Rate;
            clock = 2000;
            Check(modules.Unload("l2a1lsc"), "The actual loaded module must have one genuine clear owner.");
            Check(module.ClearCalls == 1 && !module.SourceModuleLoaded && modules.SourceOrder.Count == 0 && module.LiveTextures == 0 &&
                countdown.Running == 0 && countdown.DisplayEnabled == 0 && countdown.LastUpdateTick == clock &&
                countdown.RemainingTicks == remaining && countdown.Rate == rate && state.PrimaryWarnings.SequenceEqual(primary) &&
                state.SecondaryWarnings.SequenceEqual(secondary) && hook.ModuleName == "l2a1lsc" && hook.FunctionMask == 1,
                "Real module clear resets only panel flags/time and all five cached pointers, retaining warning bytes, duration/rate and the process hook.");
            Check(uploaded.All(texture => !GodotObject.IsInstanceValid(texture)),
                "Unloading the actual level module must destroy all five privately owned native ImageTextures immediately.");
            loader.Execute(new(autoexec, 0), load);
            // Actual front-load resets the hook before any subsequent player AI; reload here exercises its genuine same-owner registration.
            countdown.RemainingTicks = 900; countdown.Running = 1;
            module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
            Check(module.LiveTextures == 5 && module.TextureLookups == 10 && module.ClearCalls == 1,
                "A genuine reload keeps the same module owner and lazily imports a fresh set of five textures after source clear.");
            uploaded = Enumerable.Range(0, 5).Select(index => module.SourceTexture(index)!).ToArray();
            var newOwner = new Node { Name = "ReparentedLevelScriptOwner" }; root.AddChild(newOwner);
            module.Reparent(newOwner); moduleOwner.Free();
            Check(GodotObject.IsInstanceValid(module) && playerModules.PlayerHookBound && module.LiveTextures == 5 && module.SourceModuleLoaded,
                "Changing native parents must preserve the genuine module, independent player hook and private cached resources.");
            newOwner.Free();
            Check(module.ResourcesDisposed && playerModules.PlayerHookBound && module.ClearCalls == 2 && modules.SourceOrder.Count == 0 &&
                uploaded.All(texture => !GodotObject.IsInstanceValid(texture)) && countdown.Running == 0 && countdown.DisplayEnabled == 0,
                "Actual module Predelete clears only its own linked functions and privately owned textures, preserving the independent generic player hook.");
            hook.Set(null, 0);
            int visits = playerModules.SourceHookVisits;
            player.MissionContacts = null;
            player.Controller.AfterPlayerAi -= BeforeHook;
            player.Controller.AfterPlayerAi -= AfterHook;
            StepPlayer();
            Check(visits == 2 && playerModules.SourceHookVisits == visits + 1 && player.SourceUpdates == 3,
                "The independent generic source loop still runs once after a complete player AI when front-load has reset its module name and mask.");
            playerModules.Free();
            Check(playerModules.ResourcesDisposed && !playerModules.PlayerHookBound,
                "Deleting the generic native hook owner must unsubscribe its controller and remove its sole-player ownership record.");
            StepPlayer();
            Check(playerModules.SourceHookVisits == visits + 1 && player.SourceUpdates == 4,
                "A subsequent complete native player update must invoke no retired generic hook callback.");
            playerModules = GodotPlayerModuleHook.Bind(player, hook, modules);
            var hookOwner = new Node { Name = "ReparentedGenericPlayerHook" }; root.AddChild(hookOwner);
            playerModules.Reparent(hookOwner);
            player.Free();
            Check(playerModules.ResourcesDisposed && !playerModules.PlayerHookBound && playerModules.SourceHookVisits == 0,
                "The actual player's native destructor must release its generic hook even after that hook has acquired another native parent.");
            hookOwner.Free();
            void StepPlayer()
            {
                phase = 0; priorMonitors = module.MonitorCalls; priorUpdates = player.SourceUpdates;
                player.AdvanceSourceUpdate(default, player.Controller.Orientation.Heading);
            }
            void BeforeHook()
            {
                Check(phase == 4 && module.MonitorCalls == priorMonitors,
                    "The native post-AI boundary runs after its contact frame without an early monitor invocation.");
                phase = 5;
            }
            void AfterHook()
            {
                Check(phase == 5 && module.MonitorCalls == priorMonitors + 1,
                    "One bound source player invokes genuine slot0 once after the full AI, including repeated same-player binding.");
                phase = 6;
            }
            async Task Render()
            {
                canvas.QueueRedraw();
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            void VerifySpeechAndPortraits()
            {
                uint duration = ScorpionApproachCountdown.Duration(registry.Difficulty);
                foreach (bool alternateStory in new[] { false, true })
                {
                    alternate = alternateStory;
                    for (int warning = 0; warning < 4; warning++)
                    {
                        Array.Fill(state.PrimaryWarnings, (byte)1); Array.Fill(state.SecondaryWarnings, (byte)1);
                        state.PrimaryWarnings[warning] = state.SecondaryWarnings[warning] = 0;
                        speech.Controller.Stop(); speech.Controller.Reset();
                        uint threshold = warning switch { 0 => duration * 3 / 4, 1 => duration / 2, 2 => duration / 4, _ => duration / 8 };
                        countdown.RemainingTicks = threshold;
                        int plays = speech.PlayCount;
                        module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                        Check(state.PrimaryWarnings[warning] == 0 && speech.PlayCount == plays,
                            "Each original warning threshold is strict; equality must not latch or start speech.");
                        countdown.RemainingTicks = threshold - 1;
                        module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                        int firstSlot = warning switch { 0 => alternate ? 14 : 10, 1 => alternate ? 11 : 8,
                            2 => alternate ? 12 : 6, _ => alternate ? 14 : 9 };
                        Check(state.PrimaryWarnings[warning] == 1 && state.SecondaryWarnings[warning] == 0 && speech.PlayCount == plays + 1 &&
                            speech.Controller.Current == new SpeechRequest(8, firstSlot, 0) && speech.Player.Playing &&
                            speech.Player.Stream is AudioStreamWav pcm && pcm.GetLength() > 0,
                            "Crossing a real normal/alternate threshold must latch its source primary byte and play the exact original bank8 WAV on the shared native speech channel.");
                        speech.Controller.Stop(); speech.Controller.Reset(); speech.Controller.Allowed = false;
                        module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                        Check(state.SecondaryWarnings[warning] == 0 && speech.PlayCount == plays + 1,
                            "The caller-owned speech allowance flag must postpone only the secondary reply.");
                        speech.Controller.Allowed = true;
                        module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                        int reply = warning switch { 0 => alternate ? 15 : 9, 1 => alternate ? 13 : -1,
                            2 => alternate ? -1 : 7, _ => alternate ? 15 : 10 };
                        Check(state.SecondaryWarnings[warning] == 1 && speech.PlayCount == plays + 1 + (reply >= 0 ? 1 : 0) &&
                            (reply < 0 || speech.Controller.Current == new SpeechRequest(8, reply, 0)),
                            "The exact original secondary flag and normal/alternate reply policy must survive the actual shared speech allowance gate, including silent reply cases.");
                    }
                }
                alternate = false;
                Array.Fill(state.PrimaryWarnings, (byte)1); Array.Fill(state.SecondaryWarnings, (byte)1);
                speech.Controller.Stop(); speech.Controller.Reset(); speech.Play(8, 7, 250);
                state.PrimaryWarnings[0] = state.SecondaryWarnings[0] = 0;
                countdown.RemainingTicks = duration * 3 / 4 - 1;
                int before = speech.PlayCount;
                module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                Check(speech.PlayCount == before && speech.Controller.Current == new SpeechRequest(8, 7, 250) &&
                    state.PrimaryWarnings[0] == 1 && module.Draws[1].Piece.Quad == new HudSourceQuad(430, -2, 494, 50),
                    "The renderer samples existing Jonah speech before warning playback; rejected lower-priority speech still latches the source flag and keeps the original Jonah portrait growth.");
                Array.Fill(state.PrimaryWarnings, (byte)1); Array.Fill(state.SecondaryWarnings, (byte)1);
                speech.Controller.Stop(); speech.Controller.Reset();
                countdown.RemainingTicks = 31;
                module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                Check(module.Draws.Count == 23 && module.AllocatedPolys == 22 && module.Draws[0].Piece == module.Draws[1].Piece &&
                    module.Draws[0].Piece.Quad == new HudSourceQuad(388, -14, 482, 62),
                    "The final31-tick path must grow the actual Scorpion poly and reuse its precise geometry for Jonah, preserving22 allocations and23 submissions.");
                countdown.RemainingTicks = 0;
                module.BeginPresentationFrame(); modules.CallParameterlessUserFunction("l2a1lsc", 1);
                Check(module.Draws[0].Piece.Quad == new HudSourceQuad(451, 11, 483, 37) && module.Draws[0].Piece == module.Draws[1].Piece,
                    "The zero-time ramp must restore its source portrait size and retain the final original overlap.");
            }
        }
        finally
        {
            GodotAudioShutdown.Resource[] audio = await GodotAudioShutdown.RetirePlaybackAsync(root, tree);
            viewport.Free();
            await GodotAudioShutdown.VerifyReleasedAsync(audio, tree);
            Check(module is null || module.ResourcesDisposed, "The private native module must release even when a preceding proof fails.");
            Check(playerModules is null || playerModules.ResourcesDisposed, "The sole generic player-hook owner must release its actual controller subscription.");
            Check(presenter is null || presenter.ResourcesDisposed, "The actual panel presenter must release its border and timer font.");
            Check(font is null || font.ResourcesDisposed, "The private native original font must release its glyph textures.");
            Check(uploaded.All(texture => !GodotObject.IsInstanceValid(texture)), "The private proof must retain no native progress ImageTexture.");
        }
        GD.Print($"Verified native Scorpion approach: {assertions} checks, untouched189, full post-AI slot0, real shared countdown/speech, first-match five-texture cache, original23 quads,4:3/widescreen pixels, timeout pulse, clear/reload/reparent/Predelete.");

        void Check(bool condition, string evidence)
        {
            assertions++;
            if (!condition) throw new InvalidDataException("Native Scorpion approach: " + evidence);
        }
    }

    private sealed class ModuleLoader(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> modules) : IMissionModuleLoader
    { public void LoadCodeModule(string name) => modules.Load(name); }
    private sealed class ContactProbe(Action begin, Action end) : IMissionContactFrameHost
    {
        public void BeginContactFrame() => begin();
        public void EndContactFrame() => end();
    }
}

internal partial class ScorpionApproachProofCanvas : Node2D
{
    public GodotMissionTimerPresenter Presenter { get; set; } = null!;
    public bool Present { get; set; } = true;
    public override void _Draw()
    {
        if (!Present) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        Presenter.DrawOn(this, new HudViewportTransform(size.X, size.Y), 0);
    }
}
