using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Audio;

namespace OpenSpidey.Game.Player;

/// <summary>Original supply accounting connected to the player's real script, health, source RNG and native audio.</summary>
public sealed class GodotPlayerInventory : IPlayerWebInventoryHost
{
    private readonly GodotPlayer player;
    private readonly SoundChannelController sounds;
    private readonly GodotSpeechPlayer speech;
    private readonly Func<SoundListener> listener;
    private readonly Func<int> difficulty;
    private readonly Func<bool> consumptionDisabled;
    private readonly Func<int, int> random;
    public PlayerWebInventory State { get; }
    public short Health => player.Combat.State.Health;
    public int Suit => player.Combat.CurrentSuit;
    public uint Clock => player.Combat.Timer;
    public int Difficulty => difficulty();
    public bool SynthesizedInputActive => player.Scripts?.Active == true;
    public bool ConsumptionDisabled => consumptionDisabled();
    public bool SpeechBusy => speech.Controller.Busy;
    public FixedVector3 Position => player.Controller.Position;

    public GodotPlayerInventory(PlayerWebInventory state, GodotPlayer player, SoundChannelController sounds,
        GodotSpeechPlayer speech, Func<SoundListener> listener, Func<int> difficulty,
        Func<bool> consumptionDisabled, Func<int, int> random)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        this.speech = speech ?? throw new ArgumentNullException(nameof(speech));
        this.listener = listener ?? throw new ArgumentNullException(nameof(listener));
        this.difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        this.consumptionDisabled = consumptionDisabled ?? throw new ArgumentNullException(nameof(consumptionDisabled));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public bool IncreaseWebbing(int amount) => PlayerWebInventoryRules.Increase(State, amount, this);
    public bool DecreaseWebbing(int amount) => PlayerWebInventoryRules.Decrease(State, amount, this);
    public bool SetFireWebbing() => PlayerWebInventoryRules.SetFireWebbing(State);
    public int Random(int range) => random(range);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) => sounds.PlayAt(sound, position, listener());
    public void PlaySpeech(SpeechRequest request) => speech.Play(request.Bank, request.Slot, request.Priority);
}
