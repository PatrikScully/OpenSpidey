using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player.Scripting;

/// <summary>World and actor effects needed by the player input VM; the VM owns timing and command lifetimes.</summary>
public interface IPlayerScriptHost
{
    FixedVector3 Position { get; set; }
    FixedVector3 Velocity { get; set; }
    short CameraHeading { get; }
    int AnimationId { get; }
    bool AnimationFinished { get; }
    bool AnimationSignal { get; set; }
    bool IsCrawling { get; }
    int ScriptedActionValue { get; set; }
    FixedVector3 TriggerPosition(int nodeId);
    void ResetIdleTimer();
    void TeleportToNode(int nodeId);
    void PlayVoice(short bank, short track, short priority);
    void PlayAnimation(int animationId, ScriptAnimationPolicy policy);
    void Stand();
    void EnterDelayedStandState();
    void SetScriptFlag(bool enabled);
    void RequestMenuExit();
}
