using OpenSpidey.Core.Audio;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerKillQuipEnemy(uint BodyFlags, short Health, uint BehaviorFlags);

/// <summary>The five global source taunt IDs and NotifyKill diagnostics, shared by the player service.</summary>
public sealed class PlayerKillQuipHistory
{
    public int[] RecentSounds { get; } = new int[5];
    public int LastBank { get; set; }
    public int LastVariant { get; set; }
    public int NotificationCount { get; set; }
}

public interface IPlayerKillQuipHost
{
    uint Clock { get; }
    short Health { get; }
    IReadOnlyList<PlayerKillQuipEnemy> SourceBaddyOrder { get; }
    // Source calls global XA playback directly; it does not attach speech to the player.
    void PlaySpeech(SpeechRequest request);
}

/// <summary>Original CPlayer::NotifyKill windows, eligibility scan, retry history and XA request.</summary>
public sealed class PlayerKillQuips(PlayerKillQuipHistory history)
{
    public byte EncounterActive { get; set; }
    public int StartingHealth { get; set; }
    public uint EncounterClock { get; set; }
    public PlayerKillQuipHistory History { get; } = history ?? throw new ArgumentNullException(nameof(history));

    // CPlayer::SetFirstContactDetails; later contact retains the original clock/health snapshot.
    public void SetFirstContactDetails(short health, uint clock)
    {
        if (EncounterActive != 0) return;
        EncounterActive = 1;
        StartingHealth = health;
        EncounterClock = clock;
    }

    // Original target-list update clears only field354 when its field528 count is zero.
    public void UpdateTargetCount(int targetCount) { if (targetCount == 0) EncounterActive = 0; }

    public void NotifyKill(ushort actorType, IPlayerKillQuipHost host, Func<int, int> random)
    {
        if (EncounterActive != 0 && random(2) != 0)
        {
            foreach (PlayerKillQuipEnemy enemy in host.SourceBaddyOrder)
                if ((enemy.BodyFlags & 0x200) != 0 && enemy.Health > 0 && (enemy.BehaviorFlags & 0x20) != 0) goto Done;
            int elapsed = unchecked((int)(host.Clock - EncounterClock));
            int bank, slot;
            bool checkRepeat = actorType is not (306 or 320 or 324);
            while (true)
            {
                int healthLost = unchecked(StartingHealth - host.Health);
                bool early = elapsed < 240 && healthLost < 10;
                bool late = elapsed > 1200 && healthLost > 50;
                if (!early && !late) goto Done;
                if (actorType is 306 or 320) { bank = 20; slot = 11 + ((random(4) & 0xFE) >> 1); }
                else if (actorType == 324)
                {
                    bank = 46;
                    slot = early ? 8 + ((random(8) & 0xFE) >> 1) : 13 + ((random(4) & 0xFE) >> 1);
                }
                else { bank = early ? 60 : 62; slot = (random(early ? 32 : 20) & 0xFE) >> 1; }
                int sound = unchecked((bank << 4) + slot);
                History.LastBank = bank;
                History.LastVariant = slot;
                if (checkRepeat && History.RecentSounds.Contains(sound)) continue;
                Array.Copy(History.RecentSounds, 1, History.RecentSounds, 0, 4);
                History.RecentSounds[4] = sound;
                // History advances even when the global speech controller rejects playback.
                host.PlaySpeech(new(bank, slot, 20));
                break;
            }
        }
    Done:
        History.NotificationCount = unchecked(History.NotificationCount + 1);
    }
}
