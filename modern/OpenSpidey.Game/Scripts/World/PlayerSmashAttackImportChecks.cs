using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine posed smash feet, original ribbon texture, shared particle phases and recovery ownership in the private player world.</summary>
internal static class PlayerSmashAttackImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PlayerInteractionFixture fixture, byte[] originalBits)
    {
        GodotPlayer player = fixture.Player; GodotPlayerMelee melee = fixture.Melee;
        PlayerCombatState combat = player.Combat.State;
        if (combat.FirstSmokeTrailActive || combat.SecondSmokeTrailActive)
            throw new InvalidDataException("Private smash proof requires no pre-existing player foot-trail pointers.");
        var viewport = new SubViewport { Name = "OriginalSmokeTrailProofViewport", Size = new(320, 240), OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        var group = new Node3D { Name = "OriginalSmokeTrailProofWorld" };
        owner.AddChild(viewport); viewport.AddChild(group);
        var camera = new Camera3D { Name = "OriginalSmokeTrailProofCamera", Current = true, Near = .01f, Far = 10000 };
        group.AddChild(camera);
        var smoke = GodotSmokeTrails.Create(originalBits); group.AddChild(smoke);
        var particles = new SourceParticleScheduler();
        var emitted = new List<object>();
        smoke.OwnerCreated += bit => { emitted.Add(bit.Identity); particles.Attach(SourceParticleList.NonRendered, bit); };
        smoke.SegmentCreated += bit => { emitted.Add(bit.Identity); particles.Attach(SourceParticleList.Linked2EndedLeftover, bit); };
        smoke.BitRemoved += identity => particles.Detach(identity);
        uint clock = fixture.Clock;
        var state = new PlayerSmashAttackState(melee.State);
        var attack = new GodotPlayerSmashAttack(player, state, fixture.Directory, fixture.Session, smoke, () => clock);
        uint oldAction = combat.ActionState, lastAction = combat.LastHitAction, lastClock = combat.LastHitClock;
        ushort collision = combat.CollisionFlags, animation = combat.AnimationId;
        int oldFrame = combat.AnimationFrame, oldDelay = combat.Field8DC;
        short oldSuppression = combat.FieldEA6;
        byte ae4 = combat.FieldAE4, ae5 = combat.FieldAE5, e8c = combat.FieldE8C;
        FixedVector3 velocity = combat.Velocity, normal = combat.CrawlSurfaceNormal;
        FixedVector3 forward = combat.ForwardDirection, up = combat.UpDirection, right = melee.State.RightDirection;
        int effectiveHeading = melee.State.EffectiveHeading;
        bool meleeFinished = melee.State.AnimationFinished;
        PlayerOrientationSnapshot orientation = player.Controller.Orientation.Capture();
        int clip = player.Model.ClipId, frame = player.Model.Playback.Frame;
        byte finished = player.Model.Playback.Finished;
        try
        {
            melee.PlaySingleAnimation(133, 0, -1); melee.ApplyPlayerPose();
            FixedVector3[] firstPositions = [melee.GetPlayerHookPosition(5), melee.GetPlayerHookPosition(6)];
            attack.CreateSmashAttackTrails();
            SmokeTrail first = attack.CurrentTrails[0]!, second = attack.CurrentTrails[1]!;
            Assert(smoke.Count == 2 && smoke.SegmentCount == 8 && combat.FirstSmokeTrailActive && combat.SecondSmokeTrailActive,
                "Actual source584/588 must own two four-segment trails from the posed foot hooks.");
            Assert(first.Points.All(point => point == firstPositions[0]) && second.Points.All(point => point == firstPositions[1]),
                "Ribbon ring buffers must start at actual imported posed hooks5/6.");
            object[] constructorOrder = [first, first.Segments[3], first.Segments[2], first.Segments[1], first.Segments[0],
                second, second.Segments[3], second.Segments[2], second.Segments[1], second.Segments[0]];
            Assert(emitted.SequenceEqual(constructorOrder) && particles.Identities(SourceParticleList.NonRendered).SequenceEqual(new object[] { second, first }) &&
                particles.Identities(SourceParticleList.Linked2EndedLeftover).SequenceEqual(second.Segments.Cast<object>().Concat(first.Segments)),
                "Actual constructors must emit NonRendered owners before reverse child allocation and prepend the real Linked2Ended list.");
            for (int posedFrame = 1; posedFrame <= 4; posedFrame++)
            {
                combat.AnimationFrame = posedFrame; melee.ApplyPlayerPose(); attack.UpdateTrails();
                Assert(first.Points[posedFrame] == melee.GetPlayerHookPosition(5) && second.Points[posedFrame] == melee.GetPlayerHookPosition(6),
                    "Source buildPose must update each foot trail after evaluating its genuine animation joints.");
                particles.Move(++clock);
            }
            Assert(first.Segments[3].End == melee.GetPlayerHookPosition(5) && second.Segments[3].End == melee.GetPlayerHookPosition(6) &&
                first.Segments.All(segment => segment.Frame == 0 && segment.FrameFraction == 0),
                "Actual four-step foot endpoints and ribbon half-frame animation must advance exactly once per shared source bit phase.");
            Vector3[] points = smoke.Segments.SelectMany(segment => new[] { PsxCoordinates.FixedWorldPosition(segment.Start), PsxCoordinates.FixedWorldPosition(segment.End) }).ToArray();
            Vector3 minimum = points.Aggregate((a, b) => new Vector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
            Vector3 maximum = points.Aggregate((a, b) => new Vector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
            Vector3 target = (minimum + maximum) / 2; float distance = Math.Max((maximum - minimum).Length() * 3, 4);
            camera.GlobalPosition = target + new Vector3(distance * .3f, distance * .2f, distance); camera.LookAt(target, Vector3.Up);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            smoke.Rebuild();
            Assert(smoke.SourceFrames.Count == 1 && smoke.SourceFrames[0].OffsetX == -16 && smoke.SourceFrames[0].OffsetY == -7 &&
                smoke.SourceFrames[0].Width == 32 && smoke.SourceFrames[0].Height == 15 && smoke.RenderMesh.GetSurfaceCount() > 0,
                "The genuine ribbon32×15 frame must draw actual visible connected foot quads with its PC width rule.");
            foreach (var pair in smoke.SourceTextures)
            {
                DecodedTexture source = PsxTextureReader.Read(originalBits).Single(texture => texture.Checksum == pair.Key);
                using Image image = pair.Value.GetImage();
                Assert(image.GetWidth() == source.Width && image.GetHeight() == source.Height && image.GetData().SequenceEqual(source.RgbaPixels),
                    "Native smoke must upload the original ribbon texture and palette unchanged.");
            }
            for (int surface = 0; surface < smoke.RenderMesh.GetSurfaceCount(); surface++)
            {
                using var arrays = smoke.RenderMesh.SurfaceGetArrays(surface);
                Assert(arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length == 6 &&
                    smoke.RenderMesh.SurfaceGetMaterial(surface) is StandardMaterial3D { AlbedoTexture: not null, BlendMode: BaseMaterial3D.BlendModeEnum.Add } material &&
                    material.AlbedoColor.A == 128 / 255f,
                    "Each actual linked quad must upload two textured triangles and original blend2/alpha80.");
            }
            attack.FadeSmokeTrail(0);
            Assert(!combat.FirstSmokeTrailActive && combat.SecondSmokeTrailActive && attack.CurrentTrails[0] is null && first.FadeAway == 1 && smoke.Count == 2,
                "Source damage must clear only the selected player trail pointer while its owned particles continue fading.");
            while (!first.IsDead) particles.Move(++clock);
            Assert(!first.IsDisposed && first.Segments.All(segment => particles.Identities(SourceParticleList.Linked2EndedLeftover).Contains(segment)),
                "Dead smoke owner must leave all protected children live until its NonRendered cleanup destructor.");
            particles.RemoveDead();
            Assert(first.IsDisposed && smoke.Count == 1 && smoke.SegmentCount == 4 && particles.Identities(SourceParticleList.Linked2EndedLeftover).SequenceEqual(second.Segments),
                "Source owner cleanup must retire its exact four children without affecting the second trail.");
            combat.ActionState = 0x1000000; combat.Field8DC = 0; combat.LastHitAction = 0x1000000; combat.LastHitClock = clock;
            combat.FieldE8C = 123; attack.Update();
            Assert(combat.ActionState == 4 && combat.AnimationId == 175 && combat.Velocity == default && combat.FieldE8C == 0 &&
                !combat.SecondSmokeTrailActive && second.FadeAway == 1 && attack.CurrentTrails[1] is null,
                "Actual native smash hit recovery must orient upright, play175, clear E8C byte, enter falling and fade both player trail pointers.");
            while (smoke.Count != 0) { particles.Move(++clock); particles.RemoveDead(); }
            Assert(particles.Identities(SourceParticleList.NonRendered).Count == 0 && particles.Identities(SourceParticleList.Linked2EndedLeftover).Count == 0,
                "Final fade must retire all source owner/child identities through their actual cleanup families.");
            GD.Print("Verified genuine native smash trails: posed foot hooks5/6, original ribbon32×15 texture, two real linked strips, exact bit emission/move/protected cleanup and native175 falling recovery.");
        }
        finally
        {
            attack.FadeSmashAttackTrails(); viewport.Free(); particles.Clear();
            combat.ActionState = oldAction; combat.LastHitAction = lastAction; combat.LastHitClock = lastClock; combat.CollisionFlags = collision;
            combat.Field8DC = oldDelay; combat.FieldEA6 = oldSuppression; combat.FieldAE4 = ae4; combat.FieldAE5 = ae5; combat.FieldE8C = e8c;
            combat.Velocity = velocity; combat.CrawlSurfaceNormal = normal;
            combat.ForwardDirection = forward; combat.UpDirection = up;
            player.Model.RunClip(clip); player.Model.SetSourceAnimationFrame(frame); player.Model.SetAnimationFinished(finished);
            combat.AnimationId = animation; combat.AnimationFrame = oldFrame;
            player.Controller.Orientation.Restore(orientation); melee.ApplyPlayerPose();
            melee.State.RightDirection = right; melee.State.EffectiveHeading = effectiveHeading; melee.State.AnimationFinished = meleeFinished;
        }
        Assert(smoke.ResourcesDisposed, "Native smoke teardown must release its mesh, source textures and materials.");
    }

    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native player smash: {evidence}"); }
}
