using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Actual preparation and post-pose source504/505 presentation using shared sound, actuator, clock and player resource owners.</summary>
public sealed class GodotPlayerAdditionalEffects : IPlayerAdditionalEffectPreparationHost
{
    private readonly GodotPlayer player;
    private readonly PlayerFollowCamera camera;
    private readonly GodotPlayerAdditionalBodies parts;
    private readonly SoundChannelController sounds;
    private readonly GodotControllerFeedback feedback;
    private readonly Func<PlayerAdditionalBodyCheats> cheats;
    private readonly Func<uint> clock;
    private readonly Func<int, int> random;
    public PlayerWaterEffectState Water { get; }
    public PlayerAdditionalBodyCheats Cheats => cheats();
    public bool VibrationEnabled => feedback.Enabled;
    public ushort ActuatorTime => unchecked((ushort)feedback.RemainingTicks(0, 1));

    public GodotPlayerAdditionalEffects(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerAdditionalBodies parts,
        PlayerWaterEffectState water, SoundChannelController sounds, GodotControllerFeedback feedback,
        Func<PlayerAdditionalBodyCheats> cheats, Func<uint> clock, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(water); ArgumentNullException.ThrowIfNull(sounds); ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(cheats); ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(random);
        this.player = player; this.camera = camera; this.parts = parts; Water = water; this.sounds = sounds;
        this.feedback = feedback; this.cheats = cheats; this.clock = clock; this.random = random;
        if (player.Melee is null || player.Inventory is null || !ReferenceEquals(camera.Target, player))
            throw new InvalidOperationException("Additional effect presentation requires the genuine posed player, supply owner and its camera.");
    }
    public void Prepare() => PlayerAdditionalEffectPreparation.Prepare(player.Combat.State, parts.State, Water, this);
    public void FinishFrame()
    {
        GodotPlayerMelee melee = player.Melee!;
        PlayerKickHandle handle = parts.State.SenseBody;
        GodotPlayerAdditionalBody? sense = parts.Recover(ref handle);
        parts.State.SenseBody = handle;
        if (sense is not null)
        {
            sense.State.Position = melee.GetPlayerHookPosition(8);
            sense.State.Angles = PartAngles(7);
            PlayerAdditionalBodyRules.SetSensePulse(sense.State, clock(), Cheats); sense.Synchronize();
        }
        for (int slot = 0; slot < parts.State.BonusFists.Length; slot++)
        {
            handle = parts.State.BonusFists[slot];
            GodotPlayerAdditionalBody? fist = parts.Recover(ref handle);
            parts.State.BonusFists[slot] = handle;
            if (fist is null) continue;
            fist.State.Position = melee.GetPlayerHookPosition(slot == 0 ? 4 : 3);
            fist.State.Angles = PartAngles(slot == 0 ? 10 : 5);
            PlayerAdditionalBodyRules.SetFistPresentation(fist.State, camera.SourcePosition, player.Inventory!.State.FireWebbing != 0, random);
            fist.Synchronize();
        }
    }
    private OpenSpidey.Core.Actors.ActorAngles PartAngles(int part) => PlayerAdditionalBodyRules.PartAngles(
        player.Controller.Orientation.Basis, player.Model.ReadSourceEffectPartPose(part, (player.ActorState.ItemFlags & 4) != 0));
    public uint PlaySenseSound() => sounds.Play(26, 8192);
    public void StopSound(uint handle) => sounds.Stop(handle);
    public void ActivateSenseActuator(ushort duration) => feedback.Activate(0, duration, 1, 60);
    public PlayerAdditionalBody? Recover(ref PlayerKickHandle handle) => parts.Recover(ref handle)?.State;
    public PlayerAdditionalBodyCreation CreateSense()
    {
        PlayerKickHandle handle = parts.CreateSense(Cheats);
        return new(handle, parts.SourceOrder[0].State);
    }
    public PlayerAdditionalBodyCreation CreateFist(int slot)
    {
        PlayerKickHandle handle = parts.CreateFist(slot, Cheats);
        return new(handle, parts.SourceOrder[0].State);
    }
    public void Delete(PlayerAdditionalBody body) => parts.Delete(parts.SourceOrder.Single(native => ReferenceEquals(native.State, body)));
}
