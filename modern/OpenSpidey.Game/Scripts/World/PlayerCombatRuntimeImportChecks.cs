using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Runs actual input through the same combat composition used by the playable scene.</summary>
public static class PlayerCombatRuntimeImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory)
    {
        var viewport = new SubViewport { OwnWorld3D = true, Size = new(640, 480) };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D();
            viewport.AddChild(root);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A1_G.psx");
            root.AddChild(world);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
            RestartPoint restart = new MissionCheckpoints(triggers).Select("Re_Restart01").Point;
            GodotPlayer player = playerFactory();
            root.AddChild(player);
            player.SetPhysicsProcess(false);
            if (player.Controller.Position != new OpenSpidey.Core.Mathematics.FixedVector3(
                restart.Position.X, restart.Position.Y, restart.Position.Z) << 12)
                throw new InvalidDataException("Live combat checks require the genuine first-level restart position.");
            var camera = new PlayerFollowCamera { Target = player, Current = true };
            root.AddChild(camera);
            camera.SetPhysicsProcess(false);
            camera.MakeCurrent();
            var registry = new GodotActorRegistry { Triggers = triggers, Player = player, Difficulty = 2,
                RandomSource = new OpenSpidey.Core.Mathematics.GameRandom(0x12A6CC58).Next };
            root.AddChild(registry);
            registry.SetPhysicsProcess(false);
            uint clock = 1000;
            var runtime = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry,
                () => world, camera, owner.GetNode<GodotSpeechPlayer>("Speech"), () => clock);
            root.AddChild(runtime);
            runtime.SetPhysicsProcess(false);
            PlayerFightData fight = PlayerFightData.Decode(original.DistanceDefinitions.Span, original.ComboDefinitions.Span);
            await Physics();
            await Physics();
            for (int update = 0; update < 30 && !player.Controller.IsGrounded; update++) Step();
            Assert(player.Controller.IsGrounded && player.Controller.Action == PlayerAction.Idle,
                "Original player must settle on the original restart roof before combat input.");
            Assert(ReferenceEquals(player.Melee!.Sense.Quips, runtime.KillQuips.Rules),
                "Live melee and kill notifications must share the same encounter history.");
            VerifyAttack(PlayerButton.Punch, 1);
            VerifyAttack(PlayerButton.Kick, 0);
            player.Model.VerifyCurrentPose();
            runtime.Impacts.Emit(new(0, 0, checked((ushort)player.Model.ClipId)), player.Controller.Position, 0);
            Assert(runtime.Impacts.Rings.Count == 2 && runtime.Impacts.Lines.Count == 6,
                "Live composition must emit genuine impact rings and lines.");
            for (int update = 0; update < 100; update++) Step();
            Assert(runtime.Impacts.Rings.Count == 0 && runtime.Impacts.Lines.Count == 0 && runtime.Rocks.Count == 0 && runtime.Particles.Count == 0,
                "The final source particle phase must expire every impact without depending on rendering.");
            var sounds = owner.GetNode<GodotSoundChannels>("SoundChannels");
            var webs = GodotPlayerWebRuntime.Create(archive, repository, player, runtime, camera,
                () => clock, registry.RandomSource,
                (sound, position, _) => unchecked((int)sounds.Controller.PlayAt(sound, position, new(camera.SourcePosition >> 12))));
            root.AddChild(webs);
            FixedVector3 target = player.Controller.Position + new FixedVector3(0, 400 << 12, 0);
            FixedVector3 hand = player.Melee!.GetPlayerHookPosition(0);
            FixedVector3 muzzle = hand + ((player.Melee.GetPlayerHookPosition(1) - hand) >> 1);
            int webbing = player.Inventory!.State.Webbing;
            Assert(webs.Actions.FireWeb(new(false, 900, target, false, default)) == 0 &&
                webs.Projectiles.Shots.Single().Position == muzzle && player.Inventory.State.Webbing == webbing - 900,
                "Assembled ordinary FireWeb must use the actual posed hands, shared inventory and owned projectile registration.");
            for (int update = 0; update < 180; update++) Step();
            Assert(webs.Projectiles.Count == 0 && runtime.Particles.Count == 0,
                "Ordinary web impact and owned fragments must finish in the real mixed source bit phases.");
            Assert(player.Inventory.SetFireWebbing(), "Native web composition must enter the genuine source fire inventory variant.");
            hand = player.Melee.GetPlayerHookPosition(0);
            muzzle = hand + ((player.Melee.GetPlayerHookPosition(1) - hand) >> 1);
            Assert(webs.Actions.FireWeb(new(false, 900, target, false, default)) == 0 &&
                webs.FlamingProjectiles.Projectiles.Single().Position == muzzle,
                "Assembled fire variant must use its original firebits projectile and the same posed muzzle.");
            for (int update = 0; update < 180; update++) Step();
            Assert(webs.FlamingProjectiles.ProjectileCount == 0 && webs.FlamingProjectiles.SparkCount == 0 &&
                webs.FlamingProjectiles.TrailCount == 0 && runtime.Particles.Count == 0,
                "Assembled fire web must retire every original flat trail and pixel spark without a second local update phase.");
            GD.Print("Verified playable combat composition: actual punch/kick button dispatch, original fight clips, source pose and idle recovery, shared encounter owner and scheduled native impact cleanup.");
            GD.Print("Verified native web composition: actual posed FireWeb muzzle, shared consumption, ordinary/fire original assets, world contacts and complete mixed-phase effect cleanup.");
            PlayerWebManualInputImportChecks.Verify(player, webs, runtime, StepInput);
            CameraCommandModeImportChecks.Verify(root, camera, triggers.Nodes[0]);
            PlayerLookaroundCameraValueImportChecks.Verify(root, player, camera, runtime, webs, archive);
            await PlayerLookaroundCameraImportChecks.VerifyAsync(root, player, camera, runtime, webs, archive.Read("data/bits.psx"));
            CameraTrackingImportChecks.Verify(player, camera);
            await PowerUpImportChecks.VerifyAsync(root, archive, repository, player, runtime, sounds,
                owner.GetNode<GodotSpeechPlayer>("Speech"), () => clock);
            await ContactCommandImportChecks.VerifyAsync(root, archive, repository, player);
            await WaterPostImportChecks.VerifyAsync(root, repository);
            await EnvironmentChunkImportChecks.VerifyAsync(root, archive, player);
            await ClaimedEnvironmentChunkImportChecks.VerifyAsync(root, archive, player);
            await ManipulationOwnershipImportChecks.VerifyAsync(root, archive);
            await MissionActorDeathImportChecks.VerifyAsync(root, archive, sounds, () => new(camera.SourcePosition >> 12));

            void VerifyAttack(PlayerButton button, ushort move)
            {
                Step(new(button, button));
                Assert(player.Controller.Action == PlayerAction.PunchCombo && player.Melee!.State.Move == move &&
                    player.Model.ClipId == fight.Moves[move].Animation,
                    $"Actual {button} input must enter original combo{move} through the terrain controller.");
                for (int update = 0; update < fight.Moves[move].Frames.Count * 4 && player.Controller.Action == PlayerAction.PunchCombo; update++) Step();
                Assert(player.Controller.Action == PlayerAction.Idle && player.Model.ClipId == 0,
                    $"Original {button} frame stream must complete and return the real player to standing.");
            }
            void Step(PlayerButtonInput buttons = default)
                => StepInput(default, buttons);
            void StepInput(PlayerInputSample sample, PlayerButtonInput buttons)
            {
                clock += 2;
                player.AdvanceSourceUpdate(sample, 0, 2, buttons);
                runtime._PhysicsProcess(1.0 / 60);
                runtime._PhysicsProcess(1.0 / 60);
            }
        }
        finally { viewport.Free(); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        static void Assert(bool condition, string evidence)
        {
            if (!condition) throw new InvalidDataException(evidence);
        }
    }
}
