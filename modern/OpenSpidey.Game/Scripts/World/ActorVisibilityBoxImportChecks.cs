using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Exact source box handler applied to a genuine production NPC bank, live flags and unchanged posed model.</summary>
internal static class ActorVisibilityBoxImportChecks
{
    public static void Verify(GodotActorRegistry registry, GodotVisibilityBoxHost host, GodotThugActor target)
    {
        GodotThugActor[] order = registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotThugActor>().ToArray();
        IVisibilityBoxItem[] items = host.SourceItems(SourceVisibilityList.Baddy).ToArray();
        IVisibilityBoxItem[] staticItems = host.SourceItems(SourceVisibilityList.StaticEnvironment).ToArray();
        ushort[] staticFlags = staticItems.Select(item => item.ItemFlags).ToArray();
        ushort[] flags = order.Select(actor => actor.ItemFlags).ToArray();
        Transform3D transform = target.Transform;
        Aabb pose = target.Model.PoseBounds;
        int count = registry.BaddyConstruction.State.Count;
        try
        {
            Assert(items.Length == order.Length && items.All(item => item.RegionUsable) &&
                items.Select(item => item.SourcePosition).SequenceEqual(order.Select(actor => actor.Position)),
                "Visibility host retains the actual production BaddyList head order, current positions and exact loaded animation/model bank generations.");
            int index = Array.IndexOf(order, target);
            Execute(false);
            Assert((target.ItemFlags & 1) != 0 && (items[index].ItemFlags & 1) != 0 && !target.Model.Visible &&
                target.Transform == transform && target.Model.PoseBounds == pose && registry.BaddyConstruction.State.Count == count,
                "Source192 hides the same actor flag and native posed model without changing geometry, animation, source identity or base count.");
            Execute(true);
            Assert((target.ItemFlags & 1) == 0 && (items[index].ItemFlags & 1) == 0 &&
                target.Model.Visible == ((target.ItemFlags & 0x8001) == 0) && target.Transform == transform && target.Model.PoseBounds == pose &&
                staticItems.Select(item => item.ItemFlags).SequenceEqual(staticFlags),
                "Source192 restores live Baddy flags/native visibility while leaving canonical static ENV items unchanged.");
            target.Model.VerifyCurrentPose();
            Assert(registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotThugActor>().SequenceEqual(order),
                "Original box visibility never suspends, reparents or reorders allocated source bodies.");
        }
        finally
        {
            for (int index = 0; index < order.Length; index++)
            {
                order[index].ItemFlags = flags[index];
                order[index].Model.Visible = (flags[index] & 0x8001) == 0;
            }
        }
        GD.Print("Verified native canonical192 visibility: genuine production bank generations/source head order, shared actor flag words, persistent posed meshes and unchanged static ENV/count.");

        void Execute(bool visible)
        {
            var arguments = new byte[32];
            BinaryPrimitives.WriteUInt16LittleEndian(arguments, visible ? (ushort)1 : (ushort)0);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(4), 0xA55A);
            FixedVector3 minimum = (target.Position >> 12) - new FixedVector3(1, 1, 1), maximum = minimum + new FixedVector3(2, 2, 2);
            int[] components = [minimum.X, minimum.Y, minimum.Z, maximum.X, maximum.Y, maximum.Z];
            for (int component = 0; component < components.Length; component++)
                BinaryPrimitives.WriteInt32LittleEndian(arguments.AsSpan(6 + component * 4), components[component]);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(30), 255);
            new VisibilityBoxCommandHandler(192, host).Execute(new(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0),
                new(192, 0, arguments));
        }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native actor visibility boxes: {evidence}"); }
}
