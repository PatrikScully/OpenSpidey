using System.Diagnostics;
using System.Text;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L1A4 mixed no-player census, death restart, source191 static vault policy and real live story phases.</summary>
internal static class StoryLevelFourSessionImportChecks
{
    private static readonly ushort[] InitialNodes = [19, 51, 54, 58, 59, 62, 63, 71, 78, 81, 82, 83,
        153, 154, 155, 156, 166, 200, 258, 260, 261, 262, 284, 295, 324, 325, 343, 346, 374];
    private static readonly ushort[] RestartOpcodes = [213, 104, 191, 170, 180, 180, 180, 180, 180, 180,
        166, 169, 191, 191, 191, 191, 191, 191, 191, 191, 191, 191, 191, 141, 302, 3];

    public static async Task VerifyAsync(StoryLevelSession story)
    {
        TriggerFile triggers = story.Registry.Triggers;
        TriggerNode autoexec = triggers.Nodes[0], restartNode = triggers.Nodes[1];
        RestartPoint restart = RestartPoint.Read(restartNode);
        IReadOnlyList<TriggerInstruction> autoProgram = TriggerCommandReader.Read(autoexec), restartProgram = TriggerCommandReader.Read(restartNode);
        var initial = new List<GodotScriptActor>();
        var later = new List<(GodotScriptActor Actor, MissionStartupPhase Phase)>();
        var traces = new List<MissionTrace>();
        var handles = new List<(GodotScriptActor Actor, GodotActorSessionHandle Handle)>();
        GodotEnemyRuntime enemies = Descendants(story).OfType<GodotEnemyRuntime>().Single();
        GodotLevelObjectRuntime objects = Descendants(story).OfType<GodotLevelObjectRuntime>().Single();
        GodotSwitchEnvironment? environment = null;
        GodotSwitchEnvironmentItem[] claimed = [];
        uint[] expectedClaims = [];
        (GodotScriptActor Actor, MissionStartupPhase Phase)[] startupBodies = [];
        ImportedLevelSource? source = null;
        ImportedSceneMesh? staticVault = null;
        Mesh? vaultMesh = null;
        ImportedCollisionBody[] vaultCollision = [];
        Transform3D vaultTransform = default;
        GodotPlayer? player = null;
        FixedVector3 restartPosition = default;
        int checks = 0, playerConstructions = 0, gameplayStarts = 0, startupLive = 0, startupBaddies = 0;
        Assert(story.Failure is null && story.Startup.Phase == MissionStartupPhase.Autoexec && story.SourceClock == 0 &&
            story.Player is null && story.Registry.Player is null && story.Registry.LiveCount == 0 &&
            !enemies.IsPlayerBound && !objects.IsPlayerBound && objects.PreparedEnvironment is null &&
            story.Application.MissionName.Equals("L1A4_T", StringComparison.OrdinalIgnoreCase) && story.Checkpoints.Save.RestartPointName.Length == 0,
            "Actual L1A4 launch begins before autoexec, lazy ENV claims, no-player constructors and original player/time.");
        Assert(autoProgram.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[]
                { 140, 147, 126, 126, 126, 126, 142, 126, 189, 189, 128, 202, 200, 132 }) &&
            autoProgram.Where(instruction => instruction.Opcode == 189).Select(Text).SequenceEqual(new[] { "hostage", "thug" }) &&
            autoProgram.Where(instruction => instruction.Opcode == 126).Select(Text)
                .SequenceEqual(new[] { "L1A4_L", "hostage", "THUG", "L1A4_O", "expgrnd" }) &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 140)) == "Re_Start_Death" &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 128)) == "L1A4_G",
            "Original autoexec0 retains exact protected/level bank and hostage/thug189 order, real expgrnd load, death restart and source132 speech policy, with no cinema.");
        Assert(restart is { NodeId: 1, Name: "Re_Start_Death" } && restart.Position == new TriggerPosition(627, -420, -2640) &&
            restart.Links.SequenceEqual(new ushort[] { 2, 130, 157, 327 }) &&
            restartProgram.Select(instruction => instruction.Opcode).SequenceEqual(RestartOpcodes) &&
            restartProgram.Count(instruction => instruction.Opcode == 191) == 12,
            "Untouched restart1 retains exact position,2/130/157/327 pulse order and every original command, including all twelve named static visibility operations.");
        story.Registry.Spawned += OnSpawn; story.Mission.Trace += OnTrace;
        story.PlayerConstructed += OnPlayer; story.GameplayStarted += OnGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                Healthy();
                Assert(story.SourceClock == 0 && story.Registry.SourceUpdates == 0 && initial.All(actor => actor.SourceUpdates == 0) && !story.Cinema.IsPlaying,
                    "Real L1A4 autoexec/census/restart advances no actor/player AI or source clock and creates no intro barrier.");
                if (deadline.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Genuine L1A4 startup did not reach Playing.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Healthy();
            GodotPlayer actualPlayer = player ?? throw new InvalidDataException("Actual L1A4 player construction was not observed.");
            GodotPlayerScripts scripts = actualPlayer.Scripts ?? throw new InvalidDataException("Actual L1A4 player VM ownership is missing.");
            ImportedSceneMesh actualVault = staticVault ?? throw new InvalidDataException("The actual static G155 vault model is missing.");
            ImportedLevelSource environmentSource = source ?? throw new InvalidDataException("The actual L1A4 G-region is missing.");
            Assert(playerConstructions == 1 && gameplayStarts == 1 && story.Startup.Phase == MissionStartupPhase.Playing &&
                story.Startup.InitialActorsCreated == 29 && initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) &&
                initial.OfType<GodotHostageActor>().Count() == 2 && initial.OfType<GodotPlatformActor>().Count() == 2 &&
                initial.OfType<GodotManipulationActor>().Count() == 11 && initial.OfType<GodotControlActor>().Count() == 12 &&
                initial.OfType<GodotSwitchActor>().Count() == 2 && startupLive == 32 && startupBaddies == 19,
                "Original29 census/16 CBaddy leases precedes one CPlayer and three real restart controls; plain401/407 remain outside source NumBaddies. " + Diagnostics());
            Assert(enemies.IsPlayerBound && objects.IsPlayerBound && ReferenceEquals(environment, objects.PreparedEnvironment) &&
                claimed.SequenceEqual(objects.Environment.Items) && claimed.Select(item => item.State.ModelChecksum).Order().SequenceEqual(expectedClaims) &&
                handles.All(entry => ReferenceEquals(enemies.SceneOwnership.Actors.RecoverHandle(entry.Handle), entry.Actor)) &&
                initial.OfType<GodotSwitchActor>().All(actor => enemies.SceneOwnership.Directory.SourceControls.Contains(actor)),
                "Actual post-census binding retains every401/407/native handle and the same ENV instances, matching only existing source shadow/switch checksums. " +
                $"claims=[{string.Join(',', claimed.Select(item => item.State.ModelChecksum.ToString("X8")))}], expected=[{string.Join(',', expectedClaims.Select(value => value.ToString("X8")))}]. " + Diagnostics());
            Assert(startupBodies.Select(entry => entry.Actor.SourceNodeId).SequenceEqual(new ushort[] { 130, 157, 327 }) &&
                startupBodies.All(entry => entry.Phase == MissionStartupPhase.Restart) &&
                traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 2) == 1 &&
                traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 331) == 1,
                "Original linked2 is a command point,130/157/327 are real constructors, and327's genuine42B4 queues331 before the startup barrier opens. " + Diagnostics());
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram);
            CompletedOnce(triggers.Nodes[2], TriggerCommandReader.Read(triggers.Nodes[2]));
            CompletedOnce(triggers.Nodes[331], TriggerCommandReader.Read(triggers.Nodes[331]));
            Assert(story.Checkpoints.Current is { NodeId: 1, Name: "Re_Start_Death" } && story.Application.EndLevelNode == 197 &&
                story.Registry.SuspensionDistance == 35000 && story.Rendering.SourceDepth.Globals is { First: 500, Second: -30 } &&
                story.PlayerBrightness.State.DarkSurfaceTarget == 160 &&
                restartPosition == new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12 && story.Rendering.Backdrops.Count == 0,
                "Authored restart applies end197, suspension35000, OT500/−30 and exact placement; no184/171 overwrites the original player brightness160 or invents backdrops.");
            Assert(ReferenceEquals(actualVault.Mesh, vaultMesh) && !actualVault.Visible && actualVault.Transform == vaultTransform &&
                environmentSource.Scene.Models[155].Checksum == 0x2A023428 && environmentSource.Scene.Models[155].Flags == 8 &&
                vaultCollision.All(body => body.SourceFlags == 1 && body.CollisionLayer == 0) &&
                !story.Registry.SourceActors(ActorUpdateGroup.Environment).Any(actor => actor.SourceNodeId == 45),
                "Actual restart191 hides the static G155 VaultDoor01 duplicate and collision while retaining its original mesh/transform/model metadata; independent dynamic45 remains unconstructed until authored gameplay requests it.");
            deadline.Restart();
            while (story.SourceClock < 120)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Actual L1A4 source frames did not advance120 ticks.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Healthy();
            Assert(actualPlayer.SourceUpdates > 0 && story.Registry.SourceUpdates > 0 && actualPlayer.CameraStartup is { State.Initialized: 1 } &&
                initial.OfType<GodotHostageActor>().All(actor => actor.SourceUpdates > 0 && actor.FrameTiming.AccumulatedTicks > 0 &&
                    actor.State.State != HostageBehaviorState.Initializing) &&
                initial.OfType<GodotManipulationActor>().All(actor => actor.SourceUpdates > 0) &&
                initial.OfType<GodotSwitchActor>().All(actor => actor.SourceUpdates > 0),
                "Playing runs actual player305/401/407/world phases through canonical shared timing instead of leaving the L1A4 scene as a static imported map. " + Diagnostics());
            TriggerInstruction originalInput = TriggerCommandReader.Read(triggers.Nodes[289]).Single(instruction => instruction.Opcode == 199);
            PlayerScriptProgram input = PlayerScriptProgram.Read(originalInput.Arguments.Span, originalInput.SourceOffset + 2);
            Assert(traces.Any(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == 289 && trace.Opcode == 199) &&
                scripts.CurrentProgram is { } current && current.Instructions.SequenceEqual(input.Instructions) && scripts.ProgramsStarted == 1 &&
                later.Any(entry => entry.Actor is GodotThugActor { SourceNodeId: 9, State.Health: 50 }) &&
                later.Any(entry => entry.Actor is GodotPowerUpActor { SourceNodeId: 158, Type: 8 }) &&
                later.Any(entry => entry.Actor is GodotPowerUpActor { SourceNodeId: 159, Type: 15 }),
                "Real130/157 completion executes untouched289 forward-pad program, genuine thug9 and original pickups158/159 through actual mission/factory owners. " + Diagnostics());
            foreach (GodotHostageActor hostage in initial.OfType<GodotHostageActor>()) hostage.Model.VerifyCurrentPose();
            actualPlayer.Model.VerifyCurrentPose(); story.PlayerBrightness.VerifyUploads();
            Assert(story.PlayerBrightness.RenderVisits > 0 && story.PlayerBrightness.LitPartCount == 18 &&
                story.Rendering.SourceDepth.RenderScratch is { First: 500, Second: -30 } && !actualVault.Visible &&
                ReferenceEquals(actualVault.Mesh, vaultMesh) && story.Startup.InitialActorsCreated == 29 && playerConstructions == 1 && gameplayStarts == 1,
                "Real L1A4 rendering retains all18 player lighting parts, original OT scratch and persistent hidden static vault mesh without repeating startup.");
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram);
            await ActorCameraShakeImportChecks.VerifyAsync(story, (GodotActorCameraShakeHost)story.Registry.CameraShake!);
            GodotScriptActor[] allocated = Enum.GetValues<ActorUpdateGroup>().SelectMany(story.Registry.SourceActors)
                .Concat(story.Registry.SuspendedSourceList.SourceOrder.OfType<GodotScriptActor>()).Distinct().ToArray();
            story.SceneObjects.KillAllSceneObjects(LevelExitReason.RetryRequested);
            Assert(story.SceneObjects.IsDestroyed && story.Player is null && story.Registry.Player is null && !GodotObject.IsInstanceValid(actualPlayer) &&
                allocated.All(actor => !GodotObject.IsInstanceValid(actor)) && story.Registry.LiveCount == 0 && story.Registry.BaddyConstruction.State.Count == 0 &&
                Enum.GetValues<ActorUpdateGroup>().All(group => story.Registry.SourceList(group).Count == 0) &&
                enemies.SceneOwnership.Directory.SourceBodies.Count == 0 && enemies.SceneOwnership.Directory.SourceControls.Count == 0,
                "Actual L1A4 Init_KillAll destroys its player and all mixed active/suspended bodies, leaving no source-list identity or constructor lease.");
            enemies.Modules.UnloadAllCodeModules();
            Assert(enemies.Modules.Modules.SourceOrder.Count == 0, "Genuine fixed-order source module unload clears L1A4 hostage/thug after scene bodies are destroyed.");
            GD.Print($"Passed {checks} actual L1A4 story checks: original autoexec0,29 no-player bodies/16 leases, restart1/42B4/191 static vault, genuine289 input/NPC/pickup/source frames and teardown.");
        }
        finally
        {
            story.Registry.Spawned -= OnSpawn; story.Mission.Trace -= OnTrace;
            story.PlayerConstructed -= OnPlayer; story.GameplayStarted -= OnGameplay;
        }

        void OnSpawn(GodotScriptActor actor)
        {
            if (story.Startup.Phase != MissionStartupPhase.CreatingInitialActors) { later.Add((actor, story.Startup.Phase)); return; }
            Assert(story.Player is null && story.Registry.Player is null && !enemies.IsPlayerBound && !objects.IsPlayerBound &&
                story.SourceClock == 0 && actor.SourceUpdates == 0, $"Actual L1A4 initial node {actor.SourceNodeId}/{actor.SourceActorType} observes no player/AI.");
            initial.Add(actor);
            if (actor is GodotHostageActor or GodotManipulationActor or GodotSwitchActor)
                handles.Add((actor, enemies.SceneOwnership.Actors.StoreHandle(actor)));
            if (actor is GodotSwitchActor) environment ??= objects.PreparedEnvironment ?? throw new InvalidDataException("Actual407 lacks its genuine ENV owner.");
        }
        void OnTrace(MissionTrace trace) => traces.Add(trace);
        void OnPlayer()
        {
            playerConstructions++;
            Assert(initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) && story.Registry.LiveCount == 29 &&
                story.Registry.BaddyConstruction.State.Count == 16 && initial.All(actor => actor.SourceUpdates == 0) &&
                !enemies.IsPlayerBound && !objects.IsPlayerBound && story.Startup.Phase == MissionStartupPhase.SelectingRestart,
                "Only the completed29 census constructs the genuine CPlayer after16 CBaddy leases and before binding/restart/AI.");
            GodotHostageActor[] hostages = initial.OfType<GodotHostageActor>().ToArray();
            Assert(hostages.Select(actor => actor.SourceNodeId).SequenceEqual(new ushort[] { 19, 71 }) &&
                hostages.Select(actor => actor.State.SourceConstructionOrdinal).SequenceEqual(new[] { 0, 6 }) &&
                enemies.Modules.Modules.SourceOrder.Select(module => module.Name).SequenceEqual(new[] { "thug", "hostage" }),
                "Original L1A4 hostage19/71 keep postincrement ordinals0/6 and actual189 head order thug/hostage.");
            ImportedLevelWorld world = Descendants(story).OfType<ImportedLevelWorld>().Single();
            ImportedLevelSource currentSource = world.Sources.Single(candidate => candidate.AssetPath.EndsWith("L1A4_G.psx", StringComparison.OrdinalIgnoreCase));
            source = currentSource;
            staticVault = world.Geometry.GetChildren().SelectMany(group => group.GetChildren()).OfType<ImportedSceneMesh>()
                .Single(mesh => mesh.SourceInstanceIndex == 155 && mesh.GetParent().Name.ToString().Equals("L1A4_G", StringComparison.OrdinalIgnoreCase));
            vaultCollision = world.Collision.GetChildren().SelectMany(group => group.GetChildren()).OfType<ImportedCollisionBody>()
                .Where(body => string.Equals(body.SourceAsset, currentSource.AssetPath, StringComparison.OrdinalIgnoreCase) && body.InstanceIndex == 155).ToArray();
            vaultMesh = staticVault.Mesh; vaultTransform = staticVault.Transform;
            Assert(currentSource.Scene.Instances.Count == 254 && currentSource.Scene.Models[155].Checksum == 0x2A023428 &&
                staticVault.Visible && vaultMesh is not null && vaultMesh.GetSurfaceCount() > 0 && vaultCollision.Length > 0 &&
                vaultCollision.All(body => body.SourceFlags == 0 && body.CollisionLayer != 0),
                "Before restart the original static G155 vault duplicate owns visible geometry and real collision, ready for the exact authored191 hide policy.");
            claimed = objects.Environment.Items.ToArray();
            HashSet<uint> checksums = currentSource.Scene.Models.Select(model => model.Checksum).OfType<uint>().ToHashSet();
            var expected = new HashSet<uint>();
            foreach (GodotScriptActor actor in initial.Where(actor => actor is GodotSwitchActor or GodotManipulationActor))
            {
                TriggerActorDefinition definition = actor.Definition;
                var cursor = new ActorScriptCursor(definition.BehaviorData,
                    checked(triggers.Nodes[definition.NodeId].SourceOffset + definition.ObjectDataOffset + 18));
                uint first = cursor.ReadAlignedDword(), second = cursor.ReadAlignedDword();
                if (actor is GodotSwitchActor) { expected.Add(first); expected.Add(second); }
                else if (second != 0 && checksums.Contains(second)) expected.Add(second);
            }
            expectedClaims = expected.Order().ToArray();
        }
        void OnGameplay()
        {
            gameplayStarts++; player = story.Player; startupBodies = later.ToArray();
            startupLive = story.Registry.LiveCount; startupBaddies = story.Registry.BaddyConstruction.State.Count;
            restartPosition = player?.Controller.Position ?? throw new InvalidDataException("Actual L1A4 gameplay started without CPlayer.");
            Assert(story.SourceClock == 0 && story.Registry.SourceUpdates == 0 && startupBodies.All(entry => entry.Actor.SourceUpdates == 0),
                "The original restart/2/331 lists drain before any genuine L1A4 source world or player simulation.");
        }
        void CompletedOnce(TriggerNode node, IReadOnlyList<TriggerInstruction> expected)
        {
            MissionTrace[] completed = traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == node.Id).ToArray();
            Assert(completed.Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(expected.Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset))),
                $"Original node {node.Id} retains every authored instruction/order exactly once. " + Diagnostics());
        }
        string Diagnostics() => $"phase={story.Startup.Phase}, clock={story.SourceClock}, live={story.Registry.LiveCount}, baddies={story.Registry.BaddyConstruction.State.Count}, " +
            $"initial=[{string.Join(',', initial.Select(actor => $"{actor.SourceNodeId}/{actor.SourceActorType}"))}], later=[{string.Join(',', later.Select(entry => $"{entry.Actor.Definition.NodeId}/{entry.Actor.SourceActorType}/{entry.Phase}"))}], " +
            $"traces=[{string.Join(',', traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted).Select(trace => $"{trace.NodeId}/{trace.Opcode}@{trace.SourceOffset:X}"))}].";
        void Healthy()
        {
            if (story.Failure is { } failure) throw new InvalidDataException("Actual L1A4 production dependency failed.", failure);
            if (story.Startup.Failure is { } startupFailure) throw new InvalidDataException("Actual L1A4 startup failed.", startupFailure);
        }
        void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException("Native L1A4 story: " + evidence); checks++; }
    }

    private static string Text(TriggerInstruction instruction)
    {
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException("Original string has no terminator.");
        return Encoding.Latin1.GetString(instruction.Arguments.Span[..end]);
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
