using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerTargetTests
{
    private sealed record Identity(int Id);
    private sealed class Target(JsonElement source) : IPlayerMeleeTarget
    {
        public object Identity { get; } = new Identity(source[0].GetInt32());
        public FixedVector3 Position => Vector(source, 9);
        public ushort Type => source[1].GetUInt16();
        public ushort ItemFlags => 0;
        public IPlayerMeleeTarget? Next => null;
    }
    private sealed class Host(JsonElement input) : IPlayerTargetSelectionHost
    {
        private readonly JsonElement[] items = input.GetProperty("items").EnumerateArray().ToArray();
        private readonly Target[] targets = input.GetProperty("items").EnumerateArray().Select(source => new Target(source)).ToArray();
        public List<int> Events { get; } = [];
        public IEnumerable<PlayerTargetCandidate> SourceBaddyOrder => targets.Select((target, index) => new PlayerTargetCandidate(target,
            items[index][2].GetUInt16(), items[index][3].GetUInt16(), unchecked((uint)items[index][4].GetInt32()), 0, 0));
        public IEnumerable<PlayerSwitchCandidate> SourceControlOrder => targets.Select(target => new PlayerSwitchCandidate(target.Identity, target.Type));
        public bool LineOfSight(FixedVector3 start, FixedVector3 end, int firstSourceArgument, int secondSourceArgument)
        {
            if (start != Vector(input.GetProperty("pos"), 0)) throw new InvalidOperationException("Target LOS changed source start.");
            JsonElement item = items.First(source => Vector(source, 9) == end);
            Events.AddRange([1, item[0].GetInt32(), firstSourceArgument, secondSourceArgument, item[5].GetInt32()]);
            return item[5].GetInt32() != 0;
        }
        public PlayerSwitchAimPoint? GetSwitchAutoAimPoint(PlayerSwitchCandidate item)
        {
            JsonElement source = items.First(source => source[0].GetInt32() == ((Identity)item.Identity).Id);
            int id = source[0].GetInt32();
            return PlayerTargetSelection.GetSwitchAutoAimPoint(source[6].GetInt32(),
                new(new Identity(id + 10000), Vector(source, 12)), new(new Identity(id + 20000), Vector(source, 15)));
        }
        public PlayerSwitchTrace LineToItem(FixedVector3 start, FixedVector3 end, int mode)
        {
            if (start != Vector(input.GetProperty("pos"), 0)) throw new InvalidOperationException("Switch trace changed source start.");
            JsonElement source = items.First(source => Vector(source, 12) == end || Vector(source, 15) == end);
            int id = source[0].GetInt32(), hit = source[7].GetInt32(); uint face = source[8].GetUInt32();
            Events.AddRange([2, id, mode, hit, unchecked((int)face)]);
            return new(hit == 0 ? null : new Identity(id), face);
        }
        public PlayerKickHandle MakeSwitchHandle(PlayerSwitchCandidate? item)
        {
            int id = item?.Identity is Identity identity ? identity.Id : 0;
            Events.AddRange([3, id]);
            return new(item?.Identity, (uint)(id == 0 ? 0 : id + 1000));
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerTargetTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-targets.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            int Int(string key) => input.GetProperty(key).GetInt32();
            short[] matrix = input.GetProperty("matrix").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            var rotation = new FixedJointTransform(matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5], matrix[6], matrix[7], matrix[8], 17, 19, 23);
            var host = new Host(input);
            int selected = 0, handleId = 0; uint validation = 99;
            if (Int("kind") == 0)
            {
                IPlayerMeleeTarget? target = PlayerTargetSelection.SelectBaddy(Vector(input.GetProperty("pos"), 0), rotation, host,
                    Int("range"), Int("cone"), Int("weight"), Int("facing"));
                selected = target?.Identity is Identity identity ? identity.Id : 0;
            }
            else
            {
                PlayerKickSwitchSelection selection = PlayerTargetSelection.SelectSwitch(Vector(input.GetProperty("pos"), 0), rotation, host,
                    Int("range"), Int("cone"), Int("weight"), Int("facing"));
                handleId = selection.Handle.Target is Identity identity ? identity.Id : 0;
                validation = selection.Handle.Validation;
                if (selection.AutoAimPosition is { } point)
                {
                    JsonElement source = input.GetProperty("items").EnumerateArray().First(source => Vector(source, 12) == point || Vector(source, 15) == point);
                    selected = source[0].GetInt32() + (source[6].GetInt32() == 1 ? 10000 : 20000);
                }
            }
            assert(selected == expected[0].GetInt32() && handleId == expected[1].GetInt32() && validation == expected[2].GetUInt32() &&
                host.Events.SequenceEqual(expected[3].EnumerateArray().Select(value => value.GetInt32())),
                $"PlayerTarget{index} matches original PC selector scoring, signed cached distance, source list/tie order, hard cone gate, actual Q12 camera rotation, LOS arguments, switch407-only/mode-specific point, face mask and owner handle.");
            index++;
        }
    }
    private static FixedVector3 Vector(JsonElement source, int offset) => new(source[offset].GetInt32(), source[offset + 1].GetInt32(), source[offset + 2].GetInt32());
}
