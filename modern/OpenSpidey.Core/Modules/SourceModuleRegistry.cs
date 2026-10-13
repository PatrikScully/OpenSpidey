using System.Text;
using OpenSpidey.Assets.Models;

namespace OpenSpidey.Core.Modules;

/// <summary>Eight source user-function slots and a genuine module-clear owner, with native pointers replaced by typed arguments/results.</summary>
public sealed class SourceModule<TArguments, TResult>
{
    private readonly Func<TArguments, TResult>?[] functions = new Func<TArguments, TResult>?[8];
    private readonly Action?[] parameterlessFunctions = new Action?[8];
    private Action? clear;
    public string Name { get; }
    public uint Checksum { get; }
    internal SourceModule(string name, uint checksum) { Name = name; Checksum = checksum; }
    public void SetClear(Action action) => clear = action ?? throw new ArgumentNullException(nameof(action));
    public void SetUserFunction(uint slot, Func<TArguments, TResult> function)
    {
        if (slot >= functions.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        functions[slot] = function ?? throw new ArgumentNullException(nameof(function));
        parameterlessFunctions[slot] = null;
    }
    /// <summary>Actual Reloc_CallUserFunction slots invoked with null argument/result pointers, such as l2a1lsc0/1.</summary>
    public void SetParameterlessUserFunction(uint slot, Action function)
    {
        if (slot >= parameterlessFunctions.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        parameterlessFunctions[slot] = function ?? throw new ArgumentNullException(nameof(function));
        functions[slot] = null;
    }
    internal Func<TArguments, TResult>? Function(uint slot) => slot < functions.Length ? functions[slot] :
        throw new ArgumentOutOfRangeException(nameof(slot));
    internal Action? ParameterlessFunction(uint slot) => slot < parameterlessFunctions.Length ? parameterlessFunctions[slot] :
        throw new ArgumentOutOfRangeException(nameof(slot));
    internal void Clear() => (clear ?? throw new InvalidOperationException($"Source module {Name} has no genuine clear owner."))();
}

/// <summary>Reloc_Load/Unload/CallUserFunction identity and list order, independent of obsolete executable module loading.</summary>
public sealed class SourceModuleRegistry<TArguments, TResult>
{
    private static readonly HashSet<string> sourceNames = new(StringComparer.Ordinal)
    {
        "thug", "l2a1lsc", "scorpion", "jonah", "blackcat", "hostage", "cop", "chopper", "rhino", "venom",
        "lizman", "l5a4lsc", "l5a5lsc", "l5a6lsc", "l5a7lsc", "l6a1lsc", "l6a2lsc", "l6a3lsc", "mj", "simby",
        "spclone", "mysterio", "turret", "docock", "carnage", "submarin", "superock", "training", "torch", "shell", "lizard"
    };
    private readonly Dictionary<string, Action<SourceModule<TArguments, TResult>>> initializers = new(StringComparer.Ordinal);
    private readonly LinkedList<SourceModule<TArguments, TResult>> loaded = new();
    public IReadOnlyList<SourceModule<TArguments, TResult>> SourceOrder => loaded.ToArray();
    public void Register(string name, Action<SourceModule<TArguments, TResult>> initialize)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        if (!sourceNames.Contains(name)) throw new NotSupportedException($"{name} is absent from the original module table.");
        if (!initializers.TryAdd(name, initialize)) throw new InvalidOperationException($"Source module {name} was already registered.");
    }
    public void Load(string name)
    {
        name = SourceName(name);
        uint checksum = Checksum(name);
        if (loaded.Any(module => module.Checksum == checksum)) return;
        if (!initializers.TryGetValue(name, out var initialize))
            throw new NotSupportedException($"Source module {name} has no implemented module initialization.");
        // Initializers and nested loads see this record at the head, exactly as Reloc_Load.
        var node = loaded.AddFirst(new SourceModule<TArguments, TResult>(name, checksum));
        try { initialize(node.Value); }
        catch { loaded.Remove(node); throw; }
    }
    public bool IsLoaded(string name) => loaded.Any(module => module.Checksum == Checksum(name));
    public bool Unload(string name) => Unload(Checksum(name));
    public bool Unload(uint checksum)
    {
        for (var node = loaded.First; node is not null; node = node.Next)
        {
            if (node.Value.Checksum != checksum) continue;
            node.Value.Clear(); // Clear observes its record as loaded; unlink follows actual destruction.
            loaded.Remove(node); return true;
        }
        return false;
    }

    /// <summary>Reloc_UnloadAll's fixed checksum/name order; each genuine clear runs before its module is unlinked.</summary>
    public void UnloadAll()
    {
        uint[] checksums = [0xFDBCF4F8, 0x2736FBF8, 0xF84F55C0, 0x1CBA85B9, 0x2528AEEE,
            0xF4DCD5FD, 0x1B982E4C, 0xFF9CB2BA, 0x1A96491C, 0x88F1EEF3,
            0x9A44411D, 0x22F82678, 0x81070B0A, 0x93B2A4E4, 0x2B0EC381];
        foreach (uint checksum in checksums) Unload(checksum);
        string[] names = ["blackcat", "carnage", "chopper", "docock", "l2a1lsc", "lizard", "lizman",
            "mj", "spclone", "submarin", "superock", "torch", "training", "turret", "venom"];
        foreach (string name in names) Unload(name);
        if (loaded.Count != 0) throw new InvalidOperationException("Reloc_UnloadAll retained a source code module.");
    }
    public TResult CallUserFunction(string name, uint slot, TArguments arguments)
    {
        if (slot >= 8) throw new ArgumentOutOfRangeException(nameof(slot));
        uint checksum = Checksum(name);
        foreach (SourceModule<TArguments, TResult> module in loaded)
            if (module.Checksum == checksum && module.Function(slot) is { } function) return function(arguments);
        throw new InvalidOperationException($"Source module {name} user function {slot} is not loaded.");
    }
    public void CallParameterlessUserFunction(string name, uint slot)
    {
        if (slot >= 8) throw new ArgumentOutOfRangeException(nameof(slot));
        uint checksum = Checksum(name);
        foreach (SourceModule<TArguments, TResult> module in loaded)
            if (module.Checksum == checksum && module.ParameterlessFunction(slot) is { } function) { function(); return; }
        throw new InvalidOperationException($"Source module {name} parameterless user function {slot} is not loaded.");
    }
    private static uint Checksum(string name) => SourceAssetChecksum.Compute(Encoding.Latin1.GetBytes(SourceName(name)));
    private static string SourceName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int end = name.IndexOf('\0');
        ReadOnlySpan<char> text = name.AsSpan(0, end < 0 ? name.Length : end);
        foreach (char character in text)
            if (character > 255) throw new InvalidDataException("Original module names require source Latin1 bytes.");
        return text.ToString();
    }
}
