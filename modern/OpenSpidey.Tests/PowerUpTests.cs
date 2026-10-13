using System.Text.Json;
using OpenSpidey.Core.Actors.PowerUps;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

internal static class PowerUpTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PowerUpTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.powerup.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => unchecked((int)value.GetInt64())).ToArray();
            var state = new PowerUpState(); var host = new Host(state); byte result = 0;
            switch (test.GetProperty("kind").GetString())
            {
                case "constructor":
                    host.Height = input[4]; host.Progress.CollectedComicMask = (uint)input[5];
                    PowerUpBehavior.Construct(state, (ushort)input[0], Vector(input, 6), Vector(input, 9), (uint)input[1], input[2], input[3], host);
                    break;
                case "ai":
                    state.Type = (ushort)input[0]; state.Lifetime = (ushort)input[1]; state.IsThreeDimensional = (byte)input[2]; state.ItemFlags = (ushort)input[3];
                    state.FailedCollectionFlash = (byte)input[4]; state.FadeCounter = input[5]; state.PlayerDistance = (uint)input[6]; state.Falling = (byte)input[7];
                    state.GroundCheckFinished = (byte)input[8]; state.GroundHeight = input[9]; host.LogicFrame = input[10]; state.Timing.PrepareClock((uint)input[11], 0);
                    host.Height = input[12]; state.Health = (short)input[13]; state.Field11E = state.Field120 = 16; state.GroundClearance = 128;
                    state.AngularVelocityY = 24; state.AccelerationY = 32768; state.FrictionY = 5; state.WorkingPosition = new(123456, -234567, 345678);
                    state.Velocity = new(12345, 54321, -6789); state.SignalNode = ushort.MaxValue;
                    PowerUpBehavior.Update(state, host);
                    break;
                case "effect":
                    state.Type = (ushort)input[0]; state.Health = (short)input[1]; state.HasNode = (byte)input[2]; state.NodeId = 123; state.SignalNode = 321;
                    state.FadeCounter = input[3]; host.SpeechBusy = input[4] != 0; host.Clock = (uint)input[5]; state.LastSpeechClock = (uint)input[6];
                    host.Accepted = input[7] != 0; host.LevelId = input[8]; host.PlayerMaxHealth = input[9]; host.Revision = input[10];
                    host.Progress.CollectedComicMask = (uint)input[11]; host.Progress.CostumeUnlockFlags = (uint)input[12]; state.Position = new(123456, -234567, 345678);
                    result = PowerUpBehavior.TakeEffect(state, host);
                    break;
            }
            JsonElement expected = test.GetProperty("expected"); string context = $"Original full power-up {test.GetProperty("kind").GetString()} [{string.Join(',', input)}]";
            long[] actual = Snapshot(state), wanted = expected.GetProperty("state").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), context + $" state fields: actual [{string.Join(',', actual)}], expected [{string.Join(',', wanted)}].");
            assert(host.Events.SequenceEqual(expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt64())),
                context + $" exact boundary order: actual [{string.Join(',', host.Events)}], expected {expected.GetProperty("events")}.");
            assert(result == expected.GetProperty("result").GetInt32() && host.Revision == expected.GetProperty("revision").GetInt32() &&
                host.Progress.CollectedComicMask == expected.GetProperty("comic").GetUInt32() && host.Progress.CostumeUnlockFlags == expected.GetProperty("costume").GetUInt32(),
                context + " preserves actual effect result and shared pickup/save fields.");
        }
        for (int difficulty = 0; difficulty <= 3; difficulty++)
        {
            ushort? expectedType = difficulty == 3 ? null : difficulty <= 1 ? (ushort)15 : (ushort)14;
            assert(PowerUpBehavior.RemapType(14, difficulty) == expectedType, "Source difficulty wrapper preserves authored health14 suppression/remapping.");
            assert(PowerUpBehavior.RemapType(15, difficulty) == (difficulty == 3 ? 14 : 15) && PowerUpBehavior.RemapType(16, difficulty) == (difficulty == 3 ? 15 : 16), "Source hardest difficulty reduces health15/16 without replacing authored identities.");
        }
    }
    private static FixedVector3 Vector(IReadOnlyList<int> values, int offset) => new(values[offset], values[offset + 1], values[offset + 2]);
    private static long[] Snapshot(PowerUpState s) => [s.BodyFlags, s.ItemFlags, s.Tint,
        s.Position.X, s.Position.Y, s.Position.Z, s.WorkingPosition.X, s.WorkingPosition.Y, s.WorkingPosition.Z,
        s.Velocity.X, s.Velocity.Y, s.Velocity.Z, s.AccelerationY, s.FrictionX, s.FrictionY, s.FrictionZ,
        s.Yaw, s.AngularVelocityY, s.Health, s.Radius, s.PlayerDistance, s.HasNode, s.NodeId, s.SignalNode,
        s.IsThreeDimensional, s.Dropping, s.Falling, s.GroundCheckFinished, s.GroundClearance, s.GroundHeight,
        s.ShadowPosition.X, s.ShadowPosition.Y, s.ShadowPosition.Z, s.ShadowScale, s.ShadowThreshold,
        s.Field11E, s.Field120, s.Field122, s.FailedCollectionFlash, s.FadeCounter, s.Lifetime, s.LastSpeechClock,
        s.PickupBitPresent ? 1 : 0, s.GlowPresent ? 1 : 0];
    private sealed class Host(PowerUpState state) : IPowerUpHost
    {
        public List<long> Events { get; } = []; public CampaignProgress Progress { get; } = new();
        public int Height { get; set; } public bool Accepted { get; set; } public int Revision { get; set; }
        public int LogicFrame { get; set; } public uint Clock { get; set; } public int LevelId { get; set; }
        public int PlayerMaxHealth { get; set; } public bool SpeechBusy { get; set; }
        public int Random(int bound) { Events.AddRange([1, bound]); return bound - 1; }
        public int GroundHeight(FixedVector3 p, int above, int below) { Events.AddRange([2, p.X, p.Y, p.Z, above, below]); return Height; }
        public void ShadowOn() => Events.Add(3); public void KillShadow() => Events.Add(4);
        public void InitializeItemsBank() => Events.Add(5); public void SelectModel(uint checksum) => Events.AddRange([6, checksum, state.Health]);
        public void AttachToPowerUpList() => Events.Add(7); public void SendNodePulses(ushort node) => Events.AddRange([8, node]);
        public void SendSignal(ushort node) => Events.AddRange([9, node]); public void MarkDead() { }
        public void EmitGlow(GlowFlashParameters p) => Events.AddRange([10, p.Position.X, p.Position.Y, p.Position.Z, p.OddRadius,
            p.CentreRed, p.CentreGreen, p.CentreBlue, p.Points, 1, p.FringeWidth]);
        public void CreatePickupBit(uint tint) => Events.AddRange([11, state.Health]);
        public void SetPickupBitPosition(FixedVector3 p) => Events.AddRange([12, p.X, p.Y, p.Z]);
        public void DeletePickupBit() => Events.Add(13); public void DeleteGlow() => Events.Add(14);
        public bool IncreaseHealth(int amount) { Events.AddRange([15, amount]); return Accepted; }
        public bool IncreaseWebbing(int amount) { Events.AddRange([16, amount]); return Accepted; }
        public bool SetFireWebbing() { Events.Add(17); return Accepted; } public bool SetArmor(bool active) { Events.Add(18); return Accepted; }
        public void IncreasePickupRevision(int amount) => Revision = unchecked(Revision + amount);
        public void PlaySound(ushort sound, int volume, int pitch) => Events.AddRange([20, sound, volume, pitch]);
        public void PlaySpeech(int bank, int slot, int priority) => Events.AddRange([19, bank, slot, priority]);
    }
}
