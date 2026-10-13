using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;

internal static class LevelOneBombTests
{
    private sealed class Host(long[] input) : ILevelOneBombHost
    {
        private int cursor = (int)input[36];
        public List<long> Events { get; } = [];
        public int Difficulty => (int)input[22];
        public uint Timer => (uint)input[23];
        public LevelOneBombPlayerBasis? PlayerBasis => input[29] != 0 ? new(Vector(30), Vector(33)) : null;
        private FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        public int Random(int bound)
        { int value = bound != 0 ? (cursor++ * 7 + 1) % bound : 0; Events.AddRange([6, bound, value]); return value; }
        public int GetGroundHeight(FixedVector3 position, int above, int below)
        { Events.AddRange([1, position.X, position.Y, position.Z, above, below]); return (int)input[11]; }
        public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode)
        {
            Events.AddRange([4, start.X, start.Y, start.Z, end.X, end.Y, end.Z, mode]);
            return input[12] != 0 ? new(this, default, Vector(13)) : null;
        }
        public void ShakeCamera(FixedVector3 position, int magnitude) => Events.AddRange([2, position.X, position.Y, position.Z, magnitude]);
        public void PlaySound(uint sound, FixedVector3 position, int pitch) => Events.AddRange([3, sound, position.X, position.Y, position.Z, pitch]);
        public void SignalLinkedActors(ushort node) => Events.AddRange([5, node]);
        public void PulseLinkedActors(ushort node) => Events.AddRange([7, node]);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(LevelOneBombTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.level-one-bomb.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var body = new ActorBodyMotion { Position = Vector(1), Velocity = Vector(4), Acceleration = Vector(7),
                AngularVelocity = new((short)input[38], -7, 123) };
            var manipulation = new ManipulationState((ushort)input[37], body) { ItemFlags = (ushort)input[16], Flags = (int)input[17] };
            var script = new ActorScriptState { ActorType = 401, NodeId = manipulation.Node, PendingSignals = (ushort)input[21] };
            var state = new LevelOneBombState(manipulation, script) { Armed = (byte)input[18],
                ExpirationSignalSent = (byte)input[19], GroundContact = (byte)input[20] };
            var countdown = new MissionCountdown { Rate = (uint)input[24], RemainingTicks = (uint)input[25],
                DisplayEnabled = (byte)input[26], Running = (byte)input[27], LastUpdateTick = (uint)input[28] };
            var host = new Host(input);
            switch (input[0])
            {
                case 0: LevelOneBombBehavior.Construct(state, countdown); break;
                case 1: LevelOneBombBehavior.Advance(state, countdown, (int)input[10], host); break;
                case 2: LevelOneBombBehavior.DoPhysics(state, countdown, (int)input[10], host); break;
                case 3: LevelOneBombBehavior.Smash(state, host, host.Random); break;
                case 4: LevelOneBombBehavior.Die(state, countdown, host); break;
                case 5: LevelOneBombBehavior.Dispose(countdown, host.Timer); break;
            }
            long[] actual = [body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
                manipulation.ItemFlags, manipulation.Flags, state.Armed, state.ExpirationSignalSent, state.GroundContact, script.PendingSignals,
                countdown.Rate, countdown.RemainingTicks, countdown.DisplayEnabled, countdown.Running, countdown.LastUpdateTick];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Original CL1A3Bomb source/PC case {index}: inherited type401 state, timer arm/stop, grounded friction/vent/wall response and exact virtual override state.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Original CL1A3Bomb source/PC case {index}: real100/72 ground probe, source current endpoint, ordered sound/shake, expiration signal/Die pulse and inherited Throw RNG.");
            index++;
        }
    }
}
