using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class CameraRotationImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, PlayerFollowCamera camera)
    {
        TriggerFile file = TriggerFile.Read(archive.Read("data/L4A1_T.trg"));
        TriggerNode command = file.Nodes[96];
        TriggerInstruction instruction = TriggerCommandReader.Read(command)
            .Single(instruction => instruction.Opcode == (ushort)MissionOpcode.SetFixedCameraPositionAndRotation);
        new FixedCameraCommandHandler(file, camera.ScriptRig, () => camera.SourcePosition,
            MissionOpcode.SetFixedCameraPositionAndRotation, () => camera.SourceRotation).Execute(new(command, 0), instruction);
        FixedQuaternion initial = camera.ScriptRig.FixedOrientation.Value;
        bool observedRotation = false;
        for (int frame = 0; frame < 54 && camera.ScriptRig.FixedOrientation.RemainingTicks > 0; frame++)
        {
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (camera.ScriptRig.FixedOrientation.RemainingTicks < 48)
            {
                observedRotation |= camera.ScriptRig.FixedOrientation.Value != initial;
                VerifyView(camera);
            }
        }
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!observedRotation || camera.ScriptRig.Mode != 5 || camera.ScriptRig.FixedPosition.RemainingTicks != 0 ||
            camera.ScriptRig.FixedOrientation.RemainingTicks != 0 ||
            camera.ScriptRig.FixedOrientation.Desired != camera.ScriptRig.FixedOrientation.Target)
            throw new InvalidDataException("Original 48-tick camera quaternion instruction did not complete in Godot.");
        VerifyView(camera);
        camera.ScriptRig.SetMode(3);
        GD.Print("Verified Godot camera rotation: original four-word quaternion, timed transition, damped view matrix and demo-mode return.");
    }

    private static void VerifyView(PlayerFollowCamera camera)
    {
        var source = camera.ScriptRig.FixedOrientation.Value.ToMatrix();
        Basis converted = PsxCoordinates.CharacterTransform(source).Basis;
        if (PsxCoordinates.OriginalRotation(converted) != source)
            throw new InvalidDataException("Camera quaternion coordinate conversion failed its exact inverse.");
        Basis expected = converted.Orthonormalized();
        Vector3 position = PsxCoordinates.FixedWorldPosition(camera.ScriptRig.FixedPosition.Value);
        if (camera.GlobalPosition.DistanceTo(position) > 0.0001f || camera.GlobalBasis.X.DistanceTo(expected.X) > 0.00001f ||
            camera.GlobalBasis.Y.DistanceTo(expected.Y) > 0.00001f || camera.GlobalBasis.Z.DistanceTo(expected.Z) > 0.00001f ||
            !float.IsFinite(camera.GlobalBasis.Determinant()) || Mathf.Abs(camera.GlobalBasis.Determinant() - 1) > 0.01f)
            throw new InvalidDataException($"Godot quaternion view mismatch: position error={camera.GlobalPosition.DistanceTo(position)}, " +
                $"basis errors={camera.GlobalBasis.X.DistanceTo(expected.X)}/{camera.GlobalBasis.Y.DistanceTo(expected.Y)}/{camera.GlobalBasis.Z.DistanceTo(expected.Z)}, " +
                $"determinant={camera.GlobalBasis.Determinant()}, expected={expected.Determinant()}, " +
                $"rotation={camera.ScriptRig.FixedOrientation.Value}, remaining={camera.ScriptRig.FixedOrientation.RemainingTicks}.");
    }
}
