using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A1 story startup, native intro playback and untouched initial/restart programs.</summary>
internal static class StoryLevelSessionImportChecks
{
    public static async Task VerifyNativeDeletionAsync(StoryLevelSession scene, SceneTree tree)
    {
        if (Descendants(scene).OfType<GodotSoundChannels>().Count() != 1 ||
            Descendants(scene).OfType<GodotSpeechPlayer>().Count() != 1)
            throw new InvalidDataException("The story scene must retain its single source sound and speech owners before native deletion.");
        GodotAudioShutdown.Resource[] resources = await GodotAudioShutdown.RetirePlaybackAsync(scene, tree);
        scene.Free();
        if (GodotObject.IsInstanceValid(scene))
            throw new InvalidDataException("Native story scene deletion did not destroy its source owner.");
        await GodotAudioShutdown.VerifyReleasedAsync(resources, tree);
        GD.Print($"PASS: native story deletion releases all {resources.Length} tracked audio resources through the running AudioServer before engine shutdown.");
    }

    // Independently read original L1A1_T.trg marker1 records, in file order.
    private static readonly ushort[] OriginalInitialNodes =
        [9, 10, 16, 19, 54, 55, 61, 65, 70, 91, 92, 101, 102, 162, 165, 168, 170, 204, 229, 230, 237, 238, 285];
    private static readonly ushort[] OriginalDeathRestartOpcodes =
        [213, 195, 170, 191, 171, 171, 104, 202, 200, 141, 166, 169,
            180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 184, 302, 3];

    /// <summary>Attach after PrepareMission queues autoexec, before its first real event phase.</summary>
    public static async Task VerifyAsync(StoryLevelSession scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Failure is { } prepareFailure)
            throw new InvalidDataException("The actual story scene could not prepare its source dependencies.", prepareFailure);
        TriggerFile triggers = scene.Registry.Triggers;
        TriggerNode autoexec = triggers.Nodes.Single(node => node.TypeCode == (ushort)TriggerNodeKind.Autoexec);
        TriggerNode restartNode = triggers.Nodes[257];
        RestartPoint originalRestart = RestartPoint.Read(restartNode);
        IReadOnlyList<TriggerInstruction> autoexecProgram = TriggerCommandReader.Read(autoexec);
        IReadOnlyList<TriggerInstruction> restartProgram = TriggerCommandReader.Read(restartNode);
        TriggerActorDefinition[] originalDefinitions = triggers.Nodes.Where(node => node.TypeCode == 1)
            .Select(TriggerActorDefinition.Read).Where(definition => definition.InitializeOnLoad).ToArray();
        var traces = new List<MissionTrace>();
        var initial = new List<GodotScriptActor>();
        var otherSpawns = new List<(GodotScriptActor Actor, MissionStartupPhase Phase)>();
        (GodotScriptActor Actor, MissionStartupPhase Phase)[] startupSpawns = [];
        GodotSourceBackdrop[] sourceBackgrounds = [];
        uint stoppedClock = scene.SourceClock;
        int checks = 0, playersConstructed = 0, gameplayStarts = 0;
        bool introBlocked = false, coverObserved = false, decodedMovieObserved = false, skippedMovie = false;
        FixedVector3? restartPlayerPosition = null;
        PlayerStoryInputImportProbe? inputProbe = null;

