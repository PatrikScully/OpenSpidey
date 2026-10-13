using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.UI;

/// <summary>Original SMessage ownership. PC update/display never traverse this list, so only explicit clearing retires it.</summary>
public sealed record MissionMessage(ReadOnlyMemory<byte> Text, object? Program = null)
{
    public ushort Time => 0;
    public ushort TotalTime => 0;
}

/// <summary>One original SSimpleMessage, including unsigned age/lifetime and source scale200.</summary>
public sealed class SimpleMissionMessage
{
    internal SimpleMissionMessage(ReadOnlyMemory<byte> text, uint x, uint y, uint duration)
    { Text = text; X = x; Y = y; Duration = duration; }
    public ReadOnlyMemory<byte> Text { get; }
    public uint X { get; }
    public uint Y { get; }
    public uint Duration { get; }
    public uint Scale { get; set; } = 200;
    public uint Age { get; internal set; }
    public uint FadeFactor { get; internal set; }
}

/// <summary>Mess_Display's actual font state; PC Mess_DrawText ignores the supplied fade factor.</summary>
public static class MissionMessagePresentation
{
    public static BitmapTextColour Colour => new(0x60, 0x60, 0x80);
    public static int FontScale(SimpleMissionMessage message) => checked((int)(message.Scale & ushort.MaxValue) * 8);
    public static float Depth(ushort sort) => sort switch { 4093 => 6.0999999f, 4094 => -4, 4095 => -3, _ => 4 };
}

/// <summary>Separate source SMessage/SimpleMessage lists and actual PC timer policy; the render phase does not age messages.</summary>
public sealed class MissionMessages : IMissionCheckpointMessages
{
    private readonly ReadOnlyMemory<byte> checkpointText;
    private readonly List<MissionMessage> messages = [];
    private readonly List<SimpleMissionMessage> simple = [];
    private int lastTimer;
    public IReadOnlyList<MissionMessage> SourceMessages => messages.AsReadOnly();
    public IReadOnlyList<SimpleMissionMessage> SourceSimpleMessages => simple.AsReadOnly();
    public uint LastSimpleMessageX { get; private set; }
    public int LastSimpleMessageWidth { get; private set; }
    public int LastTimer => lastTimer;
    public event Action? Changed;

    public MissionMessages(OriginalMissionMessageData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        checkpointText = data.CheckpointText.ToArray();
    }

    public MissionMessage AddMessage(ReadOnlyMemory<byte> text, object? program = null)
    {
        var message = new MissionMessage(text, program);
        messages.Insert(0, message); Changed?.Invoke();
        return message;
    }

    public void ShowCheckpointMessage() => AddMessage(checkpointText);

    public SimpleMissionMessage AddSimpleMessage(ReadOnlyMemory<byte> text, uint x, uint y, uint duration, int measuredWidth)
    {
        var message = new SimpleMissionMessage(text, x, y, duration);
        simple.Insert(0, message);
        LastSimpleMessageX = x; LastSimpleMessageWidth = measuredWidth;
        Changed?.Invoke();
        return message;
    }

    public void DeleteAllMessages()
    {
        messages.Clear(); simple.Clear(); Changed?.Invoke();
    }

    public void ClearSimpleMessages() { simple.Clear(); Changed?.Invoke(); }

    public void Update(int timer)
    {
        if (lastTimer == 0) lastTimer = unchecked(timer - 2);
        int elapsed = unchecked(timer - lastTimer);
        lastTimer = timer;
        foreach (SimpleMissionMessage message in simple.ToArray())
        {
            message.Age = unchecked(message.Age + (uint)elapsed);
            if (message.Age > unchecked(message.Duration + 90)) { simple.Remove(message); continue; }
            if (message.Age > unchecked(message.Duration + 30))
                message.FadeFactor = unchecked(4096u - (unchecked(message.Age - message.Duration - 30) << 12) / 60);
            else if (message.Age > 30) message.FadeFactor = 4096;
            else message.FadeFactor = unchecked(message.Age << 12) / 30;
        }
        Changed?.Invoke();
    }
}
