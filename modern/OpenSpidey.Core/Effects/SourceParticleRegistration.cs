namespace OpenSpidey.Core.Effects;

/// <summary>One source bit and the operations owned by its concrete effect adapter.</summary>
public sealed record SourceParticleRegistration(
    object Identity, Action<uint> Move, Func<bool> IsAlive, Action Destroy, Func<bool>? IsProtected = null);

public enum SourceParticleList
{
    NonRendered, TextBox, Flat, Linked2EndedLeftover, Pixel, PolyLine, GPolyLine,
    Quad, GenPoly, Chunk, Glow, Glass, GLine, Special
}
