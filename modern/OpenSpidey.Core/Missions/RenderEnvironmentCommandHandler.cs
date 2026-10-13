using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;

namespace OpenSpidey.Core.Missions;

public interface IRenderEnvironmentHost
{
    void SetFog(ushort duration, ushort near, ushort range);
    void SetSkyColour(uint packed);
    void SetFadeColour(uint packed);
    void CreateBackground(uint checksum, ActorAngles angularVelocity);
    void SetBackgroundVisible(ushort index, bool visible);
    void EndCommandList();
}

/// <summary>Original fog, sky and background commands, retaining file alignment and source word packing.</summary>
public sealed class RenderEnvironmentCommandHandler : IMissionCommandHandler
{
    private static readonly MissionOpcode[] Operations = [MissionOpcode.SetFoggingParameters,
        MissionOpcode.BackgroundOn, MissionOpcode.BackgroundOff, MissionOpcode.CreateBackground,
        MissionOpcode.SetFadeColour, MissionOpcode.SetSkyColour];
    private readonly MissionOpcode operation;
    private readonly IRenderEnvironmentHost host;
    public ushort Opcode => (ushort)operation;

    public RenderEnvironmentCommandHandler(MissionOpcode operation, IRenderEnvironmentHost host)
    {
        if (!Operations.Contains(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode) throw new ArgumentException("Rendering command does not match its handler.", nameof(instruction));
        switch (operation)
        {
            case MissionOpcode.SetFoggingParameters:
                Require(6);
                host.SetFog(Word(0), Word(2), Word(4));
                break;
            case MissionOpcode.SetSkyColour:
                Require(4);
                host.SetSkyColour(((uint)Word(0) << 16) + Word(2));
                break;
            case MissionOpcode.SetFadeColour:
                Require(4);
                host.SetFadeColour(((uint)Word(0) << 16) + Word(2));
                break;
            case MissionOpcode.BackgroundOn:
            case MissionOpcode.BackgroundOff:
                Require(2);
                host.SetBackgroundVisible(Word(0), operation == MissionOpcode.BackgroundOn);
                break;
            case MissionOpcode.CreateBackground:
                int baseOffset = checked(instruction.SourceOffset + 2);
                int padding = (4 - (baseOffset & 3)) & 3;
                Require(padding + 10);
                host.CreateBackground(BinaryPrimitives.ReadUInt32LittleEndian(instruction.Arguments.Span[padding..]),
                    new(unchecked((short)Word(padding + 4)), unchecked((short)Word(padding + 6)), unchecked((short)Word(padding + 8))));
                break;
        }

        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span[offset..]);
        void Require(int length)
        {
            if (instruction.Arguments.Length != length)
                throw new InvalidDataException($"Node {context.Node.Id}, command {Opcode}, offset 0x{instruction.SourceOffset:X}: expected {length} argument bytes.");
        }
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(IRenderEnvironmentHost host) =>
        Operations.Select(operation => new RenderEnvironmentCommandHandler(operation, host));
}
