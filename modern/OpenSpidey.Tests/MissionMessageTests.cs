using System.Text.Json;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.UI;

internal static class MissionMessageTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionMessageTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.mission-messages.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var messages = new MissionMessages(new OriginalMissionMessageData("Checkpoint"u8.ToArray()));
            messages.ShowCheckpointMessage();
            messages.AddSimpleMessage("A"u8.ToArray(), 64, 100, test.GetProperty("first").GetUInt32(), 51);
            messages.AddSimpleMessage("B"u8.ToArray(), 128, 120, test.GetProperty("second").GetUInt32(), 51);
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            int index = 0;
            foreach (JsonElement timer in test.GetProperty("timers").EnumerateArray())
            {
                messages.Update(timer.GetInt32());
                Verify(expected[index++]);
            }
            messages.ClearSimpleMessages(); Verify(expected[index++]);
            messages.DeleteAllMessages(); Verify(expected[index]);
            assert(messages.LastSimpleMessageX == 128 && messages.LastSimpleMessageWidth == 51,
                "Source message clearing preserves the latest simple-message placement/width fields used by authored TextBox208.");

            void Verify(JsonElement source)
            {
                JsonElement[] actualMessages = source.GetProperty("messages").EnumerateArray().ToArray();
                JsonElement[] actualSimple = source.GetProperty("simple").EnumerateArray().ToArray();
                JsonElement[] draws = source.GetProperty("draws").EnumerateArray().ToArray();
                assert(messages.LastTimer == source.GetProperty("timer").GetInt32() &&
                    messages.SourceMessages.Count == actualMessages.Length && messages.SourceSimpleMessages.Count == actualSimple.Length &&
                    messages.SourceMessages.Count + messages.SourceSimpleMessages.Count == source.GetProperty("live").GetInt32(),
                    "Actual PC source message heads, unsigned lifetime deletion, explicit clear ownership and zero-sentinel clock initialization match.");
                for (int entry = 0; entry < actualMessages.Length; entry++)
                {
                    MissionMessage message = messages.SourceMessages[entry]; JsonElement original = actualMessages[entry];
                    assert(message.Text.Span[0] == original[0].GetByte() && message.Time == original[1].GetUInt16() &&
                        message.TotalTime == original[2].GetUInt16() && message.Program is null && original[3].GetInt32() == 0,
                        "Original checkpoint SMessages retain head order, null program and zero timers because PC update/display never traverse that list.");
                }
                assert(draws.Length == actualSimple.Length, "Only the genuine SimpleMessage list submits font draws on PC.");
                for (int entry = 0; entry < actualSimple.Length; entry++)
                {
                    SimpleMissionMessage message = messages.SourceSimpleMessages[entry]; JsonElement original = actualSimple[entry];
                    assert(message.Text.Span[0] == original[0].GetByte() && message.Age == original[1].GetUInt32() &&
                        message.Duration == original[2].GetUInt32() && message.X == original[3].GetUInt32() &&
                        message.Y == original[4].GetUInt32() && message.Scale == original[5].GetUInt32() && message.FadeFactor == original[6].GetUInt32(),
                        "Actual PC simple-message head order, timer wrap, thirty-tick fade-in, unsigned fade calculation and duration+90 removal are preserved.");
                    JsonElement draw = draws[entry]; BitmapTextColour colour = MissionMessagePresentation.Colour;
                    assert(draw[0].GetByte() == message.Text.Span[0] && draw[1].GetInt32() == unchecked((int)message.X) &&
                        draw[2].GetInt32() == unchecked((int)message.Y) && draw[3].GetInt32() == MissionMessagePresentation.FontScale(message) &&
                        draw[4].GetInt32() == 0 && draw[5].GetByte() == colour.Red && draw[6].GetByte() == colour.Green &&
                        draw[7].GetByte() == colour.Blue && draw[8].GetInt32() == 8 &&
                        draw[9].GetUInt32() == BitConverter.SingleToUInt32Bits(MissionMessagePresentation.Depth(test.GetProperty("sort").GetUInt16())),
                        "Source display uses left justification, scale8*word, RGB96/96/128, flags8 and recovered sort depth; fade is absent from actual font arguments.");
                }
            }
        }
    }
}
