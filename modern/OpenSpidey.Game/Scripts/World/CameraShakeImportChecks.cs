using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Native shake composition in the existing isolated imported L1A3 fixture.</summary>
public static class CameraShakeImportChecks
{
    public static void Verify(PlayerInteractionFixture fixture)
    {
        uint time=1000;
        var camera=new PlayerFollowCamera {Name="OriginalSmallCameraShakeCheck",Target=fixture.Player,SourceTotalTime=()=>time};
        fixture.Root.AddChild(camera);camera.SetPhysicsProcess(false);
        try
        {
            FixedVector3 position=fixture.Camera.SourcePosition;
            FixedQuaternion baseline=FixedQuaternion.Identity;
            camera.ScriptRig.SetFixedPositionAndRotation(position,position,baseline,baseline,0);
            camera._PhysicsProcess(1.0/60);camera._PhysicsProcess(1.0/60);
            short baselineHeading=camera.SourceHeading;
            var expected=new CameraShakeState();expected.Shake(default,2);
            camera.Shake(new(int.MinValue,int.MaxValue,123456),2);
            for(int update=0;update<10;update++)
            {
                FixedQuaternion display=expected.Advance(baseline,time);
                camera._PhysicsProcess(1.0/60);
                camera._PhysicsProcess(1.0/60);
                if(camera.ShakeState.Amplitude!=expected.Amplitude || camera.SourceRotation!=display ||
                    camera.SourceViewRotation!=FixedRotation.TransposeRotation(display.ToMatrix()))
                    throw new InvalidDataException($"Native small camera shake source update{update} lost its quaternion, decay cadence or displayed view matrix.");
                if(camera.GlobalPosition!=PsxCoordinates.FixedWorldPosition(position) || camera.ScriptRig.FixedOrientation.Value!=baseline || camera.SourceHeading!=baselineHeading)
                    throw new InvalidDataException("Display shake changed fixed camera placement, unshaken script quaternion or source yaw.");
                time=unchecked(time+2);
            }
            if(camera.ShakeState.Active)throw new InvalidDataException("Native small camera shake did not finish after nine source updates.");
            camera.SourceTotalTime=null;
            try {camera.Shake(default,2);throw new InvalidDataException("Native camera shake accepted a missing source clock.");}
            catch(InvalidOperationException) { }
            GD.Print("Verified native small camera shake: actual imported world, qy/base/qx/qz display, source two-tick cadence, nine-step decay and preserved fixed script/placement/yaw.");
        }
        finally {camera.Free();fixture.Camera.MakeCurrent();}
    }
}
