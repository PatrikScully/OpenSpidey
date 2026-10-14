using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;

namespace OpenSpidey.Game.World;

/// <summary>Unmodified L2A1/L2A2 control programs use one real XA WAV player, its original priority gate and positional bookkeeping.</summary>
internal static class ActorSpeechImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, string mediaCache)
    {
        var scope = new Node3D { Name = "OriginalActorSpeechProof" }; owner.AddChild(scope);
        var speech = new GodotSpeechPlayer { Name = "ActualXAVoice", CacheDirectory = mediaCache };
        scope.AddChild(speech); speech.SetPhysicsProcess(false);
        uint clock = 123;
        var requests = new List<(SpeechRequest Request, FixedVector3 Position)>();
        GodotAudioShutdown.Resource[] resources = [];
        AudioStreamPlayer channel = speech.Player;
        try
        {
            TriggerFile first = TriggerFile.Read(archive.Read("data/L2A1_T.trg"));
            GodotActorRegistry firstActors = CreateRegistry(first, "L2A1OriginalSpeechActors");
            BaddyConstructionCount firstLeases = firstActors.BaddyConstructionState;
            firstActors.Spawn(first.Nodes[92]);
            GodotControlActor actor = firstActors.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single();
            FixedVector3 position = new FixedVector3(actor.Definition.Position.X, actor.Definition.Position.Y, actor.Definition.Position.Z) << 12;
            Assert(requests.SequenceEqual(new[] { (new SpeechRequest(8, 1, 666), position) }) &&
                speech.Controller.LastPositionStart == new PositionSpeechStart(8, 1, clock) && speech.Controller.Current == new SpeechRequest(8, 1, 666) && firstLeases.Count == 1,
                "Actual L2A1 control92 must consume its original450A8/1/666 at its own position and publish accepted positional XA bookkeeping.");
            VerifyNativeClip(8, 1, 666, "L2A1_20.bik");
            PositionSpeechStart retained = speech.Controller.LastPositionStart;
            int accepted = speech.PlayCount;
            actor.ScriptState.SkipDuringDeath = true;
            actor.Runtime.ParseAt(firstActors.ScriptAtNode(92), new(FastForwardDeath: true));
            Assert(requests.Count == 1 && speech.PlayCount == accepted && speech.Controller.LastPositionStart == retained,
                "The unchanged source92 program must reach its original stop without invoking any XA transport during control203 death fast-forward.");
            actor.ScriptState.SkipDuringDeath = false;
            actor.Runtime.ParseAt(firstActors.ScriptAtNode(92));
            Assert(requests.Count == 2 && speech.PlayCount == accepted && speech.Controller.LastPositionStart == retained,
                "An equal-priority native actor request must preserve the currently playing voice and last positional marker.");

            speech.Controller.Stop();
            clock += 7;
            actor.Runtime.ParseAt(firstActors.ScriptAtNode(92));
            Assert(speech.Controller.Pending == new SpeechRequest(8, 1, 666) && speech.PlayCount == accepted && speech.Controller.LastPositionStart == retained,
                "Actual450A during XA cooldown must queue through the shared speech owner and leave positional bookkeeping unchanged.");
            for (int vblank = 0; vblank < 30; vblank++) { clock++; speech.Controller.UpdateVblank(); }
            Assert(speech.PlayCount == accepted + 1 && speech.Controller.Pending is null && speech.Controller.Current == new SpeechRequest(8, 1, 666) &&
                speech.Controller.LastPositionStart == retained && ReferenceEquals(channel, speech.Player),
                "The original pending replay must use XAPlay on the same native channel without inventing a second positional-start callback.");
            firstActors.Free();
            Assert(speech.Player.Playing && speech.Controller.Busy && firstLeases.Count == 0 && !GodotObject.IsInstanceValid(actor),
                "A control destructor must release its actor lease without stopping its independent original XA channel.");

            TriggerFile second = TriggerFile.Read(archive.Read("data/L2A2_T.trg"));
            GodotActorRegistry secondActors = CreateRegistry(second, "L2A2OriginalSpeechActors");
            BaddyConstructionCount secondLeases = secondActors.BaddyConstructionState;
            (ushort Node, int Slot, int Priority, string Name)[] originals =
            [
                (153, 0, 0, "L2A2_01.bik"), (154, 3, 1, "L2A2_03a.bik"), (155, 2, 2, "L2A2_02.bik"),
                (165, 5, 255, "L2A2_05.bik"), (166, 4, 255, "L2A2_04.bik")
            ];
            foreach ((ushort node, int slot, int priority, string name) in originals)
            {
                speech.Controller.Reset(); clock += 11;
                secondActors.Spawn(second.Nodes[node]);
                VerifyNativeClip(13, slot, priority, name);
                Assert(speech.Controller.LastPositionStart == new PositionSpeechStart(13, slot, clock) && ReferenceEquals(channel, speech.Player),
                    $"Actual L2A2 control{node} must retain source450A priority{priority} and overwrite only the accepted positional marker on the one native XA channel.");
            }
            retained = speech.Controller.LastPositionStart;
            speech.Controller.Reset(); clock += 13;
            Assert(speech.Play(76, 1, 666) && speech.Controller.LastPositionStart == retained && ReferenceEquals(channel, speech.Player),
                "Global source XAPlay must leave the previous actor positional marker unchanged while using the same genuine voice owner.");
            VerifyNativeClip(76, 1, 666, "stan_01.bik");
            Assert(secondLeases.Count == 5, "Five unchanged L2A2 voice controls must own five genuine CBaddy constructor leases.");
            secondActors.Free();
            Assert(secondLeases.Count == 0, "Actual L2A2 control destruction must release all five source constructor leases.");
            resources = await GodotAudioShutdown.RetirePlaybackAsync(scope, owner.GetTree());
            Assert(resources.Length == 7, "The private proof must own the seven genuine imported speech PCM resources.");
            GD.Print("Verified native unchanged L2A1 control92450A, five L2A2 authored voices, actual PCM/source gain/one XA channel, death fast-forward/equal-priority/pending replay policy and separate after-play positional markers.");
        }
        finally
        {
            if (resources.Length == 0 && GodotObject.IsInstanceValid(speech))
                resources = await GodotAudioShutdown.RetirePlaybackAsync(scope, owner.GetTree());
            scope.Free();
            await GodotAudioShutdown.VerifyReleasedAsync(resources, owner.GetTree());
            Assert(!GodotObject.IsInstanceValid(channel), "Actual speech Predelete must destroy its private native playback node after releasing PCM mixer references.");
        }

        GodotActorRegistry CreateRegistry(TriggerFile triggers, string name)
        {
            var registry = new GodotActorRegistry
            {
                Name = name, Triggers = triggers, ProcessMode = Node.ProcessModeEnum.Disabled,
                VoicePlayback = (bank, slot, priority) => speech.Play(bank, slot, priority),
                PositionVoicePlayback = (bank, slot, sourcePosition, priority) =>
                {
                    requests.Add((new(bank, slot, priority), sourcePosition));
                    speech.PlayAtPosition(bank, slot, priority, () => clock);
                }
            };
            scope.AddChild(registry);
            return registry;
        }
        void VerifyNativeClip(int bank, int slot, int priority, string name)
        {
            Assert(speech.Controller.Busy && speech.Controller.Current == new SpeechRequest(bank, slot, priority) && speech.CurrentClip?.Name == name &&
                speech.Player.Playing && speech.Player.Stream is AudioStreamWav wav && wav.GetLength() > 0 && wav.MixRate > 0 &&
                Math.Abs(speech.Player.VolumeLinear - SpeechController.BinkVolume(speech.Volume) / 32768f) < 0.0001f,
                $"Source actor voice{bank}/{slot} must import its actual cached{name} PCM and retain the original XA gain and priority{priority}.");
        }
    }

    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
