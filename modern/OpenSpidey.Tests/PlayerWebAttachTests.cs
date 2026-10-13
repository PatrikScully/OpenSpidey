using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWebAttachTests
{
    private sealed class Host(int[] input) : IPlayerWebAttachHost
    {
        public List<int> Events { get; } = [];
        public bool AnimationFinished => input[2] != 0;
        public bool HasExplicitAim => input[4] != 0;
        public FixedVector3 ExplicitAimPosition => new(11, -12, 13);
        public ActorAngles TrajectoryAngles => new(7, -8, 9);
        public bool CheckJump() { Events.Add(2); return input[5] != 0; }
        public void SwitchToStandMode() => Events.Add(1);
        public int FireWeb(PlayerFireWebRequest r)
        {
            Events.AddRange([3, r.UseHeldTarget ? 1 : 0, r.Cost, r.Target.X, r.Target.Y, r.Target.Z,
                r.HitSomething ? 1 : 0, r.Normal.X, r.Normal.Y, r.Normal.Z]);
            return input[6];
        }
        public void CreateWebDrips(bool firstHand, bool secondHand) => Events.AddRange([4, firstHand ? 1 : 0, secondHand ? 1 : 0]);
        public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([5, animation, firstFrame, lastFrame]);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerWebAttachTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-web-attach.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var state = new PlayerMeleeState(new(100, 100)
            { ActionState = 0x10000, AnimationId = (ushort)input[0], AnimationFrame = input[1], FieldEA6 = 77 })
            { WebAttackSubstate = (byte)input[3], WebAttackStarted = (byte)input[4] };
            var host = new Host(input);
            PlayerWebAttachBehavior.Step(state, host);
            assert(state.Combat.FieldEA6 == expected[0].GetInt32() && state.WebAttackSubstate == expected[1].GetInt32(),
                $"Web attach case {index}: source movement suppression and one-shot release latch.");
            assert(host.Events.SequenceEqual(expected[2].EnumerateArray().Select(v => v.GetInt32())),
                $"Web attach case {index}: exact release/aim/cost, drips, jump order and animation recovery callbacks.");
            assert(state.WebAttackStarted == input[4], "Web attach552 release latch never overwrites the independent8ED explicit-aim flag.");
            index++;
        }
    }
}
