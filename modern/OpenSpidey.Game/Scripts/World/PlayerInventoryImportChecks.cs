using Godot;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class PlayerInventoryImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotPlayer player, GodotSoundChannels sounds,
        GodotSpeechPlayer sessionSpeech, PlayerFollowCamera camera)
    {
        var speech = new GodotSpeechPlayer { Name = "WebSupplyCheckSpeech", CacheDirectory = sessionSpeech.CacheDirectory };
        owner.AddChild(speech);
        speech.SetPhysicsProcess(false);
        var state = new PlayerWebInventory { Webbing = 4096, Cartridges = 7, FireWebbing = 1 };
        bool disabled = false;
        int randomCalls = 0;
        int Random(int range)
        {
            if (range != 3) throw new InvalidDataException("Native empty web supply requested an unexpected random bound.");
            randomCalls++;
            return 2;
        }
        var inventory = new GodotPlayerInventory(state, player, sounds.Controller, speech,
            () => new(camera.SourcePosition >> 12), () => 2, () => disabled, Random);
        int started = sounds.StartedCount;
        try
        {
            if (!inventory.DecreaseWebbing(4096) || state.Webbing != 4096 || state.Cartridges != 6 || state.FireWebbing != 0 ||
                sounds.StartedCount != started + 1 || sounds.LiveVoices == 0)
                throw new InvalidDataException("Native web cartridge reload lost original supply accounting or positional SFX.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            state.Webbing = 0; state.Cartridges = 0; state.FireWebbing = 1;
            if (inventory.DecreaseWebbing(300) || state.FireWebbing != 0 || randomCalls != 1 || speech.PlayCount != 1 ||
                speech.Controller.Current != new SpeechRequest(33, 4, 0) || speech.CurrentClip != SpeechCatalog.Get(33, 4) ||
                speech.Player.Stream is not AudioStreamWav wave || wave.GetLength() <= 0 || !speech.Player.Playing)
                throw new InvalidDataException("Native empty web supply failed to play the original warning WAV and reject consumption.");
            state.FireWebbing = 1;
            if (inventory.DecreaseWebbing(300) || randomCalls != 1 || speech.PlayCount != 1 || state.FireWebbing != 0)
                throw new InvalidDataException("Native empty supply did not retain the global busy-speech gate.");
            disabled = true; state.FireWebbing = 1;
            if (!inventory.DecreaseWebbing(300) || state.Webbing != 0 || state.FireWebbing != 1)
                throw new InvalidDataException("Native consumption-disable gate mutated original supply state.");
            disabled = false;
            if (!inventory.IncreaseWebbing(5000) || state.Webbing != 904 || state.Cartridges != 1 ||
                state.LastCartridgeClock != player.Combat.Timer || state.PickupRevision != 1 || !inventory.SetFireWebbing() ||
                state.Webbing != 4096 || state.FireWebbing != 1 || state.PickupRevision != 2)
                throw new InvalidDataException("Native pickup/fire supply lost source clock, single-cartridge rollover or shared revision.");
            GD.Print("Verified native web inventory: exact cartridge reload/SFX, original empty-supply WAV, speech/consumption gates, pickup clock and fire-web refill.");
        }
        finally { speech.Free(); }
    }
}
