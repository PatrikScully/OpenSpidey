using System.Collections.Frozen;
using OpenSpidey.Core.Animation;

namespace OpenSpidey.Core.Player;

/// <summary>Animation links recovered from the PC table at 0x555C6C, keyed by clip ID after startup sorting.</summary>
public static class PlayerAnimationFollowOns
{
    private static readonly FrozenDictionary<ushort, ushort> links = new Dictionary<ushort, ushort>
    {
        [1]=21, [6]=21, [21]=21, [11]=0, [12]=0, [13]=0, [194]=194, [200]=200,
        [3]=19, [52]=52, [53]=19, [54]=19, [55]=19, [56]=19, [19]=19, [20]=0, [5]=5,
        [27]=27, [28]=28, [31]=21, [37]=0, [29]=29, [30]=30, [270]=271, [10]=23,
        [9]=0, [23]=23, [24]=0, [4]=5, [191]=192, [197]=198, [192]=192, [198]=198,
        [170]=0, [187]=0, [172]=173, [173]=174, [174]=0, [175]=176, [176]=176,
        [177]=228, [178]=180, [180]=0, [181]=19, [25]=21, [89]=50, [91]=52,
        [93]=0, [120]=123, [121]=0, [122]=0, [123]=123, [124]=0, [125]=128,
        [128]=128, [126]=128, [231]=232, [212]=226, [219]=226, [222]=226, [216]=226,
        [225]=226, [226]=228, [228]=228, [232]=233, [233]=235, [235]=235,
        [227]=19, [234]=19, [213]=0, [230]=0, [237]=0, [229]=21, [236]=21,
        [281]=19, [276]=226, [278]=226, [63]=50, [65]=50, [64]=50, [75]=50,
        [69]=52, [71]=52, [70]=52, [81]=52, [60]=50, [62]=50, [61]=50,
        [72]=50, [66]=52, [68]=52, [67]=52, [78]=52, [59]=0, [86]=0,
        [87]=0, [88]=0, [284]=0, [290]=291, [291]=291, [292]=0, [293]=0,
        [294]=0, [130]=0, [134]=0, [150]=151, [151]=151, [152]=151, [153]=0, [285]=0
    }.ToFrozenDictionary();

    public static ushort? Resolve(AnimationPlaybackState state, Func<int, int> random)
    {
        if (!state.IsFinished) return null;
        if (links.TryGetValue(state.ClipId, out ushort next)) return next;
        return state.ClipId switch
        {
            0 => 0,
            50 => (ushort)(random(2) != 0 ? 51 : 85),
            51 or 85 => 50,
            57 => (ushort)(state.Direction == 1 ? 50 : 19),
            58 => (ushort)(state.Direction == 1 ? 52 : 19),
            _ => null
        };
    }
}
