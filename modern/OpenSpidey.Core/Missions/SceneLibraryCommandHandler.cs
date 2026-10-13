using System.Text;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Synchronous library ownership operations supplied by the scene adapter.</summary>
public interface ISceneLibraryHost
{
    void LoadLibrary(string name);
    void UnloadLibrary(string name);
    void LoadEnvironment(string name);
    void SelectObjectLibrary(string name);
}

public sealed class SceneLibraryCommandHandler : IMissionCommandHandler
{
    private readonly ISceneLibraryHost host;
    private readonly MissionOpcode operation;
    public ushort Opcode => (ushort)operation;

    public SceneLibraryCommandHandler(MissionOpcode operation, ISceneLibraryHost host)
    {
        if (operation is not (MissionOpcode.LoadSceneLibrary or MissionOpcode.UnloadSceneLibrary or
            MissionOpcode.LoadEnvironment or MissionOpcode.SynchronizeLibraries or MissionOpcode.SetObjectLibrary))
            throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.host = host;
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        // The modern host completes loads synchronously, so SpoolLock has already been satisfied.
        if (operation == MissionOpcode.SynchronizeLibraries) return;
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException($"Node {context.Node.Id}: library name has no terminator.");
        string name = Encoding.Latin1.GetString(instruction.Arguments.Span[..end]);
        switch (operation)
        {
            case MissionOpcode.LoadSceneLibrary: host.LoadLibrary(name); break;
            case MissionOpcode.UnloadSceneLibrary: host.UnloadLibrary(name); break;
            case MissionOpcode.LoadEnvironment: host.LoadEnvironment(name); break;
            case MissionOpcode.SetObjectLibrary: host.SelectObjectLibrary(name); break;
        }
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(ISceneLibraryHost host) =>
        new MissionOpcode[] { MissionOpcode.LoadSceneLibrary, MissionOpcode.UnloadSceneLibrary,
            MissionOpcode.LoadEnvironment, MissionOpcode.SynchronizeLibraries, MissionOpcode.SetObjectLibrary }
        .Select(operation => new SceneLibraryCommandHandler(operation, host));
}
