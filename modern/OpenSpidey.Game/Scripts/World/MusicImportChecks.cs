using System.Numerics;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Real L1A3 authored366/368 fade instructions, source music WAVs and canonical CPlayer gain/handle ownership.</summary>
internal static class MusicImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, GodotPlayer player)
    {
        PlayerCombatState combat = player.Combat.State;
        int oldClock = combat.FightMusicClock, oldNearby = combat.Field528, oldVolume = combat.Field52C, oldCombat = combat.Field534,
            oldTime = combat.FightMusicTime, oldGain = combat.FightMusicGain;
        uint oldHandle = combat.FightMusicVoiceHandle;
        byte oldEnabled = combat.FightMusicEnabled, oldDown = combat.FightMusicFadeDown, oldUp = combat.FightMusicFadeUp;
        var scope = new Node { Name = "OriginalMusicRenderProof" }; owner.AddChild(scope);
        var backend = new GodotSoundChannels { Archive = archive, LevelGroup = SoundCatalog.Find("L1A3") }; scope.AddChild(backend);
        backend.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/L1A3.sfx"), levelSpecific: true);
        GodotObject[] resources = [];
        AudioStreamPlayer? firstVoice = null, secondVoice = null;
        try
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
            TriggerInstruction down = TriggerCommandReader.Read(triggers.Nodes[366]).Single(instruction => instruction.Opcode == 215);
            TriggerInstruction up = TriggerCommandReader.Read(triggers.Nodes[368]).Single(instruction => instruction.Opcode == 216);
            var state = new SourceMusicState();
            int post = 0, training = 0;
            var music = new GodotMusicController(state, backend, () => combat, () => 0x103, () => post, () => training);
            combat.FightMusicEnabled = 1; combat.FightMusicClock = 300; combat.FightMusicTime = 300;
            combat.Field528 = 1; combat.Field52C = 0; combat.Field534 = 0;
            combat.FightMusicGain = 4096; combat.FightMusicFadeDown = combat.FightMusicFadeUp = 0;
            uint clock = 2; music.Render(clock);
            uint first = combat.FightMusicVoiceHandle;
            int channel = ValidChannel(first);
            firstVoice = backend.PlayerForChannel(channel) ?? throw new InvalidDataException("Source C004 music did not create a native voice.");
            SoundDefinition original = backend.Controller.LevelBank.Resolve(0xC004)
                ?? throw new InvalidDataException("The real L1A3 sound bank has no original alias4.");
            Assert(combat.FightMusicClock == 0 && combat.Field52C == 12288 && state.FightVariant == 0 && state.IntroCompleted == 0 &&
                firstVoice.Playing && firstVoice.Stream is AudioStreamSynchronized { } stream &&
                stream.GetSyncStream(0) is AudioStreamWav { } wav && wav.GetLength() > 0 && wav.MixRate > 0 &&
                wav.LoopMode == (original.Looped ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled) &&
                backend.StartedCount == 1 && backend.Controller.ReadChannel(channel).Mix.Volume == (12288 * 0x2FFF >> 14),
                "Original103 render must replay sourceC004 at computed combat volume, retain its actual handle and import the genuine source PCM/loop policy.");
            int started = backend.StartedCount, mixes = backend.MixUpdates;
            music.Render(clock);
            Assert(backend.StartedCount == started && backend.MixUpdates == mixes && combat.FightMusicClock == 0,
                "A second render in the same source vblank must emit no additional music request or gain update.");

            new FightMusicFadeCommandHandler(215, () => combat).Execute(new(triggers.Nodes[366], 0), down);
            Assert(combat.FightMusicGain == 4096 && backend.MixUpdates == mixes,
                "Authored366 source215 must only select fade bytes before the actual player and render phases.");
            for (int frame = 0; frame < 32; frame++)
            {
                PlayerFightMusicFade.Advance(combat, 2); clock += 2; music.Render(clock);
                int expected = unchecked((ushort)(short)(unchecked(combat.Field52C * combat.FightMusicGain) >> 12));
                expected = Math.Min(expected, 16383);
                Assert(backend.Controller.ReadChannel(channel).Mix.Volume == expected && firstVoice.Playing && backend.StartedCount == started,
                    "The actual render phase must apply the source gain to the retained music voice without reapplying master volume or synthesizing another track.");
            }
            Assert(combat.FightMusicGain == 0 && combat.FightMusicFadeDown == 1 &&
                firstVoice.Stream is AudioStreamSynchronized silent &&
                Math.Abs(silent.GetSyncStreamVolume(0) - SoundVolume.Decibels(0)) < 0.0001f,
                "Completed authored down-fade must retain the source down byte and live PCM voice at the actual zero-volume attenuation.");
            new FightMusicFadeCommandHandler(216, () => combat).Execute(new(triggers.Nodes[368], 0), up);
            for (int frame = 0; frame < 32; frame++)
            {
                PlayerFightMusicFade.Advance(combat, 2); clock += 2; music.Render(clock);
            }
            Assert(combat.FightMusicGain == 4096 && combat.FightMusicFadeDown == 0 && combat.FightMusicFadeUp == 0 &&
                combat.FightMusicVoiceHandle == first && backend.Controller.ReadChannel(channel).Mix.Volume == 12288 &&
                backend.StartedCount == started && firstVoice.Playing,
                "Authored368 source216 must restore the same genuine voice and let Music_MusicUpdate retire only the completed up byte.");

            uint second = backend.Controller.Play(0xC005, 8192); int secondChannel = ValidChannel(second);
            secondVoice = backend.PlayerForChannel(secondChannel) ?? throw new InvalidDataException("The original alias5 music voice is missing.");
            int secondVolume = backend.Controller.ReadChannel(secondChannel).Mix.Volume;
            backend.Controller.SetVoiceVolume(first | second, 4096);
            Assert(backend.Controller.ReadChannel(channel).Mix.Volume == 4096 &&
                backend.Controller.ReadChannel(secondChannel).Mix.Volume == secondVolume && firstVoice.Playing && secondVoice.Playing,
                "Original472150 multi-bit music handles must modify only the lowest selected allocated voice.");
            mixes = backend.MixUpdates;
            backend.Controller.SetVoiceVolume(uint.MaxValue, -1);
            Assert(backend.MixUpdates == mixes && backend.Controller.ReadVoiceVolume(0) == ushort.MaxValue,
                "The original stale bit0 handle must store its ushort without touching later live music voices.");

            int playerClock = combat.FightMusicClock;
            post = 1; clock += 10; music.Render(clock);
            training = 1; post = 0; clock += 10; music.Render(clock);
            training = 0; music.Render(clock);
            Assert(combat.FightMusicClock == playerClock && state.LastVblank == clock && backend.MixUpdates == mixes,
                "Actual post-water/training gates must consume their render clock and prevent music catch-up or same-vblank playback.");
            clock += 2; music.Render(clock);
            Assert(combat.FightMusicClock == playerClock + 2, "The next ungated render must resume with the genuine two-vblank delta.");

            resources = backend.ReadOwnedResources().Select(entry => (GodotObject)entry.Owner).ToArray();
            music.StopPlayerVoice(combat);
            Assert(combat.FightMusicVoiceHandle == 0 && combat.Field528 == 0 && combat.Field52C == 0 &&
                backend.LiveVoices == 1 && secondVoice.Playing,
                "The actual player destructor's538 stop must clear its own music fields and preserve another source voice.");
            backend.Controller.Stop(second);
            Assert(backend.LiveVoices == 0, "The independent original alias5 voice must stop through its actual SFX mask.");
            GD.Print("Verified native sourceL1A3 C004/C005 music, actual366215/368216 phase fades, same-vblank/water/training gates, first-bit/stale voice policy and player538 destruction.");
        }
        finally
        {
            scope.Free();
            combat.FightMusicClock = oldClock; combat.FightMusicVoiceHandle = oldHandle; combat.Field528 = oldNearby;
            combat.Field52C = oldVolume; combat.Field534 = oldCombat; combat.FightMusicTime = oldTime;
            combat.FightMusicGain = oldGain; combat.FightMusicEnabled = oldEnabled;
            combat.FightMusicFadeDown = oldDown; combat.FightMusicFadeUp = oldUp;
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(resources.All(resource => !GodotObject.IsInstanceValid(resource)) &&
                (firstVoice is null || !GodotObject.IsInstanceValid(firstVoice)) && (secondVoice is null || !GodotObject.IsInstanceValid(secondVoice)),
                "Actual music owner Predelete must retire both synchronized native streams, all cached PCM and the stopped playback nodes.");
        }
    }

    private static int ValidChannel(uint handle) => handle is 0 or uint.MaxValue
        ? throw new InvalidDataException($"Original music request returned unavailable handle{handle}.") : BitOperations.TrailingZeroCount(handle);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
