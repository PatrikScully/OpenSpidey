using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed class PlayerAutoAimState
{
    public ushort FieldE04 {get;set;}
    public ushort FieldE06 {get;set;}
    public ushort FieldE08 {get;set;}
    public FixedVector3? SwitchAimPoint {get;set;}
    public IPlayerMeleeTarget? Target {get;set;}
}

public interface IPlayerAutoAimHost
{
    byte SurfaceTransition {get;}
    int ElapsedTicks {get;}
    bool HasReticle {get;}
    short ReticleYaw {get;set;}
    IPlayerMeleeTarget? SelectTargetBaddy(int distance,int cone,int distanceWeight,int coneWeight);
    ushort GetTargetHeight(IPlayerMeleeTarget target);
    void CreateReticle();
    void DeleteReticle();
    void PositionReticle(FixedVector3 position);
}

/// <summary>Original CPlayer::SelectAutoAimTarget, including retained DCC during surface transition and reticle503 ownership.</summary>
public static class PlayerAutoAimSelection
{
    public static byte Update(PlayerAutoAimState state,IPlayerAutoAimHost host)
    {
        state.FieldE04=state.FieldE06=state.FieldE08=0;state.SwitchAimPoint=null;
        if(host.SurfaceTransition!=0)
        {
            if(host.HasReticle)host.DeleteReticle();
            return host.SurfaceTransition;
        }
        state.Target=null;
        IPlayerMeleeTarget? target=host.SelectTargetBaddy(2048,2896,4096,4096);
        if(target is not null)
        {
            state.Target=target;
            FixedVector3 position=target.Position with{Y=unchecked(target.Position.Y-(host.GetTargetHeight(target)<<12))};
            if(!host.HasReticle)host.CreateReticle();
            host.PositionReticle(position);
            host.ReticleYaw=unchecked((short)(host.ReticleYaw+16*unchecked((ushort)host.ElapsedTicks)));
        }
        else if(host.HasReticle)host.DeleteReticle();
        return host.SurfaceTransition;
    }
}
