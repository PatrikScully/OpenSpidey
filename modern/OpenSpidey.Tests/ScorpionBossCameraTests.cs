using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Cameras;

/// <summary>CScorpion AI488590's boss-camera entry/exit call order, through the camera adapter, against the real CCamera members and417CB0.</summary>
internal static class ScorpionBossCameraTests
{
    private const int BossEnter = 13, BossExit = 14;

    private sealed class Extension : ICameraAiSequenceExtension
    {
        private ScorpionBossCamera? camera;
        private int saved;
        private ushort savedHorizontal;
        private short savedVertical;

        public bool Execute(int kind, CameraAi ai, CameraFocusTarget boss, Action<CameraFocusTarget?> setBoss)
        {
            if (kind is not (BossEnter or BossExit)) return false;
            camera ??= new(ai, _ => null, _ => throw new InvalidOperationException("The fixture camera holds no hidden items."));
            if (kind == BossEnter)
            {
                if (saved == 0) { Save(); saved = 1; }
                camera.SetMode(16);
                camera.SpinRate = 127;
                camera.SetHorizontalDistance(savedHorizontal, 0);
                camera.SetVerticalDistance(savedVertical, 0);
                camera.StationaryRadius = 0;
                setBoss(boss);
            }
            else
            {
                Save();
                camera.SetMode(3);
                saved = 1;
                setBoss(null);
            }
            return true;
        }

        public IEnumerable<int> Outputs(CameraAi ai, CameraFocusTarget? boss) =>
            [ai.Boss.SpinRate, ai.Boss.StationaryRadius, saved, savedHorizontal, savedVertical, boss is null ? 0 : 1];

        private void Save()
        {
            savedHorizontal = camera!.GetHorizontalDistance();
            savedVertical = camera.GetVerticalDistance();
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        (int cases, int frames) = CameraAiTests.RunSequences("scorpion-boss-camera.json", "Original Scorpion boss camera488590/417CB0", assert,
            () => new Extension());
        assert(cases == 400 && frames > 3000, "The Scorpion boss camera fixture covers every captured sequence.");
    }
}
