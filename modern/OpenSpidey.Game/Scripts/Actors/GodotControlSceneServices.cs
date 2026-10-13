using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.Actors;

/// <summary>Canonical Panel compass and real positional SFX owners, available before CPlayer.</summary>
public sealed class GodotControlSceneServices : IControlActorEffectHost
{
    private readonly SoundChannelController sounds;
    private readonly Func<SoundListener> listener;
    private readonly Func<uint> clock;
    public CompassState Compass { get; }
    public uint Clock => clock();

    public GodotControlSceneServices(CompassState compass, SoundChannelController sounds,
        Func<SoundListener> listener, Func<uint> clock)
    {
        Compass = compass ?? throw new ArgumentNullException(nameof(compass));
        this.sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        this.listener = listener ?? throw new ArgumentNullException(nameof(listener));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    public void CreateCompass(FixedVector3 position) => Compass.Create(position);
    public void DestroyCompass() => Compass.Destroy();
    public void FlashCompass() => Compass.FlashTimer = 300;
    public uint PlayGlobalSound(uint sound) => sounds.Play(sound);
    public uint PlayPositionSound(uint sound, FixedVector3 position) => sounds.PlayAt(sound, position, listener());
    public void StopSound(uint handle) => sounds.Stop(handle);
    public void UpdateSoundPosition(uint handle, FixedVector3 position) => sounds.UpdatePosition(handle, position, listener());
}
