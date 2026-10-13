using OpenSpidey.Core.Audio;

internal static class ActorSpeechAttachmentTests
{
    public static void Run(Action<bool, string> assert)
    {
        var attachment = new ActorSpeechAttachment();
        var request = new SpeechRequest(1, 9, 50);
        attachment.Attach(uint.MaxValue - 40, request.Bank, request.Slot);
        assert(attachment.CanStop(258, request), "Actor speech ownership survives clock wrap up to the strict 299-vblank boundary.");
        assert(!attachment.CanStop(259, request), "Source speech ownership expires at exactly 300 vblanks.");
        assert(!attachment.CanStop(uint.MaxValue - 41, request), "Unsigned backwards-clock subtraction cannot stop unrelated earlier playback.");
        assert(!attachment.CanStop(0, request with { Bank = 2 }) && !attachment.CanStop(0, request with { Slot = 10 }),
            "Actor speech stop requires both source bank and slot, independently of priority.");
        assert(attachment.CanStop(0, request with { Priority = 0 }) && !attachment.CanStop(0, null),
            "Priority changes do not revoke matching ownership, while absent playback cannot be stopped.");
        attachment.Attach(500, 2, 3);
        assert(!attachment.CanStop(500, request) && attachment.CanStop(500, new(2, 3, 1)),
            "A later attachment replaces both the source clock and clip identity.");
    }
}
