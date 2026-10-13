using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Real L1A2 constructor stream and raw inherited commands over existing production native bodies.</summary>
internal static class ActorLinkedBodyImportChecks
{
    public static void VerifyL1A2Constructor(Node3D owner, PkrArchive archive)
    {
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A2_T.trg"));
        TriggerActorDefinition[] census = triggers.Nodes.Where(node => node.TypeCode == 1).Select(TriggerActorDefinition.Read)
            .Where(definition => definition.InitializeOnLoad).ToArray();
        ushort[] expected = [15, 16, 68, 74, 100, 102, 103, 104, 105, 151, 154, 156, 158, 163, 168];
        Assert(census.Select(definition => definition.NodeId).SequenceEqual(expected) &&
            census.Count(definition => definition.ActorType == 402) == 4 && census.Count(definition => definition.ActorType == 203) == 11,
            "Untouched L1A2 marker1 census is15 in source file order: four ENV platforms and eleven controls, with no initial NPC/player constructor.");
        TriggerNode node = triggers.Nodes[154];
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        ushort[] words = Enumerable.Range(0, definition.BehaviorData.Length / 2)
            .Select(index => BinaryPrimitives.ReadUInt16LittleEndian(definition.BehaviorData.Span[(index * 2)..])).ToArray();
        Assert(node.SourceOffset == 0x188A && definition.ActorType == 203 && definition.Links.Count == 0 &&
            definition.Markers.SequenceEqual(new byte[] { 1, 2, 5 }) &&
            words.SequenceEqual(new ushort[] { 0x4105, 0x4511, 100, 0x4512, 100, 0x4104, 0, 0x4280, 8100, 0x4102, 0, 0x4100 }),
            "Actual node154 has no linked targets:100 is each raw CSuper field value;4105 discovers labels and4280 is the authored8100 delay.");
        var registry = new GodotActorRegistry { Name = "L1A2NoPlayerLinkedCommandProof", Triggers = triggers,
            Environment = new(LevelId: 0x102) };
        owner.AddChild(registry); registry.SetPhysicsProcess(false);
        try
        {
            registry.Spawn(node);
            GodotControlActor actor = registry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single();
            Assert(registry.Player is null && actor.SourceUpdates == 0 && registry.SourceUpdates == 0 &&
                actor.ScriptState.Active && actor.ScriptState.WaitTicks == 8100 && !actor.ScriptState.SkipDuringDeath &&
                actor.Runtime.SourcePosition == 0x18BC && actor.Body.ScriptWaitTicks == 8100 &&
                actor.Body.Recorded.ElapsedTicks == 16200 && actor.Body.Recorded.TotalTicks == 0,
                "The genuine no-player203 constructor executes both inherited raw commands then yields exactly before JumpAndYield0, retaining delay8100/field2B0=16200 and death-skip false without an AI frame.");
            Assert(registry.SourceList(ActorUpdateGroup.Enemy).Count == 0 && registry.SourceList(ActorUpdateGroup.Environment).Count == 0 &&
                registry.SourceList(ActorUpdateGroup.Control).Count == 1 && actor.SourceDepth.First == 0 && actor.SourceDepth.Second == 0 &&
                registry.BaddyConstruction.State.Count == 1,
                "Empty authored links do not write the control’s own CSuper slots, create a target, or visit unrelated source world lists.");
            registry.DestroyImmediately(actor);
            Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0 && registry.BaddyConstruction.State.Count == 0,
                "Real logical deletion/Predelete releases the actual203 constructor lease once without adding a control effect owner.");
        }
        finally { registry.Free(); }
        GD.Print("Verified genuine L1A2 no-player node154 constructor: raw4511/4512, empty authored links, exact4280 delay8100/cursor18BC and native203 lifetime.");
    }

    public static void VerifyProductionBodies(GodotActorRegistry registry, GodotThugActor target)
    {
        var native = new GodotLinkedBaddyCommands(registry);
        IActorLinkedBodyTarget[] bodies = native.ActiveBaddies().ToArray();
        GodotScriptActor[] order = registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotScriptActor>().ToArray();
        ushort[] flags = bodies.Select(body => body.ItemFlags).ToArray();
        short[] first = bodies.Select(body => body.SourceDepth.First).ToArray(), second = bodies.Select(body => body.SourceDepth.Second).ToArray();
        int targetIndex = Array.IndexOf(order, target), updates = target.SourceUpdates, count = registry.BaddyConstruction.State.Count;
        Transform3D transform = target.Transform; Aabb bounds = target.Model.PoseBounds;
        Assert(targetIndex >= 0 && bodies.Select(body => body.SourceNodeId).SequenceEqual(order.Select(actor => actor.SourceNodeId)) &&
            bodies.Select(body => body.SourceDepth).SequenceEqual(order.Select(actor => actor.SourceDepth)),
            "Linked-body target views share the actual production newest-first source identities and canonical per-body CSuper storage.");
        try
        {
            // Bounded command data varies only links/words. Every body, flag, model and list owner below is the real production owner.
            var host = new CommandData([target.SourceNodeId, target.SourceNodeId], native);
            Execute(0x4511, 0x8001); Execute(0x4512, 0x212B); Execute(0x450F, 0);
            Assert(target.SourceDepth.First == -32767 && target.SourceDepth.Second == 0x212B &&
                (target.ItemFlags & 1) != 0 && bodies[targetIndex].ItemFlags == target.ItemFlags,
                "Raw high-bit and variable-like words write the real signed100/102 slots without reading script variables;450F aliases the real native flag word.");
            Execute(0x450F, 1);
            Assert(target.ItemFlags == (ushort)(flags[targetIndex] & 0xFFFE) &&
                bodies.Where((_, index) => index != targetIndex).Select(body => body.ItemFlags)
                    .SequenceEqual(flags.Where((_, index) => index != targetIndex)) && host.HeadReads == 8,
                "Each duplicate link restarts the genuine source head and visits only matching active bodies, preserving all other native flags.");
            var depth = new SourcePolygonDepthState(new(300, -30, -17)); depth.BeginRender();
            Assert(depth.ItemScratch(true, target.SourceDepth) == new SourceOrderTablePushback(-32767, 0x212B, -17) &&
                depth.RenderScratch == depth.Globals && registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.SequenceEqual(order) &&
                target.SourceUpdates == updates && registry.BaddyConstruction.State.Count == count && target.Transform == transform && target.Model.PoseBounds == bounds,
                "Rendering reads the same signed body metadata while preserving scene scratch, pose, transform, head order, source clock and constructor count.");
            target.Model.VerifyCurrentPose();

            void Execute(ushort opcode, ushort value)
            {
                byte[] bytes = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
                var cursor = new ActorScriptCursor(bytes);
                Assert(ActorLinkedBodyCommands.TryExecute(opcode, cursor, target.SourceNodeId, host) && cursor.Position == 2,
                    "The inherited native command consumes exactly one raw word through the independently verified Core source leaf.");
            }
        }
        finally
        {
            for (int index = 0; index < bodies.Length; index++)
            {
                bodies[index].ItemFlags = flags[index]; bodies[index].SourceDepth.First = first[index]; bodies[index].SourceDepth.Second = second[index];
            }
        }
        GD.Print("Verified native linked-body metadata: actual production source-head identities/flags, raw4511/4512 signed CSuper storage, duplicate-link scans and unchanged native pose/count/render scratch.");
    }

    private sealed class CommandData(IReadOnlyList<ushort> links, GodotLinkedBaddyCommands native) : IActorLinkedBodyCommandHost
    {
        public int HeadReads { get; private set; }
        public IReadOnlyList<ushort> Links(ushort nodeId) => links;
        public IEnumerable<IActorLinkedBodyTarget> ActiveBaddies() { HeadReads++; return native.ActiveBaddies(); }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native inherited linked bodies: {evidence}"); }
}
