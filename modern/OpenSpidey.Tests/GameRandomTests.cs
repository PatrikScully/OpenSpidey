using System.Text.Json;
using OpenSpidey.Core.Mathematics;

internal static class GameRandomTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(GameRandomTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.game-random.json")
            ?? throw new InvalidDataException("Original random-stream fixtures are missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var random = new GameRandom(test.GetProperty("seed").GetInt32());
            int index = 0;
            JsonElement expected = test.GetProperty("expected");
            foreach (JsonElement bound in test.GetProperty("bounds").EnumerateArray())
            {
                JsonElement step = expected[index++];
                assert(random.Next(bound.GetInt32()) == step[0].GetInt32() &&
                    random.State == new GameRandomState(step[1].GetInt32(), step[2].GetInt32(), step[3].GetInt32()),
                    "Original random output and all three state words retain signed wrap, update order and zero-bound consumption.");
            }
            GameRandomState saved = random.State;
            int next = random.Next(480);
            random.Restore(saved);
            assert(random.Next(480) == next, "Restoring the source stream resumes the same gameplay random sequence.");
        }
    }
}
