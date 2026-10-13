using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

internal static class MissionStartupTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionStartupTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.mission-startup.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ushort opcode = test.GetProperty("opcode").GetUInt16();
            int sourceOffset = test.GetProperty("sourceOffset").GetInt32();
            var host = new StartupHost(test.GetProperty("items"));
            byte[] arguments = BuildArguments(test, sourceOffset);
            TriggerInstruction instruction = new(opcode, sourceOffset, arguments);
            TriggerNode node = new(0, (ushort)TriggerNodeKind.Autoexec, 0, ReadOnlyMemory<byte>.Empty);
            IMissionCommandHandler handler = opcode switch
            {
                147 => new GameLevelCommandHandler(host),
                213 => new FightMusicTimeCommandHandler(host),
                _ => new VisibilityBoxCommandHandler(opcode, host)
            };
            handler.Execute(new(node, 0), instruction);
            JsonElement expected = test.GetProperty("expected");
            if (opcode is 141 or 192 or 209)
            {
                VerifyDecodedBoundary(opcode, sourceOffset, arguments, assert);
                assert(expected.GetProperty("gameLevel").GetInt32() == 0x5678 && host.GameLevel == 0x13579BDF,
                    $"Case{caseIndex}: actual PC box traversal reaches the immediately following SetGameLevel; visibility itself leaves the scalar untouched.");
                SourceVisibilityList[] dispatch = opcode switch
                {
                    141 => [SourceVisibilityList.StaticEnvironment, SourceVisibilityList.Baddy],
                    192 => [SourceVisibilityList.Baddy],
                    _ => [SourceVisibilityList.EnvironmentBody, SourceVisibilityList.PowerUp]
                };
                assert(host.Dispatch.SequenceEqual(Enumerable.Range(0, test.GetProperty("boxes").GetArrayLength()).SelectMany(_ => dispatch)),
                    $"Case{caseIndex}: boxes dispatch each original active source list in order; empty authored lists are a true no-op.");
            }
            else
                assert(host.GameLevel == expected.GetProperty("gameLevel").GetInt32(),
                    $"Case{caseIndex}: command147 zero extends its source word into the trigger global without changing another startup field.");
            assert(host.FightMusicTime == expected.GetProperty("fightMusicTime").GetInt32(),
                $"Case{caseIndex}: command213 writes only the player's C60 fight-music time and zero extends source values through65535.");
            JsonElement[] groups = expected.GetProperty("flags").EnumerateArray().ToArray();
            for (int list = 0; list < groups.Length; list++)
            {
                ushort[] flags = groups[list].EnumerateArray().Select(value => value.GetUInt16()).ToArray();
                assert(host.Items[list].Select(item => item.ItemFlags).SequenceEqual(flags),
                    $"Case{caseIndex}, list{list}: signed wrapped boxes, inclusive bounds, all-axis outside selection, usable-region gates and flag0-only writes match untouched PC items.");
            }
            caseIndex++;
        }
        var box = new SourceVisibilityBox(new(-1, -1, -1), new(1, 1, 1));
        assert(!box.Selects(new(2, 0, 0), false) && box.Selects(new(2, 2, 2), false) && box.Selects(new(1, 1, 1), true),
            "Original outside selection requires every axis outside; inclusive boundary points remain inside.");
    }

    private static byte[] BuildArguments(JsonElement test, int sourceOffset)
    {
        var bytes = new List<byte>();
        if (test.GetProperty("opcode").GetInt32() is 147 or 213)
            Word(test.GetProperty("operand").GetUInt16());
        else
        {
            Word(test.GetProperty("visible").GetUInt16()); Word(test.GetProperty("inside").GetUInt16());
            foreach (JsonElement box in test.GetProperty("boxes").EnumerateArray())
            {
                while (((sourceOffset + 2 + bytes.Count) & 3) != 0) Word(0xA55A);
                foreach (JsonElement component in box.EnumerateArray())
                {
                    uint value = unchecked((uint)component.GetInt32());
                    Word(unchecked((ushort)value)); Word((ushort)(value >> 16));
                }
            }
            Word(255);
        }
        return bytes.ToArray();
        void Word(ushort value) { bytes.Add((byte)value); bytes.Add((byte)(value >> 8)); }
    }

    private static void VerifyDecodedBoundary(ushort opcode, int sourceOffset, byte[] arguments, Action<bool, string> assert)
    {
        var data = new byte[2 + 2 + arguments.Length + 6];
        BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)TriggerNodeKind.Autoexec);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), opcode);
        arguments.CopyTo(data, 4);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4 + arguments.Length), 147);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6 + arguments.Length), 0x5678);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8 + arguments.Length), ushort.MaxValue);
        var node = new TriggerNode(0, (ushort)TriggerNodeKind.Autoexec, (sourceOffset + 2) & 3, data);
        IReadOnlyList<TriggerInstruction> decoded = TriggerCommandReader.Read(node);
        assert(decoded.Count == 2 && decoded[0].Arguments.Span.SequenceEqual(arguments) && decoded[1].Opcode == 147 &&
            BinaryPrimitives.ReadUInt16LittleEndian(decoded[1].Arguments.Span) == 0x5678,
            "The real mission decoder retains absolute dword padding and stops at the box sentinel before the following startup command.");
    }

    private sealed class StartupHost : IMissionGameLevelHost, IMissionFightMusicHost, IVisibilityBoxHost
    {
        public int GameLevel { get; set; } = 0x13579BDF;
        public int FightMusicTime { get; set; } = 0x2468ACE0;
        public VisibilityItem[][] Items { get; }
        public List<SourceVisibilityList> Dispatch { get; } = [];
        public StartupHost(JsonElement items) => Items = items.EnumerateArray().Select(group => group.EnumerateArray().Select(item =>
        {
            int[] position = item.GetProperty("position").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            return new VisibilityItem(new(position[0], position[1], position[2]), item.GetProperty("flags").GetUInt16(),
                item.GetProperty("usable").GetBoolean());
        }).ToArray()).ToArray();
        public IEnumerable<IVisibilityBoxItem> SourceItems(SourceVisibilityList list)
        { Dispatch.Add(list); return Items[(int)list]; }
    }

    private sealed class VisibilityItem(FixedVector3 position, ushort flags, bool usable) : IVisibilityBoxItem
    {
        public FixedVector3 SourcePosition => position;
        public bool RegionUsable => usable;
        public ushort ItemFlags { get; private set; } = flags;
        public void SetVisible(bool visible) => ItemFlags = EnvironmentVisibilityPolicy.SetVisible(ItemFlags, visible);
    }
}
