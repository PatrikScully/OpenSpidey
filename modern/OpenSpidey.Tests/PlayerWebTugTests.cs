using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWebTugTests
{
    public static void Run(Action<bool, string> assert)
    {
        using (Stream stream = typeof(PlayerWebTugTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-web-tug.json")!)
        using (JsonDocument document = JsonDocument.Parse(stream))
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                JsonElement expected = test.GetProperty("expected");
                var combat = new PlayerCombatState(100, 100) { ActionState = 0x20000, AnimationId = (ushort)input[0],
                    AnimationFrame = input[1], FieldEA6 = 77, ForwardDirection = new(234, -345, 456), UpDirection = new(-567, 678, -789) };
                var melee = new PlayerMeleeState(combat) { RightDirection = new(891, -912, 123), WebFiringSide = (byte)input[5] };
                var ownerHost = new OwnerHost();
                using var owner = new WebStrandOwner(ownerHost);
                var webs = new PlayerFireWebState(combat) { CurrentStrand = input[4] != 0 ? owner : null };
                var state = new PlayerWebTugState(combat) { SnapReleased = input[3] };
                var host = new Host(input);
                PlayerWebTugBehavior.Step(melee, webs, state, host);
                assert(combat.FieldEA6 == expected[0].GetInt32() && state.SnapReleased == expected[1].GetInt32() &&
                    (webs.CurrentStrand is null ? 0 : 1) == expected[2].GetInt32(),
                    "Original action20000 suppresses motion and clears only the shared current web when its exact release frame is reached.");
                assert(host.Events.SequenceEqual(expected[3].EnumerateArray().Select(value => value.GetInt32())),
                    "Original action20000 preserves jump-before-snap, directional impulse/path routing and field544 recovery animation order.");
                assert(combat.WebPresent == (webs.CurrentStrand is not null), "Action20000 keeps source fieldE6C presence synchronized with combat ownership.");
            }
        using Stream pathStream = typeof(PlayerWebTugTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-tug-path.json")!;
        using JsonDocument paths = JsonDocument.Parse(pathStream);
        foreach (JsonElement test in paths.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            IReadOnlyList<FixedVector3>? result = PlayerTugWebPath.Calculate(new(input[0], input[1], input[2]), new(input[3], input[4], input[5]));
            int[] actual = result?.SelectMany(point => new[] { point.X, point.Y, point.Z }).ToArray() ?? [];
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())),
                "Actual PC tug path matches full-distance radius, horizontal VectorNormal, shrinking first four radii and strict vertical524288 gate.");
        }
    }

    private sealed class Host(int[] input) : IPlayerWebTugHost
    {
        public List<int> Events { get; } = [];
        public bool AnimationFinished => input[2] != 0;
        public bool CheckJump() { Events.Add(1); return input[6] != 0; }
        public IReadOnlyList<FixedVector3>? CalculatePath(WebStrandOwner strand)
        { Events.Add(2); return input[7] != 0 ? new FixedVector3[] { new(1, 2, 3) } : null; }
        public void Snap(WebStrandOwner strand, FixedVector3 direction, IReadOnlyList<FixedVector3>? path) =>
            Events.AddRange([3, direction.X, direction.Y, direction.Z, path is null ? 0 : 1]);
        public void OrientToNormal(bool useTarget, FixedVector3 target) => Events.AddRange([4, useTarget ? 1 : 0, target.X, target.Y, target.Z]);
        public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([5, animation, firstFrame, lastFrame]);
        public void SwitchToStandMode() => Events.Add(6);
    }
    private sealed class OwnerHost : IWebStrandOwnerHost
    {
        public object PlayerIdentity => this;
        public int PlayerWebShot => 0;
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => default;
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => null;
        public void AttachOwner(WebStrandOwner owner) { }
        public void DetachOwner(WebStrandOwner owner) { }
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) => throw new InvalidOperationException();
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) => throw new InvalidOperationException();
    }
}
