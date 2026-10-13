using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Publishes player/camera commands to the interpreter before their genuine post-census owners exist.</summary>
internal sealed class DeferredMissionCommands
{
    private readonly Dictionary<ushort, IMissionCommandHandler?> handlers;
    public IEnumerable<IMissionCommandHandler> Commands => handlers.Keys.Select(opcode => new Command(opcode, this));

    public DeferredMissionCommands(IEnumerable<ushort> opcodes) =>
        handlers = opcodes.ToDictionary(opcode => opcode, _ => (IMissionCommandHandler?)null);

    public void Bind(IEnumerable<IMissionCommandHandler> commands)
    {
        IMissionCommandHandler[] supplied = commands.ToArray();
        if (supplied.Length != handlers.Count || supplied.Select(command => command.Opcode).Distinct().Count() != handlers.Count ||
            supplied.Any(command => !handlers.ContainsKey(command.Opcode)))
            throw new ArgumentException("Post-census command owners must match the declared source commands.", nameof(commands));
        if (handlers.Values.Any(handler => handler is not null))
            throw new InvalidOperationException("Source player command owners have already been bound.");
        foreach (IMissionCommandHandler command in supplied) handlers[command.Opcode] = command;
    }

    private sealed class Command(ushort opcode, DeferredMissionCommands owner) : IMissionCommandHandler
    {
        public ushort Opcode => opcode;
        public void Execute(MissionCommandContext context, TriggerInstruction instruction)
        {
            IMissionCommandHandler handler = owner.handlers[opcode] ?? throw new InvalidOperationException(
                $"Source command {opcode} requires the actual post-census player/camera owner.");
            handler.Execute(context, instruction);
        }
    }
}
