using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A3 OBJ pickup/carry/throw and vault ENV switch activation through the imported player and live session.</summary>
public static class PlayerInteractionImportChecks
{
    public static async Task VerifyFixtureAsync(Node3D owner, PlayerInteractionFixture fixture)
    {
        GodotPlayer player = fixture.Player; GodotPlayerMelee melee = fixture.Melee;
        var sound = player.Combat.Sound ?? throw new InvalidOperationException("Native pickup proof requires actual source sound playback.");
        var positionSound = player.Combat.PositionSound ?? throw new InvalidOperationException("Native throw proof requires actual positional sound playback.");
        var sounds = new List<ushort>(); var positionSounds = new List<ushort>();
        player.Combat.Sound = (id, volume, pitch) => {
            if (id == 7 && (!ReferenceEquals(fixture.Session.HeldActor, melee.Kick.HeldObject?.Identity) || volume != 0x2000 || pitch != 0))
                throw new InvalidDataException("Actual pickup SFX7 must occur after session ownership and source Pickup.");
            sounds.Add(id); sound(id, volume, pitch);
        };
        player.Combat.PositionSound = (id, position, pitch) => {
            if (id == 37 && (fixture.Session.HeldActor is null || position != player.Combat.State.Position || pitch != 0))
                throw new InvalidDataException("Actual throw SFX37 must precede held pointer release at source player position.");
            positionSounds.Add(id); positionSound(id, position, pitch);
        };
        try
        {
            GodotManipulationActor item = fixture.SpawnManipulation(193), alternate = fixture.SpawnManipulation(218);
            Assert(item.Body.Position == new FixedVector3(598, -68, 704) << 12 && item.Body.Angles.Y == 4040 && item.State.ModelChecksum == 0x5301B900 &&
                item.State.Parameter108 == 85 && item.State.Flags == 0 && item.State.ChunkModels.SequenceEqual(new uint[] { 0x7640E7BD, 0xEF49B607, 0x984E8691, 0x062A1332 }),
                "Independent original node193 position, yaw, OBJ checksum, reach and chunk table must bind to the actual native actor.");
            Assert(alternate.Body.Position == new FixedVector3(1745, -79, 2077) << 12 && alternate.State.ModelChecksum == 0x44EF15C1 && alternate.State.Parameter108 == 80 &&
                alternate.State.Flags == 0 && fixture.Session.SourceEnvironmentalOrder.Count == 2,
                "Actual node218 alternate OBJ and source environment list are required; large-object flags are never manufactured.");
            await Physics(); await Physics();
            FixedVector3 approach = FindApproach(item.Colliders, item.Body.Position, fixture, item);
            PlacePlayer(approach, item.Body.Position);
            melee.PrepareTargets(2); TriggerKick();
            Assert(melee.CheckKick() && player.Combat.State.ActionState == 0x100000 && player.Model.ClipId == 190,
                "Real original OBJ ray/facing selection must enter actual pickup190 rather than a synthetic combo or forced handle.");
            PlayerKickHandle handle = melee.Kick.ManipulationTarget;
            Assert(ReferenceEquals(fixture.Session.RecoverEnvironmentalHandle(ref handle), item), "CheckKick must store a genuine recoverable environmental weak handle.");
            Frame(9); melee.UpdateInteraction(ushort.MaxValue);
            Assert(fixture.Session.HeldActor is null && sounds.Count(id => id == 7) == 0 && item.State.PulseSent == 0,
                "Source pickup frame9 must leave actual OBJ, shadow, pulse and audio untouched.");
            Frame(10); melee.UpdateInteraction(ushort.MaxValue);
            Assert(ReferenceEquals(fixture.Session.HeldActor, item) && ReferenceEquals(melee.Kick.HeldObject?.Identity, item) && player.Combat.State.HeldObjectPresent &&
                (item.State.ItemFlags & 0x820) == 0x820 && item.State.PulseSent == 1 && sounds.Count(id => id == 7) == 1 && fixture.Pulses.SequenceEqual(new ushort[] { 466 }),
                "Actual frame10 Pickup must hold the real body, set original item flags, pulse original link466 and play genuine SFX7 once.");
            Assert(item.State.ShadowItem?.Identity is GodotSwitchEnvironmentItem shadow && !shadow.Mesh.Visible && item.Visible &&
                item.Colliders.SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()).All(shape => shape.Disabled),
                "Pickup must hide its genuine source ENV shadow, retain visible OBJ and exclude held original collision shapes.");
            FixedVector3 originalPosition = item.Body.Position;
            melee.State.HeadingDelta = 37;
            melee.FinishFrame(); player.Model.VerifyCurrentPose();
            FixedVector3 left = melee.GetPlayerHookPosition(1), right = melee.GetPlayerHookPosition(0), midpoint = left + (right - left) / 2;
            FixedVector3 direction = FixedVectorMath.Normalize((midpoint - player.Combat.State.Position) >> 6);
            FixedVector3 expectedCarry = midpoint + direction * 85 + player.Combat.State.ForwardDirection * 32;
            Assert(item.Body.Position == expectedCarry && item.Body.Position != originalPosition && item.Body.Angles.Y == unchecked((short)(4040 + 37)) &&
                item.Transform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(expectedCarry)),
                "Carry must use original hook1/0 from the real eighteen-part pose, reach85, forward32 and wrapped source yaw; native OBJ follows that fixed transform.");
            melee.State.HeadingDelta = 0;
            Frame(11); melee.UpdateInteraction(ushort.MaxValue);
            Assert(fixture.Pulses.Count == 1 && sounds.Count(id => id == 7) == 1 && fixture.Session.SourceEnvironmentalOrder.Any(candidate => ReferenceEquals(candidate.Identity, item)),
                "Held objects remain in their original environment list and cannot repeat pickup pulse/audio.");

