using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerAirborneWallTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerAirborneWallTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-airborne-wall.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            {
                ActionState = (uint)input[0], CollisionFlags = (ushort)input[1], AnimationId = (ushort)input[12],
                Position = new((int)input[13], (int)input[14], (int)input[15]), Velocity = new(123, -456, 789),
                CrawlSurfaceNormal = new(111, -222, 333), IsCrawling = input[16] != 0, HeadingTurnTicksRemaining = (int)input[17]
            };
            PlayerWorldHit? contact = input[2] == 0 ? null : new(new(new((int)input[3], (int)input[4], (int)input[5]),
                new((short)input[6], (short)input[7], (short)input[8]), 0, (uint)input[9], false), 0, 0, 0);
            var host = new Host(combat, input);
            bool result = PlayerAirborneWallAttachment.Check(new(combat), contact, (ushort)input[11], host);
            long[] actual = [result ? 1 : 0, combat.ActionState, combat.AnimationId, combat.IsCrawling ? 1 : 0,
                combat.HeadingTurnTicksRemaining, combat.Position.X, combat.Position.Y, combat.Position.Z,
                combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z, combat.CrawlSurfaceNormal.X,
                combat.CrawlSurfaceNormal.Y, combat.CrawlSurfaceNormal.Z];
            JsonElement expected = row.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Original whole airborne wall{index}: source flags/item/shortnormal/ground sentinel, unsigned standoff and exact position/crawl/velocity/turn/clip writes.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Original whole airborne wall{index++}: ordered original ground query, source up-target orientation, animation and sound.");
        }
    }

    private sealed class Host(PlayerCombatState combat, long[] input) : IPlayerAirborneWallAttachmentHost
    {
        public List<long> Events { get; } = [];
        public int GroundHeight(FixedVector3 position, int above, int below)
        { Events.AddRange([1,position.X,position.Y,position.Z,above,below]); return (int)input[10]; }
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Events.AddRange([2,useTarget ? 1 : 0,target.X,target.Y,target.Z]);
        public void PlayAnimation(ushort animation) { Events.AddRange([3,animation,0,-1]); combat.AnimationId = animation; }
        public void PlaySound(ushort sound, int volume, int pitch) => Events.AddRange([4,sound,volume,pitch]);
    }
}
