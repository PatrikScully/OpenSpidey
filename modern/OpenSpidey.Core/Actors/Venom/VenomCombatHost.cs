using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>SLineInfo after M3dColij_InitLineInfo (Length at 0x44) and M3dZone_LineToItem mode 1 (Distance at 0x40, pItem at 0x68).</summary>
public readonly record struct VenomLineHit(int Distance, int Length);

/// <summary>Player, web, effect and collision boundaries of the CVenom combat states.</summary>
public interface IVenomCombatHost
{
    ActorProcedureState Actor { get; }
    ActorProcedureList Procedures { get; }
    /// <summary>MechList player action word (0xE1C).</summary>
    uint PlayerActionState { get; }
    /// <summary>MechList player health (0xE2).</summary>
    short PlayerHealth { get; }
    /// <summary>MechList player field 0x36C.</summary>
    int PlayerField36C { get; }
    /// <summary>MechList player wall-crawl flag (0xAD4).</summary>
    bool PlayerCrawling { get; }
    byte PlayerField8E8 { get; }
    byte PlayerField8E9 { get; }
    /// <summary>CItem 0x128 animation frame.</summary>
    void SetAnimationFrame(int frame);
    /// <summary>Recover the CTrapWebEffect behind CBaddy 0x104 (trap) or 0x10C (tug), burst it and clear the handle; no-op without a handle.</summary>
    void BurstWeb(bool tug);
    bool WebHeld(bool tug);
    /// <summary>Hook count (CGPolyLine mNumSegs) of the recovered 0x104 web, or -1 when the handle is empty or dead.</summary>
    int TrapWebHookCount();
    /// <summary>CBody::OutlineOn pose joints 16 and 17 translation shorts (0xC6..0xCA and 0xD2..0xD6).</summary>
    void SetArmPose(FixedVector3 first, FixedVector3 second);
    /// <summary>The recovered CAIProc_StateSwitchSendMessage target: a message to this venom.</summary>
    void SendSelfMessage(int code);
    /// <summary>M3dColij_InitLineInfo plus M3dZone_LineToItem(info, 1); null when pItem stays empty.</summary>
    VenomLineHit? LineToItem(FixedVector3 start, FixedVector3 end);
    /// <summary>CVenomElectrified created or deleted by the 0x330 goo timer.</summary>
    void SetElectrified(bool present);
    /// <summary>Virtual delete of the CWeb stored at 0x428.</summary>
    void DeleteWeb();
}

/// <summary>M3dColij_InitLineInfo Length: integer root of the short-truncated whole-unit deltas, |dy| for vertical lines.</summary>
public static class VenomLineProbe
{
    public static int Length(FixedVector3 start, FixedVector3 end)
    {
        short dx = unchecked((short)(unchecked(end.X - start.X) >> 12));
        short dy = unchecked((short)(unchecked(end.Y - start.Y) >> 12));
        short dz = unchecked((short)(unchecked(end.Z - start.Z) >> 12));
        int horizontal = dz * dz + dx * dx;
        if (horizontal != 0) return FixedVectorMath.OriginalSquareRoot(horizontal + dy * dy);
        return dy >= 0 ? dy : -dy;
    }
}
