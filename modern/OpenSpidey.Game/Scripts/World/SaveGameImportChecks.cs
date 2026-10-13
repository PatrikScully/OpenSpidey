using System.Text.Json.Nodes;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.World;

/// <summary>Actual native save choices, held input, private atomic files, reload validation and destruction cancellation.</summary>
internal static class SaveGameImportChecks
{
    public static async Task VerifyAsync(Node owner, GameSessionState application, SoundChannelController sounds, PkrArchive archive)
    {
        string directory = "user://tests/save-" + Guid.NewGuid().ToString("N");
        var store = new GameSaveStore(directory + "/progress.json");
        GameSaveDocument original = GameSaveDocument.Capture(application);
        GodotSaveConfirmation? confirmation = GodotSaveConfirmation.Create(application, store, sounds);
        GodotCampaignFade? fade = null;
        GameSessionState? restored = null;
        bool acceptHeld = Input.IsActionPressed("ui_accept");
        string? jump = OpenSpidey.Game.Player.PlayerControls.SourceActionName(0x10);
        bool jumpHeld = jump is not null && Input.IsActionPressed(jump);
        Input.MouseModeEnum mouse = Input.MouseMode;
        owner.AddChild(confirmation);
        try
        {
            if (await store.LoadAsync() is not null) throw new InvalidDataException("A unique private save proof path already contains data.");
            Input.ActionPress("ui_accept");
            if (jump is not null) Input.ActionPress(jump);
            Task<bool> decision = confirmation.RequestSaveAsync();
            await Frame(owner); await Frame(owner);
            ConfirmationDialog dialog = confirmation.Dialog ?? throw new InvalidDataException("The real save dialog is missing.");
            if (confirmation.InputReady || !dialog.GetOkButton().Disabled || decision.IsCompleted || File.Exists(store.NativePath))
                throw new InvalidDataException("Held source accept input bypassed the original release barrier or wrote without confirmation.");
            dialog.GetOkButton().EmitSignal(BaseButton.SignalName.Pressed);
            await Frame(owner);
            if (decision.IsCompleted || File.Exists(store.NativePath)) throw new InvalidDataException("An unarmed native confirmation wrote a save.");
            Input.ActionRelease("ui_accept");
            if (jump is not null) Input.ActionRelease(jump);
            await Ready(owner, confirmation);
            dialog.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed);
            if (await decision || File.Exists(store.NativePath)) throw new InvalidDataException("Declining the actual native dialog wrote a private save.");

            decision = confirmation.RequestSaveAsync();
            await Ready(owner, confirmation);
            dialog.GetOkButton().EmitSignal(BaseButton.SignalName.Pressed);
            if (!await decision) throw new InvalidDataException("The actual accepted native save did not complete its durable write.");
            GameSaveDocument saved = await new GameSaveStore(store.UserPath).LoadAsync()
                ?? throw new InvalidDataException("A new store owner could not reload the accepted private save.");
            if (!Same(saved, original)) throw new InvalidDataException("Durable reload changed real carried or campaign fields.");
            byte[] acceptedBytes = File.ReadAllBytes(store.NativePath);
            JsonObject json = JsonNode.Parse(acceptedBytes)?.AsObject() ?? throw new InvalidDataException("Accepted save is not actual JSON.");
            foreach (string excluded in new[] { "previous", "sceneAsset", "triggerSweep", "renderTest", "gameLevel", "assets" })
                if (json.ContainsKey(excluded)) throw new InvalidDataException($"Private save incorrectly persisted runtime field {excluded}.");
            decision = confirmation.RequestSaveAsync();
            await Ready(owner, confirmation);
            dialog.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed);
            if (await decision || !File.ReadAllBytes(store.NativePath).SequenceEqual(acceptedBytes))
                throw new InvalidDataException("Declining a second confirmation modified the existing durable file.");

            int[] counts = saved.CompletionCounts.ToArray(); counts[33] = 255;
            GameSaveDocument changed = saved with
            {
                CarriedPlayer = saved.CarriedPlayer with { Webbing = int.MinValue, Cartridges = int.MaxValue, ArmourUnlocked = 170, ArmourOn = 187 },
                CompletionCounts = counts, CostumeUnlockFlags = 0x80000100, CharacterViewerUnlockFlags = uint.MaxValue
            };
            await store.SaveAsync(changed);
            GameSaveDocument reloaded = await new GameSaveStore(store.UserPath).LoadAsync()
                ?? throw new InvalidDataException("Atomic replacement lost the private save.");
            if (!Same(reloaded, changed) || Directory.GetFiles(ProjectSettings.GlobalizePath(directory)).Length != 1)
                throw new InvalidDataException("Atomic save replacement changed raw source values or left an unfinished sibling file.");
            restored = new GameSessionState { Name = "RestoredSaveProof" };
            owner.AddChild(restored);
            var previous = new PlayerCarriedSaveState(17, -18, 19, 128, 255);
            restored.CarriedHistory = new(default, previous);
            PlayerTriggerZoneSweep sweep = restored.TriggerSweep;
            changed.RestoreProgress(restored);
            string geometry = GodotMissionLevelTransitions.ResolveAuthoredGeometry(archive, changed.MissionName);
            restored.SelectMission(changed.MissionName, geometry);
            var checkpoint = restored.OpenLevel(geometry, "IgnoredLaunchRestart");
            if (restored.CarriedHistory.Saved != changed.CarriedPlayer.ToState() || restored.CarriedHistory.Previous != previous ||
                !ReferenceEquals(sweep, restored.TriggerSweep) || checkpoint.RestartPointName != changed.RestartPointName ||
                !restored.Campaign.CompletionCounts.SequenceEqual(changed.CompletionCounts.Select(count => (byte)count)) ||
                restored.Difficulty != changed.Difficulty || restored.MovieViewerUnlockFlags != changed.MovieViewerUnlockFlags)
                throw new InvalidDataException("Actual application restore lost durable fields, reused launch restart or reset independent runtime history.");
            (changed with { RestartPointName = "" }).RestoreProgress(restored);
            if (restored.OpenLevel(geometry, "IgnoredLaunchRestart").RestartPointName != "")
                throw new InvalidDataException("An empty original saved restart became a guessed launch checkpoint.");

