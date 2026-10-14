using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Owns an encounter body's actual imported model resources across reparenting and constructor failure.</summary>
public partial class GodotScorpionEncounterModel : Node3D
{
    private GodotScorpionEncounterResources resources = null!;
    private SourceVertexLightingState lightingState = null!;
    private readonly List<Resource> owned = [];
    private GodotSourceVertexLighting? lighting;
    private bool disposed;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public IReadOnlyList<ActorPartBox> CollisionBoxes { get; private set; } = [];
    public FixedJointTransform SourceBasis { get; private set; }

    public static GodotScorpionEncounterModel Create(GodotScorpionEncounterResources resources, string modelName,
        SourceVertexLightingState lightingState)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(lightingState);
        var owner = new GodotScorpionEncounterModel { Name = $"Source{modelName}Model", resources = resources, lightingState = lightingState };
        try
        {
            owner.Model = resources.CreateModel(modelName);
            owner.CapturePrivateResources();
            owner.Model.AdvanceAutomatically = false;
            owner.Model.RestoreSourcePlayback(OpenSpidey.Core.Animation.AnimationPlaybackState.Initial);
            owner.AddChild(owner.Model);
            owner.CollisionBoxes = resources.CollisionBoxes(modelName);
            return owner;
        }
        catch { owner.Free(); throw; }
    }

    public void AssignSourceLight(short actorType)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SourceModelLight source = actorType switch
        {
            ScorpionState.ActorType => resources.SourceData.ScorpionLight,
            JonahState.ActorType => resources.SourceData.JonahLight,
            _ => throw new NotSupportedException($"Source encounter light requires actor type{actorType}.")
        };
        lighting = new(Model, source, lightingState);
    }

    public void SynchronizeSourcePose(FixedVector3 position, ActorAngles angles, ushort itemFlags)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SourceBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition(position);
        Transform = transform;
        Model.Visible = (itemFlags & 0x8001) == 0;
    }

    public void SynchronizeSourceLighting(ushort itemFlags) =>
        (lighting ?? throw new InvalidOperationException("The source constructor has not assigned its real boss light.")).Synchronize(SourceBasis, itemFlags);
    internal void VerifyNativePoseAndLighting()
    {
        Model.VerifyCurrentPose();
        lighting?.VerifyUploads();
    }
    internal Resource[] CaptureOwnedResources() => owned.ToArray();

    private void CapturePrivateResources()
    {
        owned.AddRange(Model.CaptureOwnedResources());
        if (owned.Count == 0) throw new InvalidDataException("The actual encounter model has no owned native mesh resources.");
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        if (Model is not null && GodotObject.IsInstanceValid(Model)) Model.Free();
        foreach (Resource resource in owned)
            if (GodotObject.IsInstanceValid(resource)) resource.Dispose();
        owned.Clear();
    }
}
