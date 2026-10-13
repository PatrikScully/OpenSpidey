using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

internal static class PlayerFightMusicFadeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlayerFightMusicFadeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-fight-music-fade.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] v = Read(test.GetProperty("input"));
            var state = new PlayerCombatState(73, 100) { FightMusicEnabled = (byte)v[1], FightMusicGain = v[2],
                FightMusicFadeDown = (byte)v[3], FightMusicFadeUp = (byte)v[4], FightMusicTime = 54321 };
            int family = test.GetProperty("family").GetInt32();
            if (family == 0) PlayerFightMusicFade.Advance(state, v[5]);
            else if (family == 1)
                new FightMusicFadeCommandHandler((ushort)v[0], () => state).Execute(new(new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty), 0),
                    new TriggerInstruction((ushort)v[0], 0, ReadOnlyMemory<byte>.Empty));
            else PlayerFightMusicFade.InitializeLevel(state, v[0]);
            int[] actual = [state.FightMusicEnabled, state.FightMusicGain, state.FightMusicFadeDown, state.FightMusicFadeUp];
            int[] expected = Read(test.GetProperty("expected"));
            assert(actual.SequenceEqual(expected), $"Original fight-music case{index++} family{family}: input[{string.Join(',', v)}], actual[{string.Join(',', actual)}], expected[{string.Join(',', expected)}].");
            assert(state.FightMusicTime == 54321 && state.Health == 73 && state.ActionState == 1,
                "Source fade command/AI modifies only C5C/C64/C68/C69, retaining C60 duration, health and player action.");
        }
    }
    private static int[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt32()).ToArray();
}
