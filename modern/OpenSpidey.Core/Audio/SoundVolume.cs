using System.Text.Json;

namespace OpenSpidey.Core.Audio;

/// <summary>Shared signed-volume conversion through the PC SFX attenuation curve.</summary>
public static class SoundVolume
{
    private static readonly int[] curve = ReadCurve();
    public static float Decibels(int volume)
    {
        if (volume == int.MinValue || Math.Abs(volume) >= 16384)
            throw new InvalidDataException("Sound volume is outside the recovered signed range.");
        int index = Math.Min(Math.Abs(volume) >> 5, 255);
        return (-39 * curve[index]) / 100f;
    }
    private static int[] ReadCurve()
    {
        using Stream stream = typeof(SoundVolume).Assembly.GetManifestResourceStream("OpenSpidey.Core.Audio.sound-volume.json")!;
        return JsonSerializer.Deserialize<int[]>(stream) ?? throw new InvalidDataException("Sound attenuation metadata is missing.");
    }
}
