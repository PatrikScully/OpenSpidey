using System.Buffers.Binary;
using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Terminal verification-only switches over genuine posed foot trails, protected CDome pieces and two authored L1A3 held401 bodies.</summary>
internal static class PlayerSynthesizedInputImportChecks
{
    public static void Verify(StoryLevelSession story)
    {
        GodotPlayer player = story.Player ?? throw new InvalidDataException("The source switch proof requires the actual story player.");
        GodotPlayerScripts scripts = player.Scripts ?? throw new InvalidDataException("The actual player has no input VM.");
        GodotPlayerCombatRuntime combat = Descendants(story).OfType<GodotPlayerCombatRuntime>().Single();
        GodotPlayerWebRuntime web = Descendants(story).OfType<GodotPlayerWebRuntime>().Single();
        GodotPlayerMelee melee = player.Melee!;
        GodotPlayerSmashAttack smash = web.Smash!;
        GodotWebDomes domes = web.Domes!;
        GodotManipulationActor[] items = story.Registry.SourceActors(ActorUpdateGroup.Environment).OfType<GodotManipulationActor>().Take(2).ToArray();
        Assert(items.Length == 2 && combat.Session.HeldActor is null && melee.Kick.HeldObject is null && !player.Combat.State.HeldObjectPresent,
            "The proof retains two genuine authored L1A3 bodies and begins without a substituted held pointer.");
        var sourceHost = new GodotActorPlayerScriptHost(() => player);
        if (player.Controller.Lookaround is not GodotPlayerLookaroundRuntime lookaround)
            throw new InvalidDataException("The source470D proof requires the actual player/camera lookaround owner.");
        lookaround.Camera.EnterLookaroundMode();
        Assert(player.Combat.State.LookaroundActive, "The real source mode7/lookaround entry is active before470D.");
        var timer = new ActorScriptCursor(Words([0, 8000]), 256);
        ActorPlayerScriptCommands.TryExecute(0x470D, timer, sourceHost);
        Assert(timer.Position == 2 && player.Combat.State.FieldE18 == 0 && player.Combat.State.LookaroundActive,
            "Original470D zero consumes one word without saving speed or exiting genuine lookaround.");
        short speed = unchecked((short)player.Animation.Speed);
        ActorPlayerScriptCommands.TryExecute(0x470D, timer, sourceHost);
        Assert(timer.Position == 4 && player.Combat.State.FieldE18 == 8000 && player.Controller.InputSuppression.RemainingTicks == 8000 &&
            player.Combat.State.SavedInputAnimationSpeed == speed && player.Controller.InputSuppression.SavedAnimationSpeed == speed && !player.Combat.State.LookaroundActive,
            "Original470D8000 publishes the same E18/E12 fields read by damage/HUD and exits the actual lookaround/camera owner.");

        int programs = scripts.ProgramsStarted, checks = 4;
        foreach ((GodotManipulationActor item, uint action) in items.Zip(new uint[] { 1, 16 }))
        {
            melee.PlaySingleAnimation(0x11C, 0, -1); player.Model.SetSourceAnimationFrame(4); player.Controller.CaptureCombatState(2);
            PlayerKickHandle handle = domes.Spawn(0); web.Attack!.State.DomeHandle = handle;
            story.Registry.SourceList(ActorUpdateGroup.Miscellaneous).Update();
            WebDome dome = domes.Recover(ref handle) ?? throw new InvalidDataException("The actual AB8 source dome failed validation.");
            WebDomePiece[] pieces = dome.Pieces.Cast<WebDomePiece>().ToArray();
            GodotWebDomeModel[] models = new[] { (object)dome }.Concat(pieces).Select(domes.ModelFor).ToArray();
            Assert(pieces.Length == 5 && pieces.All(piece => (piece.BodyFlags & 0x20) != 0) && models.All(GodotObject.IsInstanceValid),
                "Positive AB8 switching owns its actual dome and five protected source36 companion model resources."); checks++;
            melee.ApplyPlayerPose(); smash.CreateSmashAttackTrails();
            SmokeTrail[] trails = smash.CurrentTrails.Cast<SmokeTrail>().ToArray();
            Assert(trails.Length == 2 && trails.All(trail => trail.FadeAway == 0 && !trail.IsDisposed),
                "Both actual source584/588 foot pointers retain live trail owners before the script switch."); checks++;
            var owned = item.CaptureOwnedResources();
            combat.Session.Hold(item); item.Pickup(); melee.Kick.HeldObject = item.KickObject;
            player.Combat.State.ActionState = action; player.Combat.State.FieldAE5 = 1;
            scripts.SkipState.SetAllowed(1); player.Controller.SetScriptVelocity(new(111, -222, 333));
            for (int slot = 0; slot < 20; slot++)
            {
                player.PhysicalPad[slot] = new(1, 1, 1);
                melee.State.Input.Pressed[slot] = melee.State.Input.Triggered[slot] = melee.State.Input.Mirrors[slot] = 1;
            }
            // This is a bounded verification input, independent of untouched authored startup/player streams.
            var embedded = new ActorScriptCursor(Words([0, 9, 2, 0, 255, 0x42B4, 122, 0x4100]), 512);
            ActorPlayerScriptCommands.TryExecute(0x470E, embedded, sourceHost);
            Assert(embedded.Position == 10 && embedded.PeekWord() == 0x42B4 && scripts.ProgramsStarted == ++programs && scripts.Active &&
                scripts.Runtime is { Decoding: true, CheckpointNode: 0 } && player.Combat.State.SynthesizedInputActive &&
                player.Controller.Velocity == default && player.Combat.State.Velocity == default && player.Combat.State.FieldAE5 == 0 && scripts.SkipState.Allowed == 0,
                "The real470E commits after the embedded zero/FF stream, resets original velocity/AI flags/checkpoint and keeps later actor42B4 outside the player VM."); checks++;
            Assert(trails.All(trail => trail.FadeAway == 1 && !trail.IsDisposed) && smash.CurrentTrails.All(trail => trail is null) &&
                !player.Combat.State.FirstSmokeTrailActive && !player.Combat.State.SecondSmokeTrailActive,
                "Script switching marks both original foot trails to fade and clears their player pointers while their shared particle owners remain live."); checks++;
            Assert(dome.IsDisposed && web.Attack.State.DomeHandle == default && models.All(model => model.ResourcesDisposed && !GodotObject.IsInstanceValid(model)) &&
                !story.Registry.SourceList(ActorUpdateGroup.Miscellaneous).SourceOrder.Any(body => ReferenceEquals(body, dome) || pieces.Any(piece => ReferenceEquals(body, piece))),
                "AB8 cleanup executes actual dome/piece deleting destructors, releases protected companion native resources and clears the complete handle."); checks++;
            Assert(!GodotObject.IsInstanceValid(item) && item.ResourcesDisposed && owned.Materials.IsDisposed && owned.Resources.All(resource => !GodotObject.IsInstanceValid(resource)) &&
                combat.Session.HeldActor is null && melee.Kick.HeldObject is null && !player.Combat.State.HeldObjectPresent && !story.Registry.Owns(item),
                "The actual held401 deleting destructor clears session/list/handle ownership and all private mesh/material/shape resources without generating a Smash explosion."); checks++;
            Assert(player.Model.ClipId == ((action & 16) != 0 ? 21 : 0) && player.Model.Playback.Frame == 0 &&
                player.PhysicalPad.All(button => button == default) && melee.State.Input.Pressed.All(value => value == 0) &&
                melee.State.Input.Triggered.All(value => value == 0) && melee.State.Input.Mirrors.All(value => value == 0),
                "Held cleanup selects original clip0/21 and resets all twenty genuine pad records and their source input aliases."); checks++;
            PlayerKickHandle stale = handle;
            Assert(domes.Recover(ref stale) is null && stale.Target is null && stale.Validation == handle.Validation,
                "Destroyed AB8 generations fail actual handle validation while preserving the validation word."); checks++;
            web.Attack.State.DomeHandle = stale;
            int animation = player.Model.ClipId;
            scripts.StartEmbedded(Words([0, 9, 2, 0, 255]), 1024);
            programs++;
            Assert(web.Attack.State.DomeHandle.Target is null && web.Attack.State.DomeHandle.Validation == stale.Validation && player.Model.ClipId == animation,
                "A stale AB8 pointer keeps its original validation word and an absent held object does not invent a sound reset or animation change."); checks++;
        }
        melee.ApplyPlayerPose(); player.Model.VerifyCurrentPose();
        GD.Print($"Passed {checks} native player script-switch checks: actual470D/lookaround/E18/E12, embedded470E, fading584/588, protectedAB8 destruction, two real held401 destructor/clip paths and20pad aliases. These terminal probes do not claim ordinary campaign traversal.");
    }

    private static byte[] Words(ushort[] words)
    {
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        return bytes;
    }
    private static void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException("Native player script switch: " + evidence); }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
