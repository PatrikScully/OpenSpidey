using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Game.Player;

public partial class GodotPlayerWebRuntime
{
    public GodotPlayerAdditionalBodies? AdditionalBodies { get; private set; }
    public GodotPlayerAdditionalEffects? AdditionalEffects { get; private set; }
    public GodotPlayerHandTrails? HandTrails { get; private set; }
    public GodotPlayerFlamingFists? FlamingFists { get; private set; }
    public GodotPlayerDistanceAttackPreparation? DistanceAttackPreparation { get; private set; }
    private IPlayerWaterEffectFrameHost? waterFrameHost;
    public bool PlayerEffectPhasesEnabled { get; private set; }

    /// <summary>Binds source water/status/trail/presentation phases only after their real owners have been verified and composed.</summary>
    public void EnablePlayerEffectPhases(IPlayerWaterEffectFrameHost actualSkyOwner)
    {
        ArgumentNullException.ThrowIfNull(actualSkyOwner);
        if (PlayerEffectPhasesEnabled) throw new InvalidOperationException("Source player effect phases are already bound.");
        if (AdditionalEffects is null || HandTrails is null || FlamingFists is null || Swing is null || Smash is null || player.Melee is null)
            throw new InvalidOperationException("Source effect phase activation requires the existing real additional, hand, ember, swing and posed player owners.");
        waterFrameHost = actualSkyOwner;
        player.Controller.BeforeDamageTimers += RestorePlayerWater;
        player.Controller.AfterDamageTimers += PreparePlayerAdditionalEffects;
        player.Melee.AfterPose += UpdatePlayerTrails;
        PlayerEffectPhasesEnabled = true;
    }

    private void RestorePlayerWater() => SourceWaterPostEffects.PreparePlayer(AdditionalEffects!.Water, waterFrameHost!);
    private void PreparePlayerAdditionalEffects() => AdditionalEffects!.Prepare();
    private void UpdatePlayerTrails()
    {
        HandTrails!.Update();
        Smash!.UpdateTrails();
    }
    private void DetachPlayerEffectPhases()
    {
        if (!PlayerEffectPhasesEnabled) return;
        player.Controller.BeforeDamageTimers -= RestorePlayerWater;
        player.Controller.AfterDamageTimers -= PreparePlayerAdditionalEffects;
        if (player.Melee is { } melee) melee.AfterPose -= UpdatePlayerTrails;
        PlayerEffectPhasesEnabled = false;
        waterFrameHost = null;
    }

    public void ConfigureDistanceAttack(GodotPlayerCombatRuntime combat, PlayerFollowCamera camera, PsxAssetLibrary items,
        SoundChannelController sounds, PlayerWaterEffectState water, Func<PlayerAdditionalBodyCheats> cheats)
    {
        if (AdditionalBodies is not null) throw new InvalidOperationException("Source additional bodies and distance cleanup already belong to this player.");
        if (SmokeTrails is null || Smash is null) throw new InvalidOperationException("Distance cleanup requires the existing four-slot source smoke family.");
        ArgumentNullException.ThrowIfNull(cheats);
        GodotPlayerAdditionalBodies parts = GodotPlayerAdditionalBodies.Create(items, new());
        try
        {
            player.AddChild(parts);
            HandTrails = new(player, SmokeTrails, Smash.TrailColour);
            AdditionalEffects = new(player, camera, parts, water, sounds, combat.ControllerFeedback, cheats, () => Actions.Clock, Actions.Random);
            FlamingFists = new(player, Embers, Actions.Random);
            DistanceAttackPreparation = new(player, combat, this, camera, parts, HandTrails, water);
            player.DeleteAdditionalPlayerBodies = parts.DeletePlayerOwnedParts;
            AdditionalBodies = parts;
        }
        catch
        {
            parts.Free(); HandTrails = null; AdditionalEffects = null; FlamingFists = null; DistanceAttackPreparation = null;
            throw;
        }
    }
}
