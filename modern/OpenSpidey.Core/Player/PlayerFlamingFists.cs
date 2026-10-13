using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original4B826A..4B8346 flaming-fist simby5 emissions, before swing changes the posed hand endpoints.</summary>
public static class PlayerFlamingFists
{
    public static void Emit(byte fireWebbing, int fistBonusTicks, ushort animation,
        FixedVector3 leftHand, FixedVector3 rightHand, Func<int, int> random, Action<FixedVector3, int> emitEmber)
    {
        if (fireWebbing == 0 || fistBonusTicks == 0) return;
        int count = animation is 102 or 104 or 106 or 213 ? 3 : random(4) != 0 ? 0 : 1;
        for (int index = 0; index < count; index++) emitEmber(leftHand, 256);
        count = animation is 100 or 106 or 213 ? 3 : random(4) != 0 ? 0 : 1;
        for (int index = 0; index < count; index++) emitEmber(rightHand, 256);
    }
}
