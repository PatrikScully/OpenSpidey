using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Real scene dependencies for source319; the session binds each hook to its canonical owners.</summary>
public sealed class GodotBlackCatConfiguration
{
    public required Func<string, ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<int, OriginalBlackCatData> PoseData { get; init; }
    public required DecodedTexture ShadowTexture { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<bool> FastForwardRequested { get; init; }
    public required Func<GodotBlackCatActor, bool> IsSourceCulled { get; init; }
    public required Action<SourceParticleRegistration> SourceShadowCreated { get; init; }
    public required Action<object> SourceShadowRemoved { get; init; }
}
