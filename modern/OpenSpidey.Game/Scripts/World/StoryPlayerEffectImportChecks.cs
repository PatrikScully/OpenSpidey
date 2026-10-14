using System.Buffers.Binary;
using System.Diagnostics;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Observes real live Story source phases, then retains the actual web owner for the existing genuine scene teardown proof.</summary>
internal static class StoryPlayerEffectImportChecks
{
    private sealed record EmberBirth(FireEmber Ember, int Frame, int Orbit, int Amplitude, int Delay, FixedVector3 Position);

    public static async Task<GodotPlayerWebRuntime> VerifyAsync(StoryLevelSession scene)
    {
        GodotPlayer player = scene.Player ?? throw new InvalidDataException("Live player-effect proof requires the actual Story player.");
        GodotPlayerWebRuntime web = Descendants(scene).OfType<GodotPlayerWebRuntime>().Single();
        GodotPlayerCombatRuntime combat = Descendants(scene).OfType<GodotPlayerCombatRuntime>().Single();
        GodotPlayerMelee melee = player.Melee!;
        GodotPlayerAdditionalBodies parts = web.AdditionalBodies ?? throw new InvalidDataException("Actual Story additional bodies are not configured.");
        PlayerAdditionalEffectState effects = parts.State;
        GodotPlayerSmashAttack smash = web.Smash!;
        GodotPlayerHandTrails hands = web.HandTrails!;
        Assert(web.PlayerEffectPhasesEnabled && ReferenceEquals(web.AdditionalEffects!.Water, scene.Application.WaterEffect) &&
            ReferenceEquals(scene.Application.WaterPost.Water, scene.Application.WaterEffect) && player.Scripts?.Active == false,
            "Live effect proof requires enabled source phases, one application water owner and the completed genuine manual cutscene skip.");
        Assert(parts.Count == 0 && effects.SenseTicks == 0 && effects.SenseSoundHandle == 0 &&
            player.Combat.State.Field5AC == 0 && player.Combat.State.Field5B0 == 0 &&
            hands.State.Current.All(trail => trail is null) && smash.CurrentTrails.All(trail => trail is null),
            "The real story player must begin with its source sense/bonus/trail pointers empty; this proof must not replace existing gameplay resources.");

        AnimationPlaybackState originalAnimation = player.Model.Playback;
        uint originalAction = player.Combat.State.ActionState;
        FixedVector3 originalVelocity = player.Controller.Velocity;
        byte originalFire = player.Inventory!.State.FireWebbing;
        int originalDamage = player.Combat.State.Field36C, originalSenseKind = effects.SenseKind;
        int originalWater = scene.Application.WaterEffect.Value;
        FixedVector3[] originalHandOffsets = hands.State.PreviousOffsets.ToArray();
        ushort originalItemFlags = player.ActorState.ItemFlags;
        bool originalPoseEnabled = player.Model.SourcePoseEnabled;
        uint originalSkyTarget = scene.Rendering.SkyColourTarget;
        uint sourceSkyColour = originalSkyTarget == 0x466973 ? 0x154070u : originalSkyTarget;
        int restorations = scene.Rendering.SkyRestorations, checks = 0, frame = 0, phase = 0, handBirths = 0;
        int actorCount = 0, leaseCount = 0;
        uint originalClock = scene.SourceClock;
        int originalUpdates = player.SourceUpdates;
        bool observing = true, restored = false;
        var births = new List<EmberBirth>();
        var phaseOrder = new List<string>();
        var createdBodies = new List<GodotPlayerAdditionalBody>();

        player.Controller.BeforeDamageTimers += BeforeDamage;
        player.Controller.AfterDamageTimers += AfterDamage;
        melee.AfterPose += AfterPose;
        melee.BeforeHeldObjectPlacement += AfterPresentation;
        parts.BodyCreated += BodyCreated;
        web.SmokeTrails!.OwnerCreated += TrailCreated;
        web.Embers.SourceBitCreated += EmberCreated;
        try
        {
            // The typed source196 owner writes the real player state; preparation owns every subsequent side effect.
            byte[] arguments = new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(arguments, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(2), 4);
            TriggerNode context = scene.Registry.Triggers.Nodes[257];
            new PlayerSenseBuzzCommandHandler(() => effects).Execute(new(context, 0), new(196, 0, arguments));
            Check(effects.SenseKind == 1 && effects.SenseTicks == 4 && parts.Count == 0 && effects.SenseSoundHandle == 0,
                "Actual source196 must only write kind/duration, with genuine504/26 creation deferred until source preparation.");
            scene.Application.WaterEffect.Value = 1;
            player.Combat.State.Field36C = 7; player.Combat.State.Field5AC = 1;
            player.Inventory.State.FireWebbing = 1;
            player.Combat.State.ActionState = (uint)PlayerAction.ScriptedDelay;
            player.Combat.State.Velocity = default; player.Controller.ApplyCombatState();
            melee.RunAnimation(106, 1, -1);
            smash.CreateSmashAttackTrails();

            await Until(() => frame == 2);
            observing = false;
            int completedSourceFrames = player.SourceUpdates - originalUpdates;
            Check(scene.SourceClock > originalClock && player.SourceUpdates >= originalUpdates + 2 &&
                phaseOrder.SequenceEqual(new[] { "water", "damage", "pose", "presentation", "water", "damage", "pose", "presentation" }),
                "Two real source updates must visit water, damage/status, posed trails and presentation in original order without directly invoking any phase.");
            Check(handBirths == 2 && createdBodies.Select(body => body.State.Type).SequenceEqual(new ushort[] { 504, 505, 505 }) &&
                effects.SenseTicks == 0 && effects.SenseSoundHandle == 0 && parts.Count == 2 &&
                !GodotObject.IsInstanceValid(createdBodies[0]) && createdBodies[0].Model.ResourcesDisposed,
                "The actual sense timer must expire through source preparation, stopping26 and deleting504 while both genuine505 bonus models remain.");
            Check(scene.Rendering.SkyRestorations == restorations + 2 && scene.Application.WaterEffect.Value == 0 &&
                scene.Rendering.SkyColourTarget == originalSkyTarget && scene.Rendering.SkyColour == sourceSkyColour,
                "Each active sense-water frame must restore the real authored sky target before damage timers; expiry must leave the same shared water flag clear.");
            Check(births.Count == 12 && births.All(birth =>
                birth.Ember.OrbitPhase == unchecked(birth.Orbit + (completedSourceFrames - birth.Frame) * 100) &&
                birth.Ember.AmplitudePhase == unchecked(birth.Amplitude + (completedSourceFrames - birth.Frame) * 100) &&
                birth.Ember.FadeDelay == Math.Max(0, birth.Delay - (completedSourceFrames - birth.Frame)) &&
                birth.Ember.Position.Y == unchecked(birth.Position.Y - birth.Ember.Velocity.Y * (completedSourceFrames - birth.Frame)) &&
                combat.Particles.Identities(SourceParticleList.Flat).Contains(birth.Ember)),
                "Twelve actual flaming-fist embers must enter the canonical Flat phase and move in their same birth source frame, preserving exact later-frame counts.");

            // Release temporary state through the real later player phases, rather than deleting active bonus/trail bodies by assertion helpers.
            player.Combat.State.Field5AC = 0; player.Inventory.State.FireWebbing = originalFire;
            player.Combat.State.ActionState = originalAction; player.Combat.State.Velocity = originalVelocity;
            player.Controller.ApplyCombatState(); melee.RunAnimation(originalAnimation.ClipId, originalAnimation.Frame, -1);
            smash.FadeSmashAttackTrails();
            await Until(() => parts.Count == 0 && hands.State.Current.All(trail => trail is null));
            Check(player.Combat.State.Field5B0 == 0 && effects.BonusFists.All(handle => handle.Target is null) &&
                createdBodies.All(body => !GodotObject.IsInstanceValid(body) && body.Model.ResourcesDisposed) &&
                smash.CurrentTrails.All(trail => trail is null),
                "Real bonus shrink/idle source phases must remove both505 models and hand pointers, while source foot ribbons fade under their real owner.");
            restored = true;
            GD.Print($"PASS: {checks} live Story effect checks: typed196, app water/sky before damage, actual504/505 posed models, hand-before-foot trails, source flaming/Flat timing and natural cleanup.");
            return web;
        }
        finally
        {
            observing = false;
            player.Controller.BeforeDamageTimers -= BeforeDamage;
            player.Controller.AfterDamageTimers -= AfterDamage;
            melee.AfterPose -= AfterPose; melee.BeforeHeldObjectPlacement -= AfterPresentation;
            parts.BodyCreated -= BodyCreated; web.SmokeTrails!.OwnerCreated -= TrailCreated; web.Embers.SourceBitCreated -= EmberCreated;
            if (GodotObject.IsInstanceValid(player))
            {
                player.Inventory.State.FireWebbing = originalFire; player.Combat.State.Field36C = originalDamage;
                player.Combat.State.Field5AC = 0; effects.SenseKind = originalSenseKind;
                scene.Application.WaterEffect.Value = originalWater;
                player.Combat.State.ActionState = originalAction; player.Combat.State.Velocity = originalVelocity;
                player.Controller.ApplyCombatState(); melee.RunAnimation(originalAnimation.ClipId, originalAnimation.Frame, -1);
                player.Model.PlaybackSpeed = originalAnimation.Speed;
                if (originalAnimation.Mode == AnimationPlaybackMode.Loop) player.Model.CycleClip(originalAnimation.ClipId, originalAnimation.Direction);
                player.Model.ApplySourceJointPose(originalPoseEnabled); player.ActorState.ItemFlags = originalItemFlags;
                if (restored) originalHandOffsets.CopyTo(hands.State.PreviousOffsets, 0);
            }
        }

        void BeforeDamage()
        {
            if (!observing) return;
            Check(phase is 0 or 4 && frame < 2 && scene.Application.WaterEffect.Value == 0 &&
                scene.Rendering.SkyRestorations == restorations + frame + 1 && player.Combat.State.Field36C == 7 - frame * 2,
                "Bound water restoration must run before the actual damage-timer decrement on every source frame.");
            phase = 1; phaseOrder.Add("water");
            actorCount = scene.Registry.LiveCount; leaseCount = scene.Registry.BaddyConstructionState.Count;
        }
        void AfterDamage()
        {
            if (!observing) return;
            Check(phase == 1 && player.Combat.State.Field36C == 5 - frame * 2 && effects.SenseTicks == 2 - frame * 2 &&
                player.Combat.State.Field5B0 == (frame + 1) * 768 && parts.Count == (frame == 0 ? 3 : 2) &&
                scene.Application.WaterEffect.Value == (frame == 0 ? 1 : 0) &&
                (frame == 0 ? effects.SenseSoundHandle is not 0 and not uint.MaxValue : effects.SenseSoundHandle == 0),
                "Actual damage timers must precede504/505 preparation, with original elapsed2 sense expiry and bonus growth.");
            phase = 2; phaseOrder.Add("damage");
        }
        void BodyCreated(GodotPlayerAdditionalBody body)
        {
            if (!observing) return;
            Check(phase == 1 && body.State.BodyFlags == 0x16 && body.State.ModelIndex == (body.State.Type == 504 ? 4 : 8) &&
                scene.Registry.LiveCount == actorCount && scene.Registry.BaddyConstructionState.Count == leaseCount,
                "Real additional constructors must execute during post-damage preparation and use the source plain-body flags/items models.");
            createdBodies.Add(body);
        }
        void TrailCreated(SourceParticleRegistration bit)
        {
            if (!observing || bit.Identity is not SmokeTrail trail || trail.Segments.Length != 6) return;
            Check(phase == 2 && frame == 0 && smash.CurrentTrails.All(foot => foot is { Cursor: 0 }),
                "Both source hand constructors must run after preparation/pose and before the first posed foot-trail endpoint update.");
            handBirths++;
        }
        void AfterPose()
        {
            if (!observing) return;
            Check(phase == 2 && player.Model.Playback.ClipId == 106 && hands.State.Current.All(trail => trail is SmokeTrail) &&
                smash.CurrentTrails.Select((foot, index) => foot is not null && foot.Cursor == frame + 1 &&
                    foot.Points[foot.Cursor] == melee.GetPlayerHookPosition(index + 5)).All(value => value),
                "The genuine after-pose hook must finish both hand trails before updating both foot endpoints from current posed hooks5/6. " +
                $"phase={phase}, frame={frame}, clip={player.Model.Playback.ClipId}, action={player.Combat.State.ActionState:X}, " +
                $"programs={player.Scripts!.ProgramsStarted}, scriptActive={player.Scripts.Active}, " +
                $"programOffset={player.Scripts.CurrentProgram?.Instructions.FirstOrDefault().SourceOffset:X}, " +
                $"cleanup={player.Scripts.CutsceneSkip!.CleanupCount}, checkpoint={player.Scripts.Runtime?.CheckpointNode}, " +
                $"hands=[{string.Join(',', hands.State.Current.Select(trail => trail?.GetType().Name ?? "null"))}], " +
                $"feet=[{string.Join(';', smash.CurrentTrails.Select((foot, index) => foot is null ? "null" :
                    $"cursor={foot.Cursor}, point={foot.Points[foot.Cursor]}, hook={melee.GetPlayerHookPosition(index + 5)}"))}].");
            phase = 3; phaseOrder.Add("pose");
        }
        void EmberCreated(SourceParticleRegistration bit)
        {
            if (!observing) return;
            var ember = (FireEmber)bit.Identity;
            int side = births.Count(birth => birth.Frame == frame) / 3;
            Check(phase == 3 && side < 2 && ember.Position == melee.GetPlayerHookPosition(side == 0 ? 1 : 0) &&
                parts.SourceOrder.Where(body => body.State.Type == 505).All(body => body.State.Position == melee.GetPlayerHookPosition(
                    ReferenceEquals(body, parts.SourceOrder[0]) ? 3 : 4) && body.Model.RenderMesh.GetSurfaceCount() > 0),
                "Genuine source flaming presentation must follow the posed505 updates and emit three left then three right unadjusted hand particles.");
            births.Add(new(ember, frame, ember.OrbitPhase, ember.AmplitudePhase, ember.FadeDelay, ember.Position));
        }
        void AfterPresentation(ref PlayerHandHooks posedHands)
        {
            if (!observing) return;
            PlayerKickHandle handle = effects.SenseBody;
            GodotPlayerAdditionalBody? sense = parts.Recover(ref handle);
            Check(phase == 3 && births.Count(birth => birth.Frame == frame) == 6 &&
                (frame == 0 ? sense is not null && sense.State.Position == melee.GetPlayerHookPosition(8) && sense.Model.RenderMesh.GetSurfaceCount() > 0 : sense is null) &&
                posedHands.Left == melee.GetPlayerHookPosition(1) && posedHands.Right == melee.GetPlayerHookPosition(0),
                "Actual posed sense/fist/flaming presentation must finish before held placement without fabricating or adjusting hands in this nonswing source state.");
            phase = 4; phaseOrder.Add("presentation"); frame++;
            if (frame == 2) observing = false;
        }
        async Task Until(Func<bool> complete)
        {
            var deadline = Stopwatch.StartNew();
            while (!complete())
            {
                if (scene.Failure is { } failure) throw new InvalidDataException("Live source effect proof stopped the actual Story scene.", failure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                    throw new TimeoutException($"Actual player effect phase did not complete: sourceUpdates={player.SourceUpdates}, frame={frame}, phase={phase}, parts={parts.Count}, ticks={effects.SenseTicks}, bonus={player.Combat.State.Field5B0}.");
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        void Check(bool condition, string evidence) { checks++; Assert(condition, evidence); }
    }

    public static void VerifyTeardown(StoryLevelSession scene, GodotPlayerWebRuntime web)
    {
        Assert(scene.SceneObjects.IsDestroyed && scene.Player is null && !GodotObject.IsInstanceValid(web) &&
            !web.PlayerEffectPhasesEnabled && web.AdditionalBodies is { Count: 0 } && web.HandTrails!.State.Current.All(trail => trail is null),
            "Actual Init_KillAll must destroy real player/web/additional owners and unsubscribe all live source effect phases on genuine Predelete.");
        GD.Print("PASS: actual Story teardown removes the bound player-effect hooks and all additional-body/hand owner pointers.");
    }

    private static void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException(evidence); }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
