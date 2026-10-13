using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerHitTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerHitTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-hit.json")
            ?? throw new InvalidDataException("Player hit source fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            PlayerCombatState state = JsonSerializer.Deserialize<PlayerCombatState>(test.GetProperty("initial"))!;
            JsonElement context = test.GetProperty("context");
            var host = new Host(state)
            {
                CurrentSuit = context.GetProperty("CurrentSuit").GetInt32(), SymbioteRelated = context.GetProperty("SymbioteRelated").GetInt32(),
                Timer = context.GetProperty("Timer").GetUInt32(), CameraFlag180 = context.GetProperty("CameraFlag180").GetBoolean(),
                Sight = context.GetProperty("LineOfSight").GetBoolean()
            };
            int randomValue = context.GetProperty("RandomValue").GetInt32();
            ActorHit hit = JsonSerializer.Deserialize<ActorHit>(test.GetProperty("hit"));
            bool result = true;
            switch (test.GetProperty("mode").GetInt32())
            {
                case 0:
                    result = PlayerHitRules.Apply(state, host, hit, bound =>
                    {
                        host.Events.AddRange([20, bound, randomValue]);
                        return randomValue;
                    });
                    break;
                case 1: PlayerDeathRules.Enter(state, host); break;
                case 2: PlayerHitRecoveryRules.Step(state, host, test.GetProperty("completed").GetUInt16()); break;
                default: throw new InvalidDataException("Unknown player hit fixture mode.");
            }
            JsonElement expected = test.GetProperty("expected");
            assert(result == (expected.GetProperty("result").GetInt32() != 0),
                $"Player combat case {index}: source hit acceptance and directed early returns match.");
            JsonElement actualState = JsonSerializer.SerializeToElement(state);
            foreach (JsonProperty property in expected.GetProperty("state").EnumerateObject())
                assert(Same(actualState.GetProperty(property.Name), property.Value),
                    $"Player combat case {index}: {property.Name} matches recovered hit/death/recovery state.");
            long[] expectedEvents = expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(expectedEvents),
                $"Player combat case {index}: source boundary events differ. " +
                $"Actual=[{string.Join(',', host.Events)}]; expected=[{string.Join(',', expectedEvents)}]; " +
                $"input={test.GetRawText()}.");
            index++;
        }
    }

    private static bool Same(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind != expected.ValueKind) return false;
        if (expected.ValueKind == JsonValueKind.Object)
            return expected.EnumerateObject().All(property => actual.TryGetProperty(property.Name, out JsonElement value) && Same(value, property.Value));
        return actual.GetRawText() == expected.GetRawText();
    }

    private sealed class Host(PlayerCombatState state) : IPlayerHitHost
    {
        public int CurrentSuit { get; init; }
        public int SymbioteRelated { get; init; }
        public uint Timer { get; init; }
        public bool CameraFlag180 { get; init; }
        public bool Sight { get; init; }
        public List<long> Events { get; } = [];
        public void StopSound(int handle) => Events.AddRange([1, handle]);
        public void PutCameraBehind(int argument) => Events.AddRange([2, argument]);
        public void FadeSmokeTrail(int sourceSlot) => Events.AddRange([3, sourceSlot]);
        public void SmashHeldObject() => Events.Add(4);
        public void SetArmorTextures(bool enabled) => Events.AddRange([5, enabled ? 1 : 0]);
        public void SaveArmor(int amount, byte active) => Events.AddRange([6, amount, active]);
        public void StopMyXa() => Events.Add(7);
        public void ExitLookaroundMode() { Events.Add(8); state.LookaroundActive = false; }
        public void SwitchWebToBlob() => Events.Add(9);
        public void PlaySingleAnimation(ushort clip, int from, int to)
        {
            Events.AddRange([10, clip, from, to]); state.AnimationId = clip; state.AnimationFrame = from;
        }
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Events.AddRange([11, useTarget ? 1 : 0, target.X, target.Y, target.Z]);
        public void Electrify() => Events.Add(12);
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument)
        {
            Events.AddRange([13, from.X, from.Y, from.Z, to.X, to.Y, to.Z, flags, argument, Sight ? 1 : 0]);
            return Sight;
        }
        public void ResetDeathAnimationSounds(ushort clip) => Events.AddRange([14, clip]);
        public void RunAnimation(ushort clip, int from, int to)
        {
            Events.AddRange([15, clip, from, to]); state.AnimationId = clip; state.AnimationFrame = from;
        }
        public void DeleteDeathEffect() => Events.Add(16);
        public void SetCameraField12C(int value) => Events.AddRange([17, value]);
        public void SwitchToStandMode() { Events.Add(18); state.ActionState = 1; state.AnimationId = 0; state.AnimationFrame = 0; }
        public void SetLevelStatus(int value) => Events.AddRange([19, value]);
        public void PlaySound(ushort sound, int volume, int pitch) => Events.AddRange([21, sound, volume, pitch]);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) => Events.AddRange([22, sound, position.X, position.Y, position.Z, volume]);
    }
}
