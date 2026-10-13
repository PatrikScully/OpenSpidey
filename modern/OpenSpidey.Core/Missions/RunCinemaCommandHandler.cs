using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cinematics;

namespace OpenSpidey.Core.Missions;

/// <summary>RunCinema blocks subsequent commands until playback or a permitted skip completes.</summary>
public sealed class RunCinemaCommandHandler(ICinemaPlayback playback, bool restartedAfterDeath = false) : IMissionBlockingCommandHandler
{
    private bool skipDeathAutoexec = restartedAfterDeath;
    public ushort Opcode => (ushort)MissionOpcode.RunCinema;
    public bool IsBlocking => playback.IsPlaying;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (skipDeathAutoexec && context.Node.TypeCode is (ushort)TriggerNodeKind.Autoexec or (ushort)TriggerNodeKind.AlternativeAutoexec)
        {
            skipDeathAutoexec = false;
            return;
        }
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
        // The recovered dispatch's default branch has no movie.
        if (CinemaCatalog.Cinemas.TryGetValue(id, out CinemaDefinition? cinema)) playback.Play(cinema);
    }
}
