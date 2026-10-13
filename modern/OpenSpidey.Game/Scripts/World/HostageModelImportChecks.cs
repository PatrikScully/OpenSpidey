using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Independent original weld/part-order geometry and native pose checks for both source hostage model families.</summary>
public static class HostageModelImportChecks
{
    public static void Run(PkrArchive archive,Node3D owner)
    {
        foreach(var asset in new[]{(Name:"hostage",Clips:7),(Name:"hostagef",Clips:14)})
        {
            byte[] bytes=archive.Read($"data/{asset.Name}.psx");
            PsxScene scene=PsxSceneReader.Read(bytes);
            var animations=PsxMatrixAnimationReader.Read(bytes) ?? throw new InvalidDataException("Original hostage matrix poses are missing.");
            if(scene.Models.Count!=15 || animations.Parents.Count!=15 || animations.ClipCount!=asset.Clips)
                throw new InvalidDataException($"Original{asset.Name} part/model/clip counts differ from private source assets.");
            ImportedAnimatedModel model=ImportedAnimatedModel.Build(scene,PsxTextureReader.Read(bytes),animations,playerPartOrder:false);
            try
            {
                model.AdvanceAutomatically=false;owner.AddChild(model);
                var result=EnemyModelImportChecks.VerifyOriginalGeometry(model,scene,animations);
                if(model.HasSourceJoints)throw new InvalidDataException("Hostage factory must not invent a special joint/link packet.");
                GD.Print($"Verified hostage model{asset.Name}:15parts/models,{asset.Clips}clips,{result.Corners}corners,{result.Welds}sourcewelds,{result.Samples}nativepose samples; original rawpart order.");
            }
            finally{model.Free();}
        }
    }
}
