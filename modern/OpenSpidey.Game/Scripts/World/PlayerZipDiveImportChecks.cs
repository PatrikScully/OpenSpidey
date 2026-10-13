using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Production chord dispatch and real source thug/animated-hook/weak-handle integration.</summary>
public static class PlayerZipDiveImportChecks
{
    public static async Task VerifyRuntimeAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory)
    {
        var viewport = new SubViewport { OwnWorld3D = true, Size = new(640, 480) };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A1_G.psx"); root.AddChild(world);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
            RestartPoint restart = new MissionCheckpoints(triggers).Select("Re_Restart01").Point;
            GodotPlayer player = playerFactory(); root.AddChild(player); player.SetPhysicsProcess(false);
            Assert(player.Controller.Position == new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12,
                "Production chord fixture requires the original first restart.");
            var camera = new PlayerFollowCamera { Target = player, Current = true }; root.AddChild(camera);
            camera.SetPhysicsProcess(false); camera.MakeCurrent();
            var registry = new GodotActorRegistry { Triggers = triggers, Player = player, Difficulty = 2,
                RandomSource = new GameRandom(0x12A6CC58).Next };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            uint clock = 1000;
            var runtime = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry,
                () => world, camera, owner.GetNode<GodotSpeechPlayer>("Speech"), () => clock);
            root.AddChild(runtime); runtime.SetPhysicsProcess(false);
            await Physics(); await Physics();
            for (int i = 0; i < 30 && !player.Controller.IsGrounded; i++) Step();
            Assert(player.Controller.IsGrounded && player.Controller.Action == PlayerAction.Idle, "Original roof settling before ZipDive chord.");
            PlayerButton chord = PlayerButton.Punch | PlayerButton.Web;
            Step(new(PlayerButton.Punch, PlayerButton.Punch));
            Assert(player.Controller.Action == PlayerAction.PunchCombo && player.Melee!.State.Move == 1,
                "Initial punch input must enter the original combo before its web chord.");
            Step(new(chord, PlayerButton.Web));
            Assert(player.Controller.Action == PlayerAction.ZipDive && player.Model.ClipId == 120 &&
                player.Model.Playback.Frame == 0 && player.Combat.State.AnimationFrame == 0,
                "Punch plus the actual source web button must enter genuine120 and reset both frame owners.");
            Assert(runtime.Directory.SourceBodies.Count == 0 && player.Melee!.State.ZipDiveHandle.Target is null,
                "The no-target case uses a genuinely empty native encounter directory.");
            bool sawUngrab = false;
            int preparedClip = -1;
            FixedJointTransform[] preparedPose = [], preparedRaw = [];
            player.Controller.BeforePlayerInput += CapturePreparedPose;
            try
            {
                for (int i = 0; i < 200 && player.Controller.Action != PlayerAction.Idle; i++)
                {
                    Step();
                    if (player.Model.ClipId != preparedClip && (player.ActorState.ItemFlags & 4) == 0)
                    {
                        Assert(Enumerable.Range(0, player.Model.SourcePartCount).All(part =>
                            player.Model.GetPartPose(part) == preparedPose[part] &&
                            player.Model.GetSourceAnimationPartPose(part) == preparedRaw[part]),
                            "Source RunAnim must retain every Ob_AI-prepared raw/render part through the transform-only finalizer until the next source pose phase.");
                    }
                    else player.Model.VerifyCurrentPose();
                    sawUngrab |= player.Model.ClipId == 121;
                }
            }
            finally { player.Controller.BeforePlayerInput -= CapturePreparedPose; }
            Assert(sawUngrab && player.Controller.Action == PlayerAction.Idle && player.Model.ClipId == 0,
                "Absent target must complete120, run genuine ungrab121 and recover idle without stale combo frames.");
            GD.Print("Verified production ZipDive chord: actual terrain input, source120→121→idle, genuine empty encounter and synchronized animation frames.");
            void Step(PlayerButtonInput buttons = default)
            {
                clock += 2; player.AdvanceSourceUpdate(default, 0, 2, buttons);
                runtime._PhysicsProcess(1.0 / 60); runtime._PhysicsProcess(1.0 / 60);
            }
            void CapturePreparedPose(uint _)
            {
                player.Model.VerifyCurrentPose();
                preparedClip = player.Model.ClipId;
                preparedPose = Enumerable.Range(0, player.Model.SourcePartCount).Select(player.Model.GetPartPose).ToArray();
                preparedRaw = Enumerable.Range(0, player.Model.SourcePartCount).Select(player.Model.GetSourceAnimationPartPose).ToArray();
            }
        }
        finally { viewport.Free(); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    public static async Task VerifyFixtureAsync(Node3D owner, PlayerInteractionFixture fixture,
        PkrArchive archive, PsxLibraryRepository repository)
    {
        var sounds = new GodotSoundChannels { Name = "OriginalL1A3ZipDiveSounds", Archive = archive, LevelGroup = SoundCatalog.Find("data/L1A3") };
        fixture.Root.AddChild(sounds);
        sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
        sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/L1A3.sfx"), levelSpecific: true);
        var visuals = GodotThugVisualEffects.Create(archive, repository, sounds.Controller,
            () => new(fixture.Camera.SourcePosition >> 12), fixture.Registry.RandomSource, () => fixture.Clock);
        fixture.Root.AddChild(visuals);
        var registry = new GodotActorRegistry { Name = "OriginalZipDiveGrabActor", Triggers = fixture.Registry.Triggers,
            Player = fixture.Player, Difficulty = fixture.Registry.Difficulty, Environment = fixture.Registry.Environment,
            RandomSource = fixture.Registry.RandomSource, Sounds = sounds.Controller,
            BaddyConstructionState = fixture.Registry.BaddyConstructionState,
            SoundListener = () => new(fixture.Camera.SourcePosition >> 12), SuspensionDistance = uint.MaxValue };
        fixture.Root.AddChild(registry); registry.SetPhysicsProcess(false); fixture.Directory.Bind(registry);
        using var actorDirectory = new GodotActorSessionDirectory(registry, fixture.Directory, new ActorSessionCheckPlatforms());
        var actors = new List<GodotThugActor>();
        var services = new GodotThugNativeServices(visuals, sounds.Controller, () => new(fixture.Camera.SourcePosition >> 12),
            owner.GetNode<GodotSpeechPlayer>("Speech"), fixture.Player,
            () => GodotActorCollisionDirectory.SourceBaddyBodies(fixture.Directory), new GrabInteractions(fixture), actorDirectory);
        GodotThugActor? thug = null;
        try
        {
            int ordinal = registry.BaddyConstruction.State.Count;
            thug = registry.BaddyConstruction.Create(304,
                () => GodotThugActor.Create(registry.Triggers.Nodes[8], registry, new GodotThugConfiguration {
                ModelFactory = visuals.Resources.CreateModel, Player = fixture.Player, Combat = new(),
                SourceBaddyOrder = () => actors.AsEnumerable().Reverse().ToArray(), Effects = services,
                SpitParticles = visuals.SpitParticles, SpriteRings = visuals.SpriteRings,
                AttackClock = () => unchecked((int)fixture.Clock), Timer = () => fixture.Clock,
                Random = registry.RandomSource, IsSourceCulled = _ => false,
                SourceConstructionOrdinal = registry.BaddyConstruction.RequireCurrentOrdinal }));
            Assert(thug.State.SourceConstructionOrdinal == ordinal && thug.State.ActorPhase == unchecked((byte)ordinal),
                "Actual ZipDive target receives the shared source-scene base-constructor phase before any crafted state entry.");
            registry.Adopt(thug); actors.Add(thug);
            if (!fixture.Directory.Contains(thug)) fixture.Directory.RegisterBody(thug);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            Assert(thug.Type == 304 && (thug.Actor.BehaviorFlags & 0x02000000) != 0,
                "Node8 must use original304 model and its actual constructor470F script flag.");
            // Explicitly crafted valid source states, not a claim that L1A3 authored a state17 start.
            thug.State.State = 17; thug.State.Substate = 3;
            uint beforeFlags = thug.Actor.BehaviorFlags; int beforePath = thug.State.PathPointIndex;
            FixedVector3 destination = thug.Position + new FixedVector3(32 << 12, 0, 0);
            Assert((thug.StateFlags.Check(17) & 2) != 0 && !thug.Grab(destination) && thug.State.State == 17 &&
                thug.State.Substate == 3 && thug.State.PathPointIndex == beforePath && thug.Actor.BehaviorFlags == beforeFlags,
                "Crafted source17 must reject native Grab without mutating state, path or body flags.");
            thug.State.State = 2; thug.State.Substate = 0;
            Assert(thug.Grab(destination) && thug.State.State == 20 && thug.State.Substate == 0 &&
                (thug.Actor.BehaviorFlags & 0x40) != 0 && thug.State.PathPointIndex > 0 &&
                thug.Path.GetPoint(thug.State.PathPointIndex) == destination,
                $"Crafted source2 must enter20 through real AddPointToPath; node{thug.Route.CurrentNode}, path{thug.State.PathPointIndex}.");
            PlayerKickHandle originalHandle = fixture.Directory.MakeHandle(thug);
            fixture.Melee.State.ZipDiveHandle = originalHandle;
            fixture.Player.Combat.State.ActionState = (uint)PlayerAction.ZipDiveKick;
            thug.RunAnimation(0);
            FixedVector3? first = null; bool moved = false;
            for (int frame = 0; frame < Math.Min(8, thug.Model.FrameCount); frame++)
            {
                thug.Model.SetSourceAnimationFrame(frame); thug.Model.VerifyCurrentPose();
                FixedVector3 hook = thug.GetDynamicHookPosition(new(0, 0, 0, 8));
                var combat = fixture.Player.Combat.State;
                FixedVector3 expected = hook - combat.UpDirection * 67 + combat.ForwardDirection * 22;
                FixedVector3 output = new(123, 456, 789); short yaw = -1;
                Assert(thug.GrabUpdate(ref output, ref yaw) && combat.Position == expected &&
                    fixture.Player.Controller.Position == expected && output == new FixedVector3(123, 456, 789) &&
                    yaw == fixture.Player.Controller.Orientation.Heading,
                    $"Actual304 source hook8/frame{frame} must place the player with up67/forward22 while preserving output-position asymmetry.");
                if (first is null) first = expected; else moved |= first.Value != expected;
            }
            Assert(moved, "Original thug animation must actually move its hook attachment across tested frames.");
            fixture.Directory.Remove(thug); fixture.Directory.RegisterBody(thug);
            PlayerKickHandle replacement = fixture.Directory.MakeHandle(thug), stale = originalHandle;
            Assert(replacement.Validation != originalHandle.Validation && fixture.Directory.Recover(ref stale) is null &&
                stale.Target is null && stale.Validation == originalHandle.Validation,
                "Re-registered real native identity must reject its earlier generation and retain the stale validation word.");
            fixture.Melee.State.ZipDiveHandle = replacement;
            thug.Lifecycle.Kill();
            for (int i = 0; i < 4; i++) registry._PhysicsProcess(1.0 / 60);
            PlayerKickHandle deleted = replacement;
            Assert(fixture.Directory.Recover(ref deleted) is null && deleted.Target is null && deleted.Validation == replacement.Validation,
                "Actual source two-pass actor deletion must expire typed weak target without clearing validation.");
            // Crafted action entry with a genuinely expired native handle: advance the original120 asset,
            // rather than supplying an invented completion flag to its target-acquisition branch.
            fixture.Melee.PlaySingleAnimation(120, 0, -1);
            fixture.Player.Combat.State.ActionState = (uint)PlayerAction.ZipDive;
            fixture.Melee.State.ZipDiveHandle = replacement;
            fixture.Player.Controller.ApplyCombatState();
            bool expiredUngrab = false;
            for (int i = 0; i < 200 && fixture.Player.Controller.Action != PlayerAction.Idle; i++)
            {
                fixture.Clock += 2; fixture.Player.AdvanceSourceUpdate(default, 0, 2);
                fixture.Player.Model.VerifyCurrentPose(); expiredUngrab |= fixture.Player.Model.ClipId == 121;
            }
            Assert(expiredUngrab && fixture.Player.Controller.Action == PlayerAction.Idle && fixture.Player.Model.ClipId == 0 &&
                fixture.Melee.State.ZipDiveHandle.Target is null && fixture.Melee.State.ZipDiveHandle.Validation == replacement.Validation,
                "Actual120 completion with deleted native target must run121 and recover idle without stale frames or validation loss.");
            // Modern safety correction for the original123 null dereference, using its existing ungrab branch.
            fixture.Melee.PlaySingleAnimation(123, 0, -1);
            fixture.Player.Combat.State.ActionState = (uint)PlayerAction.ZipDive;
            fixture.Melee.State.ZipDiveHandle = replacement;
            fixture.Melee.ZipDive.Update(ushort.MaxValue); fixture.Player.Controller.ApplyCombatState();
            Assert(fixture.Player.Model.ClipId == 121 && fixture.Melee.State.ZipDiveHandle.Target is null &&
                fixture.Melee.State.ZipDiveHandle.Validation == replacement.Validation,
                "Deleted real target while holding123 must safely select genuine121, preserving its validation word.");
            for (int i = 0; i < 200 && fixture.Player.Controller.Action != PlayerAction.Idle; i++)
            {
                fixture.Clock += 2; fixture.Player.AdvanceSourceUpdate(default, 0, 2); fixture.Player.Model.VerifyCurrentPose();
            }
            Assert(fixture.Player.Controller.Action == PlayerAction.Idle && fixture.Player.Model.ClipId == 0,
                "Expired native hold must finish ungrab121 and return idle through actual terrain dispatch.");
            GD.Print("Verified L1A3 ZipDive: crafted source17 reject/source2 Grab path, real304 animated hook8 placement, typed generation and actual lifecycle expiry, safe123→121→idle.");
        }
        finally
        {
            if (thug is not null) fixture.Directory.Remove(thug);
            fixture.Directory.Unbind(registry); registry.Free(); visuals.Free(); sounds.Free();
        }
    }

    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native ZipDive: {evidence}"); }

    private sealed class GrabInteractions(PlayerInteractionFixture fixture) : IGodotThugInteractionHost
    {
        public int DomeCount => 0;
        public bool IgnoreSphereRadius => false;
        public int PlayerWebComboValue => 0;
        private static NotSupportedException Outside(string operation) => new($"Native ZipDive attachment check unexpectedly requested {operation}.");
        public bool GrabUpdate(GodotThugActor actor, ref FixedVector3 position, ref short yaw) => fixture.Melee.ZipDive.GrabUpdate(ref position, ref yaw);
        public void SpawnGrenade(GodotThugActor actor, ThugGrenadeRequest request) => throw Outside("grenade");
        public void NotifyKill(int actorType) => throw Outside("kill quip");
        public IThugTrapWeb? RecoverWeb(GodotThugActor actor, bool tugWeb) => throw Outside("web recovery");
        public void SetPlayerWebCombo(int duration, int amount) => throw Outside("web combo");
        public void ApplyHitToOtherBody(IThugInjuryBody body, ActorHit hit) => throw Outside("other damage");
        public ActorCollisionBody DescribeEnvironmentalContact(GodotActorWorldHit hit) => throw Outside("environment metadata");
        public ActorProcedureState? RecoverOtherActor(object handle) => throw Outside("other actor");
        public ActorBodyMotion? RecoverOtherBody(ActorProcedureState actor) => throw Outside("other body");
        public object? RecoverNativeActor(ActorProcedureState actor) => throw Outside("native actor");
        public void SendMessageToOtherActor(GodotThugActor sender, object targetHandle, int code) => throw Outside("message");
        public void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition) => throw Outside("hostage camera");
        public void SendSignal(GodotThugActor actor) => throw Outside("signal");
        public bool ExecuteScriptCommand(GodotThugActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) => throw Outside($"script{opcode:X4}");
    }
}
