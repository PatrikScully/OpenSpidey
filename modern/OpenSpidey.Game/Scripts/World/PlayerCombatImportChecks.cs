using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Checks real player animation, terrain physics and posed combat contacts in an isolated native world.</summary>
public static class PlayerCombatImportChecks
{
    public static async Task VerifyAsync(Node3D owner, Func<ImportedAnimatedModel> modelFactory,
        IReadOnlyList<ActorPartBox> boxes)
    {
        var fixture = new Node3D { Name = "PlayerCombatFixture", TopLevel = true };
        owner.AddChild(fixture);
        fixture.GlobalTransform = Transform3D.Identity;
        Vector3 origin = new(3000, 0, 3000);
        var vertices = new[] { new PsxVertex(new(-1000, 0, -1000), 0), new PsxVertex(new(1000, 0, -1000), 0),
            new PsxVertex(new(-1000, 0, 1000), 0), new PsxVertex(new(1000, 0, 1000), 0) };
        var floorModel = new PsxModel(0, ushort.MaxValue, vertices,
            new[] { new PsxFace(0, new byte[] { 0, 1, 2, 3 }, 0, null, [], 0x100000) })
            { Normals = new[] { new PsxNormal(new(0, -4096, 0), 0) } };
        Node3D floor = PsxWorldCollisionBuilder.Build(new PsxScene(0x20006,
            new[] { new PsxInstance(0, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 0, 0) }, new[] { floorModel }, []));
        floor.Position = origin;
        fixture.AddChild(floor);
        var events = new List<string>();
        int assertions = 0;
        GodotPlayer player = Create(origin + new Vector3(0, .96f, 0));
        GodotPlayer falling = Create(origin + new Vector3(3, .96f, 0));
        try
        {
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            Assert(boxes.Count == 18 && boxes[5] == new ActorPartBox(6, -8, 15, -1, 4, -4),
                "Native player contact uses all eighteen original header boxes, including the source part5 bounds.");
            Step(player);
            Assert(player.Controller.IsGrounded, "Native player acquires the original floor before attack.");
            VerifyContact(player, 5);
            var hit = new ActorHit(15, 5, 8, 10, new(4096, 0, 0));
            Assert(player.Combat.Hit(hit), "Melee contact is accepted.");
            Assert(player.Combat.State.Health == 90 && player.Model.ClipId == 0xAA &&
                player.Controller.Action == PlayerAction.KnockdownRecovery, "Actual damage starts AA recovery with health 90.");
            Assert(!player.Combat.Hit(hit) && player.Combat.State.Health == 90,
                "An active hit animation rejects the second contact.");
            for (int index = 0; index < 300 && player.Controller.Action != PlayerAction.Idle; index++) Step(player);
            Assert(player.Controller.Action == PlayerAction.Idle && player.Combat.State.Field36C == 0,
                "AA finishes through source follow-ons and returns to standing without persistent immunity.");
            Assert(player.Combat.Hit(hit) && player.Combat.State.Health == 80, "The recovered player accepts another damaging hit.");
            for (int index = 0; index < 300 && player.Controller.Action != PlayerAction.Idle; index++) Step(player);
            Assert(player.Controller.Action == PlayerAction.Idle, "Second hit also recovers.");
            Assert(player.Combat.Hit(hit with { Damage = 100 }), "Lethal contact is accepted.");
            Assert(player.Combat.State.Health == 0 && player.Controller.Action == PlayerAction.Dead && player.Model.ClipId == 0xAB,
                "Grounded lethal damage starts source AB death.");
            int callbackStart = events.Count;
            for (int index = 0; index < 500 && !events.Skip(callbackStart).Contains("status:2"); index++) Step(player);
            Assert(events.Skip(callbackStart).Contains("status:2") && events.Skip(callbackStart).Contains("flash") &&
                events.Skip(callbackStart).Contains("initialize"), "Finished death animation and source 120-tick delay hand off restart effects.");

            Step(falling);
            falling.Combat.State.Position = PsxCoordinates.OriginalWorldPosition(origin + new Vector3(3, 4, 0));
            falling.Combat.State.ActionState = (uint)PlayerAction.Falling;
            falling.Combat.State.AnimationId = 0xB0;
            falling.Animation.Run(0xB0);
            falling.Controller.ApplyCombatState();
            Assert(falling.Combat.Hit(hit with { Damage = 100 }), "Airborne lethal contact is accepted.");
            Assert(falling.Controller.Action == PlayerAction.Falling && falling.Model.ClipId == 0xB0,
                "Airborne death retains falling action and B0 animation.");
            int originalY = falling.Controller.Position.Y;
            bool moved = false;
            for (int index = 0; index < 300 && falling.Controller.Action != PlayerAction.Dead; index++)
            {
                Step(falling);
                moved |= falling.Controller.Position.Y > originalY;
            }
            Assert(moved && falling.Controller.IsGrounded && falling.Controller.Action == PlayerAction.Dead && falling.Model.ClipId == 0xB6,
                "Dead airborne player falls under native terrain physics and lands into source B6 death.");
            Assert(falling.Combat.State.Health == 0, "Landing preserves zero health.");
            GD.Print($"Passed {assertions} native player combat checks: posed contact, damage, repeated hit recovery, death and airborne corpse landing.");
        }
        finally { fixture.QueueFree(); }

        GodotPlayer Create(Vector3 position)
        {
            GodotPlayer actor = GodotPlayer.Create(PsxCoordinates.OriginalWorldPosition(position), 0,
                modelFactory(), new(true, new(false, 10, true)));
            fixture.AddChild(actor);
            actor.SetPhysicsProcess(false);
            actor.Combat.ConfigureCollisionBoxes(boxes);
            GodotPlayerCombat combat = actor.Combat;
            combat.Sound = (sound, volume, pitch) => events.Add($"sound:{sound}:{volume}:{pitch}");
            combat.PositionSound = (sound, _, pitch) => events.Add($"position:{sound}:{pitch}");
            combat.SoundStopped = handle => events.Add($"stop:{handle}");
            combat.XaStopped = () => events.Add("xa");
            combat.CameraBehind = argument => events.Add($"camera:{argument}");
            combat.CameraField12C = value => events.Add($"camera12C:{value}");
            combat.LevelStatus = value => events.Add($"status:{value}");
            combat.FlashReset = () => events.Add("flash");
            combat.InitializationReset = () => events.Add("initialize");
            combat.SmokeFade = slot => events.Add($"smoke:{slot}");
            combat.HeldObjectSmash = () => events.Add("held");
            combat.ArmorTextures = active => events.Add($"armor:{active}");
            combat.ArmorSaved = (amount, active) => events.Add($"savearmor:{amount}:{active}");
            combat.LookaroundExited = () => events.Add("lookaround");
            combat.WebToBlob = () => events.Add("webblob");
            combat.ElectricEffect = () => events.Add("electric");
            combat.ElectricEffectRemoved = () => events.Add("unelectric");
            combat.DeathEffectDeleted = () => events.Add("deleteeffect");
            combat.PadActuator = (controller, time, argument, strength) => events.Add($"pad:{controller}:{time}:{argument}:{strength}");
            return actor;
        }
        void Step(GodotPlayer actor)
        {
            actor.AdvanceSourceUpdate(default, 0);
            actor.Model.VerifyCurrentPose();
            Assert(actor.Transform.Origin.DistanceTo(PsxCoordinates.FixedWorldPosition(actor.Controller.Position)) < .001f,
                "Native visual transform follows the mutable source combat position.");
        }
        void VerifyContact(GodotPlayer actor, ushort part)
        {
            ActorPartBox box = boxes[part];
            var pose = new FixedJointTransform[boxes.Count];
            for (int index = 0; index < pose.Length; index++) pose[index] = actor.Model.GetSourcePartPose(index);
            FixedVector3 position = actor.Controller.Position;
            ActorHook From(int x) => new(checked((short)(x * 16)), checked((short)((box.UpperY + box.LowerY) * 16)),
                checked((short)((box.UpperZ + box.LowerZ) * 16)), part);
            FixedVector3 Point(int x) => ActorHookPosition.Evaluate(position, position >> 12,
                actor.Controller.Orientation.Basis, pose, From(x));
            FixedVector3 from = Point(box.UpperX * 2 + 100), to = Point(box.LowerX * 2 - 100);
            actor.Combat.HiddenParts = ~(1u << part);
            Assert(actor.Combat.CollideWithPlayer(from, to, 0x2000, out ActorHook contact) && contact.PartIndex == part,
                "Melee segment crosses the original unswapped animated part box.");
            actor.Combat.HiddenParts = uint.MaxValue;
            Assert(!actor.Combat.CollideWithPlayer(from, to, 0x2000, out _), "Hidden original parts reject contact.");
            actor.Combat.HiddenParts = 0;
        }
        void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
