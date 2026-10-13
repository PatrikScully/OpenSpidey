using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;

internal static class PlayerCutsceneSkipTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(PlayerCutsceneSkipTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-cutscene-skip.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            string kind = test.GetProperty("kind").GetString()!;
            if (kind == "gate")
            {
                PlayerButtonState[] pad = Pad(input, 6);
                bool requested = PlayerCutsceneSkip.ReadManualInput(new((byte)input[2]), input[1] != 0,
                    input[3] != 0, input[4], pad);
                assert((requested ? 1 : input[5]) == expected[0].GetInt32(), $"Original PC manual skip gate{index} keeps the actual difficulty/force/active allow gates.");
                Verify(PadValues(pad), expected[1], "only original trigger bytes are consumed");
            }
            else if (kind == "command")
            {
                var state = new PlayerCutsceneSkipState(19);
                uint bank = 0xABCD0000;
                IMissionCommandHandler handler = PlayerStoryInputCommandHandler.CreateHandlers(state, value => bank = value)
                    .Single(value => value.Opcode == input[0]);
                byte[] arguments = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(arguments, (ushort)input[1]);
                handler.Execute(new(new TriggerNode(7, 6, 0, ReadOnlyMemory<byte>.Empty), 0), new((ushort)input[0], 2, arguments));
                Verify([state.Allowed, bank], expected, "222 byte normalization/223 unsigned dword and other-field preservation");
            }
            else if (kind == "cleanup")
            {
                var state = new PlayerCutsceneSkipState { FieldE00 = (uint)input[4] };
                var host = new CleanupHost(input);
                PlayerButtonState[] pad = Pad(input, 14);
                PlayerCutsceneSkip.Cleanup(state, host, pad);
                Verify([host.CameraMode, state.FieldE00, host.Abort ? 1 : 0], expected, "cleanup mode/E00/abort", 3);
                Verify(PadValues(pad), expected[3], "cleanup forces pressed Cross/virtual Jump and preserves all other pad bytes");
                Verify(host.Events, expected[4], "whole cleanup callback order and real fixed-point checkpoint direction");
            }
            else
            {
                var state = new PlayerCameraStartup((byte)input[2]);
                var host = new StartupHost(input[0], input[1] != 0, input[3]);
                state.Advance(host);
                Verify([state.Initialized, host.FloorState], expected, "first AI53C/540 fields", 2);
                Verify(host.Events, expected[2], "actual floor parameter setters, PutBehind then SetStartPosition");
                int events = host.Events.Count;
                state.Advance(host);
                assert(host.Events.Count == events, "Missing camera leaves the startup latch uninitialized; a real initialized camera is never set up twice.");
            }
            void Verify(IEnumerable<long> actualValues, JsonElement expectedValues, string operation, int? length = null)
            {
                long[] actual = actualValues.ToArray();
                IEnumerable<JsonElement> values = expectedValues.EnumerateArray();
                if (length is { } count) values = values.Take(count);
                long[] wanted = values.Select(value => value.GetInt64()).ToArray();
                assert(actual.SequenceEqual(wanted), $"Original PC {kind} case{index}, {operation}: actual[{string.Join(',', actual)}] expected[{string.Join(',', wanted)}].");
            }
            index++;
        }
        var reset = new PlayerCutsceneSkipState(255) { FieldE00 = 91 };
        reset.BeginScript();
        assert(reset.Allowed == 0 && reset.FieldE00 == 91, "Replacing a synthesized-input program clears source1A4 without performing post-mission cleanup.");
    }
    private static PlayerButtonState[] Pad(int[] input, int offset) => Enumerable.Range(0, 20)
        .Select(index => new PlayerButtonState((byte)input[offset + index * 3], (byte)input[offset + index * 3 + 1], (byte)input[offset + index * 3 + 2])).ToArray();
    private static IEnumerable<long> PadValues(IEnumerable<PlayerButtonState> pad) => pad.SelectMany(button => new long[] { button.Pressed, button.Triggered, button.Auxiliary });
    private sealed class CleanupHost(int[] input) : IPlayerCutsceneCleanupHost
    {
        public List<long> Events { get; } = [];
        public int CameraMode { get; private set; } = input[0];
        public int LevelId => input[1];
        public int CheckpointNode => input[2];
        public FixedVector3 Position => new(input[5], input[6], input[7]);
        public FixedVector3 ForwardDirection => new(input[8], input[9], input[10]);
        public bool Abort { get; private set; } = true;
        public IReadOnlyList<int> CheckpointLinks(int node) { Events.AddRange([7, node]); return input[3] == 0 ? [] : [77]; }
        public FixedVector3 TriggerPosition(int node) { Events.AddRange([8, node]); return new(input[11], input[12], input[13]); }
        public void StopSpeech() => Events.Add(0);
        public void SetCameraMode(ushort mode) { Events.AddRange([1, mode]); CameraMode = mode; }
        public void SetFloorNormalAndOrient(FixedVector3 direction) => Events.AddRange([2, 1, direction.X, direction.Y, direction.Z]);
        public void PrepareDistanceAttack(FixedVector3 direction) => Events.AddRange([3, direction.X, direction.Y, direction.Z]);
        public void PlayIdleAnimation() => Events.AddRange([4, 0, 0, -1]);
        public void Stand() => Events.Add(5);
        public void SetCameraStartPosition() => Events.Add(6);
        public void ClearAbort() => Abort = false;
    }
    private sealed class StartupHost(int mode, bool hasCamera, int floorState) : IPlayerCameraStartupHost
    {
        public List<long> Events { get; } = [];
        public bool HasCamera => hasCamera;
        public int CameraMode => mode;
        public int FloorState { get; private set; } = floorState;
        public void SetFloorParameters() { Events.AddRange([11, -13, 0, 12, 29, 0, 13, 43, 0, 14, 65535, 0, 15, -128, 0]); FloorState = 0; }
        public void PutCameraBehind() => Events.AddRange([10, 0]);
        public void SetCameraStartPosition() => Events.Add(6);
    }
}