        Assert(scene.Failure is null && scene.Startup.Phase == MissionStartupPhase.Autoexec &&
            scene.Registry.LiveCount == 0 && scene.Registry.Player is null && scene.Player is null && stoppedClock == 0,
            "The proof attaches before source autoexec and all actor/player construction, with the live source clock stopped.");
        Assert(scene.Application.MissionName.Equals("L1A1_T", StringComparison.OrdinalIgnoreCase) &&
            scene.Checkpoints.Save.RestartPointName.Length == 0 && scene.Checkpoints.Current is null,
            "The actual new-game launch has an empty saved restart; no Re_Restart01 override substitutes for autoexec140.");
        Assert(autoexec.Id == 12 && originalRestart.Name == "Re_Start_Death" &&
            originalRestart.Position == new TriggerPosition(-19268, 4851, -40748) &&
            originalRestart.Orientation == new TriggerOrientation(3243, 0, 0) &&
            originalRestart.Links.SequenceEqual(new ushort[] { 85, 242 }) &&
            restartProgram.Select(instruction => instruction.Opcode).SequenceEqual(OriginalDeathRestartOpcodes),
            "The untouched first-level death restart257 retains its genuine position, orientation, complete command sequence and85/242 links.");
        Assert(originalDefinitions.Select(definition => definition.NodeId).SequenceEqual(OriginalInitialNodes) &&
            originalDefinitions.Count(definition => definition.ActorType == 312) == 4 &&
            originalDefinitions.Count(definition => definition.ActorType == 402) == 5 &&
            originalDefinitions.Count(definition => definition.ActorType == 203) == 14,
            "Actual L1A1 marker1 data independently matches23 initial bodies: four312, five402 and fourteen203 in original file order.");
        TriggerInstruction initialRestart = autoexecProgram.Single(instruction => instruction.Opcode == 140);
        TriggerInstruction intro = autoexecProgram.Single(instruction => instruction.Opcode == 190);
        Assert(ReadString(initialRestart) == "Re_Start_Death" && ReadWord(intro) == 11,
            "Actual autoexec12 selects Re_Start_Death and requests the genuine original intro11.");

