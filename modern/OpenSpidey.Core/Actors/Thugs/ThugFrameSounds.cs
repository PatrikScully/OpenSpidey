using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugFrameSoundHost
{
    /// <summary>Original SFX_PlayPos call: the last argument changes pitch, not volume or falloff.</summary>
    void PlayFrameSound(uint sound, FixedVector3 position, int pitchOffset);
    /// <summary>Original CBaddy::MakeSpriteRing boundary, including local rotation and ground placement.</summary>
    void MakeSpriteRing(FixedVector3 localOffset);
}

/// <summary>Recovered CThug::PlaySounds (0x4DA300), called once by each AI update.
/// Frame equality is deliberate: skipped frames miss cues and repeated frames repeat them.</summary>
public static class ThugFrameSounds
{
    public static void Update(int actorType, ushort animation, ushort frame, FixedVector3 position,
        IThugFrameSoundHost host, Func<int, int> random)
    {
        if (actorType == 304)
        {
            switch (animation)
            {
                case 14 when frame == 15: host.MakeSpriteRing(new(0, 0, 409600)); break;
                case 26 when frame == 23: host.MakeSpriteRing(new(0, 0, -122880)); break;
                case 1 when frame is 27 or 13: Step(300); break;
                case 28 when frame is 4 or 14: Step(50); break;
                case 8 when frame == 5: host.PlayFrameSound(0x801D, position, 500); break;
            }
        }
        else
        {
            switch (animation)
            {
                case 26 when frame == 28: host.MakeSpriteRing(new(0, 0, -204800)); break;
                case 19 when frame == 15: host.MakeSpriteRing(new(0, 0, 204800)); break;
                case 5 when frame is 1 or 21: Step(300); break;
                case 24 when frame is 1 or 12: Step(50); break;
                case 7 when frame == 3: host.PlayFrameSound(0x801D, position, 500); break;
            }
        }

        void Step(int pitchOffset) => host.PlayFrameSound(unchecked((uint)((random(4) + 32) | 0x8000)), position, pitchOffset);
    }
}
