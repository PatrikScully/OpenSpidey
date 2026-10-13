using OpenSpidey.Assets.Models;

namespace OpenSpidey.Game.Import;

/// <summary>Actual emitted surface corners retain their original vertex identities for source vertex lighting.</summary>
public sealed record ImportedAnimatedPartGeometry(PsxModel Model, IReadOnlyList<IReadOnlyList<byte>> SurfaceVertexIndices);
