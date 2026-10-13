using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerAdditionalEffectTests
{
    private sealed record Body(int Identity, PlayerAdditionalBody State, bool Valid)
    {
        public bool Alive { get; set; } = true;
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerAdditionalEffectTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-additional-effects.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { Field5AC = (int)input[3], Field5B0 = (int)input[4], ElapsedTicks = (int)input[5] };
            var state = new PlayerAdditionalEffectState { SenseTicks = (int)input[0], SenseKind = (int)input[1], SenseSoundHandle = (uint)input[2] };
            var host = new Host(input);
            state.SenseBody = host.InitialHandle(0);
            for (int slot = 0; slot < 2; slot++) state.BonusFists[slot] = host.InitialHandle(slot + 1);
            var water = new PlayerWaterEffectState { Value = 73 };
            PlayerAdditionalEffectPreparation.Prepare(combat, state, water, host);
            long[] actual = [state.SenseTicks, state.SenseSoundHandle, water.Value, combat.Field5AC, combat.Field5B0,
                Host.Identity(state.SenseBody.Target), state.SenseBody.Validation, Host.Identity(state.BonusFists[0].Target), state.BonusFists[0].Validation,
                Host.Identity(state.BonusFists[1].Target), state.BonusFists[1].Validation];
            JsonElement expected = row.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Additional sense/fist source preparation{index}: signed/wrapped timer, shared water, SFX handle, grow/shrink and full/pointer-only empty handles.");
            long[][] bodies = host.Bodies.Select(body => new long[] { body.Identity, body.Alive ? 1 : 0, body.State.ModelIndex, body.State.Type,
                body.State.ItemFlags, body.State.BodyFlags, body.State.Scale.X, body.State.Scale.Y, body.State.Scale.Z, body.State.Rgb }).ToArray();
            JsonElement expectedBodies = expected[actual.Length];
            assert(bodies.Length == expectedBodies.GetArrayLength() && bodies.Select((body, position) =>
                body.SequenceEqual(expectedBodies[position].EnumerateArray().Select(value => value.GetInt64()))).All(value => value),
                $"Additional sense/fist source preparation{index}: genuine plain body16/model4 or8, constructor scaling, all cheat branches and source destruction.");
            assert(host.Events.SequenceEqual(expected[actual.Length + 1].EnumerateArray().Select(value => value.GetInt64())),
                $"Additional sense/fist source preparation{index++}: ordered sound/actuator/recovery/factory/list/deletion callbacks, with no extra recover on constructor pointer.");
        }
    }
    private sealed class Host : IPlayerAdditionalEffectPreparationHost
    {
        private readonly long[] input;
        public List<long> Events { get; } = [];
        public List<Body> Bodies { get; } = [];
        public PlayerAdditionalBodyCheats Cheats => new(input[9] != 0, input[10] != 0, input[11] != 0);
        public bool VibrationEnabled => input[12] != 0;
        public ushort ActuatorTime { get { Events.AddRange([8, 0, 1]); return (ushort)input[13]; } }
        public Host(long[] input)
        {
            this.input = input;
            for (int slot = 0; slot < 3; slot++) Bodies.Add(new(slot + 1, new(slot == 0 ? (ushort)504 : (ushort)505,
                slot == 0 ? (ushort)4 : (ushort)8) { ItemFlags = 0x8400, Scale = new(1111, 2222, 3333) }, input[6 + slot] != 2));
        }
        public PlayerKickHandle InitialHandle(int slot) => new(input[6 + slot] == 0 ? null : Bodies[slot], (uint)(1001 + slot));
        public static int Identity(object? target) => target is Body body ? body.Identity : 0;
        public uint PlaySenseSound() { Events.AddRange([6, 26, 8192, 0]); return 0x80000000; }
        public void StopSound(uint handle) => Events.AddRange([7, handle]);
        public void ActivateSenseActuator(ushort duration) => Events.AddRange([9, 0, duration, 1, 60]);
        public PlayerAdditionalBody? Recover(ref PlayerKickHandle handle)
        {
            Events.AddRange([4, Identity(handle.Target), handle.Validation]);
            if (handle.Target is not Body body) return null;
            if (!body.Valid) { handle = handle with { Target = null }; return null; }
            return body.State;
        }
        private PlayerAdditionalBodyCreation Create(PlayerAdditionalBody body)
        {
            var entry = new Body(Bodies.Count + 1, body, true); Bodies.Add(entry);
            Events.AddRange([0, entry.Identity, body.BodyFlags, 1, entry.Identity, 2, entry.Identity, 3, entry.Identity]);
            return new(new(entry, (uint)(1000 + entry.Identity)), body);
        }
        public PlayerAdditionalBodyCreation CreateSense() => Create(PlayerAdditionalBodyRules.CreateSense(Cheats));
        public PlayerAdditionalBodyCreation CreateFist(int slot) => Create(PlayerAdditionalBodyRules.CreateFist(Cheats));
        public void Delete(PlayerAdditionalBody body)
        {
            Body entry = Bodies.Single(value => ReferenceEquals(value.State, body)); entry.Alive = false;
            Events.AddRange([10, entry.Identity, 5, entry.Identity]);
            if (body.Type == 504) Events.AddRange([1, 0]);
        }
    }
}
