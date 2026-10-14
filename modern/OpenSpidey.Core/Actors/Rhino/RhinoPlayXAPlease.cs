using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>CRhino::PlayXAPlease at 0x47E0A0 (bank offset, slot count, Rnd bound addend).</summary>
public static class RhinoPlayXAPlease
{
    public static void Execute(RhinoState state, ReadOnlySpan<RhinoSpeechEntry> table, IRhinoHost host, int bankOffset, int slotCount, int rndAddend)
    {
        if (table.Length == 0) return;
        int pick = host.Random(slotCount + rndAddend);
        int playedMask = state.Field3DC;
        if (pick >= slotCount) return;
        if (slotCount > 1)
        {
            if (((playedMask >> bankOffset) & (1 << pick)) != 0 && ++pick >= slotCount) pick = 0;
            for (int index = 0; index < slotCount; index++) playedMask &= ~(1 << (index + bankOffset));
            playedMask |= 1 << (pick + bankOffset);
        }
        int entryIndex = bankOffset + pick;
        if ((uint)entryIndex >= (uint)table.Length) return;
        RhinoSpeechEntry entry = table[entryIndex];
        if (entry.RhinoVoice != 0)
        {
            if (host.TryPlayPositionSpeech(host.Body.Position, entry.Bank, entry.Slot, entry.Priority))
            {
                host.AttachRhinoSpeech(entry.Bank, entry.Slot);
                state.Field3DC = playedMask;
            }
        }
        else if (host.PlayerCanTalkNow() &&
                 host.TryPlayPositionSpeech(host.PlayerPosition, entry.Bank, entry.Slot, entry.Priority))
        {
            host.AttachPlayerSpeech(entry.Bank, entry.Slot);
            state.Field3DC = playedMask;
        }
    }
}
