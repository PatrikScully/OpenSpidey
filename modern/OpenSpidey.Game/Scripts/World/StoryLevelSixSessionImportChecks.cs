using System.Diagnostics;
using System.Text;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L2A2 autoexec: banks126 before modules189, real310/316 module factories and Panel bars, then120 source ticks.</summary>
internal static class StoryLevelSixSessionImportChecks
{
    public static async Task VerifyAsync(StoryLevelSession story)
    {
        TriggerFile triggers = story.Registry.Triggers;
        IReadOnlyList<TriggerInstruction> autoProgram = TriggerCommandReader.Read(triggers.Nodes[0]);
        GodotMissionLibraries libraries = story.GetNode<GodotMissionLibraries>("MissionLibraries");
        int checks = 0, gameplayStarts = 0;
        Exception? censusFailure = null;
        var spawned = new List<GodotScorpionEncounterActor>();
        Assert(story.Application.MissionName.Equals("L2A2_T", StringComparison.OrdinalIgnoreCase) &&
            autoProgram.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[]
                { 140, 302, 147, 190, 126, 126, 126, 126, 142, 126, 126, 128, 189, 189, 151 }) &&
            autoProgram.Where(instruction => instruction.Opcode == 126).Select(Text).SequenceEqual(new[] { "Scorpion", "Jameson", "L2A2_L", "L2A2_O", "expgrnd", "scimpact" }) &&
            autoProgram.Where(instruction => instruction.Opcode == 189).Select(Text).SequenceEqual(new[] { "scorpion", "jonah" }),
            "Untouched autoexec0 loads the Scorpion/Jameson banks and L2A2_G before the scorpion/jonah modules.");
        story.Registry.Spawned += OnSpawn; story.GameplayStarted += OnGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                if (censusFailure is not null) throw censusFailure;
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Genuine L2A2 startup did not reach Playing.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (censusFailure is not null) throw censusFailure;
            Healthy();
            GodotScorpionEncounterRuntime encounter = story.ScorpionEncounter;
            GodotScorpionEncounterActor scorpion = spawned.Single(actor => actor.SourceActorType == ScorpionState.ActorType);
            GodotScorpionEncounterActor jonah = spawned.Single(actor => actor.SourceActorType == JonahState.ActorType);
            deadline.Restart();
            while (story.SourceClock < 120)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Actual L2A2 source frames did not advance120 ticks.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Healthy();
            // Panel_Display skips Panel_DisplayHealthBar while the intro script holds the player.
            bool panelGated = story.Player?.Scripts?.Active == true || story.Player?.Combat.State.FieldE18 != 0;
            Assert(scorpion.SourceUpdates > 0 && jonah.SourceUpdates > 0 && encounter.HealthBars.Primary is not null &&
                (panelGated || encounter.HealthBarPresenter!.LastFrame.Count > 0),
                "The encounter AIs ran for120 source ticks and the health bars draw whenever the Panel_Display gate is open.");
            GD.Print($"Verified native L2A2 story startup: {checks} checks, module310/316 construction, shared Panel health bars and120 source ticks.");
        }
        finally { story.Registry.Spawned -= OnSpawn; story.GameplayStarted -= OnGameplay; }

        void OnSpawn(GodotScriptActor actor) { if (actor is GodotScorpionEncounterActor encounter) spawned.Add(encounter); }
        // Checked synchronously at GameplayStarted, before the first AI frame.
        void OnGameplay()
        {
            gameplayStarts++;
            try { VerifyCensus(); }
            catch (Exception error) { censusFailure ??= error; }
        }
        void VerifyCensus()
        {
            GodotScorpionEncounterActor scorpion = spawned.Single(actor => actor.SourceActorType == ScorpionState.ActorType);
            GodotScorpionEncounterActor jonah = spawned.Single(actor => actor.SourceActorType == JonahState.ActorType);
            Assert(spawned.Count == 2 && scorpion.SourceNodeId == 1 && jonah.SourceNodeId == 2 &&
                scorpion.State.Health == 900 && scorpion.State.Damage == 15 && jonah.State.Health == 120 && jonah.State.BodyFlags == 0x206 &&
                scorpion.Model.Model.Playback == scorpion.State.Animation && scorpion.Model.Model.SourcePartCount == 24 &&
                jonah.Model.Model.SourcePartCount == 16 && libraries.Repository.Loaded.ContainsKey("data/scorpion.psx") &&
                libraries.Repository.Loaded.ContainsKey("data/jameson.psx"),
                "Census builds node1 CScorpion and node2 CJonah through the module slot0 factories from the126-loaded banks.");
            GodotScorpionEncounterRuntime encounter = story.ScorpionEncounter;
            Assert(ReferenceEquals(encounter.HealthBars.Primary, scorpion.State) && ReferenceEquals(encounter.HealthBars.Secondary, jonah.State) &&
                encounter.HealthBars.PrimaryInitialHealth == 900 && encounter.HealthBars.SecondaryInitialHealth == 120 && encounter.HealthBarPresenter is not null,
                "Both constructors share one Panel health-bar owner with their constructor health baselines.");
            GD.Print($"Verified native L2A2 census: {checks} checks, module310/316 construction from126-loaded banks and shared Panel health bars.");
        }
        void Healthy()
        {
            if (story.Failure is { } failure) throw new InvalidDataException("Actual L2A2 production dependency failed.", failure);
            if (story.Startup.Failure is { } startupFailure) throw new InvalidDataException("Actual L2A2 startup failed.", startupFailure);
        }
        void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException("Native L2A2 story: " + evidence); checks++; }
    }

    private static string Text(TriggerInstruction instruction)
    {
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException("Original mission string has no terminator.");
        return Encoding.Latin1.GetString(instruction.Arguments.Span[..end]);
    }
}
