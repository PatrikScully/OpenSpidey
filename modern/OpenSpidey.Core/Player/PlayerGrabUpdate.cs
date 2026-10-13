using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>The source player fields read or written by GrabUpdate; action/surface transitions remain caller-owned.</summary>
public sealed class PlayerGrabState
{
    public uint ActionState { get; set; }
    public object? TargetHandle { get; set; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 UpDirection { get; set; }
    public FixedVector3 ForwardDirection { get; set; }
    public short Yaw { get; set; }
}

public sealed record PlayerGrabTarget(object Identity, ushort Type);

public interface IPlayerGrabHost
{
    PlayerGrabTarget? RecoverTarget(object? handle);
    FixedVector3 GetDynamicHookPosition(PlayerGrabTarget target, ActorHook hook);
    void ReportUnknownTarget(ushort type);
}

/// <summary>Original CPlayer::GrabUpdate output asymmetry and type-specific animated-hook placement.</summary>
public static class PlayerGrabUpdate
{
    public static bool Update(PlayerGrabState state, IPlayerGrabHost host, ref FixedVector3 outputPosition, ref short outputYaw)
    {
        if ((state.ActionState & 0x0E000000) == 0) return false;
        PlayerGrabTarget? target = host.RecoverTarget(state.TargetHandle);
        if ((state.ActionState & 0x08000000) != 0)
        {
            if (target is not null)
            {
                (ushort Part, int Up, int Forward)? attachment = target.Type switch
                {
                    304 or 306 or 320 => (8, 67, 22),
                    312 => (13, 67, 22),
                    317 => (10, 55, 32),
                    324 => (8, 55, 32),
                    _ => null
                };
                if (attachment is { } offsets)
                {
                    state.Position = host.GetDynamicHookPosition(target, new(0, 0, 0, offsets.Part));
                    state.Position -= state.UpDirection * offsets.Up;
                    state.Position += state.ForwardDirection * offsets.Forward;
                }
                else host.ReportUnknownTarget(target.Type);
            }
        }
        else outputPosition = state.Position - state.ForwardDirection * (target?.Type == 314 ? 58 : 32);
        outputYaw = state.Yaw;
        return true;
    }
}
