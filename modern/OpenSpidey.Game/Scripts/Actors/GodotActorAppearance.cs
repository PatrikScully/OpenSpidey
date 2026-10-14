using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

public interface IGodotActorAppearanceHost
{
    Node3D AppearanceNode { get; }
    ImportedAnimatedModel Model { get; }
    uint BodyFlags { get; set; }
    ushort ItemFlags { get; }
    uint TintColor { get; }
    FixedVector3 ShadowPosition { get; }
    int ShadowScale { get; }
}

/// <summary>Actor-owned PC tint/transparency overrides and original protected body shadow.</summary>
public sealed class GodotActorAppearance : IDisposable
{
    private sealed record Surface(MeshInstance3D Mesh, int Index, StandardMaterial3D Original,
        StandardMaterial3D Owned, SourceMaterialSnapshot Snapshot);
    private sealed record Geometry(MeshInstance3D Instance, ArrayMesh Original, ArrayMesh Owned,
        Godot.Collections.Array[] Arrays, Color[][] Colors);
    private readonly List<Geometry> geometry = [];
    private (bool Enabled,uint Rgb)? lastTint;
    private readonly IGodotActorAppearanceHost actor;
    private readonly List<Surface> surfaces = [];
    private readonly float tintGamma;
    private bool disposed;
    public GodotActorShadow Shadow { get; }
    public short ShadowDistance { get; set; }
    public short ShadowThreshold { get; set; } = 200;
    private GodotActorAppearance(IGodotActorAppearanceHost actor, GodotActorShadow shadow, float tintGamma)
    { this.actor = actor; Shadow = shadow; this.tintGamma = tintGamma; }
    public static GodotActorAppearance Create(IGodotActorAppearanceHost actor, DecodedTexture shadowTexture, float sourceTintGamma = .7f)
    {
        if (sourceTintGamma is not (.7f or .4f)) throw new ArgumentOutOfRangeException(nameof(sourceTintGamma));
        var owner = new GodotActorAppearance(actor, GodotActorShadow.Create(shadowTexture), sourceTintGamma);
        try
        {
            foreach (MeshInstance3D mesh in Descendants(actor.Model).OfType<MeshInstance3D>())
            {
                if (mesh.Mesh is null) continue;
                if (mesh.Mesh is not ArrayMesh sourceMesh) throw new NotSupportedException("Source actor geometry must be an imported ArrayMesh.");
                var ownedMesh = new ArrayMesh();
                var arrays = Enumerable.Range(0,sourceMesh.GetSurfaceCount()).Select(sourceMesh.SurfaceGetArrays).ToArray();
                var colors = arrays.Select(array => array[(int)Mesh.ArrayType.Color].AsColorArray()).ToArray();
                owner.geometry.Add(new(mesh, sourceMesh, ownedMesh, arrays, colors));
                for (int surface = 0; surface < arrays.Length; surface++)
                {
                    ownedMesh.AddSurfaceFromArrays(sourceMesh.SurfaceGetPrimitiveType(surface), arrays[surface]);
                    ownedMesh.SurfaceSetMaterial(surface, sourceMesh.SurfaceGetMaterial(surface));
                }
                mesh.Mesh = ownedMesh;
                for (int index = 0; index < mesh.Mesh.GetSurfaceCount(); index++)
                {
                    if (mesh.GetActiveMaterial(index) is not StandardMaterial3D original)
                        throw new NotSupportedException("Original actor surface requires its imported standard material.");
                    var material = (StandardMaterial3D)original.Duplicate();
                    owner.surfaces.Add(new(mesh, index, original, material, SourceMaterialSnapshot.Of(original)));
                    mesh.SetSurfaceOverrideMaterial(index, material);
                }
            }
            if (owner.surfaces.Count == 0) throw new InvalidDataException("Original actor model has no renderable surfaces.");
            actor.AppearanceNode.AddChild(owner.Shadow);
            owner.ShadowOn();
            return owner;
        }
        catch { owner.Dispose(); throw; }
    }
    public void ShadowOn() => actor.BodyFlags |= 8;
    public void KillShadow()
    {
        actor.BodyFlags &= ~8u;
        Shadow.State.Kill();
        Shadow.Rebuild();
    }
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ushort flags = actor.ItemFlags;
        bool tint = (flags & 0x480) == 0x400;
        uint rgb = actor.TintColor;
        if (lastTint != (tint,rgb))
        {
            foreach (Geometry item in geometry)
            {
                item.Owned.ClearSurfaces();
                for (int index = 0; index < item.Arrays.Length; index++)
                {
                    Color[] colors = tint ? item.Colors[index].Select(color => new Color(
                        Channel(color.R,(byte)rgb),Channel(color.G,(byte)(rgb >> 8)),Channel(color.B,(byte)(rgb >> 16)),color.A)).ToArray() : item.Colors[index];
                    item.Arrays[index][(int)Mesh.ArrayType.Color] = colors;
                    item.Owned.AddSurfaceFromArrays(item.Original.SurfaceGetPrimitiveType(index),item.Arrays[index]);
                    item.Owned.SurfaceSetMaterial(index,item.Original.SurfaceGetMaterial(index));
                }
            }
            lastTint = (tint,rgb);
        }
        float? translucency = SourceItemTranslucency.Alpha(flags);
        foreach (Surface surface in surfaces)
        {
            surface.Mesh.SetSurfaceOverrideMaterial(surface.Index,surface.Owned);
            SourceItemTranslucency.Apply(surface.Owned, surface.Snapshot, translucency);
        }
        actor.Model.Visible = (flags & 0x8001) == 0;
        Shadow.State.Update(actor.BodyFlags, actor.ShadowPosition,
            unchecked((ushort)actor.ShadowScale), ShadowDistance, ShadowThreshold);
        Shadow.Rebuild();
    }
    internal void VerifyGeometry(int? redTintByte = null,int? greenTintByte = null,int? blueTintByte = null)
    {
        foreach (Geometry item in geometry)
        {
            if (item.Instance.Mesh != item.Owned || item.Owned.GetSurfaceCount() != item.Original.GetSurfaceCount())
                throw new InvalidDataException("Actor tint lost source mesh ownership or surfaces.");
            for (int index = 0; index < item.Arrays.Length; index++)
            {
                using var original = item.Original.SurfaceGetArrays(index);
                using var current = item.Owned.SurfaceGetArrays(index);
                if (!original[(int)Mesh.ArrayType.Vertex].AsVector3Array().SequenceEqual(current[(int)Mesh.ArrayType.Vertex].AsVector3Array()) ||
                    !original[(int)Mesh.ArrayType.Bones].AsInt32Array().SequenceEqual(current[(int)Mesh.ArrayType.Bones].AsInt32Array()) ||
                    !original[(int)Mesh.ArrayType.Weights].AsFloat32Array().SequenceEqual(current[(int)Mesh.ArrayType.Weights].AsFloat32Array()))
                    throw new InvalidDataException("Actor tint changed original vertices or exact skin indices/weights.");
                Color[] before = original[(int)Mesh.ArrayType.Color].AsColorArray();
                Color[] after = current[(int)Mesh.ArrayType.Color].AsColorArray();
                if (before.Length != after.Length) throw new InvalidDataException("Actor tint changed the source color count.");
                for (int vertex = 0; vertex < before.Length; vertex++)
                {
                    Color source = before[vertex];
                    Color expected = new(Expected(source.R,redTintByte),Expected(source.G,greenTintByte),Expected(source.B,blueTintByte),source.A);
                    if (after[vertex] != expected) throw new InvalidDataException("Native actor colors differ from independent source byte tint gold.");
                }
            }
        }
        actor.Model.VerifyCurrentPose();
        static float Expected(float original,int? tint) => tint is null ? original : (((int)Math.Round(original*255f)*tint.Value)>>8)/255f;
    }
    internal void VerifyTransparency(bool enabled)
    {
        foreach (Surface surface in surfaces)
            if (!SourceItemTranslucency.IsApplied(surface.Owned, surface.Snapshot, enabled ? SourceItemTranslucency.ModelAlpha : null))
                throw new InvalidDataException("Native actor transparency differs from the source DCModel global face override mode2/alpha0xFF.");
    }
    private float Channel(float original,byte channel)
    {
        int originalByte = Math.Clamp((int)Math.Round(original*255f),0,255);
        return ((originalByte*Tint(channel)) >>8)/255f;
    }
    private int Tint(byte channel)
    {
        float normalized = channel / 255f;
        float powered = (float)Math.Pow(normalized, tintGamma);
        int sourceByte = (int)((double)Math.Min(1f, powered) * 255.0);
        return sourceByte;
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        { yield return child; foreach (Node descendant in Descendants(child)) yield return descendant; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (Surface surface in surfaces)
        {
            if (GodotObject.IsInstanceValid(surface.Mesh)) surface.Mesh.SetSurfaceOverrideMaterial(surface.Index, surface.Original);
            surface.Owned.Dispose();
        }
        surfaces.Clear();
        foreach (Geometry item in geometry)
        {
            if (GodotObject.IsInstanceValid(item.Instance)) item.Instance.Mesh = item.Original;
            foreach (Godot.Collections.Array array in item.Arrays) array.Dispose();
            item.Owned.Dispose();
        }
        geometry.Clear();
        if (GodotObject.IsInstanceValid(Shadow)) Shadow.Free();
    }
}
