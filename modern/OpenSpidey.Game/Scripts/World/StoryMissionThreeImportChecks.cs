using System.Diagnostics;
using System.Text;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L1A3 startup: original modules, mixed no-player census, persistent source401/407 and real restart/AI.</summary>
internal static class StoryMissionThreeImportChecks
{
    private static readonly ushort[] InitialNodes = [8, 10, 12, 13, 43, 45, 47, 53, 80, 81, 82, 109, 113, 122,
        127, 128, 129, 157, 160, 176, 177, 178, 192, 193, 194, 195, 215, 216, 217, 218, 219, 220, 221,
        229, 231, 272, 273, 274, 275, 279, 280, 281, 282, 283, 291, 297, 304, 307, 322, 384, 433, 438, 443, 446, 447, 451];
    private static readonly ushort[] RestartOpcodes = [213, 104, 141, 183, 183, 166, 169, 184, 191, 191, 191, 191, 191, 160, 302, 3];

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
        GodotSwitchEnvironment? initialEnvironment = null;
        GodotSwitchEnvironmentItem[] claimed = [];
        uint[] expectedClaimChecksums = [];
        (GodotScriptActor Actor, MissionStartupPhase Phase)[] startupBodies = [];
        GodotPlayer? player = null;
        FixedVector3 restartPosition = default;
        int checks = 0, playerConstructions = 0, gameplayStarts = 0, startupLive = 0, startupBaddies = 0;
        uint initialClock = story.SourceClock;
        Assert(story.Failure is null && story.Startup.Phase == MissionStartupPhase.Autoexec && initialClock == 0 &&
            story.Registry.Player is null && story.Player is null && story.Registry.LiveCount == 0 &&
            !enemies.IsPlayerBound && !objects.IsPlayerBound && objects.PreparedEnvironment is null &&
            story.Application.MissionName.Equals("L1A3_T", StringComparison.OrdinalIgnoreCase) && story.Checkpoints.Save.RestartPointName.Length == 0,
            "Actual L1A3 launch attaches before autoexec, lazy ENV claims, all constructors and the real player/clock; prepared factories require no substitute player.");
        Assert(autoProgram.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[]
                { 140, 147, 126, 126, 126, 126, 142, 189, 189, 202, 200, 128 }) &&
            autoProgram.Where(instruction => instruction.Opcode == 189).Select(Text).SequenceEqual(new[] { "thug", "hostage" }) &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 140)) == "Re_Start_Death" &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 128)) == "L1A3_G",
            "Original autoexec0 retains exact library/module order, genuine thug/hostage189, saved death restart and ENV load; it requests no cinema.");
        Assert(restart is { NodeId: 1, Name: "Re_Start_Death" } && restart.Position == new TriggerPosition(-7413, -977, -1681) &&
            restart.Links.SequenceEqual(new ushort[] { 66, 132, 134, 214, 165, 301, 395 }) &&
            restartProgram.Select(instruction => instruction.Opcode).SequenceEqual(RestartOpcodes),
            "Untouched restart1 retains exact position, source183 pair and66/132/134/214/165/301/395 pulse order, with no replacement startup program.");
        story.Registry.Spawned += OnSpawn; story.Mission.Trace += OnTrace;
        story.PlayerConstructed += OnPlayer; story.GameplayStarted += OnGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                Healthy();
                Assert(story.SourceClock == initialClock && story.Registry.SourceUpdates == 0 && initial.All(actor => actor.SourceUpdates == 0) && !story.Cinema.IsPlaying,
                    "L1A3 original autoexec/census/restart keeps real time/AI stopped and adds no invented movie barrier.");
                if (deadline.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Genuine L1A3 startup did not reach Playing.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Healthy();
            GodotPlayer actualPlayer = player ?? throw new InvalidDataException("Original L1A3 player construction was not observed.");
            GodotPlayerScripts scripts = actualPlayer.Scripts ?? throw new InvalidDataException("The actual L1A3 player VM owner is missing.");
            Assert(playerConstructions == 1 && gameplayStarts == 1 && story.Startup.Phase == MissionStartupPhase.Playing &&
                story.Startup.InitialActorsCreated == 56 && initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) &&
                initial.OfType<GodotThugActor>().Count() == 5 && initial.OfType<GodotHostageActor>().Count() == 2 &&
                initial.OfType<GodotControlActor>().Count() == 23 && initial.OfType<GodotPlatformActor>().Count() == 9 &&
                initial.OfType<GodotSwitchActor>().Count() == 3 && initial.OfType<GodotManipulationActor>().Count() == 14 &&
                startupLive == 64 && startupBaddies == 46,
                "Actual56-actor census constructs every original family once before CPlayer, then eight real restart bodies;39 initial and46 final CBaddy leases exclude all plain401/407/pickup bodies. " + Diagnostics());
            Assert(enemies.IsPlayerBound && objects.IsPlayerBound && ReferenceEquals(initialEnvironment, objects.PreparedEnvironment) &&
                claimed.SequenceEqual(objects.Environment.Items),
                "Post-census player binding preserves the same lazy ENV owner and every original claimed instance. " +
                $"bound={enemies.IsPlayerBound}/{objects.IsPlayerBound}, sameENV={ReferenceEquals(initialEnvironment, objects.PreparedEnvironment)}, " +
                $"initialClaims={claimed.Length}, currentClaims={objects.Environment.Items.Count}. " + Diagnostics());
            Assert(claimed.Length == 17 && claimed.Select(item => item.State.ModelChecksum).Order().SequenceEqual(expectedClaimChecksums) &&
                initial.OfType<GodotManipulationActor>().All(actor => actor.SourceNodeId is 272 or 273 or 274
                    ? actor.State.ShadowItem is null
                    : actor.State.ShadowItem is { } shadow && claimed.Any(item => ReferenceEquals(item.State, shadow))) &&
                initial.OfType<GodotSwitchActor>().All(actor => actor.State.OffItem is { } off && actor.State.OnItem is { } on &&
                    claimed.Any(item => ReferenceEquals(item.State, off)) && claimed.Any(item => ReferenceEquals(item.State, on))),
                "Six authored407 off/on instances and eleven existing401 shadows retain all17 real ENV identities; original272/273/274 keep their genuinely absent3DED6E2A shadow null. " +
                $"expected=[{string.Join(',', expectedClaimChecksums.Select(value => value.ToString("X8")))}], " +
                $"actual=[{string.Join(',', claimed.Select(item => item.State.ModelChecksum.ToString("X8")))}]. " + Diagnostics());
            Assert(handles.All(entry => ReferenceEquals(enemies.SceneOwnership.Actors.RecoverHandle(entry.Handle), entry.Actor)) &&
                initial.OfType<GodotSwitchActor>().All(actor => enemies.SceneOwnership.Directory.SourceControls.Contains(actor)) &&
                initial.OfType<GodotManipulationActor>().All(actor => actor.ModelIndex >= 0 && actor.Colliders.Count > 0),
                "The same14 source401,3 switches and genuine native handles preserve original model/collision/list ownership across player construction. " +
                $"handles=[{string.Join(',', handles.Select(entry => $"{entry.Actor.SourceNodeId}:{ReferenceEquals(enemies.SceneOwnership.Actors.RecoverHandle(entry.Handle), entry.Actor)}"))}], " +
                $"items=[{string.Join(',', initial.OfType<GodotManipulationActor>().Select(actor => $"{actor.SourceNodeId}:model{actor.ModelIndex}/colliders{actor.Colliders.Count}"))}]. " + Diagnostics());
            Assert(startupBodies.Select(entry => entry.Actor.SourceNodeId).SequenceEqual(new ushort[] { 66, 132, 134, 214, 165, 301, 402, 395 }) &&
                startupBodies.All(entry => entry.Phase == MissionStartupPhase.Restart) &&
                startupBodies.Single(entry => entry.Actor.SourceNodeId == 301).Actor is GodotPowerUpActor { Type: 11 } &&
                startupBodies.Single(entry => entry.Actor.SourceNodeId == 402).Actor is GodotControlActor { ScriptState.WaitTicks: 8 },
                "Original restart3 creates authored pickup301 and control395's actual42B4 immediately constructs402/wait8 before395 constructor completion; no fake pulse or extra AI. " + Diagnostics());
            GodotPlayerLookaroundRuntime lookaround = actualPlayer.Controller.Lookaround as GodotPlayerLookaroundRuntime ??
                throw new InvalidDataException("Actual183 requires the genuine player lookaround owner.");
            Assert(lookaround.Aim.State is { FloorPitch: 100, CeilingPitch: -800, WallPitch: 170 } &&
                ReferenceEquals(lookaround.Camera.State.Angles, lookaround.Aim.State) &&
                story.Checkpoints.Current is { NodeId: 1, Name: "Re_Start_Death" } && story.Application.EndLevelNode == 294 &&
                story.PlayerBrightness.State.DarkSurfaceTarget == 250 && story.Rendering.SourceDepth.Globals is { First: 60, Second: -32 } &&
                restartPosition == new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12,
                "Authored restart183 writes floor100/ceiling−800 into canonical lookaround fields, preserving wall170;302/end294,184/250, OT60/−32 and exact spawn position reach the actual player/render owners.");
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram);
            Assert(story.Rendering.Backdrops.Count == 0 && traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 1) == 1,
                "L1A3 has no invented502 backgrounds and queues its untouched death restart exactly once.");
            deadline.Restart();
            while (story.SourceClock < 120)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Original L1A3 source phases did not advance120 ticks.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Healthy();
            GodotHostageActor[] hostages = initial.OfType<GodotHostageActor>().ToArray();
            Assert(story.Registry.SourceUpdates > 0 && actualPlayer.SourceUpdates > 0 && actualPlayer.CameraStartup is { State.Initialized: 1 } &&
                hostages.All(actor => actor.SourceUpdates > 0 && actor.FrameTiming.AccumulatedTicks > 0 && actor.State.State != HostageBehaviorState.Initializing) &&
                initial.OfType<GodotThugActor>().All(actor => actor.SourceUpdates > 0 && actor.FrameTiming.AccumulatedTicks > 0) &&
                initial.OfType<GodotManipulationActor>().All(actor => actor.SourceUpdates > 0) &&
                initial.OfType<GodotSwitchActor>().All(actor => actor.SourceUpdates > 0),
                "Playing releases actual player, thug, hostage, switch and manipulation phases under the shared source clock; genuine305 initializes its ground/state after binding. " + Diagnostics());
            TriggerInstruction authoredInput = TriggerCommandReader.Read(triggers.Nodes[234]).Single(instruction => instruction.Opcode == 199);
            PlayerScriptProgram input = PlayerScriptProgram.Read(authoredInput.Arguments.Span, authoredInput.SourceOffset + 2);
            Assert(traces.Any(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == 234 && trace.Opcode == 199) &&
                scripts.CurrentProgram is { } current && current.Instructions.SequenceEqual(input.Instructions) && scripts.ProgramsStarted == 1,
                "Real control214 completion reaches original234/199 and its untouched scheduled forward/Zip physical-pad script through genuine mission events. " + Diagnostics());
            foreach (GodotHostageActor hostage in hostages) hostage.Model.VerifyCurrentPose();
            foreach (GodotThugActor thug in initial.OfType<GodotThugActor>()) thug.Model.VerifyCurrentPose();
            actualPlayer.Model.VerifyCurrentPose(); story.PlayerBrightness.VerifyUploads();
            Assert(story.PlayerBrightness.RenderVisits > 0 && story.PlayerBrightness.LitPartCount == 18 &&
                story.Rendering.SourceDepth.RenderScratch is { First: 60, Second: -32 } &&
                playerConstructions == 1 && gameplayStarts == 1 && story.Startup.InitialActorsCreated == 56,
                "Actual source render visits light all18 original player parts with L1A3 OT scratch and never repeat startup constructors.");
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram);
            await HostageMissionSignalImportChecks.VerifyAsync(story, Assert);
            await ActorScreenFlashImportChecks.VerifyAsync(story);
            await SparkImportChecks.VerifyAsync(story);
            GodotScriptActor[] allocated = Enum.GetValues<ActorUpdateGroup>().SelectMany(story.Registry.SourceActors)
                .Concat(story.Registry.SuspendedSourceList.SourceOrder.OfType<GodotScriptActor>()).Distinct().ToArray();
            story.SceneObjects.KillAllSceneObjects(LevelExitReason.RetryRequested);
            Assert(story.SceneObjects.IsDestroyed && story.Player is null && story.Registry.Player is null && !GodotObject.IsInstanceValid(actualPlayer) &&
                allocated.All(actor => !GodotObject.IsInstanceValid(actor)) && story.Registry.LiveCount == 0 && story.Registry.BaddyConstruction.State.Count == 0 &&
                Enum.GetValues<ActorUpdateGroup>().All(group => story.Registry.SourceList(group).Count == 0) &&
                enemies.SceneOwnership.Directory.SourceBodies.Count == 0 && enemies.SceneOwnership.Directory.SourceControls.Count == 0,
                "Actual L1A3 Init_KillAll frees its mixed active/suspended native bodies and player, all305/304 shadows/handles and each genuine CBaddy lease.");
            enemies.Modules.UnloadAllCodeModules();
            Assert(enemies.Modules.Modules.SourceOrder.Count == 0, "The genuine fixed-order code-module unload clears actual thug/hostage records after native scene bodies are destroyed.");
            GD.Print($"Passed {checks} actual L1A3 story checks: original autoexec0/hostage189,56 no-player constructors, retained401/407/17 ENV claims, restart1/183/42B4, real234 physical-input VM/305 AI/render and genuine teardown.");
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
                story.SourceClock == 0 && actor.SourceUpdates == 0,
                $"Actual L1A3 node {actor.SourceNodeId}/{actor.SourceActorType} constructor sees no CPlayer or source AI.");
            initial.Add(actor);
            if (actor is GodotHostageActor hostage)
            {
                var empty = new OpenSpidey.Core.Actors.Scripting.ActorScriptCursor(ReadOnlyMemory<byte>.Empty);
                Assert(enemies.Hostages.SourcePlayer is null && hostage.Type == 305 && hostage.Model.SourcePartCount == 15 &&
                    hostage.State.State == HostageBehaviorState.Initializing && (hostage.Actor.BehaviorFlags & 0x02000000) != 0 &&
                    hostage.Variables.Read(0x2132, empty) == 8191 && hostage.Variables.Read(0x2133, empty) == 0 &&
                    hostage.Variables.Read(0x2150, empty) == 0 && hostage.Variables.Read(0x2151, empty) == 0 && hostage.Variables.Read(0x2152, empty) == 0,
                    "Genuine305 constructor attaches its real male model/shadow and parses470F before type initialization, preserving exact null-player common variables.");
            }
            if (actor is GodotHostageActor or GodotManipulationActor or GodotSwitchActor)
                handles.Add((actor, enemies.SceneOwnership.Actors.StoreHandle(actor)));
            if (actor is GodotSwitchActor)
            {
                initialEnvironment ??= objects.PreparedEnvironment ?? throw new InvalidDataException("Actual407 did not create its lazy source ENV owner.");
                Assert(ReferenceEquals(initialEnvironment, objects.PreparedEnvironment), "Every no-player407 shares the same genuine ENV lookup/claim owner.");
            }
        }
        void OnTrace(MissionTrace trace) => traces.Add(trace);
        void OnPlayer()
        {
            playerConstructions++;
            Assert(initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) && story.Registry.LiveCount == 56 &&
                story.Registry.BaddyConstruction.State.Count == 39 && initial.All(actor => actor.SourceUpdates == 0) &&
                !enemies.IsPlayerBound && !objects.IsPlayerBound && story.Startup.Phase == MissionStartupPhase.SelectingRestart &&
                ReferenceEquals(story.Player, story.Registry.Player),
                "Only the exact completed56 census constructs CPlayer; native owners remain unbound with39 CBaddy leases until genuine post-census composition.");
            GodotHostageActor[] hostages = initial.OfType<GodotHostageActor>().ToArray();
            Assert(hostages.Select(actor => actor.SourceNodeId).SequenceEqual(new ushort[] { 53, 160 }) &&
                hostages.Select(actor => actor.State.SourceConstructionOrdinal).SequenceEqual(new[] { 7, 18 }) &&
                hostages[0].SourceDepth is { First: 100, Second: 100 } &&
                enemies.Modules.Modules.SourceOrder.Select(module => module.Name).SequenceEqual(new[] { "hostage", "thug" }),
                "Actual189 insertion order is hostage/thug;305 retains postincrement ordinals7/18 and census433 writes raw100/100 to original53 CSuper fields.");
            claimed = objects.Environment.Items.ToArray();
            HashSet<uint> environmentChecksums = Descendants(story).OfType<ImportedLevelWorld>().Single().Sources
                .Single(source => source.AssetPath.EndsWith("L1A3_G.psx", StringComparison.OrdinalIgnoreCase)).Scene.Models
                .Select(model => model.Checksum).OfType<uint>().ToHashSet();
            var expectedClaims = new HashSet<uint>();
            foreach (GodotScriptActor actor in initial.Where(actor => actor is GodotSwitchActor or GodotManipulationActor))
            {
                TriggerActorDefinition definition = actor.Definition;
                var cursor = new OpenSpidey.Core.Actors.Scripting.ActorScriptCursor(definition.BehaviorData,
                    checked(triggers.Nodes[definition.NodeId].SourceOffset + definition.ObjectDataOffset + 18));
                uint first = cursor.ReadAlignedDword(), second = cursor.ReadAlignedDword();
                if (actor is GodotSwitchActor) { expectedClaims.Add(first); expectedClaims.Add(second); }
                else if (second != 0)
                {
                    if (environmentChecksums.Contains(second)) expectedClaims.Add(second);
                    if (actor.SourceNodeId is 272 or 273 or 274)
                        Assert(second == 0x3DED6E2A && !environmentChecksums.Contains(second) &&
                            actor is GodotManipulationActor { State.ShadowItem: null },
                            "Untouched manhole272/273/274 references absent ENV checksum3DED6E2A; actual source lookup returns null without inventing a shadow instance.");
                }
            }
            expectedClaimChecksums = expectedClaims.Order().ToArray();
        }
        void OnGameplay()
        {
            gameplayStarts++; player = story.Player; startupBodies = later.ToArray();
            startupLive = story.Registry.LiveCount; startupBaddies = story.Registry.BaddyConstruction.State.Count;
            restartPosition = player?.Controller.Position ?? throw new InvalidDataException("Actual L1A3 Playing has no CPlayer.");
            Assert(story.SourceClock == 0 && story.Registry.SourceUpdates == 0 && startupBodies.All(entry => entry.Actor.SourceUpdates == 0),
                "Real L1A3 restart commands and nested42B4 constructors complete before the first source world/player simulation phase.");
        }
        void CompletedOnce(TriggerNode node, IReadOnlyList<TriggerInstruction> expected)
        {
            MissionTrace[] completed = traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == node.Id).ToArray();
            Assert(completed.Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(expected.Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset))),
                $"Original node {node.Id} executes every authored command/order exactly once. " + Diagnostics());
        }
        string Diagnostics() => $"phase={story.Startup.Phase}, clock={story.SourceClock}, live={story.Registry.LiveCount}, baddies={story.Registry.BaddyConstruction.State.Count}, " +
            $"initial=[{string.Join(',', initial.Select(actor => $"{actor.SourceNodeId}/{actor.SourceActorType}"))}], " +
            $"later=[{string.Join(',', later.Select(entry => $"{entry.Actor.Definition.NodeId}/{entry.Actor.SourceActorType}/{entry.Phase}"))}], " +
            $"traces=[{string.Join(',', traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted).Select(trace => $"{trace.NodeId}/{trace.Opcode}@{trace.SourceOffset:X}"))}].";
        void Healthy()
        {
            if (story.Failure is { } failure) throw new InvalidDataException("Actual L1A3 production dependency failed.", failure);
            if (story.Startup.Failure is { } failureStartup) throw new InvalidDataException("Actual L1A3 startup failed.", failureStartup);
        }
        void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException("Native L1A3 story: " + evidence); checks++; }
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
