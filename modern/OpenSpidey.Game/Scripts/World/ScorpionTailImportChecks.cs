using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Private primary-tail transport proof over genuine encounter banks. No boss actor, AI or runtime spool region is registered.</summary>
internal static class ScorpionTailImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, ReadOnlyMemory<byte> originalImage)
    {
        var scope = new Node3D { Name = "SourceScorpionTailProof" }; owner.AddChild(scope);
        var destination = new Node3D { Name = "SourceTailReparent", Position = new(17, 23, -31), Rotation = new(0.4f, -0.7f, 0.2f) }; owner.AddChild(destination);
        var libraries = new PsxLibraryRepository(archive);
        PsxAssetLibrary bank = libraries.Load("Scorpion");
        OriginalScorpionTailData data = OriginalScorpionTailData.Read(originalImage);
        uint checksum = NamedTextureTable.FindFirst(archive.Read("data/scorpion.tex"), ScorpionTailMeshLayout.TextureName);
        DecodedTexture sourceTexture = bank.Textures.First(value => value.Checksum == checksum);
        var resources = new GodotScorpionEncounterResources(archive, libraries, originalImage);
        GodotScorpionEncounterModel? model = null;
        GodotScorpionTailModel? tail = null, replacement = null;
        var owned = new List<Resource>();
        try
        {
            Assert(checksum == 0x0164485E && sourceTexture.Width == 128 && sourceTexture.Height == 128 && data.RenderHooks.Count == 10 &&
                data.RenderHooks[0] == new ActorHook(0, 368, 464, 2) && data.RenderHooks[5] == new ActorHook(0, -1, 0, 0),
                "The primary tail must consume the genuine first new_scorp_tail manifest identity and the original executable's base/tip hook packets.");
            model = GodotScorpionEncounterModel.Create(resources, "Scorpion", new SourceVertexLightingState());
            scope.AddChild(model); owned.AddRange(model.CaptureOwnedResources());
            tail = GodotScorpionTailModel.Create(bank, sourceTexture, data);
            scope.AddChild(tail); owned.AddRange(tail.CaptureOwnedResources());
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L2A2_T.trg"));
            TriggerActorDefinition definition = TriggerActorDefinition.Read(triggers.Nodes[1]);
            var pose = new FixedJointTransform[24];
            var controls = new FixedVector3[4]; var nodes = new FixedVector3[23]; var history = new FixedVector3[128];
            FixedVector3 position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
            ActorAngles angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
            FixedJointTransform basis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
            var visits = new List<ushort>();
            FixedVector3 Read(ActorHook hook)
            {
                visits.Add(hook.PartIndex);
                return ActorHookPosition.Evaluate(position, position >> 12, basis, pose, hook);
            }
            tail.Render(false, nodes, Read);
            Assert(visits.Count == 0 && !tail.Visible && tail.RenderMesh.GetSurfaceCount() == 0,
                "An uninitialized source primary-tail region must issue no hook read,geometry upload or render submission.");

            for (int clip = 0; clip < model.Model.ClipCount; clip++)
            {
                model.Model.SelectClip(clip);
                foreach (int frame in new[] { 0, model.Model.FrameCount - 1 })
                {
                    model.Model.SetSourceAnimationFrame(frame);
                    model.SynchronizeSourcePose(position, angles, 0x480);
                    for (int part = 0; part < pose.Length; part++) pose[part] = model.Model.GetSourcePartPose(part);
                    visits.Clear();
                    ScorpionTailPath.Update(controls, nodes, history, part => Read(new(0, 0, 0, part)));
                    tail.Render(true, nodes, Read);
                    tail.VerifyNativeGeometry();
                    Assert(visits.SequenceEqual(new ushort[] { 17, 18, 19, 20, 21, 22, 23, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0 }) && history[23] == nodes[0],
                        "The genuine pose must drive AI's seven authored part hooks before render's ten base/tip hooks, preserving the node23/history0 alias.");
                }
            }
            Assert(tail.SourceRenderVisits == 60 && ReferenceEquals(tail.SourceBank, bank) && ReferenceEquals(tail.SourceTexture, sourceTexture),
                "All30 genuine Scorpion clips must preserve the same privately owned primary-tail texture generation across repeated native mesh rebuilds.");

            Resource[] before = tail.CaptureOwnedResources();
            Vector3 worldPosition = tail.GlobalPosition;
            tail.Reparent(destination);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            tail.VerifyNativeGeometry();
            Assert(tail.TopLevel && tail.GlobalPosition.DistanceTo(worldPosition) < 0.00001f && before.SequenceEqual(tail.CaptureOwnedResources()) &&
                before.All(GodotObject.IsInstanceValid),
                "Reparenting beneath a translated/rotated parent must preserve the original world tail position and same private native resources.");

            libraries.Unload("Scorpion");
            PsxAssetLibrary freshBank = libraries.Load("Scorpion");
            DecodedTexture freshTexture = freshBank.Textures.First(value => value.Checksum == checksum);
            replacement = GodotScorpionTailModel.Create(freshBank, freshTexture, data);
            scope.AddChild(replacement); owned.AddRange(replacement.CaptureOwnedResources());
            Assert(!ReferenceEquals(bank, freshBank) && !ReferenceEquals(sourceTexture, freshTexture) &&
                ReferenceEquals(tail.SourceBank, bank) && ReferenceEquals(tail.SourceTexture, sourceTexture) &&
                !tail.CaptureOwnedResources().Intersect(replacement.CaptureOwnedResources()).Any(),
                "A real source-bank reload must allocate a distinct native owner, leaving the first owner's captured generation and resources intact until destruction.");
            replacement.Render(true, nodes, Read); replacement.VerifyNativeGeometry();
            tail.Free(); tail = null;
            Assert(before.All(resource => !GodotObject.IsInstanceValid(resource)),
                "Native Predelete after reparenting must immediately release every primary-tail mesh,material and texture resource.");
            replacement.VerifyNativeGeometry();
            GD.Print("Verified private native Scorpion primary tail: original executable hooks, genuine128x128 texture, all30 clips' AI-path/render-hook order,92 signed16 vertices/normals and88 source quads/UVs, shared-node history alias, transformed reparenting, bank reload and deterministic resource cleanup. Runtime S/SW regions,glow renderer and boss AI/Hit remain unregistered.");
        }
        finally
        {
            if (tail is not null && GodotObject.IsInstanceValid(tail)) tail.Free();
            if (replacement is not null && GodotObject.IsInstanceValid(replacement)) replacement.Free();
            if (model is not null && GodotObject.IsInstanceValid(model)) model.Free();
            scope.Free(); destination.Free();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(owned.All(resource => !GodotObject.IsInstanceValid(resource)),
                "The primary-tail proof must release all original model and tail native resources after reload,reparent and disposal.");
        }
    }

    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
