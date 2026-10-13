using System.Text.Json;
using OpenSpidey.Core.Audio;

internal static class ActorScriptSoundTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorScriptSoundTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.actor-script-sound.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ushort opcode = test.GetProperty("opcode").GetUInt16();
            ushort operand = test.GetProperty("operand").GetUInt16();
            JsonElement expected = test.GetProperty("expected");
            assert(expected[0].GetInt32() == 1 && expected[1].GetInt32() == 1 &&
                ActorScriptSoundCode.Global(operand, opcode == 0x428E) == expected[2].GetUInt32() &&
                expected[3].GetInt32() == 0x2000 && expected[4].GetInt32() == 0 && expected[5].GetInt32() == 2,
                "Inherited script sounds use a single raw word, signed promotion, level flag8000, range300..309 flag4000, volume2000 and pitch0 exactly as actual PC CPlatform dispatch.");
        }
    }
}
