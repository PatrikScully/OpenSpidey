using Godot;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class PlayerKillQuipImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotPlayer player, GodotSpeechPlayer sessionSpeech)
    {
        var speech = new GodotSpeechPlayer { Name = "KillQuipCheckSpeech", CacheDirectory = sessionSpeech.CacheDirectory };
        owner.AddChild(speech);
        speech.SetPhysicsProcess(false);
        uint clock = 1000;
        IReadOnlyList<PlayerKillQuipEnemy> enemies = [];
        var draws = new Queue<(int Range, int Value)>();
        int Random(int range)
        {
            if (!draws.TryDequeue(out var draw) || draw.Range != range)
                throw new InvalidDataException($"Native kill quip requested unexpected random bound {range}.");
            return draw.Value;
        }
        var quips = new GodotPlayerKillQuips(player, speech, () => clock, () => enemies, Random);
        var attachment = player.Combat.SpeechAttachment;
        var previousAttachment = (attachment.AttachedClock, attachment.Bank, attachment.Slot);
        try
        {
            quips.SetFirstContactDetails();
            clock += 100;
            quips.SetFirstContactDetails();
            if (quips.Rules.EncounterClock != 1000 || quips.Rules.StartingHealth != player.Combat.State.Health)
                throw new InvalidDataException("Native first contact lost the original encounter snapshot.");

            draws.Enqueue((2, 1)); draws.Enqueue((32, 2));
            quips.NotifyKill(304);
            SpeechClip expected = SpeechCatalog.Get(60, 1)!;
            if (speech.Controller.Current != new SpeechRequest(60, 1, 20) || speech.PlayCount != 1 ||
                speech.CurrentClip != expected || speech.Player.Stream is not AudioStreamWav wave || wave.GetLength() <= 0 ||
                !speech.Player.Playing || quips.Rules.History.RecentSounds[4] != 961)
                throw new InvalidDataException("Native kill quip failed to submit and play the original bank60/slot1 WAV.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);

            // An equal-priority request is rejected; source history still advances.
            draws.Enqueue((2, 1)); draws.Enqueue((32, 4));
            quips.NotifyKill(304);
            if (speech.PlayCount != 1 || speech.Controller.Current != new SpeechRequest(60, 1, 20) ||
                quips.Rules.History.RecentSounds[4] != 962 || quips.Rules.History.NotificationCount != 2)
                throw new InvalidDataException("Rejected native kill speech changed source history or channel priority semantics.");

            // A living, active source enemy blocks the quip before variant selection.
            enemies = [new(0x200, 1, 0x20)];
            draws.Enqueue((2, 1));
            quips.NotifyKill(304);
            quips.UpdateTargetCount(0);
            quips.NotifyKill(304);
            if (draws.Count != 0 || speech.PlayCount != 1 || quips.Rules.EncounterActive != 0 ||
                quips.Rules.History.NotificationCount != 4 || previousAttachment != (attachment.AttachedClock, attachment.Bank, attachment.Slot))
                throw new InvalidDataException("Native kill quip lost enemy eligibility, encounter completion or actor XA ownership.");
            GD.Print("Verified native player kill quips: original WAV, first-contact snapshot, global priority/history, live enemy gate and unchanged actor XA attachment.");
        }
        finally { speech.Free(); }
    }
}
