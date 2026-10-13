using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Effects;

/// <summary>Original crate shatter, source command checksum and true CFrag/GlowFlash effects with explicit scene ownership.</summary>
public sealed class GodotCrateEffects : IEnvironmentCrateHost, ICrateExplosionHost
{
    private readonly GodotItemShatter shatter;
    private readonly GodotExplosionFragments fragments;
    private readonly GodotGlowFlash glows;
    private readonly Func<GodotGrenadeEnvironmentItem, IReadOnlyList<DecodedTexture>> sourceTextures;
    private readonly Action<uint> triggerCommandPoint;
    private readonly Func<int, int> random;
    public bool LowMemory => shatter.LowMemory;

    public GodotCrateEffects(GodotItemShatter shatter, GodotExplosionFragments fragments, GodotGlowFlash glows,
        Func<GodotGrenadeEnvironmentItem, IReadOnlyList<DecodedTexture>> sourceTextures,
        Action<uint> triggerCommandPoint, Func<int, int> random)
    {
        this.shatter = shatter ?? throw new ArgumentNullException(nameof(shatter));
        this.fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
        this.glows = glows ?? throw new ArgumentNullException(nameof(glows));
        this.sourceTextures = sourceTextures ?? throw new ArgumentNullException(nameof(sourceTextures));
        this.triggerCommandPoint = triggerCommandPoint ?? throw new ArgumentNullException(nameof(triggerCommandPoint));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public bool Shatter(IEnvironmentCrate crate, bool checkSeen, bool markProcessed)
    {
        if (crate is not GodotGrenadeEnvironmentItem item)
            throw new ArgumentException("Original crate destruction requires its genuine source ENV item.", nameof(crate));
        bool result = shatter.Shatter(item.Identity, item.Bank, item.ModelIndex, item.Position, item.Angles,
            checkSeen, markProcessed, sourceTextures(item));
        if (result && markProcessed) item.MarkShattered();
        return result;
    }
    public void TriggerCommandPoint(uint checksum) => triggerCommandPoint(checksum);
    public int Random(int range) => random(range);
    public void CreateCrateExplosion(FixedVector3 position, bool small) => CrateExplosion.Create(position, small, this, random);
    public void CreateFragment(ExplosionFragmentParameters parameters) => fragments.Emit(parameters, random);
    public void CreateGlowFlash(GlowFlashParameters parameters) => glows.Emit(parameters, random);
}
