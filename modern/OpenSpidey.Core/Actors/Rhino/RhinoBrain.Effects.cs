using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

public sealed partial class RhinoBrain
{
    private readonly ActorHook[] dazedHooks = new ActorHook[RhinoState.DazedStarCount];

    /// <summary>CRhino::PlaySounds (0x481550): footsteps keyed to animation frames through the field_388 bits.</summary>
    public void PlaySounds()
    {
        int frame = host.AnimationFrame;
        switch (host.AnimationId)
        {
            case 12:
                if ((state.Field388 & 1) == 0 && frame >= 3) Footstep(1);
                else if ((state.Field388 & 2) == 0 && frame >= 0xD) Footstep(2);
                else if ((state.Field388 & 4) == 0 && frame >= 0x15) Footstep(4);
                else if ((state.Field388 & 8) == 0 && frame >= 0x1E) Footstep(8);
                break;
            case 5:
                if ((state.Field388 & 1) == 0 && frame >= 0) Footstep(1);
                else if ((state.Field388 & 2) == 0 && frame >= 8) Footstep(2);
                break;
            case 2:
                if ((state.Field388 & 1) == 0 && frame >= 0) Footstep(1);
                else if ((state.Field388 & 2) == 0 && frame >= 0xF) Footstep(2);
                break;
            case 18:
                if ((state.Field388 & 1) == 0 && frame >= 0x15) state.Field388 |= 1;
                break;
        }
    }

    /// <summary>CRhino::GetNextFootstepSFX, inlined: (Rnd(3)+76)|0x8000 until it differs from gRhinoSound.</summary>
    private void Footstep(int bit)
    {
        int sound;
        do sound = (host.Random(3) + 76) | 0x8000;
        while (sound == state.LastFootstepSound);
        state.LastFootstepSound = sound;
        host.PlayPositionalSound(sound);
        state.Field388 |= bit;
    }

    /// <summary>CRhino::DoDazedEffect (0x480480): orbit angles, Rnd(64) speed jitter and star hooks.</summary>
    private void DoDazedEffect()
    {
        bool spawn = host.AnimationId is 0x11 or 0x12;
        if (!spawn && state.DazedStarRed == 0) return;
        for (int index = 0; index < RhinoState.DazedStarCount; index++)
        {
            int angle = state.DazedStarAngles[index];
            (short sine, short cosine) = GameAngle.Heading(angle & 0xFFF);
            short bob = GameAngle.Heading(state.DazedStarSpeeds[index] & 0xFFF).Sine;
            dazedHooks[index] = new(
                unchecked((short)((768 * sine) >> 12)),
                unchecked((short)(((bob << 7) >> 12) - 320)),
                unchecked((short)(((768 * cosine) >> 12) - 416)),
                15);
            state.DazedStarAngles[index] = unchecked((short)((angle + (host.ElapsedTicks << 6)) & 0xFFF));
            state.DazedStarSpeeds[index] = unchecked((short)((state.DazedStarSpeeds[index] + host.Random(64) + 64) & 0xFFF));
        }
        // field_3E4[0] gates the next update: created at red 128, faded by 4 per update outside clips 17/18,
        // deleted once red <= 4. Respawning over an existing bit keeps its faded colour.
        if (spawn)
        {
            if (state.DazedStarRed == 0) state.DazedStarRed = 128;
        }
        else state.DazedStarRed = state.DazedStarRed <= 4 ? (byte)0 : unchecked((byte)(state.DazedStarRed - 4));
        host.UpdateDazedStars(dazedHooks, spawn);
    }

    /// <summary>The AI tail's snort: CRhinoNasalSteam puffs from both nostrils during the source frame windows.</summary>
    private void DoNasalSteam()
    {
        int animation = host.AnimationId, frame = host.AnimationFrame;
        bool snorting = (animation == 0 && frame is >= 21 and <= 38) ||
            (animation == 9 && unchecked((ushort)frame) < 10) ||
            (animation == 15 && frame is >= 1 and <= 12);
        if (!snorting)
        {
            state.CombatFlags &= ~4u;
            return;
        }
        if ((state.CombatFlags & 4) == 0)
        {
            host.PlayPositionalSound(((host.AttackClock & 1) == 0 ? 1 : 0) | 0x8040);
            state.CombatFlags |= 4;
        }
        host.EmitNasalSteam(new(-32, 128, -640, 15), new(-48, 176, -672, 15), host.Random(4096));
        host.EmitNasalSteam(new(32, 128, -640, 15), new(48, 176, -672, 15), host.Random(4096));
    }
}
