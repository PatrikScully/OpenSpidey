namespace OpenSpidey.Core.Player;

/// <summary>Source player targeting/input/action/final-pose phases supplied by the native melee owner.</summary>
public interface IPlayerMeleeFrameHost
{
    int? HeldObjectFlags { get; }
    void PrepareTargets(int ticks);
    void ReadControls(PlayerMovementInputState movement,PlayerButtonInput buttons);
    bool CheckKick();
    void UpdatePunchCombo();
    void UpdateInteraction(ushort completedAnimation);
    void UpdateZipDive(ushort completedAnimation);
    void FinishFrame();
    void FinishInput();
}
