using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Core.UI;

/// <summary>Shared source gWideScreen and panel-Y shadow; gameplay keeps the user's full viewport.</summary>
public sealed class CinematicScreenState : IActorCinematicScreenHost
{
    public int WideScreenValue { get; set; }
    public int WideScreenShadow { get; set; }

    public void Set(ushort value)
    {
        WideScreenValue = value;
        WideScreenShadow = value;
    }

    public void Reset() => WideScreenValue = WideScreenShadow = 0;
}
