using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class FixedCameraImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, PlayerFollowCamera camera)
    {
        TriggerFile file = TriggerFile.Read(archive.Read("data/L1A2_T.trg"));
        TriggerNode command = file.Nodes[22];
        TriggerInstruction instruction = TriggerCommandReader.Read(command)
            .Single(instruction => instruction.Opcode == (ushort)MissionOpcode.SetFixedCameraPosition);
        new FixedCameraCommandHandler(file, camera.ScriptRig, () => camera.SourcePosition).Execute(new(command, 0), instruction);
        bool observedMovement = false;
        for (int frame = 0; frame < 70 && camera.ScriptRig.FixedPosition.RemainingTicks != 0; frame++)
        {
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (camera.ScriptRig.FixedPosition.RemainingTicks < 64)
            {
                observedMovement = true;
                VerifyPosition(camera);
            }
        }
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!observedMovement || camera.ScriptRig.Mode != 4 || camera.ScriptRig.FixedPosition.RemainingTicks != 0 ||
            camera.ScriptRig.FixedPosition.Value != camera.ScriptRig.FixedPosition.Target)
            throw new InvalidDataException("The original 64-tick fixed-camera command did not complete in Godot.");
        VerifyPosition(camera);
        camera.ScriptRig.SetMode(3);
        GD.Print("Verified Godot fixed camera: original linked story command, 64-tick movement, exact final target and demo-mode return.");
    }

    private static void VerifyPosition(PlayerFollowCamera camera)
    {
        Vector3 expected = PsxCoordinates.FixedWorldPosition(camera.ScriptRig.FixedPosition.Value);
        if (camera.GlobalPosition.DistanceTo(expected) > 0.0001f ||
            Mathf.Abs(camera.GlobalBasis.Determinant() - 1) > 0.01f)
            throw new InvalidDataException("Native fixed camera changed its scripted position or produced an invalid view basis.");
    }
}
