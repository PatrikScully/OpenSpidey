using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Audio;

namespace OpenSpidey.Game.Player;

/// <summary>Session-owned encounter history and original kill speech on the shared native XA channel.</summary>
public sealed class GodotPlayerKillQuips : IPlayerKillQuipHost
{
    private readonly GodotPlayer player;
    private readonly GodotSpeechPlayer speech;
    private readonly Func<uint> clock;
    private readonly Func<IReadOnlyList<PlayerKillQuipEnemy>> sourceBaddyOrder;
    private readonly Func<int, int> random;

    public PlayerKillQuips Rules { get; } = new(new());
    public uint Clock => clock();
    public short Health => player.Combat.State.Health;
    public IReadOnlyList<PlayerKillQuipEnemy> SourceBaddyOrder => sourceBaddyOrder();

    public GodotPlayerKillQuips(GodotPlayer player, GodotSpeechPlayer speech, Func<uint> clock,
        Func<IReadOnlyList<PlayerKillQuipEnemy>> sourceBaddyOrder, Func<int, int> random)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.speech = speech ?? throw new ArgumentNullException(nameof(speech));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.sourceBaddyOrder = sourceBaddyOrder ?? throw new ArgumentNullException(nameof(sourceBaddyOrder));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public void SetFirstContactDetails() => Rules.SetFirstContactDetails(Health, Clock);
    public void UpdateTargetCount(int count) => Rules.UpdateTargetCount(count);
    public void NotifyKill(ushort actorType) => Rules.NotifyKill(actorType, this, random);

    // NotifyKill calls the global speech channel without CBody::AttachXA.
    public void PlaySpeech(SpeechRequest request) => speech.Play(request.Bank, request.Slot, request.Priority);
}
