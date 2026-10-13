namespace OpenSpidey.Assets.Animation;

public sealed record MatrixAnimationLibrary(IReadOnlyList<ushort> Parents,
    IReadOnlyList<ushort> CalculationOrder, IReadOnlyList<MatrixAnimationClip> Clips) : IAnimationPoseSource
{
    public int ClipCount => Clips.Count;

    public IAnimationPoseClip DecodeClip(int id)
    {
        if ((uint)id >= Clips.Count) throw new ArgumentOutOfRangeException(nameof(id));
        return Clips[id];
    }
}
