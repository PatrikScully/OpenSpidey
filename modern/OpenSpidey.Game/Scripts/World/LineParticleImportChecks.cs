using Godot;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class LineParticleImportChecks
{
    public static void Run(Node3D owner)
    {
        var shiftedParent = new Node3D { Position = new(37, 81, -22) };
        owner.AddChild(shiftedParent);
        var particles = new GodotThugSpitParticles();
        shiftedParent.AddChild(particles);
        try
        {
            var spit = new ThugSpitParticle(new(409600, 0, 0), new(4096, 0, -4096),
                2, 4096, 0x306030, 0, 0x02000000);
            particles.Emit(spit, 100);
            if (particles.Count != 1 || particles.RenderMesh.GetSurfaceCount() != 0 || particles.GlobalTransform != Transform3D.Identity)
                throw new InvalidDataException("A newly emitted world-space line trail inherited its parent's transform or lost its lifetime.");
            particles.Advance(102);
            if (particles.Count != 1 || particles.RenderMesh.GetSurfaceCount() != 1)
                throw new InvalidDataException("A line particle must survive its exact final source clock and emit a two-point trail.");
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(particles.RenderMesh.GetRid(), 0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Color[] colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            if (vertices.Length != 2 || vertices[0].DistanceTo(new(1.02f, -0.02f, 0.02f)) > 0.000001f ||
                vertices[1].DistanceTo(new(1, 0, 0)) > 0.000001f || colors.Length != 2 ||
                !colors[0].IsEqualApprox(new(48 / 255f, 96 / 255f, 48 / 255f)) || !colors[1].IsEqualApprox(Colors.Black))
                throw new InvalidDataException("Native line trails changed their source head/tail coordinates or endpoint colors.");
            particles.Advance(103);
            if (particles.Count != 0 || particles.RenderMesh.GetSurfaceCount() != 0)
                throw new InvalidDataException("Expired source line particles retained visible geometry.");
            GD.Print("Verified native line particles: source ballistic endpoints, world-space parent independence, vertex colors, exact lifetime boundary and expired geometry removal.");
        }
        finally { shiftedParent.Free(); }
    }
}
