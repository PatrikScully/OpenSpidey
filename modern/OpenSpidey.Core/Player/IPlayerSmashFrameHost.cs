using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerSmashFrameHost
{
    void Update();
    void ResolveContacts(FixedVector3 previousPosition);
}
