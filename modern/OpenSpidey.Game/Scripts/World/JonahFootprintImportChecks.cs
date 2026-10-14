using Godot;
using System.Buffers.Binary;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Private real-model footprint/quad lifetime proof; no placeholder Jonah actor or AI is registered.</summary>
internal static class JonahFootprintImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, ReadOnlyMemory<byte> originalImage)
    {
        var scope = new Node3D { Name = "SourceJonahFootprintProof" };
        var destination = new Node3D { Name = "SourceJonahFootprintReparentProof" };
        owner.AddChild(scope); owner.AddChild(destination);
        var libraries = new PsxLibraryRepository(archive);
        var resources = new GodotScorpionEncounterResources(archive,libraries,originalImage);
        var decoded = libraries.Load("shell_dc").Textures.Single(t => t.Checksum == GodotActorShadow.ShadowTextureChecksum);
        var captured = new List<Resource>();
        var nativeOwners = new List<GodotScorpionEncounterModel>();
        var shadowOwners = new List<GodotJonahFootprintShadow>();
        int samples = 0;
        try
        {
            foreach (string name in new[] { "jameson", "jjjj" })
            {
                var model = GodotScorpionEncounterModel.Create(resources,name,new SourceVertexLightingState());
                model.AssignSourceLight(JonahState.ActorType);
                scope.AddChild(model); nativeOwners.Add(model);
                captured.AddRange(model.CaptureOwnedResources());
                var shadow = GodotJonahFootprintShadow.Create(decoded);
                model.AddChild(shadow); shadowOwners.Add(shadow);
                var variables = new ActorScriptVariables(new() { ActorType=316,NodeId=2 },new RegisterOnlyWorld());
                Assert(!shadow.HasSourceQuad && shadow.CaptureOwnedResources().Length == 0,
                    "Jonah's source field374 stays unallocated before its first actual pose footprint update.");
                Resource[]? stableResources = null;
                for (int clip = 0; clip < model.Model.ClipCount; clip++)
                {
                    model.Model.SelectClip(clip);
                    foreach (int frame in new[] { 0,model.Model.FrameCount-1 })
                    {
                        model.Model.SetSourceAnimationFrame(frame);
                        foreach (short yaw in new short[] { 0,1024,2390 })
                        {
                            FixedVector3 position = new(-1480*4096+23,-31*4096+47,839*4096+91);
                            model.SynchronizeSourcePose(position,new ActorAngles(0,yaw,0),0x482);
                            model.SynchronizeSourceLighting(0x482);
                            model.VerifyNativePoseAndLighting();
                            short groundY = clip % 2 == 0 ? (short)160 : (short)-300;
                            SetGroundRegister(variables,groundY);
                            shadow.UpdateFromPose(model.Model,position,position>>12,model.SourceBasis,variables);
                            var pose = Enumerable.Range(0,16).Select(model.Model.GetSourcePartPose).ToArray();
                            FixedVector3[] source = JonahFootprintGeometry.FromPose(position,position>>12,model.SourceBasis,groundY,pose);
                            Assert(shadow.IsAlive && shadow.SourceCorners.SequenceEqual(source) && shadow.SourceCorners.All(p => p.Y == groundY<<12) &&
                                shadow.SourceQuadCreations == 1 && shadow.GetChildCount() == 1,
                                "Every real Jameson/Joel clip boundary uses actual part3/6/10/15 poses, signed ground, one native protected quad and its original corner order.");
                            Resource[] current = shadow.CaptureOwnedResources();
                            if (stableResources is null) { stableResources = current; captured.AddRange(current); }
                            Assert(current.Length == 4 && current.All(GodotObject.IsInstanceValid) && current.SequenceEqual(stableResources),
                                "Repeated source footprint updates preserve the same real mesh/material/shader/texture identities.");
                            var meshNode = shadow.GetChild<MeshInstance3D>(0);
                            Assert(meshNode.Mesh is ImmediateMesh && meshNode.Mesh.GetSurfaceCount() == 1,
                                "The source footprint owns one real immediate-mesh surface.");
                            using var arrays = meshNode.Mesh.SurfaceGetArrays(0);
                            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                            int[] order = [0,1,2,1,3,2];
                            Assert(vertices.Length == 6 && vertices.Zip(order).All(pair =>
                                pair.First.IsEqualApprox(new Vector3(source[pair.Second].X,-(float)source[pair.Second].Y,-(float)source[pair.Second].Z)/409600f)),
                                "The actual protected footprint submits the source triangle order and exact original Q12-to-Godot world coordinates.");
                            samples++;
                        }
                    }
                }
                FixedVector3 cachedPosition = new(-1480*4096+23,-31*4096+47,839*4096+91);
                FixedVector3 currentPosition = cachedPosition+new FixedVector3(1000*4096,0,-2000*4096);
                var currentPose = Enumerable.Range(0,16).Select(model.Model.GetSourcePartPose).ToArray();
                SetGroundRegister(variables,160);
                shadow.UpdateFromPose(model.Model,currentPosition,cachedPosition>>12,model.SourceBasis,variables);
                FixedVector3[] cachedCorners = JonahFootprintGeometry.FromPose(currentPosition,cachedPosition>>12,model.SourceBasis,160,currentPose);
                FixedVector3[] rebuiltCorners = JonahFootprintGeometry.FromPose(currentPosition,currentPosition>>12,model.SourceBasis,160,currentPose);
                Assert(shadow.SourceCorners.SequenceEqual(cachedCorners) && !cachedCorners.SequenceEqual(rebuiltCorners),
                    "A body moved after BuildTransform retains the genuine cached MATRIX.t for its source hooks rather than rebuilding the transform at shadow time.");
                SetGroundRegister(variables,-300);
                shadow.UpdateFromPose(model.Model,currentPosition,cachedPosition>>12,model.SourceBasis,variables);
                Assert(shadow.SourceCorners.All(p=>p.Y==-300<<12) && shadow.SourceCorners.Zip(cachedCorners).All(pair=>
                    pair.First.X==pair.Second.X && pair.First.Z==pair.Second.Z),
                    "Writing the actual source script register0/222 immediately changes only the footprint's ground plane, independently of body/reference ground fields.");
                var texture = shadow.CaptureOwnedResources().OfType<ImageTexture>().Single();
                using (Image nativePixels = texture.GetImage())
                    Assert(nativePixels.GetWidth() == 64 && nativePixels.GetHeight() == 64 && nativePixels.GetData().SequenceEqual(decoded.RgbaPixels),
                        "The real footprint texture retains the exact original Shadow2 RGBA bytes rather than a generated substitute.");
                FixedVector3[] before = shadow.SourceCorners.ToArray();
                shadow.Reparent(destination);
                await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
                Assert(shadow.IsAlive && shadow.SourceQuadCreations == 1 && shadow.SourceCorners.SequenceEqual(before) &&
                    shadow.CaptureOwnedResources().SequenceEqual(stableResources!),
                    "Native reparenting preserves the same protected quad, exact source corners and all private GPU resource identities.");
                var cascade = GodotJonahFootprintShadow.Create(decoded);
                model.AddChild(cascade); shadowOwners.Add(cascade);
                SetGroundRegister(variables,160);
                cascade.UpdateFromPose(model.Model,cachedPosition,cachedPosition>>12,model.SourceBasis,variables);
                Resource[] cascadeResources = cascade.CaptureOwnedResources();
                captured.AddRange(cascadeResources);
                model.Free();
                Assert(cascade.ResourcesDisposed && cascadeResources.All(r => !GodotObject.IsInstanceValid(r)),
                    "Deleting a native model parent also releases its child footprint's actual private quad resources through Predelete.");
                Assert(shadow.IsAlive,"A reparented footprint remains owned by its live native node until that source quad owner is deleted.");
                Resource[] shadowResources = shadow.CaptureOwnedResources();
                shadow.Free();
                Assert(shadow.ResourcesDisposed && shadowResources.All(r => !GodotObject.IsInstanceValid(r)),
                    "Actual footprint Predelete releases every private resource, including its explicitly owned shader.");
            }
            Assert(samples == 312,"Both genuine26-clip banks contribute two real frame boundaries and three original orientations.");
            GD.Print("Verified Jonah footprint:312 actual Jameson/Joel pose samples, original joints3/6/10/15, cached BuildTransform versus current-position regression, actual script register0/222 ground writes, exact Shadow2 pixels, one protected quad per owner, stable resource identities, reparenting and complete native destruction. Full Jonah actor integration remains unregistered.");
        }
        finally
        {
            foreach (GodotJonahFootprintShadow shadow in shadowOwners) if (GodotObject.IsInstanceValid(shadow)) shadow.Free();
            foreach (GodotScorpionEncounterModel model in nativeOwners) if (GodotObject.IsInstanceValid(model)) model.Free();
            scope.Free(); destination.Free();
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
            Assert(captured.All(r => !GodotObject.IsInstanceValid(r)),"All genuine model and protected shadow resources are released after the private native proof.");
        }
    }

    private static void SetGroundRegister(ActorScriptVariables variables,short value)
    {
        byte[] operands=new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(operands,0);
        BinaryPrimitives.WriteUInt16LittleEndian(operands.AsSpan(2),unchecked((ushort)value));
        variables.Write(0x2120,new(operands));
    }

    private sealed class RegisterOnlyWorld : IActorScriptVariableWorld
    {
        private static Exception Outside()=>new InvalidOperationException("The private footprint register-write proof has no world-query dependency.");
        public int Difficulty=>throw Outside();
        public int MovieInputPolls=>throw Outside();
        public FixedVector3? PlayerPosition=>throw Outside();
        public bool HasLineOfSight(FixedVector3 from,FixedVector3 to)=>throw Outside();
        public int Random(int range)=>throw Outside();
        public ushort FirstLink(ushort node)=>throw Outside();
        public void SelectModel(uint checksum)=>throw Outside();
    }

    private static void Assert(bool condition,string message)
    { if(!condition) throw new InvalidDataException("Native Jonah footprint: "+message); }
}
