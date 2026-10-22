using System.Diagnostics;
using Godot;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.World;

/// <summary>Bounded empty-save startup for missions without a dedicated census proof: legal cinema skips, Playing, then120 healthy source ticks.</summary>
internal static class StoryGenericStartupImportChecks
{
    public static async Task VerifyAsync(StoryLevelSession story)
    {
        string mission = story.Application.MissionName;
        int gameplayStarts = 0, skips = 0;
        story.GameplayStarted += OnGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                Healthy();
                if (story.Cinema.IsPlaying && (story.Cinema.ShowingCover || story.Cinema.CanSkipMovie))
                {
                    story.Cinema.RequestSkip(); skips++;
                }
                if (deadline.Elapsed > TimeSpan.FromSeconds(120))
                    throw new TimeoutException($"{mission} startup did not reach Playing (phase {story.Startup.Phase}, blocked={story.Mission.IsBlocked}).");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Healthy();
            if (story.Player is null || story.Startup.Phase != MissionStartupPhase.Playing || story.Checkpoints.Current is null)
                throw new InvalidDataException($"{mission} reported gameplay without an actual player and restart.");
            deadline.Restart();
            while (story.SourceClock < 120)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException($"{mission} source frames did not advance120 ticks.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Healthy();
            if (story.Training.SourceModuleLoaded)
            {
                if (story.Training.MonitorCalls == 0 || story.Training.TextDraws == 0 || story.Application.Countdown.DisplayEnabled == 0)
                    throw new InvalidDataException($"{mission} loaded the source training module, but its post-AI monitor did not run and draw.");
                GD.Print($"Training module level {story.Training.State.Stuff[0]:X}: {story.Training.MonitorCalls} post-AI monitor calls, " +
                    $"countdown {story.Application.Countdown.RemainingTicks}, {story.Training.TextDraws} source text draws.");
            }
            GD.Print($"Passed generic {mission} startup: {skips} legal cinema skip requests, restart {story.Checkpoints.Current!.Name}, " +
                $"{story.Startup.InitialActorsCreated} initial actors, {story.Registry.LiveCount} live, {story.SourceClock} source ticks. Mission traversal was not exercised.");
        }
        finally { story.GameplayStarted -= OnGameplay; }

        void OnGameplay() => gameplayStarts++;
        void Healthy()
        {
            if (story.Failure is { } failure) throw new InvalidOperationException($"{mission} startup failed: {failure.Message}", failure);
        }
    }
}
