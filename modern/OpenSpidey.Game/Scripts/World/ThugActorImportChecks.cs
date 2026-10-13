using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Runs the live actor adapter in the imported L1A1 world with an isolated, genuinely wired player/effect configuration.</summary>
public static class ThugActorImportChecks
{
    public static async Task VerifyAsync(Node3D owner, GodotActorRegistry registry,
        GodotThugConfiguration configuration, IReadOnlyList<ActorPartBox> playerBoxes, Action<GodotThugActor>? actorCreated = null)
    {
        if (registry.Environment.LevelId != 0x101 || configuration.Player.Combat.State.Health != 100 || playerBoxes.Count != 18)
            throw new InvalidOperationException("Live thug checks require original L1A1 geometry and an isolated health100 player with all eighteen source boxes.");
        var fixture = new Node3D { Name = "LiveThugVerification", TopLevel = true };
        owner.AddChild(fixture);
        fixture.GlobalTransform = Transform3D.Identity;
        var actors = new List<GodotThugActor>();
        uint sourceClock = configuration.Timer();
        var checkConfiguration = new GodotThugConfiguration
        {
            ModelFactory = configuration.ModelFactory, Player = configuration.Player,
            Combat = new(), SourceBaddyOrder = () => actors.AsEnumerable().Reverse().ToArray(), Effects = configuration.Effects,
            SpitParticles = configuration.SpitParticles, SpriteRings = configuration.SpriteRings,
            AttackClock = () => unchecked((int)sourceClock), Timer = () => sourceClock, Random = configuration.Random,
            SourceConstructionOrdinal = configuration.SourceConstructionOrdinal,
            IsSourceCulled = configuration.IsSourceCulled
        };
        GodotPlayerStateRestore visibility = new(configuration.Player.ActorState.InvisibilityFlag,
            configuration.Player.Combat.HiddenParts);
        int assertions = 0;
        try
        {
            configuration.Player.ActorState.InvisibilityFlag = 1;
            foreach (ushort node in new ushort[] { 9, 10, 16, 19 })
            {
                short type = OpenSpidey.Assets.Scripting.TriggerActorDefinition.Read(registry.Triggers.Nodes[node]).ActorType;
                int ordinal = registry.BaddyConstruction.State.Count;
                GodotThugActor actor = registry.BaddyConstruction.Create(type,
                    () => GodotThugActor.Create(registry.Triggers.Nodes[node], registry, checkConfiguration));
                Assert(actor.State.SourceConstructionOrdinal == ordinal && actor.State.ActorPhase == unchecked((byte)ordinal),
                    $"Original node{node} receives the shared base-constructor phase before its source AI begins.");
                actors.Add(actor);
                fixture.AddChild(actor);
                actor.Begin();
                actorCreated?.Invoke(actor);
                Assert(actor.Type == 312 && actor.State.Health == 90 && actor.AttackDamage == 10,
                    $"Original node{node} initializes actual henchman, health90 and damage10 from its source script.");
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            int[] firstWaypoints = [-1, -1, -1, -1];
            FixedVector3[] initial = actors.Select(actor => actor.Position).ToArray();
            bool[] moved = new bool[actors.Count];
            for (int update = 0; update < 180; update++)
            {
                await SourceFrames();
                sourceClock = unchecked(sourceClock + 2);
                for (int index = actors.Count - 1; index >= 0; index--)
                {
                    GodotThugActor actor = actors[index];
                    actor.Advance();
                    if (firstWaypoints[index] == -1 && actor.State.State != 0) firstWaypoints[index] = actor.Route.CurrentNode;
                    moved[index] |= actor.Position.X != initial[index].X || actor.Position.Z != initial[index].Z;
                    actor.Model.VerifyCurrentPose();
                    Assert(actor.FrameTiming.LastUpdateTick == sourceClock && actor.FrameTiming.ElapsedTicks == 2 &&
                        actor.FrameTiming.AccumulatedTicks == (update + 1) * 2 && (actor.State.BodyFlags & 4) == 0,
                        "Each source head-order iteration shares one controlled clock increment and consumes first-update flag4 exactly once.");
                }
            }
            Assert(firstWaypoints.SequenceEqual(new[] { 7, 2, 15, 21 }), "All four actors take their independently recorded original first waypoints.");
            Assert(moved.All(value => value), "Actual source AI and procedures move every original henchman along its imported patrol.");
            GodotThugActor attacker = actors[0];
            FixedVector3 savedPosition = attacker.Position;
            attacker.BuildTransform();
            FixedVector3 cachedTranslation = attacker.SourceTransformTranslation;
            Vector3 cachedOrigin = attacker.Transform.Origin;
            FixedVector3 hook = attacker.GetActorHookPosition(new(0, 0, 0, 0));
            attacker.Position += new FixedVector3(65536, 0, 0);
            Assert(attacker.SourceTransformTranslation == cachedTranslation && attacker.Transform.Origin == cachedOrigin,
                "Procedure position changes preserve the pre-message cached source/native transform.");
            Assert(attacker.GetActorHookPosition(new(0, 0, 0, 0)) == hook + new FixedVector3(61440, 0, 0),
                "Hooks combine live position with the cached source translation, preserving original fifteen-sixteenths displacement.");
            attacker.Position = savedPosition;
            attacker.Neutralize();
            attacker.Procedures.Execute(2);
            attacker.RunAnimation(0);
            configuration.Player.ActorState.InvisibilityFlag = 0;
            configuration.Player.Combat.HiddenParts = ~(1u << 5);
            ActorPartBox box = playerBoxes[5];
            var playerPose = new FixedJointTransform[18];
            for (int index = 0; index < playerPose.Length; index++) playerPose[index] = configuration.Player.Model.GetSourceAnimationPartPose(index);
            FixedVector3 Point(int x) => ActorHookPosition.Evaluate(configuration.Player.Controller.Position,
                configuration.Player.Controller.Position >> 12, configuration.Player.Controller.Orientation.Basis, playerPose,
                new(checked((short)(x * 16)), checked((short)((box.UpperY + box.LowerY) * 16)),
                    checked((short)((box.UpperZ + box.LowerZ) * 16)), 5));
            PlaceHook(Point(box.UpperX * 2 + 100));
            attacker.StartAttackMonitor(0, 1, 100, 0);
            attacker.Procedures.Execute(2);
            Assert(configuration.Player.Combat.State.Health == 100, "A monitor's first hook sample records its position without damaging the player.");
            PlaceHook(Point(box.LowerX * 2 - 100));
            attacker.Procedures.Execute(2);
            Assert(configuration.Player.Combat.State.Health == 90 && configuration.Player.Model.ClipId == 0xAA,
                "Actual enemy monitor, posed player collision, native effects and ActorHit reduce health100 to90 and start AA.");
            for (int update = 0; update < 300 && configuration.Player.Controller.Action != PlayerAction.Idle; update++)
            {
                await SourceFrames();
                configuration.Player.AdvanceSourceUpdate(default, configuration.Player.Controller.Orientation.Heading);
            }
            Assert(configuration.Player.Controller.Action == PlayerAction.Idle && configuration.Player.Combat.State.Field36C == 0,
                "The genuinely damaged native player finishes recovery without permanent immunity or frozen action.");
            GD.Print($"Passed {assertions} native live thug checks: original initialization/patrol, cached transforms and actual player attack damage/recovery.");

            void PlaceHook(FixedVector3 target)
            {
                attacker.Position = default;
                attacker.Actor.Body.Angles = default;
                attacker.BuildTransform();
                FixedVector3 offset = attacker.GetActorHookPosition(new(0, 0, 0, 0));
                attacker.Position = target - offset;
                attacker.BuildTransform();
            }
        }
        finally
        {
            configuration.Player.ActorState.InvisibilityFlag = visibility.Invisibility;
            configuration.Player.Combat.HiddenParts = visibility.HiddenParts;
            fixture.QueueFree();
        }
        async Task SourceFrames()
        {
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidDataException(message);
        }
    }
    private readonly record struct GodotPlayerStateRestore(sbyte Invisibility, uint HiddenParts);
}
