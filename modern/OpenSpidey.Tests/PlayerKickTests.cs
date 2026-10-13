using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerKickTests
{
    private sealed record Identity(int Id);
    private sealed class Host(PlayerKickState state, JsonElement input) : IPlayerKickHost
    {
        public uint Clock => input.GetProperty("clock").GetUInt32();
        public List<int> Events { get; } = [];
        public int[] Sounds { get; } = [unchecked((int)0xAAAB0023), unchecked((int)0xCCDDFFFF), -1];
        public bool SoundsSelected { get; private set; }
        public PlayerKickSwitchSelection? SwitchOverride { get; set; }
        public IReadOnlyList<PlayerKickObject> SourceBaddyOrder { get; } = ReadBodies(input.GetProperty("baddies"));
        public IReadOnlyList<PlayerKickObject> SourceEnvironmentalOrder { get; } = ReadBodies(input.GetProperty("objects"));
        public PlayerKickObject? Get(int id) => SourceBaddyOrder.Concat(SourceEnvironmentalOrder).FirstOrDefault(body => ((Identity)body.Identity).Id == id);
        public PlayerKickHandle MakeHandle(PlayerKickObject target)
        {
            int id = ((Identity)target.Identity).Id;
            Events.AddRange([1, id]);
            return new(target.Identity, (uint)(id + 1000));
        }
        public PlayerKickTrace LineToItem(FixedVector3 start, FixedVector3 end, int mode)
        {
            if (start != state.Melee.Combat.Position) throw new InvalidOperationException("CheckKick trace lost source player start.");
            JsonElement source = input.GetProperty("objects").EnumerateArray().First(body => Vector(body, 3) == end);
            int hit = source[6].GetInt32(), distance = source[7].GetInt32();
            Events.AddRange([2, end.X, end.Y, end.Z, mode, hit, distance]);
            return new(Get(hit)?.Identity, distance);
        }
        public PlayerKickSwitchSelection SelectTargetSwitch(int maximumDistance, int minimumFacing, int weight, int facingWeight)
        {
            Events.AddRange([3, maximumDistance, minimumFacing, weight, facingWeight]);
            int id = input.GetProperty("switch").GetInt32();
            return SwitchOverride ?? new(new(Get(id)?.Identity, (uint)(id == 0 ? 0 : id + 1000)), Get(id)?.Position);
        }
        public void SelectAndResetAnimationSounds(ushort animation)
        {
            SoundsSelected = true;
            for (int index = 0; Sounds[index] != -1; index++) Sounds[index] &= 0xFFFF;
        }
        public void RunAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([5, animation, firstFrame, lastFrame, unchecked((int)state.Melee.Combat.ActionState)]);
        public void SetTargetTorsoAngle(FixedVector3 position) => Events.AddRange([6, position.X, position.Y, position.Z]);
        public void InitiateCombo(ushort move, int headStart) => Events.AddRange([7, move, headStart, unchecked((int)state.Melee.Combat.ActionState), state.Melee.AttackEntryClock, state.Melee.Combat.HeadingTurnTicksRemaining]);
        private static PlayerKickObject[] ReadBodies(JsonElement bodies) => bodies.EnumerateArray().Select(body => new PlayerKickObject(new Identity(body[0].GetInt32()), body[1].GetUInt16(), Vector(body, 3), body[2].GetInt32())).ToArray();
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerKickTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-kick.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            int Int(string key) => input.GetProperty(key).GetInt32();
            var combat = new PlayerCombatState(100, 100) { ActionState = input.GetProperty("action").GetUInt32(),
                HeadingTurnTicksRemaining = Int("heading"), IsCrawling = Int("crawl") != 0,
                Position = Vector(input.GetProperty("pos"), 0), ForwardDirection = Vector(input.GetProperty("forward"), 0) };
            var melee = new PlayerMeleeState(combat) { AttackEntryClock = Int("entry") };
            var state = new PlayerKickState(melee) { SurfaceTransition = (byte)Int("surface"), SwitchActionProgress = Int("progress") };
            var host = new Host(state, input);
            state.HeldObject = host.Get(Int("held")); state.TorsoTarget = host.Get(Int("torso"));
            state.NearbyBaddy = new(host.Get(1)?.Identity, 101);
            state.ManipulationTarget = new(host.Get(2)?.Identity, 102);
            state.SwitchTarget = new(host.Get(3)?.Identity, 103);
            melee.Input.Triggered[18] = (byte)Int("kick"); melee.Input.Triggered[19] = (byte)Int("punch");
            melee.Input.Mirrors[18] = (byte)Int("mirrork"); melee.Input.Mirrors[19] = (byte)Int("mirrorp");
            melee.Input.Triggered[16] = 37; melee.Input.Pressed[18] = 41; melee.Input.Mirrors[17] = 43;
            PlayerKickObject? originalHeld = state.HeldObject, originalTorso = state.TorsoTarget;
            bool result = PlayerKickSelection.Check(state, host);
            long[] actual = [result ? 1 : 0, melee.Input.Triggered[18], melee.Input.Triggered[19], melee.Input.Mirrors[18], melee.Input.Mirrors[19],
                combat.ActionState, combat.HeadingTurnTicksRemaining, state.SwitchActionProgress, melee.AttackEntryClock,
                Id(state.NearbyBaddy), state.NearbyBaddy.Validation, Id(state.ManipulationTarget), state.ManipulationTarget.Validation,
                Id(state.SwitchTarget), state.SwitchTarget.Validation, host.SoundsSelected ? 1 : 0, host.Sounds[0], host.Sounds[1]];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(18).Select(value => value.GetInt64())) &&
                host.Events.SequenceEqual(expected[18].EnumerateArray().Select(value => value.GetInt32())),
                $"CheckKick{index} matches compiled source gates, trigger consumption, throw/pickup/switch/combo priority, signed wrapping 3D distance/facing, list order, handle words, callback order and exact animation/SFX entry resets.");
            assert(melee.Input.Triggered[16] == 37 && melee.Input.Pressed[18] == 41 && melee.Input.Mirrors[17] == 43 &&
                ReferenceEquals(state.HeldObject, originalHeld) && ReferenceEquals(state.TorsoTarget, originalTorso) &&
                combat.Position == Vector(input.GetProperty("pos"), 0) && combat.ForwardDirection == Vector(input.GetProperty("forward"), 0),
                "Entry selection preserves unrelated input, actual held/torso references and source surface frame.");
            index++;
        }
        JsonElement ordinaryKick = document.RootElement.EnumerateArray().Select(test => test.GetProperty("input")).First(input =>
            input.GetProperty("kick").GetInt32() != 0 && input.GetProperty("punch").GetInt32() == 0 &&
            input.GetProperty("crawl").GetInt32() == 0 && input.GetProperty("surface").GetInt32() == 0 &&
            input.GetProperty("baddies").GetArrayLength() == 0 && input.GetProperty("objects").GetArrayLength() == 0);
        foreach (bool returnedAim in new[] { false, true })
        {
            var melee = new PlayerMeleeState(new PlayerCombatState(100, 100));
            melee.Input.Triggered[18] = 1;
            var state = new PlayerKickState(melee);
            var host = new Host(state, ordinaryKick) { SwitchOverride = new(new(returnedAim ? null : new Identity(99), 55),
                returnedAim ? new FixedVector3(1, 2, 3) : null) };
            PlayerKickSelection.Check(state, host);
            assert(melee.Combat.ActionState == (returnedAim ? 0x80000000u : 0x800u) && state.SwitchTarget.Validation == 55 &&
                host.Events.Contains(returnedAim ? 5 : 7),
                "Original PC switch branch follows the returned auto-aim point independently of the stored handle pointer.");
        }
    }

    private static int Id(PlayerKickHandle handle) => handle.Target is Identity identity ? identity.Id : 0;
    private static FixedVector3 Vector(JsonElement values, int offset) => new(values[offset].GetInt32(), values[offset + 1].GetInt32(), values[offset + 2].GetInt32());
}
