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
        VerifyTypedContracts(assert);
        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, $"Source module boundary rejects {typeof(T).Name}."); return; }
            throw new InvalidDataException($"Expected {typeof(T).Name} at bounded source module input.");
        }
    }

    private static void VerifyTypedContracts(Action<bool, string> assert)
    {
        var modules = new SourceModuleRegistry<int, object>();
        object target = new();
        var actor = new QueryActor { Target = target, State = 5 };
        int factoryCalls = 0, queryCalls = 0, actionCalls = 0, parameterlessCalls = 0, clearCalls = 0;
        modules.Register("jonah", module =>
        {
            module.SetClear(() => clearCalls++);
            module.SetUserFunction(0, node => { factoryCalls++; return actor; });
            module.SetTypedUserFunction<QueryActor, uint>(1, value =>
            {
                queryCalls++;
                return value.State switch { 5 => 2u, 8 => 0u, _ => 1u };
            });
            module.SetUserAction<QueryActor>(2, value => { actionCalls++; value.Flags |= 0xC; });
        });
        modules.Register("scorpion", module =>
        {
            module.SetClear(() => clearCalls++);
            module.SetTypedUserFunction<QueryActor, object?>(1, value => value.Target);
        });
        modules.Load("jonah"); modules.Load("scorpion");
        assert(ReferenceEquals(modules.CallUserFunction("jonah", 0, 2), actor) && factoryCalls == 1 &&
            modules.CallTypedUserFunction<QueryActor, uint>("jonah", 1, actor) == 2 && queryCalls == 1 &&
            ReferenceEquals(modules.CallTypedUserFunction<QueryActor, object?>("scorpion", 1, actor), target),
            "Distinct source slots preserve factory references, unsigned behavior decisions and target identities in one loaded module table.");
        actor.Flags = 0x80;
        modules.CallUserAction("jonah", 2, actor);
        assert(actor.Flags == 0x8C && actionCalls == 1,
            "A typed void slot receives the actual actor identity and preserves unrelated plea flags.");
        Reject(() => modules.CallUserFunction("jonah", 1, 0));
        Reject(() => modules.CallTypedUserFunction<QueryActor, int>("jonah", 1, actor));
        Reject(() => modules.CallParameterlessUserFunction("jonah", 2));
        Reject(() => modules.CallUserAction("jonah", 1, actor));
        Reject(() => modules.CallTypedUserFunction<QueryActor, uint>("jonah", 7, actor));
        assert(factoryCalls == 1 && queryCalls == 1 && actionCalls == 1 && actor.Flags == 0x8C,
            "Missing slots and mismatched argument/result contracts fail before any source callback or actor mutation.");
        SourceModule<int, object> jonah = modules.SourceOrder.Single(module => module.Name == "jonah");
        jonah.SetParameterlessUserFunction(2, () => parameterlessCalls++);
        Reject(() => modules.CallUserAction("jonah", 2, actor));
        modules.CallParameterlessUserFunction("jonah", 2);
        assert(parameterlessCalls == 1 && actionCalls == 1,
            "Replacing a user slot with a parameterless callback removes its prior typed action contract.");
        jonah.SetUserAction<QueryActor>(2, value => { actionCalls++; value.Flags |= 0xC; });
        Reject(() => modules.CallParameterlessUserFunction("jonah", 2));
        modules.CallUserAction("jonah", 2, actor);
        assert(actionCalls == 2 && parameterlessCalls == 1,
            "Replacing a parameterless slot restores a single typed action without retaining a second callback.");
        modules.UnloadAll();
        Reject(() => modules.CallTypedUserFunction<QueryActor, uint>("jonah", 1, actor));
        modules.Load("jonah");
        assert(modules.CallTypedUserFunction<QueryActor, uint>("jonah", 1, actor) == 2 && clearCalls == 2,
            "Unload retires typed slot access and reload installs the genuine initializer's contracts again.");
        modules.UnloadAll();

        void Reject(Action call)
        {
            try { call(); }
            catch (InvalidOperationException)
            {
                assert(true, "A source module contract mismatch or unloaded slot is rejected before dispatch.");
                return;
            }
            throw new InvalidDataException("An invalid typed source module call was accepted.");
        }
    }

    private sealed class QueryActor
    {
        public object? Target { get; init; }
        public uint State { get; init; }
        public uint Flags { get; set; }
    }
    private sealed class Loader : IMissionModuleLoader
    {
        public List<string> Names { get; } = [];
        public void LoadCodeModule(string name) => Names.Add(name);
    }
}
