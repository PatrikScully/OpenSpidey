namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>SRhinoData stride 0xC at gRhinoData 0x552208 (rhino.cpp PlayXAPlease).</summary>
public readonly struct RhinoSpeechEntry(short bank, short slot, ushort rhinoVoice, short priority, int index)
{
    public short Bank { get; } = bank;
    public short Slot { get; } = slot;
    /// <summary>field_4: play at CRhino mPos instead of the player when non-zero.</summary>
    public ushort RhinoVoice { get; } = rhinoVoice;
    /// <summary>field_6: Redbook_XAPlayPos priority argument.</summary>
    public short Priority { get; } = priority;
    public int Index { get; } = index;
}
