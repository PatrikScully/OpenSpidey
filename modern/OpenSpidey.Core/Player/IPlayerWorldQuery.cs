using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerWorldHit(PlayerGroundContact Contact, int InstanceIndex, int ModelIndex, int FaceIndex,
    string? SourceAsset = null, uint? ModelChecksum = null, ulong? SupportId = null);

/// <summary>Player-terrain segment queries in original Q12 coordinates; trigger surfaces are nonblocking.</summary>
public interface IPlayerWorldQuery
{
    PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end);
}
