namespace OpenSpidey.Core.Audio;

/// <summary>Recovered CBaddy global script sound flags, including original signed-word promotion.</summary>
public static class ActorScriptSoundCode
{
    public static uint Global(ushort operand, bool levelSpecific)
    {
        uint flags = levelSpecific ? 0x8000u : 0;
        if (operand is >= 300 and <= 309) flags |= 0x4000;
        uint code = unchecked((uint)(short)operand);
        return code | flags;
    }
}
