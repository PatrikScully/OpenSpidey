using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual player hand model visibility, independent of raw hook/pose ordering.</summary>
public static class PlayerHandPartsImportChecks
{
    public static void Verify(Node3D owner,Func<ImportedAnimatedModel> factory,OriginalPlayerData original)
    {
        ImportedAnimatedModel model=factory();owner.AddChild(model);model.AdvanceAutomatically=false;
        try
        {
            if(model.SourceHiddenParts!=0x840 || model.IsModelPartVisible(6) || model.IsModelPartVisible(11) ||
                !model.IsModelPartVisible(5) || !model.IsModelPartVisible(10))
                throw new InvalidDataException("Native original player constructor must hide model6/11, after pose swaps, to avoid duplicate hands.");
            foreach((int animation,byte variant,uint mask) in new[]{(0,(byte)3,0x840u),(21,(byte)3,0x840u),(50,(byte)0,0x420u)})
            {
                if(original.FistVariants[animation]!=variant)throw new InvalidDataException($"Private original animation{animation} fist lookup differs from captured source variant{variant}.");
                model.SelectClip(animation);
                var raw=Enumerable.Range(0,18).Select(model.GetSourceAnimationPartPose).ToArray();
                var hooks=Enumerable.Range(0,18).Select(model.GetSourcePartPose).ToArray();
                model.SetSourceHiddenParts(PlayerHandParts.Select(model.SourceHiddenParts,variant));
                if(model.SourceHiddenParts!=mask || model.IsModelPartVisible(5)==model.IsModelPartVisible(6) ||
                    model.IsModelPartVisible(10)==model.IsModelPartVisible(11) || model.VisibleModelParts.Count!=16)
                    throw new InvalidDataException($"Animation{animation} does not select exactly sixteen genuine visible player model parts.");
                for(int part=0;part<18;part++)
                    if(model.GetSourceAnimationPartPose(part)!=raw[part] || model.GetSourcePartPose(part)!=hooks[part])
                        throw new InvalidDataException("Visual hand masking changed an original raw animation or hook transform.");
                model.VerifyCurrentPose();
            }
            GD.Print("Verified original player hand visibility: constructor0840, real idle0/run21/jump50 variants, exactly one model per hand pair, sixteen visible meshes and all eighteen raw hooks/poses intact.");
        }
        finally{model.Free();}
    }
}
