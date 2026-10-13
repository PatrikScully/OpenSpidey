using OpenSpidey.Core.Player;
using static OpenSpidey.Core.Audio.SourceMusicPlaylists;

namespace OpenSpidey.Core.Audio;

/// <summary>Complete original459650 render-time music selection, request handles and fight gain application.</summary>
public sealed class SourceMusicController(SourceMusicState state, ISourceMusicPlayback playback)
{
    public SourceMusicState State { get; } = state ?? throw new ArgumentNullException(nameof(state));
    private readonly ISourceMusicPlayback playback = playback ?? throw new ArgumentNullException(nameof(playback));

    public void Render(uint vblank, int levelId, PlayerCombatState? player, int postWaterEffect, int endTraining)
    {
        State.TrackEnding = 0;
        if (postWaterEffect != 0 || endTraining != 0) { State.LastVblank = vblank; return; }
        if (State.LastVblank == vblank) return;
        int delta;
        if (vblank > State.LastVblank) delta = unchecked((int)(vblank - State.LastVblank));
        else { delta = 1; State.TrackClock = 6666; State.TrackIndex = 0; }

        if (IsFightLevel(levelId)) { if (player is not null) RenderFight(player, delta, levelId); }
        else if (SelectLevel(levelId, State.Phase) is { } playlist) RenderPlaylist(playlist, delta);
        State.LastVblank = vblank;
    }

    /// <summary>Actual SFX_Unpause1168/1169 music writes, invoked by the source pause owner.</summary>
    public void Resume(PlayerCombatState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        State.TrackClock = 6666; player.FightMusicClock = 6666;
    }

    private void RenderFight(PlayerCombatState player, int delta, int levelId)
    {
        player.FightMusicClock = unchecked(player.FightMusicClock + delta);
        State.FightIntroClock = unchecked(State.FightIntroClock + delta);
        if (player.FightMusicEnabled == 0 || (player.Field528 == 0 && player.Field52C == 0)) return;
        if (levelId != 0x506) { State.SelectedIntro = FightIntroPlaylist.None; State.IntroCompleted = 1; }
        else
        {
            if (State.FightVariant == 0) State.SelectedIntro = FightIntroPlaylist.First;
            else if (State.FightVariant == 1) State.SelectedIntro = FightIntroPlaylist.Second;
            Playlist? intro = State.SelectedIntro switch
            {
                FightIntroPlaylist.None => null, FightIntroPlaylist.First => IntroA,
                FightIntroPlaylist.Second => IntroB,
                _ => throw new InvalidDataException("The source music intro pointer has no known original playlist.")
            };
            if (intro is not null && State.IntroCompleted == 0 &&
                unchecked((uint)State.FightIntroClock) > unchecked((uint)ReadDuration(intro, State.FightIntroIndex)))
            {
                State.FightIntroClock = 0;
                State.FightIntroIndex = unchecked(State.FightIntroIndex + 1);
                if (unchecked((uint)State.FightIntroIndex) >= 3)
                {
                    player.FightMusicClock = unchecked(player.FightMusicTime + 1); State.IntroCompleted = 1;
                }
                else player.FightMusicVoiceHandle = playback.Play(unchecked((uint)(intro.Sounds[State.FightIntroIndex] | 0xC000)),
                    unchecked((short)player.Field52C), 0);
            }
        }

        if (player.FightMusicClock > player.FightMusicTime && State.IntroCompleted != 0)
        {
            State.FightIntroClock = 0;
            if (player.Field528 != 0) player.Field52C = Math.Min(unchecked((player.Field528 + 11) << 10), 0x3FFF);
            else if (player.Field52C > 0x3000) player.Field52C = unchecked(player.Field52C - 0x400);
            else if (player.Field52C > 0x800) player.Field52C = unchecked(player.Field52C - 0x800);
            else player.Field52C = 0;
            if (player.Field52C != 0)
            {
                player.FightMusicVoiceHandle = playback.Play(player.Field534 != 0 ? 0xC005u : 0xC004u,
                    unchecked((short)player.Field52C), 0);
                State.IntroCompleted = 0; State.FightVariant = player.Field534 != 0 ? 1 : 0;
                State.FightIntroIndex = 0; player.FightMusicClock = 0;
            }
        }
        short gain = unchecked((short)(unchecked(player.Field52C * player.FightMusicGain) >> 12));
        if (player.FightMusicFadeUp != 0)
        {
            playback.SetVoiceVolume(player.FightMusicVoiceHandle, gain);
            if (player.FightMusicGain >= 0x1000) player.FightMusicFadeUp = 0;
        }
        else if (player.FightMusicFadeDown != 0) playback.SetVoiceVolume(player.FightMusicVoiceHandle, gain);
    }

    private void RenderPlaylist(Playlist playlist, int delta)
    {
        bool wasIdle = State.TrackClock == 6666;
        State.TrackClock = unchecked(State.TrackClock + delta);
        if (!wasIdle && unchecked((uint)State.TrackClock) <= unchecked((uint)ReadDuration(playlist, State.TrackIndex)))
        {
            if (unchecked((uint)(ReadDuration(playlist, State.TrackIndex) - State.TrackClock)) <= 3) State.TrackEnding = 1;
            return;
        }
        int index = unchecked(State.TrackIndex + 1);
        if (unchecked((uint)index) >= playlist.Sounds.Length) index = playlist.WrapIndex;
        State.TrackIndex = index;
        playback.Play(unchecked((uint)(playlist.Sounds[index] | 0xC000)), 0x2000, 0);
        State.TrackClock = 0;
    }

    private static int ReadDuration(Playlist playlist, int index) => unchecked((uint)index) < playlist.Durations.Length
        ? playlist.Durations[index] : throw new InvalidDataException($"Original music playlist cursor {index} is outside its source table.");

    private static bool IsFightLevel(int level) => level is 0x101 or 0x102 or 0x103 or 0x104 or 0x301 or 0x303 or
        0x304 or 0x305 or 0x503 or 0x504 or 0x506 or 0x507 or 0x703 or 0x801 or 0x803;

    private static Playlist? SelectLevel(int level, int phase) => level switch
    {
        0x201 => Level0201, 0x202 when phase == 0 => Level0202a, 0x202 when phase == 1 => Level0202b,
        0x302 => Level0302, 0x401 => Level0401, 0x501 => Level0501, 0x502 => Level0502, 0x604 => Level0604,
        0x701 => Level0701, 0x702 => Level0702, 0x704 => Level0704,
        0x705 when phase == 0 => Level0705a, 0x705 when phase == 1 => Level0705b,
        0x802 => Level0802, 0x805 => Level0805,
        >= 0x901 and <= 0x904 => Level0901, >= 0x1101 and <= 0x1104 => Level1101,
        0x1302 => Level1302, 0x1303 => Level1303, 0x1701 => Level1701, _ => null
    };
}
