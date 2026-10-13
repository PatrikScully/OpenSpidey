using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerGrabTests
{
    private sealed record Handle(uint First, uint Second);
    private sealed class Host(Handle handle, ushort type, bool present) : IPlayerGrabHost
    {
        public FixedVector3 HookPosition;
        public List<int> Events { get; } = [];
        private readonly PlayerGrabTarget target = new(new object(), type);
        public PlayerGrabTarget? RecoverTarget(object? value)
        {
            if (!ReferenceEquals(value, handle)) throw new InvalidOperationException("GrabUpdate recovered a different stored handle.");
            Events.AddRange([1, unchecked((int)handle.First), unchecked((int)handle.Second)]);
            return present ? target : null;
        }
        public FixedVector3 GetDynamicHookPosition(PlayerGrabTarget actor, ActorHook hook)
        {
            if (!ReferenceEquals(actor, target)) throw new InvalidOperationException("GrabUpdate queried another recovered target.");
            Events.AddRange([2, hook.X, hook.Y, hook.Z, hook.PartIndex]); return HookPosition;
        }
        public void ReportUnknownTarget(ushort unknown) => Events.Add(3);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerGrabTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-grab.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var handle = new Handle((uint)input[3], (uint)input[4]);
            var state = new PlayerGrabState { ActionState = (uint)input[0], TargetHandle = handle, Position = Vector(5),
                UpDirection = Vector(8), ForwardDirection = Vector(11), Yaw = (short)input[14] };
            var host = new Host(handle, (ushort)input[2], input[1] != 0) { HookPosition = Vector(15) };
            FixedVector3 output = Vector(18); short yaw = (short)input[21];
            int update = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                host.Events.Clear();
                bool result = PlayerGrabUpdate.Update(state, host, ref output, ref yaw);
                long[] actual = [result ? 1 : 0, state.Position.X, state.Position.Y, state.Position.Z, output.X, output.Y, output.Z, yaw];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(8).Select(value => value.GetInt64())) &&
                    host.Events.SequenceEqual(expected[8].EnumerateArray().Select(value => value.GetInt32())),
                    $"GrabUpdate{index}:{update} matches unmodified PC action gates, stored-handle recovery, type-specific source hooks, player-position/output asymmetry, own yaw and wrapping direction offsets.");
                assert(state.ActionState == (uint)input[0] && ReferenceEquals(state.TargetHandle, handle) && state.UpDirection == Vector(8) && state.ForwardDirection == Vector(11) && state.Yaw == (short)input[14],
                    "GrabUpdate preserves caller-owned action, surface direction frame, original actor handle and orientation.");
                host.HookPosition += new FixedVector3(11, -13, 17); update++;
            }
            index++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
        var targetHandle = new Handle(17, 19);
        var holding = new PlayerGrabState { ActionState = 0x08000000, TargetHandle = targetHandle, Position = new(1, 2, 3),
            UpDirection = new(0, 4096, 0), ForwardDirection = new(0, 0, 4096), Yaw = 1234 };
        var holdingHost = new Host(targetHandle, 312, true) { HookPosition = new(409600, 819200, 1228800) };
        FixedVector3 outputPosition = new(7, 11, 13); short outputYaw = -99;
        PlayerGrabUpdate.Update(holding, holdingHost, ref outputPosition, ref outputYaw);
        assert(outputPosition == new FixedVector3(7, 11, 13) && holding.Position == new FixedVector3(409600, 544768, 1318912) && outputYaw == 1234,
            "Holding a henchman uses actual part13 and moves the player while leaving the caller's output-position sentinel untouched.");
        holding.ActionState = 0;
        holdingHost.Events.Clear(); outputYaw = -99;
        bool inactive = PlayerGrabUpdate.Update(holding, holdingHost, ref outputPosition, ref outputYaw);
        assert(!inactive && holdingHost.Events.Count == 0 && outputYaw == -99 && outputPosition == new FixedVector3(7, 11, 13),
            "Inactive player grab performs no actor-handle/pose queries and writes neither output reference.");
    }
}
