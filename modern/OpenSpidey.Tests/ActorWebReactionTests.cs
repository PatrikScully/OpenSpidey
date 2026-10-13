using System.Text.Json;
using OpenSpidey.Core.Actors;

internal static class ActorWebReactionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorWebReactionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.actor-web-reactions.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(input[3] != 0, input[4] != 0);
            bool accepted = input[0] == 0
                ? ActorWebReactions.TrapWeb(unchecked((uint)input[1]), (short)input[2], host)
                : ActorWebReactions.TugWeb(unchecked((uint)input[1]), (short)input[2], host);
            JsonElement expected = test.GetProperty("expected");
            assert(accepted == (expected[0].GetInt32() != 0),
                "Actual PC baddy trap/yank acceptance preserves health and the distinct immunity flags, including failed message allocation.");
            assert(host.Events.SequenceEqual(expected[1].EnumerateArray().Select(value => value.GetInt32())),
                "Actual PC baddy web reactions preserve null-sender message5/6 construction and rejected-yank recovery/burst order.");
        }
    }

    private sealed class Host(bool allocated, bool yankContact) : IActorWebReactionHost
    {
        public List<int> Events { get; } = [];
        public bool TryCreateMessage(int code)
        {
            Events.AddRange([1, 0x44]);
            if (allocated) Events.AddRange([2, 0, 1, code, 0]);
            return allocated;
        }
        public void BurstYankContact()
        {
            Events.AddRange([3, yankContact ? 1 : 0]);
            if (yankContact) Events.Add(4);
        }
    }
}
