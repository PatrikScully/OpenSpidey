using System.Text.Json;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSupportTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream fixture = typeof(PlayerSupportTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-support.json")!;
        using JsonDocument document = JsonDocument.Parse(fixture);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            FixedVector3 position = Vector(test.GetProperty("position"));
            FixedVector3 velocity = Vector(test.GetProperty("velocity"));
            bool supported = test.GetProperty("supported").GetBoolean();
            var world = new RideWorld(test.GetProperty("blocked").GetBoolean());
            FixedVector3 result = supported ? PlayerSupportRules.Ride(position, velocity,
                test.GetProperty("airborne").GetBoolean(), world) : position;
            int[] actual = [result.X, result.Y, result.Z, world.Queried ? 1 : 0,
                world.Start.X, world.Start.Y, world.Start.Z, world.End.X, world.End.Y, world.End.Z,
                PlayerSupportRules.JumpVelocity(supported ? velocity.Y : null)];
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())),
                "Source platform riding, one-velocity travel, airborne vertical exclusion, terrain rollback and support jump impulse match recovered routines.");
        }
        var floor = new SupportFloor { Velocity = new(4096, 0, 0) };
        var player = new PlayerTerrainController(new(0, -(96 << 12), 0), 0, floor, new Animation(), new(false, new(false, 0, false)));
        player.Step(default, 0);
        assert(player.SupportBody?.Id == 7 && floor.Notifications == 1 && player.Position.X == 4096,
            "Ground physics acquires the specific support instance, notifies it and rides it during the finalizer.");
        floor.Velocity = new(8192, -4096, 0);
        floor.Ceiling = true;
        player.Step(default, 0);
        assert(player.LastTerrainStep.CeilingHit is not null && player.SupportBody?.Velocity == floor.Velocity && floor.Notifications == 2,
            "The previous support is refreshed from its live velocity before ceiling clipping and current support reacquisition.");
        floor.Dead = true;
        player.Step(default, 0);
        assert(player.SupportBody is null && floor.Notifications == 2, "Dead supporting actors are cleared before physics and cannot be reacquired.");
        floor.Dead = false;
        player.Step(default, 0);
        player.TeleportFromScript(new(100 << 12, -(96 << 12), 0));
        assert(player.SupportBody is null, "Script teleport drops the old support identity before the next contact update.");
    }
    private static FixedVector3 Vector(JsonElement element)
    {
        int[] values = element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new(values[0], values[1], values[2]);
    }
    private sealed class RideWorld(bool blocked) : IPlayerWorldQuery
    {
        public bool Queried { get; private set; }
        public FixedVector3 Start { get; private set; }
        public FixedVector3 End { get; private set; }
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            Queried = true; Start = start; End = end;
            return blocked ? new(new(default, default, 0, 0, false), 0, 0, 0) : null;
        }
    }
    private sealed class SupportFloor : IPlayerWorldQuery, IPlayerSupportWorld
    {
        public FixedVector3 Velocity { get; set; }
        public bool Dead { get; set; }
        public bool Ceiling { get; set; }
        public int Notifications { get; private set; }
        public PlayerSupportBody? FindSupport(ulong id) => id == 7 ? new(7, Velocity, Dead) : null;
        public void NotifyTrodden(ulong id) { if (id != 7) throw new Exception("Wrong support identity."); Notifications++; }
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            if (Ceiling && start.Y > -(190 << 12) && end.Y <= -(190 << 12))
                return new(new(new(start.X, -(190 << 12), start.Z), new(0, 4096, 0), 100, 0, false), 0, 0, 0);
            if (start.Y >= 0 || end.Y < 0) return null;
            return new(new(new(start.X, 0, start.Z), new(0, -4096, 0), 136, 0, true), 0, 0, 0, SupportId: 7);
        }
    }
    private sealed class Animation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Run(int clip, int from = 0, int to = -1) => Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 22, from, to);
    }
}
