using Godot;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Reparents the real composed web owner while its genuine player-owned swing and protected bits are live.</summary>
internal static class PlayerWebRuntimeLifetimeImportChecks
{
    public static void Verify(GodotPlayerWebRuntime runtime, GodotPlayerCombatRuntime combat)
    {
        var swing = runtime.Swing ?? throw new InvalidDataException("The lifetime proof requires the composed swing owner.");
        var anchor = swing.State.Current ?? throw new InvalidDataException("The lifetime proof requires a real live swing.");
        Node parent = runtime.GetParent() ?? throw new InvalidDataException("The composed web owner must belong to its actual level.");
        var holder = new Node3D { Name = "LiveWebReparentOwner" };
        parent.AddChild(holder);
        var bits = Enum.GetValues<SourceParticleList>().ToDictionary(list => list,
            list => combat.Particles.Identities(list).ToArray());
        var meshes = CaptureMeshes(runtime);
        var sourceMeshes = (runtime.Strands.LineMesh.GetRid(), runtime.Strands.KnotMesh.GetRid(),
            runtime.Embers.RenderMesh.GetRid(), runtime.Projectiles.RenderMesh.GetRid(), runtime.SmokeTrails!.RenderMesh.GetRid());
        int phase = anchor.Phase;
        uint clock = anchor.LastUpdateTick;
        try
        {
            runtime.Reparent(holder);
            VerifyOwner();
            runtime.Reparent(parent);
            VerifyOwner();
            GD.Print("PASS: live composed web runtime reparent preserves the genuine swing, protected source bits, native mesh RIDs and direct source clock.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(runtime) && !ReferenceEquals(runtime.GetParent(), parent)) runtime.Reparent(parent);
            holder.Free();
        }

        void VerifyOwner()
        {
            if (!ReferenceEquals(swing.State.Current, anchor) || anchor.IsDisposed || !anchor.Strand.Alive ||
                anchor.Phase != phase || anchor.LastUpdateTick != clock || !CaptureMeshes(runtime).SequenceEqual(meshes) ||
                sourceMeshes != (runtime.Strands.LineMesh.GetRid(), runtime.Strands.KnotMesh.GetRid(),
                    runtime.Embers.RenderMesh.GetRid(), runtime.Projectiles.RenderMesh.GetRid(), runtime.SmokeTrails!.RenderMesh.GetRid()))
                throw new InvalidDataException("Tree exit destroyed or changed the live web owner's swing, geometry or source timing.");
            foreach ((SourceParticleList list, object[] expected) in bits)
            {
                IReadOnlyList<object> actual = combat.Particles.Identities(list);
                if (actual.Count != expected.Length || actual.Where((identity, index) => !ReferenceEquals(identity, expected[index])).Any())
                    throw new InvalidDataException("Reparenting changed the shared source bit identity or list order.");
            }
        }
    }

    private static IReadOnlyList<(ulong Node, Rid Mesh)> CaptureMeshes(Node root)
    {
        var result = new List<(ulong Node, Rid Mesh)>();
        Visit(root);
        return result;
        void Visit(Node node)
        {
            if (node is MeshInstance3D { Mesh: { } mesh }) result.Add((node.GetInstanceId(), mesh.GetRid()));
            foreach (Node child in node.GetChildren()) Visit(child);
        }
    }
}
