using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Game.Import;

/// <summary>Godot adapter for original pose formats and welded character geometry.</summary>
public partial class ImportedAnimatedModel : Node3D, IPlayerAnimationDriver
{
    private IAnimationPoseSource library = null!;
    private IAnimationPoseClip clip = null!;
    private Skeleton3D? skeleton;
    private Node3D meshContainer = null!;
    private Node3D[] poseParts = [];
    private MeshInstance3D?[] partMeshes = [];
    private Color[][][] sourceBaseColours = [];
    public uint SourceHiddenParts {get;private set;}
    public uint SourceHiddenPartsUpper {get;private set;}
    public IReadOnlyList<int> VisibleModelParts=>Enumerable.Range(0,partMeshes.Length).Where(IsModelPartVisible).ToArray();
    public bool IsModelPartVisible(int part)=>partMeshes[part] is {Visible:true};
    public void SetSourceHiddenParts(uint lower,uint upper=0)
    {
        SourceHiddenParts=lower;SourceHiddenPartsUpper=upper;
        for(int part=0;part<partMeshes.Length;part++)
            if(partMeshes[part] is { } mesh)mesh.Visible=part<32?(lower&(1u<<part))==0:(upper&(1u<<(part-32)))==0;
    }
    private ExactPoseSkin? exactSkin;
    private FixedJointTransform[] pose = [];
    private FixedJointTransform[] sourceAnimationPose = [];
    private FixedJointTransform[] correctedPose = [];
    private ActorPoseLinkPacket? sourceLinks;
    private RawJointPose[] sourceJoints = [];
    private Transform3D[] boneTransforms = [];
    private IReadOnlyList<IReadOnlyList<BoundCharacterVertex>> bindings = null!;
    private bool playerPartOrder;
    private readonly Dictionary<(int ClipId, bool Loop), Aabb> clipBounds = [];
    private int currentFrame = -1;
    private bool currentLoop;
    public int ClipId { get; private set; }
    public int ClipCount => library.ClipCount;
    public int SourcePartCount => pose.Length;
    /// <summary>The original immutable model-bank scene used to create this character, retaining its real loaded generation identity.</summary>
    public PsxScene SourceScene { get; private set; } = null!;
    public IReadOnlyList<ImportedAnimatedPartGeometry> SourceGeometryParts { get; private set; } = [];
    public int FrameCount => clip.FrameCount;
    public int ReadFrameCount(int id)
    {
        if ((uint)id >= library.ClipCount) throw new ArgumentOutOfRangeException(nameof(id));
        return library.DecodeClip(id).FrameCount;
    }
    public bool Playing { get; set; } = true;
    public bool AdvanceAutomatically { get; set; } = true;
    public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
    public int PlaybackSpeed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
    public Aabb PoseBounds { get; private set; }
    public Aabb ClipBounds { get; private set; }
    /// <summary>Current rendered part pose, including the player's geometry order swaps.</summary>
    public FixedJointTransform GetPartPose(int part) => pose[part];
    /// <summary>Original animation part pose for collision and effect hooks, before render-only swaps.</summary>
    public FixedJointTransform GetSourcePartPose(int part)
    {
        int stored = playerPartOrder && pose.Length > 11 ? part switch
        {
            5 => 6, 6 => 5, 10 => 11, 11 => 10, _ => part
        } : part;
        return pose[stored];
    }
    /// <summary>Unmodified Decomp_GetAnimTransform matrix, used by the source posed box collision path.</summary>
    public FixedJointTransform GetSourceAnimationPartPose(int part) => sourceAnimationPose[part];
    /// <summary>M3dUtils_GetPartAngles uses the corrected buffer with flag4; otherwise it samples the current animation without changing rendered/Ob_AI pose storage.</summary>
    public FixedJointTransform ReadSourceEffectPartPose(int part, bool corrected)
    {
        if ((uint)part >= pose.Length) throw new ArgumentOutOfRangeException(nameof(part));
        if (corrected) return GetSourcePartPose(part);
        var sampled = new FixedJointTransform[pose.Length];
        clip.SampleFrame(Playback.Frame, Playback.Mode == AnimationPlaybackMode.Loop, sampled);
        return sampled[part];
    }
    public bool HasSourceJoints => sourceLinks is not null;
    public bool SourcePoseEnabled { get; private set; }
    public void InitializeSourceJoints(ActorPoseLinkPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (sourceLinks is not null) return; // ApplyPose retains the first allocated links and joint corrections.
        if (packet.Links.Count==0 || packet.Links.Any(link => link.Part >= pose.Length))
            throw new NotSupportedException("Native mutable pose requires bounded original part links.");
        sourceLinks = packet;
        sourceJoints = new RawJointPose[packet.Links.Count];
    }
    public RawJointPose GetSourceJoint(int joint) => sourceJoints[joint];
    public void SetSourceJoint(int joint, RawJointPose correction) => sourceJoints[joint] = correction;
    public void ApplySourceJointPose(bool enabled)
    {
        if (enabled && sourceLinks is null) throw new InvalidOperationException("Corrected pose requires allocated source links and joints.");
        SourcePoseEnabled = enabled;
        ApplyFrame(Playback.Frame, Playback.Mode == AnimationPlaybackMode.Loop);
    }
    public void SetAnimationFinished(byte finished) => Playback = Playback with { Finished = finished };
    /// <summary>Source combo frame streams assign mFrame directly; preserve fraction, direction and finish state.</summary>
    public void SetSourceAnimationFrame(int frame)
    {
        if((uint)frame>=clip.FrameCount || frame>short.MaxValue)throw new ArgumentOutOfRangeException(nameof(frame));
        Playback=Playback with {Frame=(short)frame};
        ApplyPlayback();
    }

