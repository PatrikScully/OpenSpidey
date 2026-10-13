using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Source4B9420 over real L1A3 authored401/face, actual protected swing/web/dome bits, items503/504/505 and all four posed smoke owners.</summary>
internal static class PlayerDistanceAttackPreparationImportChecks
{
    public static void Verify(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerWebRuntime webs,
        GodotPlayerCombatRuntime combat, PsxLibraryRepository repository, SoundChannelController sounds,
        GodotManipulationActor item, FixedVector3 contact, FixedVector3 normal)
    {
        int checks = 0;
        if (webs.DistanceAttackPreparation is null) webs.ConfigureDistanceAttack(combat, camera, repository.Load("items"), sounds, new(), () => default);
        GodotPlayerDistanceAttackPreparation cleanup = webs.DistanceAttackPreparation!;
        GodotPlayerAdditionalBodies parts = webs.AdditionalBodies!;
        GodotPlayerAdditionalEffects effects = webs.AdditionalEffects!;
        GodotPlayerHandTrails hands = webs.HandTrails!;
        GodotPlayerMelee melee = player.Melee!;
        GodotPlayerSwing swing = webs.Swing!;
        GodotPlayerSmashAttack smash = webs.Smash!;
        GodotSmokeTrails smoke = webs.SmokeTrails!;
        GodotWebDomes domes = webs.Domes!;
        GodotPlayerWebAttack attack = webs.Attack!;
        PlayerAdditionalEffectState state = parts.State;
        int actors = combat.Actors.Registry.LiveCount, leases = combat.Actors.Registry.BaddyConstructionState.Count;
        FixedVector3 direction = player.Controller.Orientation.Forward;
        Assert(parts.Count == 0 && state.SenseBody.Target is null && state.BonusFists.All(handle => handle.Target is null) &&
            !swing.SwingPresent && webs.Actions.State.CurrentStrand is null && melee.Kick.HeldObject is null,
            "Positive cleanup must begin with the real runtime's empty player resource pointers, retaining authored scene actors.");

        // Use real source factories and active preparation, rather than stand-in bodies or fabricated handles.
        state.SenseTicks = 24; state.SenseKind = 1;
        player.Combat.State.Field5AC = 1; player.Combat.State.Field5B0 = 0; player.Combat.State.ElapsedTicks = 2;
        effects.Prepare();
        PlayerKickHandle senseHandle = state.SenseBody, firstFistHandle = state.BonusFists[0], secondFistHandle = state.BonusFists[1];
        GodotPlayerAdditionalBody sense = parts.Recover(ref senseHandle)!, firstFist = parts.Recover(ref firstFistHandle)!, secondFist = parts.Recover(ref secondFistHandle)!;
        Assert(parts.Count == 3 && parts.SourceOrder.SequenceEqual(new[] { secondFist, firstFist, sense }) &&
            sense.State.Type == 504 && sense.State.ModelIndex == 4 && firstFist.State.Type == 505 && firstFist.State.ModelIndex == 8 &&
            sense.State.BodyFlags == 0x16 && firstFist.State.BodyFlags == 0x16 && secondFist.State.BodyFlags == 0x16 &&
            combat.Actors.Registry.LiveCount == actors && combat.Actors.Registry.BaddyConstructionState.Count == leases,
            "Actual sense/fist preparation must create the original render-only model4/8 plain16 bodies in head-insertion order without actor/baddy leases.");
        uint senseSound = state.SenseSoundHandle;
        Assert(senseSound is not 0 and not uint.MaxValue && cleanup.State.Water.Value == 1 && state.SenseTicks == 22 &&
            player.Combat.State.Field5B0 == 768 && firstFist.State.Scale == new FixedVector3(384, 384, 384),
            "Sense preparation must retain actual26 SFX handle/water/countdown and source fist grow scaling before cleanup.");
        int senseChannel = Enumerable.Range(1, 31).Single(channel => (senseSound & (1u << channel)) != 0);
        Assert(sounds.ReadChannel(senseChannel).Allocated, "Positive source sound cleanup requires a genuinely allocated native26 channel.");

        melee.PlaySingleAnimation(0x11C, 0, -1); player.Model.SetSourceAnimationFrame(4); player.Controller.CaptureCombatState(2);
        PlayerKickHandle domeHandle = domes.Spawn(0); attack.State.DomeHandle = domeHandle;
        combat.Actors.Registry.SourceList(OpenSpidey.Core.Actors.ActorUpdateGroup.Miscellaneous).Update();
        WebDome dome = domes.Recover(ref domeHandle)!;
        Assert(dome.Pieces.All(piece => piece is not null && (piece.BodyFlags & 0x20) != 0),
            "Positive AB8 cleanup must retain the real dome and its five original protected36 model companions.");

        // RunAnim selects playback; direct M3dUtils part sampling must not overwrite the retained source render pose.
        FixedJointTransform[] retained = Enumerable.Range(0, 18).Select(player.Model.GetSourcePartPose).ToArray();
        melee.RunAnimation(106, 2, -1);
        var direct = new FixedJointTransform[18];
        repository.Load("spidey").Animations!.DecodeClip(106).SampleFrame(2, false, direct);
        Assert(Enumerable.Range(0, 18).All(part => player.Model.GetSourcePartPose(part) == retained[part] &&
            player.Model.ReadSourceEffectPartPose(part, true) == retained[part] && player.Model.ReadSourceEffectPartPose(part, false) == direct[part]),
            "Actual flag4 effect angles must use corrected buffer; flag4-clear sampling must read selected clip without changing retained Ob_AI/render pose.");
        melee.ApplyPlayerPose(); hands.Update(); smash.CreateSmashAttackTrails(); effects.FinishFrame();
        SmokeTrail[] trails = smash.CurrentTrails.Concat(hands.State.Current.Cast<SmokeTrail>()).Cast<SmokeTrail>().ToArray();
        Assert(trails.Length == 4 && trails.All(trail => trail.FadeAway == 0 && !trail.IsDisposed) &&
            trails[0].Segments.Length == 4 && trails[1].Segments.Length == 4 && trails[2].Segments.Length == 6 && trails[3].Segments.Length == 6 &&
            smoke.Count == 4 && smoke.SegmentCount == 20,
            "All four source584/588/58C/590 must hold genuine posed foot/hand CSmokeTrail owners and their twenty protected ribbon bits.");
        Assert(sense.State.Position == melee.GetPlayerHookPosition(8) && firstFist.State.Position == melee.GetPlayerHookPosition(4) &&
            secondFist.State.Position == melee.GetPlayerHookPosition(3) && sense.Model.RenderMesh.GetSurfaceCount() > 0 &&
            firstFist.Model.RenderMesh.GetSurfaceCount() > 0 && secondFist.Model.RenderMesh.GetSurfaceCount() > 0,
            "Actual items sense/fists must follow original posed hooks8/4/3 and render their native source faces.");
        foreach (GodotPlayerAdditionalBody body in parts.SourceOrder) VerifyGeometry(body);
        FlamingFistsImportChecks.Verify(player, webs, combat);

        WebSwingAnchor anchor = swing.CreateSwing(contact, FixedVectorMath.OriginalDistance(player.Controller.Position, contact),
            PlayerSwingBasis.Calculate(player.Controller.Position, contact, normal, default).Matrix, normal);
        swing.State.Current = anchor;
        var currentWeb = new WebStrandOwner(webs.Actions);
        currentWeb.Fire(melee.GetPlayerHookPosition(1), contact, null, false, default, camera.SourcePosition >> 12, webs.Actions.Random);
        webs.Actions.State.CurrentStrand = currentWeb;
        KnottedWebStrand currentLine = currentWeb.Strand!;
        combat.CreateAutoAimReticle();
        GodotPlayerTargetReticle reticle = player.GetParent().GetChildren().OfType<GodotPlayerTargetReticle>().Single();
        ArrayMesh reticleMesh = reticle.RenderMesh;
        Assert(combat.AutoAimReticlePresent && reticleMesh.GetSurfaceCount() > 0 &&
            combat.Particles.Identities(SourceParticleList.GPolyLine).Contains(anchor.Strand) &&
            combat.Particles.Identities(SourceParticleList.GPolyLine).Contains(currentLine),
            "Positive cleanup must own actual503 items geometry and both genuine source swing/current-web paired polylines.");
        combat.Session.Hold(item); item.Pickup(); melee.Kick.HeldObject = item.KickObject;
        Assert(ReferenceEquals(combat.Session.HeldActor, item) && player.Combat.State.HeldObjectPresent && (item.State.ItemFlags & 0x820) == 0x820,
            "Positive held cleanup must retain the actual authored L1A3 node193 through its genuine Pickup and shared session pointer.");
        FixedVector3 dropVelocity = player.Controller.Orientation.Forward * -8 + player.Controller.Orientation.Up * 4;

        var temporary = new Node3D(); player.GetParent().AddChild(temporary);
        Node originalParent = player.GetParent();
        try
        {
            player.Reparent(temporary); reticle.Reparent(temporary);
            Assert(parts.Count == 3 && parts.Recover(ref senseHandle) == sense && parts.Recover(ref firstFistHandle) == firstFist &&
                parts.Recover(ref secondFistHandle) == secondFist && !reticle.ResourcesDisposed && reticle.RenderMesh == reticleMesh &&
                parts.SourceOrder.All(body => !body.Model.ResourcesDisposed && body.Model.RenderMesh.GetSurfaceCount() > 0),
                "Live player/additional/reticle reparent must preserve real native handles, model faces and material ownership before actual destruction.");
        }
        finally { player.Reparent(originalParent); reticle.Reparent(originalParent); temporary.Free(); }

        var removals = new List<GodotPlayerAdditionalBody>();
        void Removed(GodotPlayerAdditionalBody body)
        {
            removals.Add(body);
            if (ReferenceEquals(body, sense))
                Assert(!combat.AutoAimReticlePresent && !GodotObject.IsInstanceValid(reticle) && reticle.ResourcesDisposed &&
                    state.SenseTicks == 0 && state.SenseSoundHandle == 0 && !sounds.ReadChannel(senseChannel).Allocated &&
                    ReferenceEquals(combat.Session.HeldActor, item) && swing.SwingPresent && webs.Actions.State.CurrentStrand == currentWeb,
                    "Source ED4 unlink/destructor must follow real503 deletion and26 stop, before held/swing/current-web cleanup.");
            else
                Assert(combat.Session.HeldActor is null && melee.Kick.HeldObject is null && item.Body.Velocity == dropVelocity &&
                    anchor.IsDisposed && swing.State.Current is null && currentLine.IsDisposed && webs.Actions.State.CurrentStrand is null &&
                    player.Controller.SourceJumpImpulse.Phase == 0 && player.Controller.SourceJumpImpulse.Hold == 0,
                    "Source5B8 deletions must follow genuine−8forward+4up Drop, jump-duration reset, SwingBack/destructor and current-web deletion.");
        }
        parts.BodyRemoved += Removed;
        int released = webs.Strands.ReleasedSwings.Count, shocks = domes.ShockCount;
        GodotPlayerLookaroundRuntime lookaround = player.Controller.Lookaround as GodotPlayerLookaroundRuntime ??
            throw new InvalidOperationException("Positive cleanup requires its actual lookaround camera owner.");
        lookaround.Camera.EnterLookaroundMode();
        Assert(lookaround.Aim.State.Active && camera.SourceControlMode == 7 && lookaround.Camera.State.Path is { Length: 24 },
            "Positive cleanup must own the genuine source7 lookaround transition/path before its exact ExitLookaround branch.");
        camera.SetFocusHook(2); player.Combat.State.ArenaConstraintActive = 1;
        player.Controller.SetSwingJumpImpulse(new(-123456, 17, 23));
        player.Combat.State.Velocity = new(12345, -23456, 34567); player.Controller.ApplyCombatState();
        try { cleanup.Prepare(direction); }
        finally { parts.BodyRemoved -= Removed; }
        Assert(removals.SequenceEqual(new[] { sense, firstFist, secondFist }) && parts.Count == 0 &&
            new[] { sense, firstFist, secondFist }.All(body => !GodotObject.IsInstanceValid(body) && body.Model.ResourcesDisposed),
            "Actual additional parts must unlink/delete ED4 then fist0/fist1, releasing all native meshes/materials without actor leases.");
        Assert(state.SenseBody == default && state.BonusFists[0].Target is null && state.BonusFists[0].Validation == firstFistHandle.Validation &&
            state.BonusFists[1].Target is null && state.BonusFists[1].Validation == secondFistHandle.Validation && attack.State.DomeHandle == default,
            "Native cleanup must fully empty sense/dome handles while retaining both source fist validators.");
        Assert(webs.Strands.ReleasedSwings.Count == released + 1 && anchor.IsDisposed && currentLine.IsDisposed &&
            webs.Strands.Owners.Count == 0 && domes.ShockCount == shocks + 1 && dome.Mode == 3 && (dome.BodyFlags & 0x40) != 0 &&
            trails.All(trail => trail.FadeAway == 1 && !trail.IsDisposed) && smoke.Count == 4 &&
            smash.CurrentTrails.All(trail => trail is null) && hands.State.Current.All(trail => trail is null),
            "Prior cleanup must create genuine SwingBack/dome burst effects, destroy current web and fade rather than delete all four surviving smoke owners.");
        Assert(player.Combat.State.ArenaConstraintActive == 0 && cleanup.State.Water.Value == 0 && player.Combat.State.Field5B0 == 0 &&
            player.Combat.State.Field5AC == 0 && !player.Combat.State.IsCrawling && player.Combat.State.CrawlSurfaceNormal == new FixedVector3(0, -4096, 0) &&
            player.Controller.Velocity == default && player.Combat.State.Velocity == default && melee.State.HeadingDelta == 0 &&
            player.Combat.State.HeadingTurnTicksRemaining == 0 && !lookaround.Aim.State.Active && lookaround.Camera.State.Path is null &&
            camera.SourceControlMode == 3 && camera.FocusHookIndex == -1 && player.Combat.State.FloorCameraState == 0 &&
            combat.Actors.Registry.LiveCount == actors && combat.Actors.Registry.BaddyConstructionState.Count == leases && cleanup.PreparationCount == 1,
            "Source cleanup must reset real locomotion/orientation/floor camera and shared process fields while retaining all authored actors and leases.");
        uint staleValidation = senseHandle.Validation;
        Assert(parts.Recover(ref senseHandle) is null && senseHandle.Target is null && senseHandle.Validation == staleValidation,
            "Deleted native additional bodies must expire only the pointer half of later stale-handle recovery.");
        GD.Print($"PASS: {checks} real source distance-attack cleanup checks: authored401 Drop, items503/504/505, native reparent, actual26 sound, protected swing/current-web/dome ownership, four surviving fades and source camera/reset order.");

        void VerifyGeometry(GodotPlayerAdditionalBody body)
        {
            PsxModel model = body.Model.SourceModel;
            Assert(model.Normals.Count < model.Vertices.Count + model.Faces.Count && body.Model.SourceBank.AssetPath.Equals("data/items.psx", StringComparison.OrdinalIgnoreCase),
                "Source extra-model geometry must use its real items bank and ordinary unlit DC path, without invented RGB/lighting fades.");
            int expected = model.Faces.Sum(face => face.IsTriangle ? 3 : 6);
            int actual = Enumerable.Range(0, body.Model.RenderMesh.GetSurfaceCount()).Sum(surface =>
                body.Model.RenderMesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length);
            Assert(actual == expected, "Source extra models must upload exactly their original face corners without replacement shapes.");
            Assert(body.Model.Transform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(body.State.Position)),
                "Source extra-model native transform must retain its original posed hook position.");
        }
        void Assert(bool condition, string evidence) { checks++; if (!condition) throw new InvalidDataException(evidence); }
    }
}
