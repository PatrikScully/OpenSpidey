using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player.Scripting;

/// <summary>Source player1A4 and E00, separate from the process-wide abort byte60CFC4.</summary>
public sealed class PlayerCutsceneSkipState(byte allowed = 0)
{
    public byte Allowed { get; private set; } = allowed;
    public uint FieldE00 { get; set; }
    public void SetAllowed(ushort value) => Allowed = value == 0 ? (byte)0 : (byte)1;
    public void BeginScript() => Allowed = 0;
}

public interface IPlayerCutsceneCleanupHost
{
    int CameraMode { get; }
    int LevelId { get; }
    int CheckpointNode { get; }
    FixedVector3 Position { get; }
    FixedVector3 ForwardDirection { get; }
    IReadOnlyList<int> CheckpointLinks(int node);
    FixedVector3 TriggerPosition(int node);
    void StopSpeech();
    void SetCameraMode(ushort mode);
    void SetFloorNormalAndOrient(FixedVector3 direction);
    void PrepareDistanceAttack(FixedVector3 direction);
    void PlayIdleAnimation();
    void Stand();
    void SetCameraStartPosition();
    void ClearAbort();
}

/// <summary>Original CPlayer::AI skip gate and the separate post-mission CutSceneSkipCleanup phase.</summary>
public static class PlayerCutsceneSkip
{
    public static bool ReadManualInput(PlayerCutsceneSkipState state, bool synthesizedInput,
        bool forceMenuExit, int difficulty, Span<PlayerButtonState> pad)
    {
        RequirePad(pad);
        if (!synthesizedInput || state.Allowed == 0) return false;
        if (forceMenuExit)
        {
            if (pad[14].Triggered == 0) return false;
            pad[14] = pad[14] with { Triggered = 0 };
        }
        else
        {
            if (difficulty == 0 || (pad[3].Triggered == 0 && pad[2].Triggered == 0)) return false;
            pad[16] = pad[16] with { Triggered = 0 };
            pad[2] = pad[2] with { Triggered = 0 };
            pad[3] = pad[3] with { Triggered = 0 };
        }
        return true;
    }

    public static void Cleanup(PlayerCutsceneSkipState state, IPlayerCutsceneCleanupHost host,
        Span<PlayerButtonState> pad)
    {
        RequirePad(pad);
        host.StopSpeech();
        if (host.CameraMode != 3 && host.LevelId != 514) host.SetCameraMode(3);
        int checkpoint = host.CheckpointNode;
        if (checkpoint != 0)
        {
            IReadOnlyList<int> links = host.CheckpointLinks(checkpoint);
            FixedVector3 direction;
            if (links.Count != 0)
            {
                FixedVector3 target = host.TriggerPosition(links[0]);
                FixedVector3 horizontal = (host.Position - target) >> 12;
                direction = FixedVectorMath.Normalize(horizontal with { Y = 0 });
                host.SetFloorNormalAndOrient(direction);
            }
            else direction = host.ForwardDirection;
            host.PrepareDistanceAttack(direction);
        }
        host.PlayIdleAnimation();
        host.Stand();
        state.FieldE00 = 0;
        host.SetCameraStartPosition();
        host.ClearAbort();
        pad[16] = pad[16] with { Pressed = 1 };
        pad[3] = pad[3] with { Pressed = 1 };
    }

    private static void RequirePad(Span<PlayerButtonState> pad)
    {
        if (pad.Length != 20) throw new ArgumentException("The source pad has twenty button records.", nameof(pad));
    }
}
