namespace OpenSpidey.Core.Player;

/// <summary>Process Spidey_SetUserFunction globals and the exact eight-slot loop after the full player AI callback.</summary>
public sealed class PlayerModuleHook
{
    public string? ModuleName { get; private set; }
    public uint FunctionMask { get; private set; }
    public void Set(string? name, uint mask) { ModuleName = name; FunctionMask = mask; }
    public void Run(Action<string, uint> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (ModuleName is null) return;
        for (uint slot = 0; slot < 8; slot++)
            // Original4C6869 rereads mask, and4C687B rereads name, after every previous module callback.
            if ((FunctionMask & (1u << (int)slot)) != 0) call(ModuleName!, slot);
    }
}
