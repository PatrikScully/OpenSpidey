using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Uploads original per-vertex PC lighting to actual imported surface corners, retaining source part and weld colour order.</summary>
public sealed class GodotSourceVertexLighting
{
    private readonly ImportedAnimatedModel model;
    private readonly SourceModelLight light;
    private readonly SourceVertexLightingState state;
    private readonly Func<int, FixedJointTransform> partPose;
    private readonly System.Numerics.Vector3[][] normals;
    private readonly SourceRgb24[]?[] lastColours;
    public int LitPartCount { get; private set; }
    public int VisibleLitPartCount { get; private set; }
    public int SurfaceUpdates { get; private set; }
    public GodotSourceVertexLighting(ImportedAnimatedModel model, SourceModelLight light, SourceVertexLightingState state,
        Func<int, FixedJointTransform>? partPose = null)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        this.light = light ?? throw new ArgumentNullException(nameof(light));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.partPose = partPose ?? model.GetSourcePartPose;
        normals = model.SourceGeometryParts.Select(part => SourceVertexLighting.ReadPlainNormals(part.Model)).ToArray();
        lastColours = new SourceRgb24[model.SourcePartCount][];
    }
    public void Synchronize(FixedJointTransform actorBasis, ushort itemFlags)
    {
        if ((itemFlags & 0x8001) != 0) return;
        state.BeginActor(); LitPartCount = VisibleLitPartCount = 0;
        for (int part = 0; part < model.SourceGeometryParts.Count; part++)
        {
            ImportedAnimatedPartGeometry geometry = model.SourceGeometryParts[part];
            if (!SourceVertexLighting.IsEnabled(geometry.Model, itemFlags))
            {
                if (lastColours[part] is not null)
                { model.RestoreSourceVertexColours(part); lastColours[part] = null; SurfaceUpdates++; }
                continue;
            }
            FixedJointTransform basis = SourceVertexLighting.LightBasis(light, actorBasis, partPose(part));
            SourceRgb24[] colours = SourceVertexLighting.CalculatePart(geometry.Model, normals[part], light, basis, state);
            LitPartCount++;
            // DCModel_RenderModel tests the sub-object mask at477F3D, after its
            // vertex lighting/stitch-colour writes at477770..477793. Hidden hand
            // variants must consume the same shared scratch before submission skips.
            if (!model.IsModelPartVisible(part)) continue;
            VisibleLitPartCount++;
            if (lastColours[part] is { } previous && previous.SequenceEqual(colours)) continue;
            model.ApplySourceVertexColours(part, colours); lastColours[part] = colours; SurfaceUpdates++;
        }
    }
    internal void VerifyUploads()
    {
        for (int part = 0; part < lastColours.Length; part++)
            if (model.IsModelPartVisible(part)) model.VerifySourceVertexColours(part, lastColours[part]);
        model.VerifyCurrentPose();
    }
}
