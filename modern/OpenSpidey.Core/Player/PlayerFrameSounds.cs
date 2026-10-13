namespace OpenSpidey.Core.Player;

public readonly record struct PlayerFrameSound(uint Sound, short Volume, int PitchOffset);

/// <summary>Recovered one-event-per-update frame triggers and nonrepeating random footsteps.</summary>
public sealed class PlayerFrameSounds(Func<int, int>? random = null)
{
    private static readonly IReadOnlyDictionary<int, int[]> triggers = new Dictionary<int, int[]>
    {
        [21] = [1, 11], [59] = [4], [52] = [7, 15], [50] = [6], [51] = [6],
        [60] = [6, 15], [63] = [7, 19, 26], [192] = [2, 14], [198] = [1, 21]
    };
    private readonly Func<int, int> nextRandom = random ?? Random.Shared.Next;
    private int[] frames = [];
    public int Cursor { get; private set; }
    public uint LastFootstep { get; private set; }
    public bool Active { get; private set; }

    public void Select(int animation)
    {
        frames = triggers.TryGetValue(animation, out int[]? found) ? found : [];
        Cursor = 0;
        Active = frames.Length != 0;
    }

    public PlayerFrameSound? Update(int currentAnimation, int currentFrame, bool useLevelAliases = false)
    {
        if (!Active) return null;
        if (Cursor == frames.Length) { Active = false; return null; }
        if (currentFrame < frames[Cursor]) return null;
        uint? sound = null;
        switch (currentAnimation)
        {
            case 21: case 192: case 198:
                do { sound = (uint)(nextRandom(4) + (useLevelAliases ? 80 : 1)) | (useLevelAliases ? 0x8000u : 0); }
                while (sound == LastFootstep);
                LastFootstep = sound.Value;
                break;
            case 50: case 51: case 52: case 60: case 63: sound = (uint)(nextRandom(2) + 5); break;
            case 59: sound = 9; break;
        }
        Cursor++;
        return sound is { } id ? new(id, 0x2000, 0) : null;
    }
}