            // Complete the genuine pickup clip, preserving independent model playback rather than setting an invented finished flag.
            player.Model.AdvanceTicks(player.Model.FrameCount * 8);
            melee.State.AnimationFinished = player.Model.Playback.IsFinished;
            Assert(melee.State.AnimationFinished, "Original pickup asset must reach its genuine animation endpoint.");
            melee.UpdateInteraction(190);
            Assert(player.Combat.State.ActionState == 1 && player.Model.ClipId == 194 && ReferenceEquals(fixture.Session.HeldActor, item),
                "Source pickup completion must enter the genuine held standing clip194 without dropping ownership.");
            melee.PrepareTargets(2); TriggerKick();
            Assert(melee.CheckKick() && player.Combat.State.ActionState == 0x200000 && player.Model.ClipId == 195,
                "Kick while holding an authored small OBJ must select actual throw195.");
            int cameraCalls = fixture.CameraBehindCalls;
            Frame(5); melee.UpdateInteraction(ushort.MaxValue);
            Assert(fixture.CameraBehindCalls == cameraCalls && ReferenceEquals(fixture.Session.HeldActor, item), "Throw frame5 must not realign the actual camera or release the body.");
            Frame(6); melee.UpdateInteraction(ushort.MaxValue);
            Assert(fixture.CameraBehindCalls == cameraCalls + 1 && fixture.Camera.ScriptRig.Heading == player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall),
                "Throw frame6 must call genuine PutCameraBehind0 using the player orientation.");
            Frame(12); melee.UpdateInteraction(ushort.MaxValue);
            Assert(ReferenceEquals(fixture.Session.HeldActor, item) && positionSounds.Count(id => id == 37) == 0, "Throw frame12 retains the held body and positional audio.");
            melee.Kick.TorsoTarget = null;
            Frame(13); melee.UpdateInteraction(ushort.MaxValue);
            Assert(fixture.Session.HeldActor is null && melee.Kick.HeldObject is null && !player.Combat.State.HeldObjectPresent &&
                item.Body.Velocity == player.Combat.State.ForwardDirection * -32 + player.Combat.State.UpDirection * 16 && item.Body.Acceleration.Y == 4096 &&
                (item.State.Flags & 1) != 0 && positionSounds.Count(id => id == 37) == 1,
                "Actual source throw195/frame13 must initialize the real CManipOb ballistic motion, play SFX37 before release and clear session/combat ownership.");
            int emitted = positionSounds.Count; Frame(14); melee.UpdateInteraction(ushort.MaxValue);
            Assert(positionSounds.Count == emitted, "Released bodies must not be thrown or sounded again on the next native action update.");
            PlayerKickHandle deleted = fixture.Session.MakeEnvironmentalHandle(item.KickObject); uint validation = deleted.Validation;
            item.Lifecycle.Kill();
            for (int update = 0; update < 4; update++) fixture.Registry._PhysicsProcess(1.0 / 60);
            Assert(fixture.Session.RecoverEnvironmentalHandle(ref deleted) is null && deleted.Target is null && deleted.Validation == validation,
                "Actual source body deletion must expire environmental weak pointer while retaining its validation word.");

            PlayerAirborneEntryImportChecks.Verify(fixture, alternate);
            GodotSwitchActor control = fixture.SpawnSwitch(176);
            Assert(control.State.Mode == SourceSwitchMode.Off && control.State.OffItem?.ModelChecksum == 0x2570E809 && control.State.OnItem?.ModelChecksum == 0xF2A5820D,
                "Native interaction requires actual original vault176 models, not a substitute switch body.");
            await Physics(); await Physics();
            var off = (GodotSwitchEnvironmentItem)control.State.OffItem!.Identity;
            var on = (GodotSwitchEnvironmentItem)control.State.OnItem!.Identity;
            FixedVector3 switchPoint = control.AutoAimPoint!.Position;
            approach = FindApproach(off.Colliders, switchPoint, fixture, off);
            PlacePlayer(approach, switchPoint); melee.PrepareTargets(2); TriggerKick();
            Assert(melee.CheckKick() && player.Model.ClipId == 37 && player.Combat.State.ActionState == 0x80000000 && melee.Kick.SwitchActionProgress == 0,
                "Real original vault ENV face and player-relative target cone must select source switch37.");
            fixture.Pulses.Clear(); Frame(11); melee.UpdateInteraction(ushort.MaxValue);
            Assert(control.State.Mode == SourceSwitchMode.Off && fixture.Pulses.Count == 0, "Native switch frame11 must preserve genuine off model and pulse state.");
            Frame(12); melee.UpdateInteraction(ushort.MaxValue);
            Assert(control.State.Mode == SourceSwitchMode.On && !off.Mesh.Visible && on.Mesh.Visible && melee.Kick.SwitchActionProgress == 1 &&
                fixture.Pulses.SequenceEqual(control.Definition.Links), "Native switch frame12 must call real Flick, swap actual ENV models and pulse exact original links once.");
            int pulses = fixture.Pulses.Count; Frame(13); melee.UpdateInteraction(ushort.MaxValue);
            Assert(control.State.Mode == SourceSwitchMode.On && fixture.Pulses.Count == pulses, "Repeated active switch update cannot Flick or pulse twice.");
            player.Model.AdvanceTicks(player.Model.FrameCount * 8);
            ushort completed = (ushort)player.Model.ClipId;
            ushort? followOn = PlayerAnimationFollowOns.Resolve(player.Model.Playback, fixture.Registry.RandomSource);
            Assert(player.Model.Playback.IsFinished && completed == 37 && followOn == 0,
                "The genuine switch clip must complete and resolve its original37-to0 follow-on before dispatching the completion cache.");
            player.Model.RunClip(followOn!.Value); Frame(0);
            melee.UpdateInteraction(completed);
            Assert(player.Combat.State.ActionState == 1, "Actual completed-animation cache must finish source switch pull.");
            GD.Print("Verified actual L1A3 player interactions: original401 OBJ/shadow/pulse, real hook carry, animation/audio/camera timing, ballistic release, source weak deletion and407 vault Flick/ENV ownership.");
        }
        finally { player.Combat.Sound = sound; player.Combat.PositionSound = positionSound; }

        void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException(evidence); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        void TriggerKick() { melee.State.Input.Triggered[18] = melee.State.Input.Mirrors[18] = 1; }
        void Frame(int frame)
        {
            player.Model.SetSourceAnimationFrame(frame);
            player.Combat.State.AnimationId = (ushort)player.Model.ClipId; player.Combat.State.AnimationFrame = frame;
            melee.State.AnimationFinished = player.Model.Playback.IsFinished;
        }
        void PlacePlayer(FixedVector3 position, FixedVector3 target)
        {
            player.Controller.TeleportFromScript(position);
            FixedVector3 facing = FixedVectorMath.Normalize(((position - target) >> 12) with { Y = 0 });
            player.Controller.Orientation.Orient(new(0, -4096, 0), 0, facing);
            player.Controller.CaptureCombatState(2); player.Combat.State.ActionState = 1;
            player.RefreshCombatTransform(); fixture.Camera.GlobalPosition = player.GlobalPosition + new Vector3(0, 3, 8);
            fixture.Camera.LookAt(PsxCoordinates.FixedWorldPosition(target));
        }
    }
    private static FixedVector3 FindApproach(IReadOnlyList<ImportedCollisionBody> bodies, FixedVector3 target,
        PlayerInteractionFixture fixture, object expected)
    {
        foreach (ImportedCollisionBody body in bodies)
            foreach (var triangle in body.SourceTriangles)
            {
                // ENV collision shapes bake the original instance transform into vertices;
                // SourceTriangles remain local. OBJ actor instances use identity inside their live body transform.
                Transform3D sourceToWorld = expected is GodotSwitchEnvironmentItem environmentItem ? environmentItem.Mesh.GlobalTransform : body.GlobalTransform;
                Vector3 center = sourceToWorld * PsxCoordinates.WorldPosition((triangle.A + triangle.B + triangle.C) / 3);
                Vector3 normal = (sourceToWorld.Basis * PsxCoordinates.WorldPosition(triangle.Normal)).Normalized();
                if (Math.Abs(normal.Y) > .25f) continue;
                foreach (float reach in new[] { .64f, .32f, .16f })
                {
                    FixedVector3 position = PsxCoordinates.OriginalWorldPosition(center + normal * reach);
                    GodotActorWorldHit? hit = fixture.WorldQuery.Trace(position, target);
                    if (hit is null || !ReferenceEquals(fixture.Session.ResolveWorldContact(hit), expected)) continue;
                    FixedVector3 delta = (hit.Position - position) >> 12;
                    if (FixedVectorMath.OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z)) <= 256) return position;
                }
            }
        throw new InvalidDataException("Original interaction model has no unobstructed native face approach within source pickup/switch reach.");
    }
}