        scene.Registry.Spawned += RecordSpawn;
        scene.Mission.Trace += RecordTrace;
        scene.PlayerConstructed += RecordPlayer;
        scene.GameplayStarted += RecordGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                EnsureHealthy();
                Assert(scene.SourceClock == stoppedClock && scene.Registry.SourceUpdates == 0 &&
                    initial.All(actor => actor.SourceUpdates == 0),
                    "Source time and actor AI stay stopped through autoexec, constructor census and restart execution.");
                if (scene.Cinema.IsPlaying)
                {
                    Assert(scene.Startup.Phase == MissionStartupPhase.Autoexec && scene.Mission.IsBlocked &&
                        scene.Mission.PendingCount != 0 && initial.Count == 0 && scene.Player is null && scene.Registry.Player is null,
                        "Real cinema playback holds the autoexec continuation before any original actors or player are constructed.");
                    coverObserved |= scene.Cinema.ShowingCover;
                    if (!scene.Cinema.ShowingCover && scene.Cinema.Video.IsPlaying()) decodedMovieObserved = true;
                    // Let the actual cover and playback polls advance; never bypass the source skip gate.
                    if (!scene.Cinema.ShowingCover && scene.Cinema.CanSkipMovie)
                    {
                        Assert(decodedMovieObserved, "The actual original intro movie has started before its legal skip request.");
                        scene.Cinema.RequestSkip();
                        skippedMovie = true;
                    }
                }
                if (deadline.Elapsed > TimeSpan.FromSeconds(90))
                    throw new TimeoutException("Native original story startup did not reach Playing within90 seconds.");
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            EnsureHealthy();
            Assert(introBlocked && coverObserved && decodedMovieObserved && skippedMovie && !scene.Cinema.IsPlaying,
                "Genuine intro11 cover/movie playback blocked autoexec and completed only after CanSkipMovie allowed the native skip.");
            Assert(playersConstructed == 1 && gameplayStarts == 1 && scene.Startup.Phase == MissionStartupPhase.Playing &&
                scene.Startup.InitialActorsCreated == 23 && scene.Startup.Failure is null,
                "Initial census, player construction and gameplay activation each complete exactly once.");
            Assert(initial.Select(actor => actor.SourceNodeId).SequenceEqual(OriginalInitialNodes) &&
                initial.OfType<GodotThugActor>().Count() == 4 && initial.OfType<GodotPlatformActor>().Count() == 5 &&
                initial.OfType<GodotControlActor>().Count() == 14 &&
                startupSpawns.Length == 1 && startupSpawns[0].Actor.SourceNodeId == 242 &&
                startupSpawns[0].Actor.SourceActorType == 203 && startupSpawns[0].Phase == MissionStartupPhase.Restart,
                "The actual restart pulse constructs its original control242 after all23 initial constructors and the real player. " +
                $"Initial=[{string.Join(',', initial.Select(actor => actor.SourceNodeId))}]; startup=[{string.Join(',', startupSpawns.Select(spawn => $"{spawn.Actor.SourceNodeId}/{spawn.Actor.SourceActorType}/{spawn.Phase}"))}]; all=[{string.Join(',', otherSpawns.Select(spawn => $"{spawn.Actor.SourceNodeId}/{spawn.Actor.SourceActorType}/{spawn.Phase}"))}].");
            Assert(scene.Checkpoints.Current is { NodeId: 257, Name: "Re_Start_Death" } &&
                scene.Checkpoints.Save.RestartPointName == "Re_Start_Death" && scene.Application.EndLevelNode == 224,
                "Normal empty-save startup retains autoexec's genuine death restart, source Front_LoadGame save-name copy and302 end-node224.");
            Assert(scene.PlayerBrightness.State.DarkSurfaceTarget == 140 &&
                scene.Rendering.SourceDepth.Globals is { First: 300, Second: -30 },
                "The genuine death restart applies original184 player574 brightness140 and signed166/169 OT globals300/-30.");
            AssertCompletedExactlyOnce(autoexec, autoexecProgram);
            AssertCompletedExactlyOnce(restartNode, restartProgram);
            IReadOnlyList<TriggerInstruction> command85 = TriggerCommandReader.Read(triggers.Nodes[85]);
            Assert(command85.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[] { 195, 199 }),
                "Actual linked point85 contains its authored widescreen and source player-input script.");
            AssertCompletedExactlyOnce(triggers.Nodes[85], command85);
            Assert(scene.Mission.CommandPoints[85].PulsesReceived == 1 &&
                traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 85) == 1 &&
                traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 257) == 1,
                "The restart executes once and queues its actual linked point85 exactly once, without duplicate pre-render pulses.");
            GodotSourceBackdrops[] backdropOwners = Descendants(scene).OfType<GodotSourceBackdrops>().ToArray();
            uint[] authoredBackgrounds = restartProgram.Where(instruction => instruction.Opcode == 171)
                .Select(ReadBackgroundChecksum).ToArray();
            Assert(backdropOwners.Length == 1 && backdropOwners[0].Count == 2 &&
                backdropOwners[0].States.Select(state => state.Checksum).SequenceEqual(authoredBackgrounds),
                "The genuine restart creates exactly its two original OBJ backdrops, without earlier rendering setup duplicating them.");
            GodotSourceBackdrop[] meshes = scene.Rendering.Backdrops.GetChildren().OfType<GodotSourceBackdrop>()
                .OrderBy(mesh => mesh.State.Index).ToArray();
            Assert(meshes.Length == 2 && meshes.All(mesh => GodotObject.IsInstanceValid(mesh) && mesh.Mesh is ArrayMesh geometry &&
                geometry.GetSurfaceCount() > 0) && meshes.Select(mesh => mesh.State.Checksum).SequenceEqual(authoredBackgrounds) &&
                scene.Registry.SourceList(ActorUpdateGroup.Background).SourceOrder.SequenceEqual(meshes.Reverse()) &&
                sourceBackgrounds.SequenceEqual(meshes.Reverse()),
                "The canonical source502 Background head contains the same two genuine OBJ native meshes as the renderer, in original newest-first order.");
            Assert(restartPlayerPosition == new FixedVector3(originalRestart.Position.X,
                originalRestart.Position.Y, originalRestart.Position.Z) << 12,
                "The real player reaches the authored Q12 restart position before gameplay starts.");

            GodotThugActor[] thugs = initial.OfType<GodotThugActor>().ToArray();
            FixedVector3[] positions = thugs.Select(actor => actor.Position).ToArray();
            int[] updates = thugs.Select(actor => actor.SourceUpdates).ToArray();
            int renderVisits = scene.PlayerBrightness.RenderVisits;
            uint playingClock = scene.SourceClock;
            bool moved = false;
            deadline.Restart();
            while (scene.SourceClock - playingClock < 120 || !moved || scene.PlayerBrightness.SurfaceContacts == 0)
            {
                EnsureHealthy();
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
                moved |= thugs.Select((actor, index) => actor.Position != positions[index]).Any(value => value);
                if (deadline.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("Genuine story player/enemy simulation did not advance after startup.");
            }
            Assert(thugs.Select((actor, index) => actor.SourceUpdates > updates[index]).All(value => value) &&
                scene.Registry.SourceUpdates > 0 && scene.SourceClock > playingClock && moved,
                "Only completed startup releases the actual source clock, actor-list update and original first-level thug movement/AI.");
            Assert(sourceBackgrounds.All(mesh => mesh.State.BodyFlags == 0x10) &&
                scene.Registry.SourceList(ActorUpdateGroup.Background).SourceOrder.SequenceEqual(sourceBackgrounds) &&
                scene.Rendering.Backdrops.Count == 2,
                "Actual Background source updates clear the original constructor flag4 while retaining both native source identities and avoiding duplicate render-side construction.");
            GodotPlayer litPlayer = scene.Player ?? throw new InvalidDataException("The actual source player disappeared before lighting verification.");
            Assert(scene.PlayerBrightness.SurfaceContacts > 0 && scene.PlayerBrightness.State.LightUpdates > 0 &&
                scene.PlayerBrightness.RenderVisits > renderVisits && scene.PlayerBrightness.LitPartCount == 18 &&
                scene.PlayerBrightness.VisibleLitPartCount == Enumerable.Range(0, litPlayer.Model.SourcePartCount).Count(litPlayer.Model.IsModelPartVisible) &&
                scene.Rendering.SourceDepth.RenderVisits > 0 &&
                scene.Rendering.SourceDepth.RenderScratch is { First: 300, Second: -30 },
                "Actual source surface contacts update player brightness, upload the retained eighteen-part render lighting and copy the authored signed OT globals into real render scratch. " +
                $"Contacts={scene.PlayerBrightness.SurfaceContacts}, lightUpdates={scene.PlayerBrightness.State.LightUpdates}, renderVisits={scene.PlayerBrightness.RenderVisits} (start{renderVisits}), parts={scene.PlayerBrightness.LitPartCount}, visibleParts={scene.PlayerBrightness.VisibleLitPartCount}, " +
                $"depthVisits={scene.Rendering.SourceDepth.RenderVisits}, scratch={scene.Rendering.SourceDepth.RenderScratch.First}/{scene.Rendering.SourceDepth.RenderScratch.Second}, globals={scene.Rendering.SourceDepth.Globals.First}/{scene.Rendering.SourceDepth.Globals.Second}.");
            scene.PlayerBrightness.VerifyUploads();
            PlayerBrightnessImportChecks.VerifyHiddenModelLighting(litPlayer, scene.PlayerBrightness);
            Assert(playersConstructed == 1 && gameplayStarts == 1 && scene.Startup.InitialActorsCreated == 23,
                "Live source frames cannot repeat the constructor census, player binding or startup activation.");
            AssertCompletedExactlyOnce(restartNode, restartProgram);

            GodotPlayer livePlayer = scene.Player ?? throw new InvalidDataException("The genuine startup player disappeared before teardown.");
            GodotPlayerCombatRuntime liveCombat = Descendants(scene).OfType<GodotPlayerCombatRuntime>().Single();
            GodotCombatSceneOwnership ownership = liveCombat.SceneOwnership;
            GodotActorSessionHandle retainedHandle = ownership.Actors.StoreHandle(thugs[0]);
            await (inputProbe ?? throw new InvalidDataException("Actual gameplay did not bind its native story-input observer.")).VerifyManualSkipAsync();
            await StoryItemTranslucencyImportChecks.VerifyAsync(scene);
            GodotPlayerWebRuntime effectRuntime = await StoryPlayerEffectImportChecks.VerifyAsync(scene);
            GodotScriptActor[] allocatedActors = Enum.GetValues<ActorUpdateGroup>().SelectMany(scene.Registry.SourceActors)
                .Concat(scene.Registry.SuspendedSourceList.SourceOrder.OfType<GodotScriptActor>()).Distinct().ToArray();
            Assert(allocatedActors.Length == scene.Registry.LiveCount && scene.SceneObjects.Deletions.Count == 0,
                "The actual teardown owner starts with the scene's genuine allocated active/suspended source bodies.");
            GodotLevelSceneObjects teardown = scene.SceneObjects;
            teardown.KillAllSceneObjects(LevelExitReason.RetryRequested);
            StoryPlayerEffectImportChecks.VerifyTeardown(scene, effectRuntime);
            Assert(teardown.IsDestroyed && teardown.ExitReason == LevelExitReason.RetryRequested &&
                !GodotObject.IsInstanceValid(livePlayer) && scene.Player is null && scene.Registry.Player is null &&
                allocatedActors.All(actor => !GodotObject.IsInstanceValid(actor)) &&
                sourceBackgrounds.All(mesh => !GodotObject.IsInstanceValid(mesh)) &&
                scene.Registry.LiveCount == 0 && scene.Registry.BaddyConstruction.State.Count == 0 &&
                Enum.GetValues<ActorUpdateGroup>().All(group => scene.Registry.SourceList(group).SourceOrder.Count == 0) &&
                scene.Registry.SuspendedSourceList.SourceOrder.Count == 0 && scene.Rendering.Backdrops.Count == 0 &&
                !scene.Registry.RequireControlServices().Compass.Active &&
                ownership.Particles.Count == 0 && ownership.Directory.SourceBodies.Count == 0 &&
                ownership.Actors.RecoverHandle(retainedHandle) is null,
                "Real Init_KillAll destroys the actual player, all allocated bodies, both source502 meshes and bits, releases every baddy lease and invalidates retained native handles without disposing the borrowed scene directory early.");
            ownership.RequireRegistry(scene.Registry);
            int deletionCount = teardown.Deletions.Count;
            Assert(deletionCount > 0 && teardown.Deletions.Select(deletion => deletion.Phase)
                .Zip(teardown.Deletions.Skip(1).Select(deletion => deletion.Phase), (first, next) => (int)first <= (int)next).All(value => value),
                "Actual native destructor records retain Init_KillAll's original source phase ordering.");
            teardown.KillAllSceneObjects(LevelExitReason.RetryRequested);
            uint terminatedClock = scene.SourceClock;
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            Assert(teardown.Deletions.Count == deletionCount && scene.SourceClock == terminatedClock &&
                ownership.Particles.Count == 0 && scene.Registry.LiveCount == 0,
                "Repeated genuine teardown invokes no second native destructor and leaves the actual source simulation stopped.");
            GD.Print($"Passed {checks} native story startup checks: real intro11,23 no-player constructors, original restart257/85/242, source302/502 backdrops, live AI and genuine once-only teardown.");
        }
        finally
        {
            inputProbe?.Dispose();
            scene.Registry.Spawned -= RecordSpawn;
            scene.Mission.Trace -= RecordTrace;
            scene.PlayerConstructed -= RecordPlayer;
            scene.GameplayStarted -= RecordGameplay;
        }

        void RecordSpawn(GodotScriptActor actor)
        {
            EnsureHealthy();
            if (scene.Startup.Phase == MissionStartupPhase.CreatingInitialActors)
            {
                Assert(scene.Player is null && scene.Registry.Player is null && scene.SourceClock == stoppedClock && actor.SourceUpdates == 0,
                    $"Original initial node{actor.SourceNodeId} is constructed without a player or advancing source AI/time.");
                initial.Add(actor);
            }
            else otherSpawns.Add((actor, scene.Startup.Phase));
        }
        void RecordTrace(MissionTrace trace)
        {
            traces.Add(trace);
            if (trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == autoexec.Id && trace.Opcode == 190)
            {
                Assert(scene.Cinema.IsPlaying && scene.Mission.IsBlocked && scene.Startup.Phase == MissionStartupPhase.Autoexec &&
                    scene.Player is null && scene.Registry.Player is null && scene.Registry.LiveCount == 0,
                    "Executed source190 starts actual intro playback and suspends autoexec before the constructor phase.");
                introBlocked = true;
            }
        }
        void RecordPlayer()
        {
            playersConstructed++;
            Assert(initial.Count == 23 && initial.Select(actor => actor.SourceNodeId).SequenceEqual(OriginalInitialNodes) &&
                scene.Player is not null && ReferenceEquals(scene.Registry.Player, scene.Player) &&
                scene.Startup.Phase == MissionStartupPhase.SelectingRestart && scene.SourceClock == stoppedClock &&
                scene.Registry.BaddyConstruction.State.Count == 23 &&
                initial.All(actor => actor.SourceUpdates == 0),
                "The real CPlayer is constructed only after the complete original constructor census, before restart/AI.");
            GodotControlActor compassOwner = initial.OfType<GodotControlActor>().Single(actor => actor.SourceNodeId == 70);
            GodotControlSceneServices controls = scene.Registry.RequireControlServices();
            ushort[] originalSoundNodes = [91, 92, 101, 102, 204, 237, 238];
            Assert((compassOwner.ScriptState.Flags & 0x80) != 0 && controls.Compass.Active &&
                controls.Compass.TargetPosition == compassOwner.Variables.Position >> 12 &&
                initial.OfType<GodotControlActor>().Where(actor => originalSoundNodes.Contains(actor.SourceNodeId))
                    .All(actor => actor.Effects.SoundHandle != 0 && actor.Effects.SoundHandle != uint.MaxValue && actor.Effects.SoundPositionPeriod == 3),
                "Pre-player source450C creates the genuine Panel compass, and all seven authored450B constructor sounds acquire real shared SFX handles and signed period3 before the player exists.");
        }
        void RecordGameplay()
        {
            gameplayStarts++;
            startupSpawns = otherSpawns.ToArray();
            Assert(scene.Startup.Phase == MissionStartupPhase.Playing && scene.Mission.PendingCount == 0 && !scene.Mission.IsBlocked &&
                scene.Player is not null && scene.SourceClock == stoppedClock && initial.All(actor => actor.SourceUpdates == 0),
                "Gameplay begins only after the genuine restart and linked command lists drain, with no earlier actor frame.");
            restartPlayerPosition = scene.Player?.Controller.Position;
            inputProbe = new(scene, Assert);
            sourceBackgrounds = scene.Registry.SourceList(ActorUpdateGroup.Background).SourceOrder.OfType<GodotSourceBackdrop>().ToArray();
            Assert(sourceBackgrounds.Length == 2 && sourceBackgrounds.All(mesh => mesh.State.BodyFlags == 0x14),
                "Both genuine source502 constructors reach Playing with initial CBody flags14, before their first source EveryFrame update.");
        }
        void AssertCompletedExactlyOnce(TriggerNode node, IReadOnlyList<TriggerInstruction> instructions)
        {
            var actual = traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == node.Id)
                .Select(trace => (trace.Opcode, trace.SourceOffset)).ToArray();
            var expected = instructions.Select(instruction => (Opcode: (ushort?)instruction.Opcode, instruction.SourceOffset)).ToArray();
            Assert(actual.SequenceEqual(expected),
                $"Node{node.Id} command completions must occur once in authored order. Expected {string.Join(",", expected.Select(command => $"{command.Opcode}@{command.SourceOffset:X}"))}; actual {string.Join(",", actual.Select(command => $"{command.Opcode}@{command.SourceOffset:X}"))}.");
        }
        void EnsureHealthy()
        {
            if (scene.Failure is { } failure) throw new InvalidDataException("Actual story scene stopped; required source services must be implemented.", failure);
            if (scene.Startup.Failure is { } startupFailure) throw new InvalidDataException("Actual story startup latched a source command failure.", startupFailure);
        }
        void Assert(bool condition, string evidence)
        {
            if (!condition) throw new InvalidDataException("Native story startup: " + evidence);
            checks++;
        }
    }

    private static ushort ReadWord(TriggerInstruction instruction) => BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
    private static string ReadString(TriggerInstruction instruction)
    {
        ReadOnlySpan<byte> bytes = instruction.Arguments.Span;
        int terminator = bytes.IndexOf((byte)0);
        if (terminator < 0) throw new InvalidDataException("Source startup name has no terminator.");
        return Encoding.Latin1.GetString(bytes[..terminator]);
    }
    private static uint ReadBackgroundChecksum(TriggerInstruction instruction)
    {
        int argumentAddress = instruction.SourceOffset + 2;
        int padding = (-argumentAddress) & 3;
        return BinaryPrimitives.ReadUInt32LittleEndian(instruction.Arguments.Span[padding..]);
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
