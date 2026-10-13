using Godot;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Runs genuine terrain/model/effect/blast handlers against a fresh isolated native player.</summary>
public static class GrenadeProjectileImportChecks
{
    // The supplied scheduler must register/remove this fixture without independently ticking it.
    public static async Task VerifyAsync(Node3D owner, GodotGrenadeConfiguration genuine, GodotPlayer isolatedPlayer)
    {
        if (isolatedPlayer.Combat.State.Health != 100 || isolatedPlayer.Combat.AcceptedHits != 0)
            throw new ArgumentException("Grenade verification requires a fresh isolated player with genuine combat effects.", nameof(isolatedPlayer));
        int timer = 0, attached = 0, detached = 0;
        var sounds = new List<uint>();
        var damage = new List<GrenadeDamageList>();
        int environmentCalls = 0;
        var configuration = new GodotGrenadeConfiguration
        {
            World = genuine.World, ResolveRegion = genuine.ResolveRegion,
            Timer = () => timer, TotalTime = () => timer, LevelId = () => 0x101,
            ObjectFileRegion = genuine.ObjectFileRegion, DefaultGrenadeRegion = genuine.DefaultGrenadeRegion,
            Random = bound => bound > 0 ? bound / 2 : 0,
            Glows = genuine.Glows, Explosions = genuine.Explosions, ShadowFactory = genuine.ShadowFactory,
            PlayPositionSound = (sound, position, pitch) =>
            { sounds.Add(sound); genuine.PlayPositionSound(sound, position, pitch); },
            ApplyObjectBlast = (projectile, request) =>
            { damage.Add(request.List); genuine.ApplyObjectBlast(projectile, request); },
            ApplyEnvironmentBlast = (projectile, position, amount, radius) =>
            { environmentCalls++; genuine.ApplyEnvironmentBlast(projectile, position, amount, radius); },
            AttachProjectile = projectile => { attached++; genuine.AttachProjectile(projectile); },
            DetachProjectile = projectile => { detached++; genuine.DetachProjectile(projectile); }
        };
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        FixedVector3 playerPosition = isolatedPlayer.Controller.Position;
        int floor = configuration.World.GroundHeight(playerPosition, 300, 600);
        Assert(floor != -1, "The isolated native player must have a real source collision floor.");
        FixedVector3 start = playerPosition with { Y = unchecked(floor - (100 << 12)) };
        int glowCount = genuine.Glows.Count, modelCount = genuine.Explosions.ModelCount;
        int carrierCount = genuine.Explosions.CarrierCount, explosionCount = genuine.Explosions.Count;
        GodotGrenadeProjectile grenade = GodotGrenadeProjectile.Create(start, start + new FixedVector3(100 << 12, 0, 0),
            40, 10, 512, GrenadeTargetList.Player, 0x76921FB1, 0, 0, configuration);
        if (grenade.GetParent() is null) owner.AddChild(grenade);
        try
        {
            Assert(attached == 1 && grenade.State.ModelChecksum == 0x76921FB1 && grenade.State.Type == 63,
                "The original checksum and source player-target bullet type must survive construction.");
            var library = configuration.ResolveRegion(grenade.State.Region);
            var sourceModel = library.Scene.Models[library.FindModel(0x76921FB1)];
            int expectedVertices = sourceModel.Faces.Sum(face => face.IsTriangle ? 3 : 6);
            int actualVertices = Enumerable.Range(0, grenade.RenderMesh!.GetSurfaceCount()).Sum(surface =>
                grenade.RenderMesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length);
            Assert(grenade.SourceAsset == library.AssetPath && grenade.SourceModelIndex == library.FindModel(0x76921FB1) &&
                actualVertices == expectedVertices && Math.Abs(grenade.Basis.X.Length() - .5f) < .0001f,
                "The grenade must render every original model face at the source2048 fixed scale.");
            var glow = grenade.ProtectedGlow!;
            Assert(genuine.Glows.Count == glowCount + 1 && glow.Protected && glow.SkipTriangles &&
                glow.Sections.Count == 8 && glow.FringeCount == 2 && glow.Fringes.All(fringe => fringe.Width == 16) &&
                glow.Fringes.Take(8).All(fringe => fringe.CodeBgr == 0x3A003264) &&
                glow.Fringes.Skip(8).All(fringe => fringe.CodeBgr == 0x3A000000),
                "The protected source glow must retain both graded fringe rows and their actual colours.");
            FixedVector3 velocity = grenade.State.Velocity;
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(grenade.State.Position == start && grenade.State.Velocity == velocity,
                "Rendering must not advance the source bullet clock.");

            // Cross the genuine imported floor within the first velocity length, exercising its source normal.
            grenade.State.Velocity = new(0, 160 << 12, 0);
            timer = 1;
            grenade.Advance();
            Assert(sounds.SequenceEqual(new uint[] { 0x2A }) && grenade.State.Velocity.Y < 0 &&
                Math.Abs((long)grenade.State.Position.Y - (floor - 81920L)) < 4096 && !configuration.World.LineOfSightCheck,
                "A real floor collision must play the original bounce cue, reflect upward and restore LOS mode.");
            timer = 2;
            grenade.Advance();
            Assert(grenade.State.ShadowEnabled && grenade.SourceShadow is { State.Exists: true } &&
                grenade.SourceShadow.State.Corners.All(corner => Math.Abs((long)corner.Y - floor) < 4096),
                "The airborne grenade must project the original protected textured shadow onto the actual floor.");

            // Source AI temporarily adds81920 to Y. This positions its blast exactly on the real player.
            grenade.State.Settled = 1;
            grenade.State.Position = isolatedPlayer.Controller.Position - new FixedVector3(0, 81920, 0);
            grenade.State.StartTime = 0;
            timer = 121;
            grenade.Advance();
            Assert(isolatedPlayer.Combat.State.Health == 90 && isolatedPlayer.Combat.AcceptedHits == 1,
                "The actual player-target blast handler must deliver full centre damage through native Combat.Hit.");
            Assert(damage.SequenceEqual(new[] { GrenadeDamageList.PrimaryTargets, GrenadeDamageList.EnvironmentalBodies }) &&
                environmentCalls == 1 && sounds.Last() == 0x8029,
                "Fuse expiry must preserve source target/environment callback ordering and explosion audio.");
            Assert(genuine.Explosions.ModelCount == modelCount + 4 && genuine.Explosions.CarrierCount == carrierCount + 2 &&
                genuine.Explosions.Count == explosionCount + 2 && genuine.Glows.Count == glowCount + 2,
                "Fuse expiry must emit two actual two-model explosions and its source flash beside the protected glow.");
            Assert(grenade.State.IsDead && grenade.Visible && !grenade.ResourcesDisposed,
                "Source-dead grenades remain visible until owner deletion.");
            grenade.Advance();
            Assert(GodotObject.IsInstanceValid(grenade) && grenade.Visible && !grenade.ResourcesDisposed && detached == 0,
                "The first dead bullet update only schedules deletion.");
            grenade.Advance();
            Assert(!GodotObject.IsInstanceValid(grenade) && detached == 1 && genuine.Glows.Count == glowCount + 1,
                "The second dead update must free native model/shadow resources, remove its protected glow and detach once.");
            GD.Print("Verified original grenade model/scale, real floor bounce/normal/audio/shadow, protected glow, fuse/native player damage, real explosions/flash and two-pass cleanup.");
        }
        finally { if (GodotObject.IsInstanceValid(grenade)) grenade.Free(); }
    }

    private static void Assert(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
}
