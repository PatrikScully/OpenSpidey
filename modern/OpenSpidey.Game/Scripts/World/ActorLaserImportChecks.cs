using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class ActorLaserImportChecks
{
    public static async Task VerifyQueryPolicyAsync(Node3D owner, TriggerFile triggers, GodotPlatformActor environmentalOwner)
    {
        var fixture = new Node3D { Name = "LaserQueryVerification", TopLevel = true };
        owner.AddChild(fixture);
        fixture.GlobalPosition = new(2000, 0, -2000);
        Node3D terrain = Floor(0x100000);
        terrain.Position = Vector3.Up;
        fixture.AddChild(terrain);
        Node3D environment = Floor(0x500000);
        fixture.AddChild(environment);
        foreach (ImportedCollisionBody body in environment.GetChildren().OfType<ImportedCollisionBody>())
            body.SupportId = environmentalOwner.GetInstanceId();
        try
        {
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            var query = new GodotActorWorldQuery(owner, triggers);
            var from = PsxCoordinates.OriginalWorldPosition(fixture.GlobalPosition + Vector3.Up * 2);
            var to = PsxCoordinates.OriginalWorldPosition(fixture.GlobalPosition - Vector3.Up * 2);
            if (query.Trace(from, to) is not { SupportId: null })
                throw new InvalidDataException("Zoned laser query did not hit the nearer static terrain.");
            query.LineOfSightCheck = true;
            if (query.TraceEnvironmental(from, to)?.SupportId != environmentalOwner.GetInstanceId())
                throw new InvalidDataException("Direct laser environmental traversal recomputed LOS masks or retained static terrain.");
            if (query.Trace(from, to) is not { SupportId: null } || query.TraceEnvironmental(from, to) is not null)
                throw new InvalidDataException("A fresh zoned LOS query failed to update the inherited environmental face masks.");
            GD.Print("Verified native laser query policy: direct environmental traversal skips static terrain and inherits the last zoned masks across LOS flag changes.");
        }
        finally { fixture.QueueFree(); }
    }

    private static Node3D Floor(uint faceMetadata)
    {
        PsxVertex[] vertices = [new(new(-100, 0, -100), 0), new(new(100, 0, -100), 0),
            new(new(-100, 0, 100), 0), new(new(100, 0, 100), 0)];
        var model = new PsxModel(0, ushort.MaxValue, vertices,
            [new(0, [0, 1, 2, 3], 0, null, [], faceMetadata)]) { Normals = [new(new(0, -4096, 0), 0)] };
        return PsxWorldCollisionBuilder.Build(new(0x20006,
            [new(0, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 0, 0)], [model], []));
    }
}