    int IPlayerAnimationDriver.Speed { get => PlaybackSpeed; set => PlaybackSpeed = value; }
    void IPlayerAnimationDriver.Advance(int ticks) => AdvanceTicks(ticks);
    void IPlayerAnimationDriver.Run(int clip, int from, int to)
    {
        if (playerPartOrder) RunSourceClip(clip, from, to);
        else RunClip(clip, from, to);
    }

    public static ImportedAnimatedModel Build(PsxScene scene, IReadOnlyList<DecodedTexture> textures,
        IAnimationPoseSource animations, bool playerPartOrder)
    {
        IReadOnlyList<IReadOnlyList<BoundCharacterVertex>> vertexBindings = StitchedVertexBindings.Read(scene, animations.Parents.Count);
        var character = new ImportedAnimatedModel
        {
            SourceScene = scene,
            Name = "ImportedAnimatedModel",
            library = animations,
            playerPartOrder = playerPartOrder,
            SourceHiddenParts = playerPartOrder ? PlayerHandParts.InitialHiddenMask : 0,
            pose = new FixedJointTransform[animations.Parents.Count],
            sourceAnimationPose = new FixedJointTransform[animations.Parents.Count],
            correctedPose = new FixedJointTransform[animations.Parents.Count],
            boneTransforms = new Transform3D[animations.Parents.Count],
            bindings = vertexBindings
        };
        try
        {
            character.InitializeGeometry(scene, textures);
            character.SelectClip(0);
            return character;
        }
        catch
        {
            character.Free();
            throw;
        }
    }

