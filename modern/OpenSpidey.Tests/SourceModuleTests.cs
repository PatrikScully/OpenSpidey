using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Modules;

internal static class SourceModuleTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SourceModuleTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.source-modules.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int sequence = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            SourceModuleRegistry<(uint First, uint Second), uint> modules = new();
            List<uint> events = [];
            Register("thug", 304); Register("blackcat", 319);
            int operation = 0;
            foreach (JsonElement step in test.GetProperty("input").EnumerateArray())
            {
                int[] input = step.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                string name = input[1] == 0 ? "thug" : "blackcat";
                uint result = uint.MaxValue; events.Clear();
                switch (input[0])
                {
                    case 0: modules.Load(name); break;
                    case 1: modules.Unload(name); break;
                    case 2:
                        try { result = modules.CallUserFunction(name, (uint)input[2], ((uint)input[3], (uint)input[4])); }
                        catch (InvalidOperationException) { events.Add(99); }
                        break;
                }
                JsonElement expected = test.GetProperty("expected")[operation];
                uint[][] list = expected[1].EnumerateArray().Select(row => row.EnumerateArray().Select(value => value.GetUInt32()).ToArray()).ToArray();
                assert(result == expected[0].GetUInt32() && modules.SourceOrder.Select(module => module.Checksum).SequenceEqual(list.Select(row => row[0])) &&
                    list.All(row => row[1] == 1) && events.SequenceEqual(expected[2].EnumerateArray().Select(value => value.GetUInt32())),
                    $"Original PC source module case {sequence}, operation {operation}: load idempotence, CRC/head order, function slots and clear-before-unlink owner output.");
                operation++;
            }
            sequence++;
            void Register(string name, uint family) => modules.Register(name, module =>
            {
                module.SetClear(() =>
                {
                    events.AddRange([1, family, (uint)modules.SourceOrder.Count]);
                    events.AddRange(modules.SourceOrder.Select(value => value.Checksum));
                });
                module.SetUserFunction(0, arguments =>
                {
                    events.AddRange([2, family, arguments.First, arguments.Second]);
                    return unchecked(family + arguments.First + arguments.Second);
                });
            });
        }
        SourceModuleRegistry<int, int> bounded = new();
        bounded.Register("blackcat", module => { module.SetClear(() => { }); module.SetUserFunction(0, value => value); });
        bounded.Load("blackcat\0ignored");
        assert(bounded.IsLoaded("blackcat") && bounded.CallUserFunction("blackcat", 0, 12) == 12,
            "Source module names retain original NUL termination at the managed string boundary.");
        Reject<NotSupportedException>(() => bounded.Load("BlackCat"));
        Reject<NotSupportedException>(() => bounded.Load("cop"));
        Reject<ArgumentOutOfRangeException>(() => bounded.CallUserFunction("blackcat", 8, 0));
        assert(bounded.SourceOrder.Count == 1,
            "Unported/case-mismatched modules and invalid slots produce bounded errors without installing fake production module functions.");
        var loader = new Loader();
        var handler = new ModuleLoadCommandHandler(loader);
        var context = new MissionCommandContext(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0);
        handler.Execute(context, new(189, 0, new byte[] { 116, 104, 117, 103, 0, 0 }));
        assert(loader.Names.SequenceEqual(new[] { "thug" }), "Source189 dispatch passes the exact aligned name to the real scene module loader.");
        Reject<InvalidDataException>(() => handler.Execute(context, new(189, 0, new byte[] { 116, 104, 117, 103, 0 })));
        foreach (bool reverseLoadOrder in new[] { false, true })
        {
            var cleanup = new SourceModuleRegistry<int, int>();
            var cleared = new List<string>();
            foreach (string name in new[] { "thug", "blackcat", "carnage" })
                cleanup.Register(name, module => module.SetClear(() =>
                {
                    assert(cleanup.IsLoaded(name), "Original module clear observes its own loaded record before unlink.");
                    cleared.Add(name);
                }));
            IEnumerable<string> loadOrder = new[] { "blackcat", "thug", "carnage" };
            if (reverseLoadOrder) loadOrder = loadOrder.Reverse();
            foreach (string name in loadOrder) cleanup.Load(name);
            cleanup.UnloadAll();
            assert(cleared.SequenceEqual(new[] { "thug", "blackcat", "carnage" }) && cleanup.SourceOrder.Count == 0,
                "Reloc_UnloadAll follows its fixed checksum/name sequence independently of module head order.");
            cleanup.UnloadAll();
            assert(cleared.Count == 3, "Repeated original module cleanup runs no second clear callback.");
        }
        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, $"Source module boundary rejects {typeof(T).Name}."); return; }
            throw new InvalidDataException($"Expected {typeof(T).Name} at bounded source module input.");
        }
    }
    private sealed class Loader : IMissionModuleLoader
    {
        public List<string> Names { get; } = [];
        public void LoadCodeModule(string name) => Names.Add(name);
    }
}
