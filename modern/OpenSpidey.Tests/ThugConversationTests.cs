using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugConversationTests
{
    private sealed class Host(ThugConversationActor[] actors, long[] input) : IThugConversationHost
    {
        public ThugPlayerState Player => new(new((int)input[10], (int)input[11], (int)input[12]), 100, 0, 0, false);
        public int AttackClock => (int)input[1];
        public IReadOnlyList<ThugConversationActor> SourceBaddyOrder => actors;
        public List<int> Events { get; } = [];
        public Dictionary<ThugState, ThugState> Partners { get; } = [];
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags)
        {
            int id = Array.FindIndex(actors, actor => actor.Actor.Body.Position == from);
            Events.AddRange([1, id, from.X, from.Y, from.Z, to.X, to.Y, to.Z, (int)flags]);
            return ((int)input[5] & (1 << id)) != 0;
        }
        public void StoreConversationPartner(ThugState owner, ThugState partner)
        {
            Events.AddRange([3, partner.SourceNode]);
            Partners[owner] = partner;
        }
        public int Random(int range) { Events.AddRange([2, range]); return (int)input[4] % range; }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugConversationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-conversation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var actors = new ThugConversationActor[4];
            for (int i = 0; i < actors.Length; i++)
            {
                int offset = 13 + i * 7;
                var state = new ThugState(i, 0x1101, initialType: (int)input[offset])
                {
                    State = (int)input[offset + 1], Substate = 7, AlertTicks = (int)input[offset + 2],
                    ConversationTicks = (int)input[offset + 3], DialogueClip = 123, DialogueBank = 456
                };
                var actor = new ActorProcedureState(new ActorBodyMotion { Position = new((int)input[offset + 4], (int)input[offset + 5], (int)input[offset + 6]) });
                actors[i] = new(state, actor, new());
            }
            actors[0].State.DistanceSampleClock = (int)input[2];
            actors[0].State.CachedPlayerDistance = (int)input[3];
            var host = new Host(actors, input);
            int result = 0;
            switch (input[0])
            {
                case 0: ThugConversationBehavior.StrikeUpConversation(actors[0], host, host.Random); break;
                case 1: result = ThugConversationBehavior.CanAcknowledge(actors[1], actors[0], (int)input[7], (int)input[8], (int)input[9], host) ? 1 : 0; break;
                case 2: result = ThugConversationBehavior.FindClosest(actors[0], host, (int)input[6])?.State.SourceNode ?? -1; break;
            }
            JsonElement expected = test.GetProperty("expected");
            assert(result == expected[0].GetInt32() && actors[0].State.DistanceSampleClock == expected[1].GetInt32() &&
                actors[0].State.CachedPlayerDistance == expected[2].GetInt32(),
                $"Conversation case {caseIndex} matches source player-distance clock caching and admission result.");
            for (int i = 0; i < actors.Length; i++)
            {
                ThugState state = actors[i].State;
                long[] actual = [state.State, state.Substate, state.AlertTicks, state.ConversationTicks, state.DialogueClip, state.DialogueBank,
                    host.Partners.TryGetValue(state, out ThugState? partner) ? partner.SourceNode : -1, actors[i].Flags.CachedIndex];
                assert(actual.SequenceEqual(expected[3][i].EnumerateArray().Select(value => value.GetInt64())),
                    $"Conversation case {caseIndex}, actor {i} preserves flags cache, reciprocal handles and dialogue/timer changes, including rejected partner requests.");
            }
            assert(host.Events.SequenceEqual(expected[4].EnumerateArray().Select(value => value.GetInt32())),
                $"Conversation case {caseIndex} preserves source list order, strict nearest/tie handling, candidate-to-self LOS and random/handle creation order.");
            caseIndex++;
        }
    }
}
