using System.Buffers.Binary;
using System.Text;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Actual session/save fields and application teardown required by original level-transition commands.</summary>
public interface ILevelTransitionHost
{
    string MissionName { get; }
    LevelExitReason ExitReason { get; }
    bool HasPlayer { get; }
    void PreserveCarriedPlayerState();
    void SetExitReason(LevelExitReason reason);
    void SetMissionName(string name);
    void KillAllSceneObjects();
    void SetEndLevelNode(ushort node);
}

/// <summary>Original EndLevel, LoadNewTrg and EndLevelNode policy; engine navigation belongs to the scene host.</summary>
public sealed class LevelTransitionCommandHandler : IMissionCommandHandler
{
    private static readonly MissionOpcode[] Operations =
        [MissionOpcode.EndLevel, MissionOpcode.LoadMission, MissionOpcode.EndLevelNode, MissionOpcode.KillSceneObjects];
    private readonly MissionOpcode operation;
    private readonly ILevelTransitionHost host;
    public ushort Opcode => (ushort)operation;

    public LevelTransitionCommandHandler(MissionOpcode operation, ILevelTransitionHost host)
    {
        if (!Operations.Contains(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode) throw new ArgumentException("Level transition command does not match its handler.", nameof(instruction));
        switch (operation)
        {
            case MissionOpcode.KillSceneObjects:
                RequireSize(0);
                host.KillAllSceneObjects();
                break;
            case MissionOpcode.EndLevel:
                RequireSize(0);
                host.SetExitReason(LevelExitReason.LevelCompleted);
                break;
            case MissionOpcode.EndLevelNode:
                RequireSize(2);
                host.SetEndLevelNode(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span));
                break;
            case MissionOpcode.LoadMission:
                int terminator = instruction.Arguments.Span.IndexOf((byte)0);
                if (terminator < 0) throw new InvalidDataException($"Node {context.Node.Id}: transition mission name has no terminator.");
                string name = Encoding.Latin1.GetString(instruction.Arguments.Span[..terminator]);
                bool same = SourceNameEquals(name, host.MissionName);
                if (same && host.ExitReason == LevelExitReason.Running && !host.HasPlayer) host.PreserveCarriedPlayerState();
                host.SetExitReason(same ? LevelExitReason.RetryRequested : LevelExitReason.LevelCompleted);
                // Utils_CopyString's size9 operand copies at most nine original bytes, then writes NUL.
                host.SetMissionName(name[..Math.Min(name.Length, 9)]);
                host.KillAllSceneObjects();
                break;
        }

        void RequireSize(int size)
        {
            if (instruction.Arguments.Length != size)
                throw new InvalidDataException($"Node {context.Node.Id}, command {Opcode}: expected {size} argument bytes.");
        }
    }

    private static bool SourceNameEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++)
            if (LowerAscii(left[index]) != LowerAscii(right[index])) return false;
        return true;
        static char LowerAscii(char value) => value is >= 'A' and <= 'Z' ? (char)(value + 32) : value;
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(ILevelTransitionHost host) =>
        Operations.Select(operation => new LevelTransitionCommandHandler(operation, host));
}
