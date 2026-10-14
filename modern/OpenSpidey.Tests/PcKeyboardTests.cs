using System.Text.Json;
using OpenSpidey.Core.Input;

internal static class PcKeyboardTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PcKeyboardTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.pc-keyboard.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        var mappings = new PcKeyboardMappings();
        var keyboard = new PcKeyboardState();
        int operations = 0, polls = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement[] input = test.GetProperty("input").EnumerateArray().ToArray();
            JsonElement expected = test.GetProperty("expected");
            string kind = input[0].GetString()!;
            string name = $"PC keyboard[{operations}] {string.Join(' ', input.Select(value => value.ToString()))}";
            uint Word(int index) => (uint)input[index].GetInt64();
            switch (kind)
            {
                case "get":
                    assert(mappings.Get(Word(1)) == (uint)expected.GetInt64(), $"{name} PCINPUT_GetKeyboardMappingForAction.");
                    break;
                case "set": mappings.Set(Word(1), Word(2)); AssertTable(); break;
                case "restore": mappings.RestoreDefaults(); AssertTable(); break;
                case "table": AssertTable(); break;
                case "clear": keyboard.Clear(); break;
                case "poll":
                    int count = (int)Word(1);
                    PcKeyEvent[] records = Enumerable.Range(0, count)
                        .Select(index => new PcKeyEvent((byte)Word(2 + index * 2), (Word(3 + index * 2) & 0x80) != 0)).ToArray();
                    assert(keyboard.Poll(records) == expected[0].GetInt32(), $"{name} DXINPUT_PollKeyboard record count.");
                    byte[] state = expected[1].EnumerateArray().Select(value => (byte)value.GetInt32()).ToArray();
                    assert(keyboard.State.SequenceEqual(state), $"{name} gKeyState bytes.");
                    int[] pressed = expected[2].EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    for (int key = 0; key < 256; key++)
                        assert((keyboard.IsKeyPressed((byte)key, false) ? 1 : 0) + (keyboard.IsKeyPressed((byte)key, true) ? 2 : 0) == pressed[key],
                            $"{name} PCINPUT_IsKeyPressed key{key:X2}.");
                    polls++;
                    break;
                default: throw new InvalidDataException($"Unknown PC keyboard fixture operation {kind}.");
            }
            operations++;

            void AssertTable()
            {
                uint[] table = expected.EnumerateArray().Select(value => (uint)value.GetInt64()).ToArray();
                uint[] actual = mappings.Entries.SelectMany(entry => new[] { entry.Action, entry.Key }).ToArray();
                assert(actual.SequenceEqual(table), $"{name} gKeyboardMappings; actual[{string.Join(',', actual)}].");
            }
        }
        assert(operations == 1431 && polls == 800, $"PC keyboard fixture covers {operations} operations and {polls} polls.");
    }
}
