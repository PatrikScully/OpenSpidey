namespace OpenSpidey.Assets.Animation;

public sealed record CompressedAnimationLibrary(IReadOnlyList<ushort> Parents,
    IReadOnlyList<ushort> CalculationOrder, IReadOnlyList<CompressedAnimationClip> Clips) : IAnimationPoseSource
{
    public int ClipCount => Clips.Count;

    public IAnimationPoseClip DecodeClip(int id)
    {
        if ((uint)id >= Clips.Count) throw new ArgumentOutOfRangeException(nameof(id));
        return new CompressedPoseClip(this, Clips[id].Decode());
    }
}
