using Godot;

namespace OpenSpidey.Game.Import;

/// <summary>Uploads affine matrix poses without decomposing them into rotation and scale.</summary>
internal sealed class ExactPoseSkin : IDisposable
{
    private readonly Rid skeleton;
    private readonly int partCount;
    private readonly List<MeshInstance3D> meshes = [];
    private bool disposed;
    public bool HasRenderStorage => !disposed && RenderingServer.SkeletonGetBoneCount(skeleton) == partCount;

    public ExactPoseSkin(int partCount)
    {
        this.partCount = partCount;
        skeleton = RenderingServer.SkeletonCreate();
        RenderingServer.SkeletonAllocateData(skeleton, partCount);
    }

    public void Attach(MeshInstance3D mesh)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RenderingServer.InstanceAttachSkeleton(mesh.GetInstance(), skeleton);
        meshes.Add(mesh);
    }

    public void Apply(IReadOnlyList<Transform3D> transforms)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (transforms.Count != partCount) throw new ArgumentException("Skin pose part count differs.", nameof(transforms));
        for (int part = 0; part < partCount; part++) RenderingServer.SkeletonBoneSetTransform(skeleton, part, transforms[part]);
    }

    public Transform3D ReadTransform(int part)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return RenderingServer.SkeletonBoneGetTransform(skeleton, part);
    }

    public void Dispose()
    {
        if (disposed) return;
        foreach (MeshInstance3D mesh in meshes)
            if (GodotObject.IsInstanceValid(mesh)) RenderingServer.InstanceAttachSkeleton(mesh.GetInstance(), default);
        RenderingServer.FreeRid(skeleton);
        disposed = true;
    }
}
