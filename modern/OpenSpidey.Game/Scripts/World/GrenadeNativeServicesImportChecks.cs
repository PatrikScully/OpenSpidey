using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class GrenadeNativeServicesImportChecks
{
    private sealed class PlayerBody(GodotPlayer player) : IProjectileBlastBody
    {
        public FixedVector3 Position => player.Controller.Position;
        public IProjectileBlastBody? NextBody => null;
        public void Hit(ActorHit hit) => player.Combat.Hit(hit);
    }

    public static async Task VerifyAsync(Node3D owner, GodotActorRegistry registry, PkrArchive archive,
        PsxLibraryRepository libraries, Func<ImportedAnimatedModel> playerModelFactory,
        IReadOnlyList<ActorPartBox> boxes, short yaw, Func<uint> clock)
    {
        if (registry.GetChildren().OfType<GodotScriptActor>().Any(actor => actor.UpdateGroup is ActorUpdateGroup.Enemy or ActorUpdateGroup.Environment))
            throw new InvalidOperationException("This isolated blast scenario requires genuinely empty enemy/environmental body lists.");
        // Earlier mechanism checks claim O-library prototypes. Use the same
        // independent G-library roof verified by the original node9 sprite ring.
        TriggerActorDefinition original = TriggerActorDefinition.Read(registry.Triggers.Nodes[9]);
        var world = new GodotActorWorldQuery(owner, registry.Triggers);
        FixedVector3 position = new FixedVector3(original.Position.X, original.Position.Y, original.Position.Z) << 12;
        int floor = world.GroundHeight(position, 300, 600);
        if (floor == -1 || world.LastHit?.SourceAsset is not { } asset || !asset.Contains("L1A1_G", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Grenade checks require the real original node9 G-library roof.");
        position = position with { Y = unchecked(floor - (PlayerTerrainController.SurfaceClearance << 12)) };
        var random = new GameRandom(0x12A6CC58);
        var player = GodotPlayer.Create(position, yaw, playerModelFactory(), new(true, new(false, 10, true)), random: random.Next);
        player.Combat.ConfigureCollisionBoxes(boxes);
        player.SampleInput = () => default;
        player.SampleButtons = () => default;
        owner.AddChild(player);
        player.SetPhysicsProcess(false);
        Camera3D? previous = owner.GetViewport().GetCamera3D();
        var camera = new PlayerFollowCamera { Name = "GrenadeCheckCamera", Target = player };
        owner.AddChild(camera);
        var sounds = owner.GetNode<GodotSoundChannels>("SoundChannels");
        var speech = owner.GetNode<GodotSpeechPlayer>("Speech");
        var flash = new GodotScreenFlash { Name = "GrenadeCheckScreenFlash" };
        owner.AddChild(flash);
        var visuals = GodotThugVisualEffects.Create(archive, libraries, sounds.Controller,
            () => new(camera.SourcePosition >> 12), random.Next, clock);
        owner.AddChild(visuals);
        var projectiles = new List<GodotGrenadeProjectile>();
        try
        {
            player.Combat.SourceTimer = clock;
            player.Combat.CameraFlag = () => camera.TerrainObstructed;
            player.Combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
            player.Combat.PositionSound = (sound, point, _) => sounds.Controller.PlayAt(sound, point, new(camera.SourcePosition >> 12));
            player.Combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
            player.Combat.XaStopped = () => { if (player.Combat.SpeechAttachment.CanStop(clock(), speech.Controller.Current)) speech.Controller.Stop(); };
            player.Combat.CameraBehind = camera.PutBehind;
            player.Combat.CameraField12C = camera.SetFocusHook;
            player.Combat.FlashReset = flash.Reset;
            var body = new PlayerBody(player);
            PsxAssetLibrary objects = libraries.Load("L1A1_O");
            var configuration = new GodotGrenadeConfiguration
            {
                World = world,
                ResolveRegion = region => region == 0 ? objects : throw new InvalidDataException("The isolated source object region changed."),
                ObjectFileRegion = () => 0, DefaultGrenadeRegion = () => 0,
                Timer = () => unchecked((int)clock()), TotalTime = () => unchecked((int)clock()),
                LevelId = () => registry.Environment.LevelId, Random = random.Next,
                Glows = visuals.Glows, Explosions = visuals.Explosions,
                ShadowFactory = visuals.CreateProjectileShadow,
                PlayPositionSound = (sound, point, _) => sounds.Controller.PlayAt(sound, point, new(camera.SourcePosition >> 12)),
                AttachProjectile = projectile => projectiles.Insert(0, projectile),
                DetachProjectile = projectile =>
                {
                    if (!projectiles.Remove(projectile)) throw new InvalidOperationException("Source projectile detached more than once.");
                },
                ApplyObjectBlast = (projectile, request) =>
                {
                    IProjectileBlastBody? head = request.List switch
                    {
                        GrenadeDamageList.PrimaryTargets when projectile.State.Targets == GrenadeTargetList.Player => body,
                        GrenadeDamageList.Player => body,
                        GrenadeDamageList.PrimaryTargets or GrenadeDamageList.EnvironmentalBodies => null,
                        _ => throw new InvalidDataException("Unknown source grenade damage list.")
                    };
                    ProjectileBlastDamage.GiveScaledDamageToObjects(request.Position, request.Damage, request.Radius,
                        request.SlideDistance, request.SlideFrames, request.AttackKind, head);
                },
                ApplyEnvironmentBlast = (_, point, damage, radius) =>
                    ProjectileBlastDamage.GiveScaledDamageToEnviro(point, damage, radius, null)
            };
            await GrenadeProjectileImportChecks.VerifyAsync(owner, configuration, player);
            if (projectiles.Count != 0) throw new InvalidDataException("Native projectile ownership retained a deleted grenade.");
        }
        finally
        {
            foreach (GodotGrenadeProjectile projectile in projectiles.ToArray())
                if (GodotObject.IsInstanceValid(projectile)) projectile.Free();
            visuals.Free();
            flash.Free();
            camera.Free();
            player.Free();
            previous?.MakeCurrent();
        }
    }
}
