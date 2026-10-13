using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Original partial player joint packet and explicit combo frame sampling through the native eighteen-part rig.</summary>
public static class PlayerMeleeModelImportChecks
{
    public static void Verify(Node3D owner,Func<ImportedAnimatedModel> modelFactory,OriginalPlayerData data)
    {
        PlayerFightData fight=PlayerFightData.Decode(data.DistanceDefinitions.Span,data.ComboDefinitions.Span);
        ImportedAnimatedModel model=modelFactory();
        int samples=0;
        try
        {
            model.AdvanceAutomatically=false;owner.AddChild(model);
            model.InitializeSourceJoints(data.PoseLinks);
            foreach(ushort animation in fight.Moves.Values.Select(move=>move.Animation).Distinct())
            {
                model.RunClip(animation);
                model.AdvanceTicks(1);
                var playback=model.Playback;
                foreach(int frame in new[]{0,Math.Min(6,model.FrameCount-1),model.FrameCount-1}.Distinct())
                {
                    model.SetSourceAnimationFrame(frame);
                    if(model.Playback!=(playback with{Frame=(short)frame}))
                        throw new InvalidDataException("Source combo frame assignment changed the independent animation fraction/direction/finished state.");
                    model.ApplySourceJointPose(true);
                    for(int part=0;part<18;part++)
                        if(model.GetSourcePartPose(part)!=model.GetSourceAnimationPartPose(part))
                            throw new InvalidDataException($"Original zero-correction player packet changed raw part{part} at clip{animation}/frame{frame}.");
                    model.VerifyCurrentPose();samples++;
                }
            }
            // Source static hooks reach parts14 and17 absent from the twelve-joint packet.
            if(data.Hooks.Hooks[5].PartIndex!=14 || data.Hooks.Hooks[6].PartIndex!=17)
                throw new InvalidDataException("Original hand slide hooks were substituted with a twelve-part remap.");
            GD.Print($"Verified native player melee pose:12original joints across18parts,9source hooks,{samples}combo frame samples, zero native vertex error and independent animation clock preserved.");
        }
        finally{model.Free();}
    }
}
