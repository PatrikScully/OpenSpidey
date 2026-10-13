using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

internal static class FixedCameraTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(FixedCameraTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.fixed-camera.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var position = new FixedCameraPosition();
            position.Set(Vector(test.GetProperty("current")), Vector(test.GetProperty("target")),
                (ushort)test.GetProperty("duration").GetInt32());
            foreach (JsonElement result in test.GetProperty("expected").EnumerateArray())
            {
                position.Advance(test.GetProperty("ticks").GetInt32());
                assert(position.Value == Vector(result) && position.RemainingTicks == result[3].GetInt32(),
                    "Fixed camera position and timer match the compiled recovered setter and movement block.");
            }
        }
        TriggerNode command = CommandNode([1, 2, 3]);
        var file = new TriggerFile([command, new(1, 3, 0, new byte[4]), CameraNode(2, new(-20, 30, 40)),
            CameraNode(3, new(100, 200, 300))]);
        var rig = new ScriptedCameraRig();
        var runtime = new MissionRuntime(file, new MissionRuntimeTests.ActorFactory(),
            [new FixedCameraCommandHandler(file, rig, () => new(100, 200, 300))]);
        runtime.ReceivePulse(0);
        assert(runtime.ExecutePending() == 1 && rig.Enabled && rig.Mode == 4 &&
            rig.FixedPosition.Target == new FixedVector3(-20, 30, 40) << 12 && rig.FixedPosition.RemainingTicks == 3,
            "The fixed camera command chooses the first linked camera, skipping other node kinds in source order.");
        rig.Advance(4);
        FixedVector3 tripod = new(0, 0, 4096);
        assert(rig.DesiredPose(tripod).Position == new FixedVector3(-20, 30, 40) << 12 && rig.DesiredPose(tripod).Focus == tripod,
            "Fixed camera position snaps on the final update and keeps looking at its tracked subject.");
        rig.SetPlayerValue(CameraSurface.Floor, CameraParameter.OffsetX, 77, 0, true, default);
        assert(rig.Profiles.Get(CameraSurface.Floor, CameraParameter.OffsetX) == 77 &&
            rig.Parameter(CameraParameter.OffsetX).Value == 0,
            "Fixed mode stores player profile changes but ignores new camera offsets, as the recovered setters do.");
        rig.SetMode(3);
        assert(rig.Mode == 3 && rig.DesiredPose(tripod).Position != rig.FixedPosition.Value,
            "A later demo-mode command resumes following without retaining the fixed camera position.");
        TriggerNode noCamera = CommandNode([1]);
        new FixedCameraCommandHandler(file, rig, () => throw new Exception("Unexpected camera capture"))
            .Execute(new(noCamera, 1), TriggerCommandReader.Read(noCamera).Single());
        assert(rig.Mode == 3, "A fixed camera command without a camera link consumes its duration and leaves the camera unchanged.");
        TriggerNode anglesCommand = CommandNode([1], MissionOpcode.SetFixedCameraPositionAndRotation, 0);
        FixedQuaternion targetRotation = new(2896, 0, 0, 2896);
        TriggerNode anglesNode = CameraNode(1, new(-30, 10, 50), targetRotation);
        var anglesFile = new TriggerFile([anglesCommand, anglesNode]);
        var anglesHandler = new FixedCameraCommandHandler(anglesFile, rig, () => default,
            MissionOpcode.SetFixedCameraPositionAndRotation, () => FixedQuaternion.Identity);
        anglesHandler.Execute(new(anglesCommand, 0), TriggerCommandReader.Read(anglesCommand).Single());
        rig.Advance(2);
        assert(rig.Mode == 5 && rig.FixedOrientation.Value == targetRotation &&
            rig.FixedOrientation.RemainingTicks == 0 && rig.FixedPosition.Value == new FixedVector3(-30, 10, 50) << 12,
            "A zero-duration quaternion camera command snaps all four rotation words and position on its first update.");
        TriggerNode truncatedAngles = anglesNode with { Data = anglesNode.Data[..^1] };
        try
        {
            new FixedCameraCommandHandler(new([anglesCommand, truncatedAngles]), rig, () => default,
                MissionOpcode.SetFixedCameraPositionAndRotation, () => FixedQuaternion.Identity)
                .Execute(new(anglesCommand, 0), TriggerCommandReader.Read(anglesCommand).Single());
            throw new Exception("Truncated camera quaternion accepted.");
        }
        catch (InvalidDataException)
        {
            assert(rig.FixedOrientation.Value == targetRotation, "Truncated camera quaternions fail before changing the live camera.");
        }
    }

    public static void VerifyOriginal(TriggerFile file, TriggerNode node, TriggerInstruction instruction, Action<bool, string> assert)
    {
        var rig = new ScriptedCameraRig();
        new FixedCameraCommandHandler(file, rig, () => default, (MissionOpcode)instruction.Opcode,
            () => FixedQuaternion.Identity).Execute(new(node, 0), instruction);
        ushort id = MissionRuntime.ReadLinks(node).First(id => file.Nodes[id].TypeCode == 12);
        TriggerPosition source = TriggerPlacement.Read(file.Nodes[id]).Position;
        rig.Advance(65536);
        ushort expectedMode = instruction.Opcode == 185 ? (ushort)4 : (ushort)5;
        assert(rig.Mode == expectedMode && rig.FixedPosition.Value == new FixedVector3(source.X, source.Y, source.Z) << 12,
            "Original fixed-position camera commands resolve their linked camera node and reach its signed Q12 position.");
        if (expectedMode == 5)
        {
            int rotationOffset = TriggerPlacement.Read(file.Nodes[id]).AfterPositionOffset;
            int Word(int wordOffset) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(
                file.Nodes[id].Data.Span[(rotationOffset + wordOffset)..]);
            assert(rig.FixedOrientation.Target == new FixedQuaternion(Word(0), Word(2), Word(4), Word(6)) &&
                rig.FixedOrientation.Desired == rig.FixedOrientation.Target,
                "Original camera quaternions retain all four signed words and reach their scripted rotation.");
        }
    }

    private static FixedVector3 Vector(JsonElement value) => new(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32());

    private static TriggerNode CommandNode(ushort[] links, MissionOpcode opcode = MissionOpcode.SetFixedCameraPosition, ushort duration = 3)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)6); writer.Write((ushort)links.Length);
        foreach (ushort link in links) writer.Write(link);
        while (stream.Position % 4 != 0) writer.Write((byte)0);
        writer.Write(0u); writer.Write((ushort)opcode); writer.Write(duration); writer.Write(ushort.MaxValue);
        return new(0, 6, 0, stream.ToArray());
    }

    private static TriggerNode CameraNode(ushort id, TriggerPosition position, FixedQuaternion? rotation = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)12); writer.Write((ushort)0);
        while ((stream.Position + 102) % 4 != 0) writer.Write((byte)0);
        writer.Write(position.X); writer.Write(position.Y); writer.Write(position.Z);
        if (rotation is { } quaternion)
        {
            writer.Write((short)quaternion.X); writer.Write((short)quaternion.Y);
            writer.Write((short)quaternion.Z); writer.Write((short)quaternion.W);
        }
        return new(id, 12, 102, stream.ToArray());
    }
}
