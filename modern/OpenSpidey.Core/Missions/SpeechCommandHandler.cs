using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Audio;

namespace OpenSpidey.Core.Missions;

public sealed class SpeechCommandHandler(MissionOpcode opcode, SpeechController speech,
    Func<SpeechEnvironment> environment) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)opcode;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        switch (opcode)
        {
            case MissionOpcode.AllowSpeech: speech.Allowed = true; break;
            case MissionOpcode.DisallowSpeech: speech.Allowed = false; break;
            case MissionOpcode.PlaySpeech:
                ReadOnlySpan<byte> bytes = instruction.Arguments.Span;
                speech.Play(BinaryPrimitives.ReadUInt16LittleEndian(bytes), BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]), environment());
                break;
            default: throw new InvalidOperationException($"Speech opcode {opcode} is not handled.");
        }
    }
    public static IEnumerable<IMissionCommandHandler> CreateHandlers(SpeechController speech, Func<SpeechEnvironment> environment) =>
        new MissionOpcode[] { MissionOpcode.AllowSpeech, MissionOpcode.DisallowSpeech, MissionOpcode.PlaySpeech }
            .Select(opcode => new SpeechCommandHandler(opcode, speech, environment));
}
