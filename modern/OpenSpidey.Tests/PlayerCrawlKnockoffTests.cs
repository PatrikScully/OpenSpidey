using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

/// <summary>CPlayer::KnockSpideyFromCrawlPosition (0x4BB710) against tools/reference_player_crawl_knockoff_vectors.py.</summary>
internal static class PlayerCrawlKnockoffTests
{
    private static readonly FixedVector3 Forward = new(1, 2, 3), Up = new(4, 5, 6);

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerCrawlKnockoffTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-crawl-knockoff.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var state = new PlayerCombatState(100, 0)
            {
                IsCrawling = input[0] != 0, Field8E8 = (byte)input[1], Field8E9 = (byte)input[2], LookaroundActive = input[3] != 0,
                ActionState = unchecked((uint)input[4]), CrawlSurfaceNormal = new(0, 77, 0), FieldAE5 = 9,
                ForwardDirection = Forward, UpDirection = Up
            };
            var host = new Host();
            bool result = PlayerCrawlKnockoff.Apply(state, host);
            JsonElement expected = test.GetProperty("expected");
            int[] fields = expected.GetProperty("fields").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            int[] actual =
            [
                state.IsCrawling ? 1 : 0, state.Field8E8, state.Field8E9, state.LookaroundActive ? 1 : 0, unchecked((int)state.ActionState),
                state.CrawlSurfaceNormal.X, state.CrawlSurfaceNormal.Y, state.CrawlSurfaceNormal.Z, state.FieldAE5, host.SoundsSelected ? 1 : 0
            ];
            assert((result ? 1 : 0) == expected.GetProperty("ret").GetInt32() && actual.SequenceEqual(fields.Take(actual.Length)),
                $"Actual PC KnockSpideyFromCrawlPosition case{index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', fields)}].");
            string wanted = string.Join(' ', expected.GetProperty("events").EnumerateArray()
                .Select(e => $"[{string.Join(',', e.EnumerateArray().Select(v => v.GetInt32()))}]"));
            assert(string.Join(' ', host.Events) == wanted, $"KnockSpideyFromCrawlPosition case{index} events {string.Join(' ', host.Events)} vs {wanted}.");
            index++;
        }
        assert(index == 32, "The crawl knock-off fixture keeps every crawl/wall/ceiling/lookaround combination.");
    }

    private sealed class Host : IPlayerHitHost
    {
        public List<string> Events { get; } = [];
        public bool SoundsSelected { get; private set; }
        public void ExitLookaroundMode() => Events.Add("[1]");
        public void ResetDeathAnimationSounds(ushort clip) => SoundsSelected = clip == 175;
        public void RunAnimation(ushort clip, int from, int to) => Events.Add($"[2,{clip},{from},{to}]");
        public void OrientToNormal(bool useTarget, FixedVector3 target) =>
            Events.Add($"[3,{(useTarget ? 1 : 0)},{(target == Up ? 3204 : target == Forward ? 3180 : -1)}]");
        public int CurrentSuit => throw Unused();
        public int SymbioteRelated => throw Unused();
        public uint Timer => throw Unused();
        public bool CameraFlag180 => throw Unused();
        public void StopSound(int handle) => throw Unused();
        public void PutCameraBehind(int argument) => throw Unused();
        public void FadeSmokeTrail(int sourceSlot) => throw Unused();
        public void SmashHeldObject() => throw Unused();
        public void SetArmorTextures(bool enabled) => throw Unused();
        public void SaveArmor(int amount, byte active) => throw Unused();
        public void PlaySound(ushort sound, int volume, int pitch) => throw Unused();
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) => throw Unused();
        public void StopMyXa() => throw Unused();
        public void SwitchWebToBlob() => throw Unused();
        public void Electrify() => throw Unused();
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument) => throw Unused();
        public void PlaySingleAnimation(ushort clip, int from, int to) => throw Unused();
        public void DeleteDeathEffect() => throw Unused();
        public void SetCameraField12C(int value) => throw Unused();
        public void SwitchToStandMode() => throw Unused();
        public void SetLevelStatus(int value) => throw Unused();
        private static InvalidOperationException Unused() => new("KnockSpideyFromCrawlPosition never calls this.");
    }
}
