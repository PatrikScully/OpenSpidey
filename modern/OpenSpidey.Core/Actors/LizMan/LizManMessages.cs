using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

public interface ILizManMessageHost : ILizManPerceptionHost
{
    /// <summary>CBaddy 0x2F0 route flags.</summary>
    uint RouteFlags { get; set; }
    /// <summary>Mem_RecoverPointer on a CMessage sender handle; null when the sender is gone.</summary>
    FixedVector3? RecoverSenderPosition(object? handle);
    /// <summary>CBaddy::PathCheck (0x403310) with clearance 55 and a hit point seeded with <paramref name="to"/>.</summary>
    ActorPathCheckResult PathCheckWithHit(FixedVector3 from, FixedVector3 to);
}

/// <summary>CLizMan::ProcessMessages (0x450640) and RunToWhereActionIs (0x450270).</summary>
public static class LizManMessages
{
    public static void Process(LizManState state, ThugMessageQueue messages, ILizManMessageHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(host);
        for (ThugMessage? message = messages.Head; message is not null; message = message.Next)
        {
            int current = state.State;
            if (current == 20 && message.Code < 11)
            {
                message.Flags |= 1;
                continue;
            }
            switch (message.Code)
            {
                case 5:
                    if (current != 14)
                    {
                        host.Neutralize();
                        state.State = 14;
                        state.Substate = 0;
                        break;
                    }
                    if (state.Substate is not (3 or 2)) continue;
                    state.Field1F8 = 0;
                    state.Substate = 2;
                    break;
                case 7:
                    if (state.Field390 == 0 && current is 2 or 1 &&
                        host.RecoverSenderPosition(message.SenderHandle) is { } sender)
                        RunToWhereActionIs(state, host, sender);
                    break;
                case 10:
                    if (current is 2 or 1) RunToWhereActionIs(state, host, message.Vector0);
                    state.Field374 = host.TotalTime;
                    break;
                case 0x13:
                    throw new NotSupportedException("CLizMan::ProcessMessages message 0x13 needs the field_33C drop offset (0x450640).");
                case 6 when current == 15:
                    break;
                case 0xE when (state.BehaviorFlags & 0x40) != 0:
                    break;
                case 6:
                case 0xE:
                case 0xF:
                case 0x14:
                    throw new NotSupportedException(
                        $"CLizMan::ProcessMessages message {message.Code} needs the unported web/pose handling (0x450640).");
            }
            message.Flags |= 1;
        }
        messages.Cleanup();
    }

    public static void RunToWhereActionIs(LizManState state, ILizManMessageHost host, FixedVector3 action)
    {
        FixedVector3 position = host.Body.Position;
        if (unchecked((uint)LizManJumpScan.ApproximateDistance(position, action)) > 0x5DC) return;
        if (!host.AddPointToPath(position, 0x5DC)) return;
        FixedVector3 approach = new(
            unchecked(action.X + (action.X - position.X <= 0 ? 409600 : -409600)), position.Y,
            unchecked(action.Z + (action.Z - position.Z <= 0 ? 409600 : -409600)));
        if (host.PlayerUnavailable == 0 && host.PathCheck(position, host.PlayerPosition) == 0 &&
            host.AddPointToPath(host.PlayerPosition, 0x5DC))
        {
            Commit(state, host);
            return;
        }
        ActorPathCheckResult result = host.PathCheckWithHit(position, approach);
        approach = result.HitPoint;
        if (result.Code == 0)
        {
            if (host.AddPointToPath(approach, 0x5DC)) Commit(state, host);
            return;
        }
        if (result.Code != 2 || unchecked((uint)LizManJumpScan.ApproximateDistance(approach, position)) < 0x64) return;
        FixedVector3 partial = (((approach - position) >> 12) * 3700) + position;
        if (host.AddPointToPath(partial, 0)) Commit(state, host);
    }

    private static void Commit(LizManState state, ILizManMessageHost host)
    {
        host.Neutralize();
        state.State = 2;
        state.Field374 = unchecked(host.TotalTime - 240);
        state.CombatFlags &= ~0x10000000u;
        host.RouteFlags |= 1;
        state.Substate = 0;
    }
}
