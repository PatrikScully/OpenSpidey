using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Box commands mutate the same real G155 item, native mesh and collision used by named visibility.</summary>
internal static class VisibilityBoxImportChecks
{
    public static void Verify(GodotEnvironmentVisibility visibility, GodotEnvironmentVisibilityItem item,
        ImportedSceneMesh mesh, IReadOnlyList<ImportedCollisionBody> bodies)
    {
        GodotEnvironmentVisibilityItem[] sourceItems = visibility.SourceItems.ToArray();
        ushort[] savedFlags = sourceItems.Select(item => item.ItemFlags).ToArray();
        uint[] layers = bodies.Select(body => body.CollisionLayer).ToArray();
        Transform3D transform = mesh.Transform;
        var host = new StaticHost(sourceItems);
        var handler = new VisibilityBoxCommandHandler(141, host);
        FixedVector3 centre = item.SourcePosition >> 12;
        try
        {
            Execute(false, true, centre - new FixedVector3(1, 1, 1), centre + new FixedVector3(1, 1, 1));
            Assert(item.RegionUsable && (item.ItemFlags & 1) != 0 && !mesh.Visible && mesh.Transform == transform &&
                bodies.All(body => (body.SourceFlags & 1) != 0 && body.CollisionLayer == 0),
                "Box141 changes the canonical static ENV flag and existing mesh/collision without creating another item identity.");
            Execute(true, true, centre - new FixedVector3(1, 1, 1), centre + new FixedVector3(1, 1, 1));
            Assert((item.ItemFlags & 1) == 0 && mesh.Visible && mesh.Transform == transform &&
                bodies.Select(body => body.CollisionLayer).SequenceEqual(layers),
                "The aligned box command restores the same native static mesh and its original collision layers.");
            Execute(false, false, centre + new FixedVector3(2, -1, -1), centre + new FixedVector3(3, 1, 1));
            Assert((item.ItemFlags & 1) == 0 && mesh.Visible,
                "Actual ENV remains visible when only X is outside: source outside mode skips items whose Y or Z falls inside.");
            Execute(false, false, centre + new FixedVector3(2, 2, 2), centre + new FixedVector3(3, 3, 3));
            Assert((item.ItemFlags & 1) != 0 && !mesh.Visible,
                "Actual ENV hides when its position is outside all three source axis intervals.");
            int dispatches = host.Dispatches;
            handler.Execute(new(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0), new(141, 0,
                new byte[] { 0, 0, 1, 0, 255, 0 }));
            Assert(host.Dispatches == dispatches,
                "The empty141 visibility list used by L1A1 restart performs no source traversal or native mutation.");
        }
        finally
        {
            for (int index = 0; index < sourceItems.Length; index++)
                if (sourceItems[index].ItemFlags != savedFlags[index]) sourceItems[index].SetVisible((savedFlags[index] & 1) == 0);
        }
        Assert(sourceItems.Select(item => item.ItemFlags).SequenceEqual(savedFlags) && mesh.Visible && mesh.Transform == transform,
            "The native box proof restores each original item flag and native identity for the untouched vault script.");
        GD.Print("Verified native141 boxes: canonical G155 flags/position/region, original outside policy, persistent static mesh/collision and empty L1A1 restart semantics.");

        void Execute(bool visible, bool inside, FixedVector3 minimum, FixedVector3 maximum)
        {
            // Source opcode at offset0 means two bytes of dword padding after the two flag words.
            var arguments = new byte[4 + 2 + 24 + 2];
            BinaryPrimitives.WriteUInt16LittleEndian(arguments, visible ? (ushort)1 : (ushort)0);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(2), inside ? (ushort)1 : (ushort)0);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(4), 0xA55A);
            int[] components = [minimum.X, minimum.Y, minimum.Z, maximum.X, maximum.Y, maximum.Z];
            for (int axis = 0; axis < components.Length; axis++)
                BinaryPrimitives.WriteInt32LittleEndian(arguments.AsSpan(6 + axis * 4), components[axis]);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(30), 255);
            handler.Execute(new(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0), new(141, 0, arguments));
        }
    }

    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native visibility boxes: {evidence}"); }

    private sealed class StaticHost(IReadOnlyList<GodotEnvironmentVisibilityItem> items) : IVisibilityBoxHost
    {
        public int Dispatches { get; private set; }
        public IEnumerable<IVisibilityBoxItem> SourceItems(SourceVisibilityList list)
        {
            Dispatches++;
            // This proof runs before a dynamic body is constructed in its isolated actual world.
            return list == SourceVisibilityList.StaticEnvironment ? items : [];
        }
    }
}
