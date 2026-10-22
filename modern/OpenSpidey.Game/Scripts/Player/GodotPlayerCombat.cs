using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Native player damage and posed-part contact adapter with explicit source effect ownership.</summary>
public sealed class GodotPlayerCombat : IPlayerHitHost, IPlayerDamageTimerHost, IPlayerDeathProgressHost, IMissionFightMusicHost
{
    private readonly GodotPlayer player;
    private IReadOnlyList<ActorPartBox>? collisionBoxes;
    private FixedJointTransform[] collisionPose = [];
    public PlayerCombatState State { get; }
    public int FightMusicTime { get => State.FightMusicTime; set => State.FightMusicTime = value; }
    public void InitializeFightMusicLevel(int levelId) => PlayerFightMusicFade.InitializeLevel(State, levelId);
    public ActorSpeechAttachment SpeechAttachment { get; } = new();
    public uint HiddenParts { get; set; }
    public Func<int>? Suit { get; set; }
    public Func<int>? Symbiote { get; set; }
    public Func<uint>? SourceTimer { get; set; }
    public Func<bool>? CameraFlag { get; set; }
    public Func<bool>? Vibration { get; set; }
    public Action<ushort, int, int>? Sound { get; set; }
    public Action<ushort, FixedVector3, int>? PositionSound { get; set; }
    public Action<int>? SoundStopped { get; set; }
    public Action? XaStopped { get; set; }
    public Action<int>? CameraBehind { get; set; }
    public Action<int>? CameraField12C { get; set; }
    public Action<int>? LevelStatus { get; set; }
    public Action? FlashReset { get; set; }
    public Action? InitializationReset { get; set; }
    public Action<int>? SmokeFade { get; set; }
    public Action? HeldObjectSmash { get; set; }
    public Action<bool>? ArmorTextures { get; set; }
    public Action<int, byte>? ArmorSaved { get; set; }
    public Action? LookaroundExited { get; set; }
    public Action? WebToBlob { get; set; }
    public Action? ElectricEffect { get; set; }
    public Action? ElectricEffectRemoved { get; set; }
    public Action? DeathEffectDeleted { get; set; }
    public Action<int, ushort, int, int>? PadActuator { get; set; }
    public int CurrentSuit => Suit?.Invoke() ?? 0;
    public int SymbioteRelated => Symbiote?.Invoke() ?? 0;
    public uint Timer => SourceTimer?.Invoke() ?? player.Controller.SourceTimeTicks;
    public bool CameraFlag180 => CameraFlag?.Invoke() ?? false;
    public bool VibrationEnabled => Vibration?.Invoke() ?? false;
    public int AcceptedHits { get; private set; }

    public GodotPlayerCombat(GodotPlayer player, short health, int maxHealth)
    {
        this.player = player;
        State = new(health, maxHealth);
        player.Controller.ConfigureCombat(State, this, this, this, player.CostumeVisibility);
    }

    public void ConfigureCollisionBoxes(IReadOnlyList<ActorPartBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        if (boxes.Count == 0) throw new ArgumentException("Player collision requires the original part boxes.", nameof(boxes));
        collisionBoxes = boxes;
        collisionPose = new FixedJointTransform[boxes.Count];
    }

    public bool CollideWithPlayer(FixedVector3 from, FixedVector3 to, int scale, out ActorHook hit)
    {
        if (collisionBoxes is null) throw new InvalidOperationException("Player combat collision requires original PSX model-header boxes.");
        for (int part = 0; part < collisionPose.Length; part++) collisionPose[part] = player.Model.GetSourceAnimationPartPose(part);
        return ActorPartCollision.TryCollide(player.Controller.Position, player.Controller.Orientation.Basis,
            collisionPose, collisionBoxes, HiddenParts, from, to, scale, out hit);
    }

    /// <summary>CPlayer::KnockSpideyFromCrawlPosition (0x4BB710).</summary>
    public bool KnockFromCrawlPosition()
    {
        bool knocked = player.Controller.KnockFromCrawlPosition();
        player.RefreshCombatTransform();
        return knocked;
    }

    public bool Hit(ActorHit hit)
    {
        State.SynthesizedInputActive = player.Scripts?.Active == true;
        bool accepted = player.Controller.Hit(hit);
        if (accepted) AcceptedHits++;
        player.RefreshCombatTransform();
        return accepted;
    }

    public void StopSound(int handle) => Require(SoundStopped, nameof(SoundStopped))(handle);
    public void StopFightMusic()
    {
        uint handle = State.FightMusicVoiceHandle;
        State.Field528 = State.Field52C = 0;
        if (handle == 0) return;
        StopSound(unchecked((int)handle));
        State.FightMusicVoiceHandle = 0;
    }
    public void PutCameraBehind(int argument) => Require(CameraBehind, nameof(CameraBehind))(argument);
    public void FadeSmokeTrail(int sourceSlot) => Require(SmokeFade, nameof(SmokeFade))(sourceSlot);
    public void SmashHeldObject() => Require(HeldObjectSmash, nameof(HeldObjectSmash))();
    public void SetArmorTextures(bool enabled) => Require(ArmorTextures, nameof(ArmorTextures))(enabled);
    public void SaveArmor(int amount, byte active) => Require(ArmorSaved, nameof(ArmorSaved))(amount, active);
    public void PlaySound(ushort sound, int volume, int pitch) => Require(Sound, nameof(Sound))(sound, volume, pitch);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) => Require(PositionSound, nameof(PositionSound))(sound, position, pitchOffset);
    public void StopMyXa() => Require(XaStopped, nameof(XaStopped))();
    public void ExitLookaroundMode() => Require(LookaroundExited, nameof(LookaroundExited))();
    public void SwitchWebToBlob() => Require(WebToBlob, nameof(WebToBlob))();
    public void Electrify() => Require(ElectricEffect, nameof(ElectricEffect))();
    public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument)
    {
        if (flags != 0 || argument != 0) throw new NotSupportedException("Player hit LOS adapter only accepts the recovered zero flags/argument.");
        return player.Controller.CombatLineOfSight(from, to);
    }
    public void PlaySingleAnimation(ushort clip, int from, int to) => player.Animation.Run(clip, from, to);
    public void RunAnimation(ushort clip, int from, int to) => player.Model.RunClip(clip, from, to);
    public void OrientToNormal(bool useTarget, FixedVector3 target) => player.Controller.OrientFromCombat(useTarget, target);
    public void ResetDeathAnimationSounds(ushort clip) => player.FrameSounds.Select(clip);
    public void DeleteDeathEffect() => Require(DeathEffectDeleted, nameof(DeathEffectDeleted))();
    public void SetCameraField12C(int value) => Require(CameraField12C, nameof(CameraField12C))(value);
    public void SwitchToStandMode()
    {
        player.Controller.ApplyCombatState();
        player.Controller.Stand();
        player.Controller.CaptureCombatState(State.ElapsedTicks);
    }
    public void SetLevelStatus(int value) => Require(LevelStatus, nameof(LevelStatus))(value);
    public void ActuatorOn(int controller, ushort time, int argument, int strength) => Require(PadActuator, nameof(PadActuator))(controller, time, argument, strength);
    public void UnElectrify() => Require(ElectricEffectRemoved, nameof(ElectricEffectRemoved))();
    public void ResetInitializationFlag() => Require(InitializationReset, nameof(InitializationReset))();
    public void ResetFlash() => Require(FlashReset, nameof(FlashReset))();

    private static T Require<T>(T? callback, string sourceEffect) where T : class =>
        callback ?? throw new InvalidOperationException($"Player combat source effect {sourceEffect} is not connected.");
}
