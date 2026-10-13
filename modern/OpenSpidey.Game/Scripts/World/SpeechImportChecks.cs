using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class SpeechImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotPlayer player, GodotSpeechPlayer speech)
    {
        byte[] script = [0, 0, 4, 0, 67, 0, 15, 0, 250, 0, 0, 0, 255, 0];
        player.Scripts!.Start(PlayerScriptProgram.Read(script));
        int updates = player.SourceUpdates;
        player.SetPhysicsProcess(true);
        for (int frame = 0; frame < 4 && player.SourceUpdates == updates; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.SetPhysicsProcess(false);
        if (speech.PlayCount != 1 || speech.CurrentClip?.Name != "stan_00.bik" || !speech.Controller.Busy ||
            speech.Controller.Priority != 250 || speech.Player.Stream is not AudioStreamWav wave ||
            wave.MixRate != 44100 || wave.Stereo || wave.GetLength() is < 32 or > 33 ||
            Mathf.Abs(speech.Player.VolumeLinear - 1.5f) > 0.0001f)
            throw new InvalidDataException("The player script did not play its original tutorial speech stream.");
        speech.Controller.Pause(true);
        // The mixer uses wall time; physics catch-up can emit several ticks before its pause fade finishes.
        await Task.Delay(200);
        double pausedPosition = speech.Player.GetPlaybackPosition();
        await Task.Delay(200);
        if (!speech.Player.StreamPaused || Math.Abs(speech.Player.GetPlaybackPosition() - pausedPosition) > 0.000001)
            throw new InvalidDataException($"Paused speech cursor mismatch: paused={speech.Player.StreamPaused}, " +
                $"controller={speech.Controller.Paused}, before={pausedPosition}, after={speech.Player.GetPlaybackPosition()}.");
        speech.Controller.Pause(false);
        if (speech.Play(0, 0, 249) || !speech.Play(0, 0, 251) || speech.Controller.Priority != 251 || speech.PlayCount != 2)
            throw new InvalidDataException("Native speech lost its priority suppression or preemption.");
        for (int frame = 0; frame < 600 && speech.NaturalCompletions == 0; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (speech.NaturalCompletions != 1 || speech.Controller.Busy || speech.Controller.Cooldown is <= 0 or > 30)
            throw new InvalidDataException("Speech natural completion did not begin the original cooldown.");
        if (speech.Play(0, 1, 10) || speech.Play(0, 2, 20) || speech.Controller.Pending != new SpeechRequest(0, 1, 10))
            throw new InvalidDataException("Speech cooldown did not retain the first pending request.");
        for (int frame = 0; frame < 35 && speech.PlayCount == 2; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (speech.PlayCount != 3 || speech.CurrentClip?.Index != 1 || speech.Controller.Priority != 10)
            throw new InvalidDataException("Queued speech failed to begin after the vblank cooldown.");
        speech.Controller.Stop();
        speech.Controller.Reset();
        if (!speech.Play(41, 6, 10) || speech.Player.Playing)
            throw new InvalidDataException("Known missing source speech did not preserve the original failed-open behavior.");
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (speech.Controller.Busy || speech.Controller.Cooldown is <= 0 or > 30)
            throw new InvalidDataException("Missing source voice failed to finish its accepted request.");
        speech.Controller.Stop();
        GD.Print("Verified Godot speech: scripted tutorial WAV, source gain, pause, priorities, natural completion, pending cooldown and missing-source handling.");
    }
}
