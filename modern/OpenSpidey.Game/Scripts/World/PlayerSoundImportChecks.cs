using Godot;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Audio;

namespace OpenSpidey.Game.World;

internal static class PlayerSoundImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotPlayerSoundEffects sounds)
    {
        int decoded = sounds.Backend.ValidateCatalogSamples();
        GD.Print($"Verified native PCM import for {decoded} unique original SFX WAVs across all 58 PC groups.");
        if (sounds.StartedCount == 0 || sounds.LastSound is < 1 or > 4)
            throw new InvalidDataException("Original walking animation did not emit a native footstep.");
        int completed = sounds.FinishedCount;
        sounds.Play(new PlayerFrameSound(9, 0x2000, 0));
        var voice = sounds.LastVoice ?? throw new InvalidDataException("Player sound did not allocate a shared channel.");
        if (voice.Stream is not AudioStreamSynchronized stream || stream.GetSyncStream(0) is not AudioStreamWav wav ||
            wav.MixRate <= 0 || wav.GetLength() <= 0 || voice.PitchScale != 1 ||
            Mathf.Abs(stream.GetSyncStreamVolume(0) - PlayerSoundCatalog.Decibels((0x2000 * 0x2FFF) >> 14)) > 0.0001f ||
            Mathf.Abs(stream.GetSyncStreamVolume(1) - stream.GetSyncStreamVolume(0) + 3.25f) > 0.0001f)
            throw new InvalidDataException("Native player WAV changed its source sample clock, pitch or attenuation.");
        for (int frame = 0; frame < 600 && sounds.FinishedCount == completed; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (sounds.FinishedCount == completed || GodotObject.IsInstanceValid(voice))
            throw new InvalidDataException("Completed player sound retained its native one-shot node.");
        GD.Print("Verified Godot player sounds: walking footsteps, source WAV/attenuation, original pitch and native one-shot cleanup.");
    }
}