            byte[] committed = File.ReadAllBytes(store.NativePath);
            bool rejected = false;
            try { await store.SaveAsync(changed with { CompletionCounts = new int[33] }); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected || !File.ReadAllBytes(store.NativePath).SequenceEqual(committed))
                throw new InvalidDataException("Invalid save fields replaced a prior committed file.");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); rejected = false;
                try { await store.SaveAsync(original, cancellation.Token); }
                catch (OperationCanceledException) { rejected = true; }
                if (!rejected || !File.ReadAllBytes(store.NativePath).SequenceEqual(committed))
                    throw new InvalidDataException("Cancelled durable write changed an earlier committed save.");
            }
            foreach (byte[] corrupt in Corruptions(committed))
            {
                File.WriteAllBytes(store.NativePath, corrupt); rejected = false;
                try { await store.LoadAsync(); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected || !File.ReadAllBytes(store.NativePath).SequenceEqual(corrupt))
                    throw new InvalidDataException("Malformed private save was accepted or silently overwritten.");
            }
            await store.SaveAsync(changed);
            decision = confirmation.RequestSaveAsync();
            await Ready(owner, confirmation);
            confirmation.Free(); confirmation = null;
            rejected = false;
            try { await decision; }
            catch (OperationCanceledException) { rejected = true; }
            if (!rejected || !Same((await store.LoadAsync())!, changed))
                throw new InvalidDataException("Actual confirmation-owner destruction failed to cancel its pending choice safely.");

            fade = new GodotCampaignFade { Name = "SaveFadeProof" };
            owner.AddChild(fade);
            Task fading = fade.BeginSepiaAsync();
            if (fade.State.Word != SourceGameFadeState.SepiaWord)
                throw new InvalidDataException("Native sepia did not begin the original packed fade word.");
            await fading;
            if (fade.State.Active || fade.VblanksAdvanced != 120)
                throw new InvalidDataException("Native completion fade did not wait exactly120 source vblanks.");
            fade.HideSnapshot();
            fading = fade.BeginSepiaAsync();
            fade.Free(); fade = null; rejected = false;
            try { await fading; }
            catch (OperationCanceledException) { rejected = true; }
            if (!rejected) throw new InvalidDataException("Actual fade owner destruction did not cancel its pending native completion barrier.");
            GD.Print("PASS: real native save accept/decline, held-input barrier, flushed atomic private JSON, raw campaign/carried reload, corruption rejection, independent history, source120-vblank sepia and actual destruction cancellation.");
        }
        finally
        {
            if (confirmation is not null && GodotObject.IsInstanceValid(confirmation)) confirmation.Free();
            if (fade is not null && GodotObject.IsInstanceValid(fade)) fade.Free();
            if (restored is not null && GodotObject.IsInstanceValid(restored)) restored.Free();
            if (acceptHeld) Input.ActionPress("ui_accept"); else Input.ActionRelease("ui_accept");
            if (jump is not null) { if (jumpHeld) Input.ActionPress(jump); else Input.ActionRelease(jump); }
            Input.MouseMode = mouse;
            string nativeDirectory = ProjectSettings.GlobalizePath(directory);
            if (Directory.Exists(nativeDirectory)) Directory.Delete(nativeDirectory, recursive: true);
        }
    }

    private static bool Same(GameSaveDocument left, GameSaveDocument right) =>
        left.CompletionCounts.SequenceEqual(right.CompletionCounts) && (left with { CompletionCounts = right.CompletionCounts }) == right;

    private static IEnumerable<byte[]> Corruptions(byte[] original)
    {
        yield return "{\"version\":1"u8.ToArray();
        JsonObject missing = JsonNode.Parse(original)!.AsObject(); missing.Remove("missionName");
        yield return System.Text.Encoding.UTF8.GetBytes(missing.ToJsonString());
        JsonObject unknown = JsonNode.Parse(original)!.AsObject(); unknown["previous"] = 17;
        yield return System.Text.Encoding.UTF8.GetBytes(unknown.ToJsonString());
        JsonObject future = JsonNode.Parse(original)!.AsObject(); future["version"] = 999;
        yield return System.Text.Encoding.UTF8.GetBytes(future.ToJsonString());
        yield return new byte[65537];
    }

    private static async Task Ready(Node owner, GodotSaveConfirmation confirmation)
    {
        for (int frame = 0; frame < 12 && !confirmation.InputReady; frame++) await Frame(owner);
        if (!confirmation.InputReady) throw new InvalidDataException("Released actual confirm input did not arm its native dialog.");
    }

    private static async Task Frame(Node owner) { await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame); }
}
