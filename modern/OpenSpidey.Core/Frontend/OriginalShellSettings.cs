using OpenSpidey.Core.Saves;

namespace OpenSpidey.Core.Frontend;

/// <summary>Process-global gGameState slots and G_BOOT_ROM_SOUND_MODE the shell edits (gGameState 0x6B4814).</summary>
public sealed class OriginalShellSettings
{
    public const short DefaultMusicAndSfx = 11087, DefaultMovie = 177, DefaultVoice = 201;
    private readonly short[] mapping = new short[8];
    /// <summary>gGameState[12], 0..0x3FFF.</summary>
    public short MusicAndSfxLevel { get; set; } = DefaultMusicAndSfx;
    /// <summary>gGameState[13], 0..255.</summary>
    public short VoiceLevel { get; set; } = DefaultVoice;
    /// <summary>gGameState[11], 0..255.</summary>
    public short MovieLevel { get; set; } = DefaultMovie;
    /// <summary>Nonzero selects mono.</summary>
    public byte BootRomSoundMode { get; set; }
    public int DifficultyLevel { get; set; } = 2;
    /// <summary>0x60CFC7, set when kid mode is the difficulty.</summary>
    public bool PracticeDifficulty { get; set; }
    /// <summary>gGameState[0..7]: digital then analogue pad mappings.</summary>
    public ReadOnlySpan<short> PadMapping => mapping;
    public int ScreenX { get; private set; }
    public int ScreenY { get; private set; }

    /// <summary>PShell_ApplyGameState 0x47A430: the save supplies difficulty, mappings and screen offset; the live levels overwrite its copies.</summary>
    public void ApplyGameState(OriginalSaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        DifficultyLevel = save.Difficulty;
        PracticeDifficulty = save.Difficulty == 0;
        save.MovieLevel = MovieLevel;
        save.MusicAndSfxLevel = MusicAndSfxLevel;
        save.VoiceLevel = VoiceLevel;
        save.BootRomSoundMode = BootRomSoundMode;
        for (int index = 0; index < 4; index++)
        {
            mapping[index] = save.DigitalMapping(index);
            mapping[4 + index] = save.AnalogueMapping(index);
        }
        ScreenX = save.ScreenX;
        ScreenY = (short)save.ScreenY;
    }

    /// <summary>Shell_SFXMusic 0x4977D0 left/right step for the current line; returns true when the value changed.</summary>
    public bool StepSlider(int line, int direction, OriginalSaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (direction is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(direction));
        switch (line)
        {
            case 0:
                if (direction == 0) return false;
                MusicAndSfxLevel = (short)Math.Clamp(MusicAndSfxLevel + (direction << 14) / 16, 0, 0x3FFF);
                save.MusicAndSfxLevel = MusicAndSfxLevel;
                return true;
            case 1:
                if (direction == 0) return false;
                VoiceLevel = (short)Math.Clamp(VoiceLevel + (direction << 8) / 16, 0, 255);
                save.VoiceLevel = VoiceLevel;
                return true;
            case 2:
                MovieLevel = (short)Math.Clamp(MovieLevel + (direction << 8) / 16, 0, 255);
                save.MovieLevel = MovieLevel;
                return direction != 0;
            default:
                throw new ArgumentOutOfRangeException(nameof(line));
        }
    }

    /// <summary>Shell_SFXMusic "audio": DCSetBootROMSoundMode toggle copied into the save.</summary>
    public void ToggleSoundMode(OriginalSaveGame save)
    {
        BootRomSoundMode = BootRomSoundMode == 0 ? (byte)1 : (byte)0;
        save.BootRomSoundMode = BootRomSoundMode;
    }

    /// <summary>Shell_SFXMusic "initial settings".</summary>
    public void ResetLevels(OriginalSaveGame save)
    {
        MusicAndSfxLevel = DefaultMusicAndSfx;
        MovieLevel = DefaultMovie;
        VoiceLevel = DefaultVoice;
        save.MusicAndSfxLevel = DefaultMusicAndSfx;
        save.MovieLevel = DefaultMovie;
        save.VoiceLevel = DefaultVoice;
    }

    /// <summary>DrawSlider 0x498060 knob value for each line.</summary>
    public int SliderValue(int line) => line switch
    {
        0 => (MusicAndSfxLevel << 8) / 0x3FFF,
        1 => VoiceLevel,
        2 => MovieLevel,
        _ => throw new ArgumentOutOfRangeException(nameof(line))
    };

    /// <summary>DrawSlider knob x: values above 256 or equal to 255 pin to the end.</summary>
    public static int KnobX(int x, int value)
    {
        if (value < 0) value = 0;
        else if (value > 256 || value == 255) value = 256;
        return ((180 * value) >> 8) + x + 9;
    }
}