    private void InitializeGeometry(PsxScene scene, IReadOnlyList<DecodedTexture> textures)
    {
        if (library is CompressedAnimationLibrary)
        {
            skeleton = new Skeleton3D { Name = "Skeleton" };
            meshContainer = skeleton;
            AddChild(meshContainer);
            for (int part = 0; part < library.Parents.Count; part++)
            {
                skeleton.AddBone($"Part{part:D2}");
                skeleton.SetBoneRest(part, Transform3D.Identity);
            }
            for (int part = 0; part < library.Parents.Count; part++)
            {
                int parent = library.Parents[part];
                if (parent != part) skeleton.SetBoneParent(part, parent);
            }
        }
        else
        {
            meshContainer = new Node3D { Name = "MatrixParts" };
            AddChild(meshContainer);
        }
        poseParts = new Node3D[library.Parents.Count];
        for (int part = 0; part < poseParts.Length; part++)
        {
            poseParts[part] = new Node3D { Name = $"ExactPart{part:D2}" };
            meshContainer.AddChild(poseParts[part]);
        }
        partMeshes=new MeshInstance3D?[library.Parents.Count];
        sourceBaseColours = new Color[library.Parents.Count][][];
        var materials = new PsxMaterialLibrary(textures);
        var sourceGeometry = new ImportedAnimatedPartGeometry[library.Parents.Count];
        for (int part = 0; part < library.Parents.Count; part++)
        {
            var sourceVertices = new List<IReadOnlyList<byte>>();
            sourceBaseColours[part] = [];
            sourceGeometry[part] = new(scene.Models[part], sourceVertices.AsReadOnly());
            if ((scene.Models[part].Flags & 0x20) != 0) continue;
            ArrayMesh? mesh = PsxGeometryBuilder.BuildModel(scene.Models[part], scene.ColorTable, materials, bindings[part], sourceVertices);
            if (mesh is null) continue;
            sourceBaseColours[part] = Enumerable.Range(0, mesh.GetSurfaceCount()).Select(surface =>
            {
                using var arrays = mesh.SurfaceGetArrays(surface);
                return arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            }).ToArray();
            var instance=new MeshInstance3D
            {
                Name = $"Mesh{part:D2}", Mesh = mesh, Skeleton = new NodePath(""), ExtraCullMargin = 10
            };
            partMeshes[part]=instance;
            meshContainer.AddChild(instance);
        }
        SourceGeometryParts = Array.AsReadOnly(sourceGeometry);
        SetSourceHiddenParts(SourceHiddenParts,SourceHiddenPartsUpper);
    }

    public void ApplySourceVertexColours(int part, IReadOnlyList<SourceRgb24> colours)
    {
        if ((uint)part >= partMeshes.Length || colours.Count != SourceGeometryParts[part].Model.Vertices.Count)
            throw new ArgumentException("Source vertex colours must describe this actual imported part.", nameof(colours));
        UploadSourceVertexColours(part, colours);
    }

    public void RestoreSourceVertexColours(int part)
    {
        if ((uint)part >= partMeshes.Length) throw new ArgumentOutOfRangeException(nameof(part));
        UploadSourceVertexColours(part, null);
    }

    private void UploadSourceVertexColours(int part, IReadOnlyList<SourceRgb24>? colours)
    {
        if (partMeshes[part]?.Mesh is not ArrayMesh mesh)
            throw new InvalidDataException("A lit source part has no actual imported ArrayMesh.");
        IReadOnlyList<IReadOnlyList<byte>> sourceVertices = SourceGeometryParts[part].SurfaceVertexIndices;
        if (sourceVertices.Count != mesh.GetSurfaceCount()) throw new InvalidDataException("Source colour metadata differs from emitted surfaces.");
        var arrays = Enumerable.Range(0, mesh.GetSurfaceCount()).Select(mesh.SurfaceGetArrays).ToArray();
        var materials = Enumerable.Range(0, mesh.GetSurfaceCount()).Select(mesh.SurfaceGetMaterial).ToArray();
        var primitives = Enumerable.Range(0, mesh.GetSurfaceCount()).Select(mesh.SurfaceGetPrimitiveType).ToArray();
        try
        {
            for (int surface = 0; surface < arrays.Length; surface++)
            {
                Color[] output = colours is null ? sourceBaseColours[part][surface].ToArray() :
                    arrays[surface][(int)Mesh.ArrayType.Color].AsColorArray();
                if (output.Length != sourceVertices[surface].Count) throw new InvalidDataException("Source corner colours differ from native geometry vertices.");
                if (colours is not null)
                    for (int vertex = 0; vertex < output.Length; vertex++)
                    {
                        SourceRgb24 rgb = colours[sourceVertices[surface][vertex]];
                        output[vertex] = new(rgb.Red / 255f, rgb.Green / 255f, rgb.Blue / 255f, output[vertex].A);
                    }
                arrays[surface][(int)Mesh.ArrayType.Color] = output;
            }
            mesh.ClearSurfaces();
            for (int surface = 0; surface < arrays.Length; surface++)
            { mesh.AddSurfaceFromArrays(primitives[surface], arrays[surface]); mesh.SurfaceSetMaterial(surface, materials[surface]); }
        }
        finally { foreach (var array in arrays) array.Dispose(); }
    }

