using Godot;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Effects;

/// <summary>Player impact source emission and explicitly scheduled original ring/line lifetimes.</summary>
public partial class GodotPlayerCombatImpacts : Node3D, IPlayerCombatImpactHost
{
    private Func<uint> clock = null!;
    private Func<int, int> random = null!;
    private Func<FixedVector3, int> groundY = null!;
    private Action<FixedVector3, int, uint, Func<int, int>> bouncingRock = null!;
    public GodotThugCombatRings Rings { get; private set; } = null!;
    public GodotPlayerImpactLines Lines { get; private set; } = null!;
    public uint Clock => clock();
    public static GodotPlayerCombatImpacts Create(byte[] originalBits, Func<uint> sourceClock, Func<int, int> sourceRandom,
        Func<FixedVector3, int> getGroundY, Action<FixedVector3, int, uint, Func<int, int>> createBouncingRock)
    {
        ArgumentNullException.ThrowIfNull(sourceClock); ArgumentNullException.ThrowIfNull(sourceRandom);
        ArgumentNullException.ThrowIfNull(getGroundY); ArgumentNullException.ThrowIfNull(createBouncingRock);
        var result = new GodotPlayerCombatImpacts { Name = "OriginalPlayerCombatImpacts", clock = sourceClock, random = sourceRandom,
            groundY = getGroundY, bouncingRock = createBouncingRock };
        try
        {
            result.Rings = GodotThugCombatRings.Create(originalBits); result.AddChild(result.Rings);
            result.Lines = new(); result.AddChild(result.Lines);
            return result;
        }
        catch { result.Free(); throw; }
    }
    public void Emit(PlayerCombatImpactContext context, FixedVector3 position, int kind) => PlayerCombatImpact.Create(context, position, kind, this, random);
    public void Advance(uint sourceClock) { Rings.Advance(sourceClock); Lines.Advance(sourceClock); }
    public int GetGroundY(FixedVector3 position) => groundY(position);
    public void EmitBouncingRock(FixedVector3 position, int floor, uint selector, Func<int, int> sourceRandom) => bouncingRock(position, floor, selector, sourceRandom);
    public void EmitRing(ThugCombatImpactRing ring) => Rings.Emit(ring);
    public void EmitLine(PlayerImpactLineParticle particle, LineParticleMotion motion) => Lines.Emit(particle, motion);
}