    internal void VerifySourceVertexColours(int part, IReadOnlyList<SourceRgb24>? colours)
    {
        if (partMeshes[part]?.Mesh is not ArrayMesh mesh) return;
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = mesh.SurfaceGetArrays(surface);
            Color[] actual = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            IReadOnlyList<byte> indices = SourceGeometryParts[part].SurfaceVertexIndices[surface];
            if (actual.Length != indices.Count) throw new InvalidDataException("Native lit corners lost their source vertex identities.");
            for (int vertex = 0; vertex < actual.Length; vertex++)
            {
                Color expected = sourceBaseColours[part][surface][vertex];
                if (colours is not null)
                {
                    SourceRgb24 rgb = colours[indices[vertex]];
                    expected = new(rgb.Red / 255f, rgb.Green / 255f, rgb.Blue / 255f, expected.A);
                }
                if (actual[vertex] != expected) throw new InvalidDataException("Native source lighting changed a corner's original RGB/alpha output.");
            }
        }
    }

    public void SelectClip(int id)
    {
        LoadClip(id, loop: true);
        // Inspection selection explicitly restarts; CycleClip preserves an already selected actor clip.
        Playback = ActorAnimationPlayback.Run(Playback, (ushort)id, (ushort)clip.FrameCount);
        Playback = ActorAnimationPlayback.Cycle(Playback with { Direction = 1 }, (ushort)id, (ushort)clip.FrameCount);
        ApplyPlayback();
    }

    public void RunClip(int id, int from = 0, int to = -1)
    {
        LoadClip(id, loop: false);
        Playback = ActorAnimationPlayback.Run(Playback, (ushort)id, (ushort)clip.FrameCount, from, to);
        ApplyPlayback();
    }

    /// <summary>Original CSuper::RunAnim changes playback only; source ApplyPose/Ob_AI owns when those fields become posed joints and skin.</summary>
    public void RunSourceClip(int id, int from = 0, int to = -1)
    {
        LoadClip(id, loop: false);
        Playback = ActorAnimationPlayback.Run(Playback, (ushort)id, (ushort)clip.FrameCount, from, to);
    }

    public void CycleClip(int id, sbyte direction = 1)
    {
        LoadClip(id, loop: true);
        Playback = ActorAnimationPlayback.Cycle(Playback, (ushort)id, (ushort)clip.FrameCount, direction);
        ApplyPlayback();
    }

    private void LoadClip(int id, bool loop)
    {
        if ((uint)id >= library.ClipCount || id > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(id));
        clip = library.DecodeClip(id);
        if (clip.FrameCount is <= 0 or > ushort.MaxValue)
            throw new InvalidDataException($"Clip {id} has an invalid actor frame count: {clip.FrameCount}.");
        ClipId = id;
        currentFrame = -1;
        if (clipBounds.TryGetValue((id, loop), out Aabb cached))
        {
            ClipBounds = cached;
            return;
        }
        FixedJointTransform[] rendered = pose.ToArray(), raw = sourceAnimationPose.ToArray(), corrected = correctedPose.ToArray();
        Aabb? bounds = null;
        try
        {
            for (int frame = 0; frame < clip.FrameCount; frame++)
            {
                EvaluatePose(frame, loop);
                Aabb extent = ComputePoseBounds();
                bounds = bounds is { } existing ? existing.Merge(extent) : extent;
            }
        }
        finally
        { rendered.CopyTo(pose, 0); raw.CopyTo(sourceAnimationPose, 0); corrected.CopyTo(correctedPose, 0); }
        ClipBounds = bounds ?? new Aabb();
        clipBounds.Add((id, loop), ClipBounds);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Playing && AdvanceAutomatically) AdvanceTicks(1);
    }

    /// <summary>Actor controllers disable automatic advancement and supply their recovered elapsed ticks here.</summary>
    public void AdvanceTicks(int elapsedTicks)
    {
        AdvanceSourceTicks(elapsedTicks);
    }

    /// <returns>The source elapsed field after UpdateFrame, including its original zero-to2 conversion.</returns>
    public int AdvanceSourceTicks(int elapsedTicks)
    {
        var step = ActorAnimationPlayback.Advance(Playback, elapsedTicks);
        Playback = step.State;
        ApplyPlayback();
        return step.ElapsedTicks;
    }

    private void ApplyPlayback()
    {
        if ((uint)Playback.Frame >= clip.FrameCount)
            throw new InvalidDataException($"Clip {ClipId} advanced to frame {Playback.Frame} outside {clip.FrameCount} frames " +
                $"(direction {Playback.Direction}, speed {Playback.Speed}).");
        bool loop = Playback.Mode == AnimationPlaybackMode.Loop;
        if (Playback.Frame != currentFrame || loop != currentLoop) ApplyFrame(Playback.Frame, loop);
    }

    public override void _Ready()
    {
        exactSkin = new ExactPoseSkin(pose.Length);
        foreach (MeshInstance3D mesh in meshContainer.GetChildren().OfType<MeshInstance3D>()) exactSkin.Attach(mesh);
        exactSkin.Apply(boneTransforms);
    }

    public override void _ExitTree()
    {
        exactSkin?.Dispose();
        exactSkin = null;
        RequestReady();
    }

    private void ApplyFrame(int frame, bool loop = true)
    {
        EvaluatePose(frame, loop);
        for (int part = 0; part < pose.Length; part++) boneTransforms[part] = PsxCoordinates.CharacterTransform(pose[part]);
        if (skeleton is not null)
        {
            foreach (ushort part in library.CalculationOrder)
            {
                int parent = library.Parents[part];
                Transform3D local = parent == part ? boneTransforms[part] : boneTransforms[parent].AffineInverse() * boneTransforms[part];
                skeleton.SetBonePose(part, local);
            }
        }
        for (int part = 0; part < poseParts.Length; part++) poseParts[part].Transform = boneTransforms[part];
        exactSkin?.Apply(boneTransforms);
        PoseBounds = ComputePoseBounds();
        currentFrame = frame;
        currentLoop = loop;
    }

    private void EvaluatePose(int frame, bool loop = true)
    {
        clip.SampleFrame(frame, loop, sourceAnimationPose);
        if (SourcePoseEnabled)
        {
            ActorJointPoseBuilder.Apply(sourceAnimationPose, sourceLinks!.Links, sourceJoints, correctedPose,
                clip is MatrixAnimationClip { Interval: > 1 });
            correctedPose.CopyTo(pose, 0);
        }
        else sourceAnimationPose.CopyTo(pose, 0);
        // RenderSuperItem swaps these pairs for the player after evaluating the original hierarchy.
        if (playerPartOrder && pose.Length > 11)
        {
            (pose[5], pose[6]) = (pose[6], pose[5]);
            (pose[10], pose[11]) = (pose[11], pose[10]);
        }
    }

    private Aabb ComputePoseBounds()
    {
        Aabb? bounds = null;
        foreach (IReadOnlyList<BoundCharacterVertex> vertices in bindings)
            foreach (BoundCharacterVertex vertex in vertices)
            {
                System.Numerics.Vector3 point = pose[vertex.PartIndex].TransformPoint(vertex.Position);
                Vector3 converted = PsxCoordinates.CharacterPosition(point);
                bounds = bounds is { } existing ? existing.Expand(converted) : new Aabb(converted, Vector3.Zero);
            }
        return bounds ?? new Aabb();
    }

    public void VerifyAllClips()
    {
        int previousClip = ClipId;
        int frames = 0;
        float largestError = 0;
        bool renderStorage = exactSkin?.HasRenderStorage == true;
        bool[] modes = library is MatrixAnimationLibrary ? [false, true] : [true];
        for (int id = 0; id < ClipCount; id++)
        {
            SelectClip(id);
            foreach (bool loop in modes)
            for (int frame = 0; frame < FrameCount; frame++)
            {
                ApplyFrame(frame, loop);
                var actualTransforms = new Transform3D[pose.Length];
                for (int part = 0; part < pose.Length; part++)
                    actualTransforms[part] = ReadSkinTransform(part, renderStorage);
                foreach (IReadOnlyList<BoundCharacterVertex> vertices in bindings)
                    foreach (BoundCharacterVertex vertex in vertices)
                    {
                        Vector3 local = PsxCoordinates.CharacterPosition(vertex.Position);
                        Vector3 expected = boneTransforms[vertex.PartIndex] * local;
                        Vector3 actual = actualTransforms[vertex.PartIndex] * local;
                        float error = actual.DistanceTo(expected);
                        if (!float.IsFinite(error) || error > 0.00001f)
                            throw new InvalidDataException($"Clip {id}, frame {frame}, part {vertex.PartIndex}: skin pose differs by {error} meters.");
                        largestError = Mathf.Max(largestError, error);
                    }
                frames++;
            }
        }
        SelectClip(previousClip);
        string backend;
        if (renderStorage) backend = "exact skin";
        else backend = "pose nodes (render storage unavailable)";
        GD.Print($"Verified Godot {backend}: {ClipCount} clips, {frames} frame samples; largest vertex transform error={largestError:F6} meters.");
    }

    public void VerifyPlayback()
    {
        AnimationPlaybackState saved = Playback;
        bool savedPlaying = Playing;
        bool savedAutomatic = AdvanceAutomatically;
        int selected = -1;
        for (int id = 0; id < ClipCount; id++)
        {
            IAnimationPoseClip candidate = library.DecodeClip(id);
            if (candidate.FrameCount < 3) continue;
            if (selected < 0) selected = id;
            if (library is not MatrixAnimationLibrary) break;
            if (candidate is MatrixAnimationClip { Interval: > 1 } matrix && (matrix.FrameCount - 1) % matrix.Interval != 0)
            { selected = id; break; }
        }
        try
        {
            Playing = false;
            AdvanceAutomatically = false;
            PlaybackSpeed = 0x10000;
            if (selected < 0)
            {
                RunClip(0);
                VerifyPlaybackPose(0, loop: false);
                AdvanceTicks(2);
                VerifyPlaybackPose(FrameCount - 1, loop: false);
                CycleClip(0);
                VerifyPlaybackPose(FrameCount - 1, loop: true);
                AdvanceTicks(2);
                VerifyPlaybackPose(0, loop: true);
            }
            else
            {
                RunClip(selected);
                VerifyPlaybackPose(0, loop: false);
                AdvanceTicks(1);
                VerifyPlaybackPose(0, loop: false);
                if (Playback.Fraction != 0x8000) throw new InvalidDataException("Normal playback lost its half-frame clock.");
                AdvanceTicks(1);
                VerifyPlaybackPose(1, loop: false);
                AdvanceTicks(FrameCount * 2 + 1);
                VerifyPlaybackPose(FrameCount - 1, loop: false);
                if (!Playback.IsFinished) throw new InvalidDataException("One-shot playback did not finish.");
                CycleClip(selected);
                VerifyPlaybackPose(FrameCount - 1, loop: true);
                AdvanceTicks(2);
                VerifyPlaybackPose(0, loop: true);
                RunClip(selected, -1, 0);
                AdvanceTicks(1);
                VerifyPlaybackPose(FrameCount - 2, loop: false);
                if (Playback.Fraction != 0x8000) throw new InvalidDataException("Reverse playback lost its borrowed fraction.");
                PlaybackSpeed = 0x8000;
                RunClip(selected);
                AdvanceTicks(2);
                VerifyPlaybackPose(0, loop: false);
                AdvanceTicks(2);
                VerifyPlaybackPose(1, loop: false);

                AnimationPlaybackState paused = Playback;
                AdvanceAutomatically = true;
                _PhysicsProcess(1.0 / 60);
                if (Playback != paused) throw new InvalidDataException("Paused model advanced its animation.");
                Playing = true;
                AdvanceAutomatically = false;
                _PhysicsProcess(1.0 / 60);
                if (Playback != paused) throw new InvalidDataException("Actor-owned playback advanced automatically.");
                AdvanceAutomatically = true;
                _PhysicsProcess(1.0 / 60);
                if (Playback.Fraction != 0x4000) throw new InvalidDataException("Automatic playback did not use the recovered clock speed.");
                VerifyPlaybackPose(1, loop: false);
            }
            GD.Print($"Verified Godot actor playback: clip {ClipId}, {FrameCount} frames; one-shot, loop and clock-to-pose sampling.");
        }
        finally
        {
            LoadClip(saved.ClipId, saved.Mode == AnimationPlaybackMode.Loop);
            Playback = saved;
            ApplyPlayback();
            Playing = savedPlaying;
            AdvanceAutomatically = savedAutomatic;
        }
    }

    public void VerifyCurrentPose() => VerifyPlaybackPose(Playback.Frame, Playback.Mode == AnimationPlaybackMode.Loop);

    private void VerifyPlaybackPose(int frame, bool loop)
    {
        if (Playback.Frame != frame || (Playback.Mode == AnimationPlaybackMode.Loop) != loop)
            throw new InvalidDataException($"Clip {ClipId}: playback produced {Playback.Frame}/{Playback.Mode}, expected {frame}/{loop}.");
        var expectedPose = new FixedJointTransform[pose.Length];
        clip.SampleFrame(frame, loop, expectedPose);
        for (int part = 0; part < expectedPose.Length; part++)
            if (GetSourceAnimationPartPose(part) != expectedPose[part])
                throw new InvalidDataException($"Clip {ClipId}, frame {frame}, part {part}: raw animation collision pose differs from source.");
        if (SourcePoseEnabled)
        {
            var expectedCorrected = new FixedJointTransform[pose.Length];
            ActorJointPoseBuilder.Apply(expectedPose, sourceLinks!.Links, sourceJoints, expectedCorrected,
                clip is MatrixAnimationClip { Interval: > 1 });
            expectedPose = expectedCorrected;
        }
        for (int part = 0; part < expectedPose.Length; part++)
            if (GetSourcePartPose(part) != expectedPose[part])
                throw new InvalidDataException($"Clip {ClipId}, frame {frame}, part {part}: source hook pose contains rendering swaps.");
        if (playerPartOrder && expectedPose.Length > 11)
        {
            (expectedPose[5], expectedPose[6]) = (expectedPose[6], expectedPose[5]);
            (expectedPose[10], expectedPose[11]) = (expectedPose[11], expectedPose[10]);
        }
        bool renderStorage = exactSkin?.HasRenderStorage == true;
        for (int part = 0; part < expectedPose.Length; part++)
        {
            Transform3D expected = PsxCoordinates.CharacterTransform(expectedPose[part]);
            Transform3D actual = ReadSkinTransform(part, renderStorage);
            float error = Math.Max(Math.Max(actual.Origin.DistanceTo(expected.Origin), actual.Basis.X.DistanceTo(expected.Basis.X)),
                Math.Max(actual.Basis.Y.DistanceTo(expected.Basis.Y), actual.Basis.Z.DistanceTo(expected.Basis.Z)));
            if (!float.IsFinite(error) || error > 0.000001f)
                throw new InvalidDataException($"Clip {ClipId}, frame {frame}, part {part}: clock-selected skin pose differs from the source sample.");
        }
    }

    /// <summary>Checks corrected source hook matrices and actual native skin storage against independent gold matrices.</summary>
    public void VerifySourcePose(ReadOnlySpan<FixedJointTransform> expectedSourcePose)
    {
        if (expectedSourcePose.Length != pose.Length) throw new ArgumentException("Gold pose does not contain every original part.", nameof(expectedSourcePose));
        bool renderStorage = exactSkin?.HasRenderStorage == true;
        for (int part = 0; part < pose.Length; part++)
        {
            if (GetSourcePartPose(part) != expectedSourcePose[part])
                throw new InvalidDataException($"Part {part}: source corrected hook pose differs from original PC gold.");
            int stored = playerPartOrder && pose.Length > 11 ? part switch
            { 5 => 6, 6 => 5, 10 => 11, 11 => 10, _ => part } : part;
            Transform3D expected = PsxCoordinates.CharacterTransform(expectedSourcePose[part]);
            Transform3D actual = ReadSkinTransform(stored, renderStorage);
            float error = Math.Max(Math.Max(actual.Origin.DistanceTo(expected.Origin), actual.Basis.X.DistanceTo(expected.Basis.X)),
                Math.Max(actual.Basis.Y.DistanceTo(expected.Basis.Y), actual.Basis.Z.DistanceTo(expected.Basis.Z)));
            if (!float.IsFinite(error) || error > .000001f)
                throw new InvalidDataException($"Part {part}: native corrected skin matrix differs from original PC gold.");
        }
    }

    private Transform3D ReadSkinTransform(int part, bool renderStorage)
    {
        if (renderStorage) return exactSkin!.ReadTransform(part);
        return poseParts[part].Transform;
    }

}
