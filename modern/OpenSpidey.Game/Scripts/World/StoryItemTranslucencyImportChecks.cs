using System.Diagnostics;
using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Live Story proof of CItem flag800: the PC suit-5 switch through real AI frames, rendered player/ENV materials and camera F9 item hiding.</summary>
internal static class StoryItemTranslucencyImportChecks
{
    public static async Task VerifyAsync(StoryLevelSession scene)
    {
        GodotPlayer player = scene.Player ?? throw new InvalidDataException("Translucency proof requires the actual Story player.");
        PlayerFollowCamera camera = Descendants(scene).OfType<PlayerFollowCamera>().Single();
        GodotCameraItemHiding hiding = camera.SourceHideItems?.Target as GodotCameraItemHiding ??
            throw new InvalidDataException("The Story camera must own the native F9 item-hiding provider.");
        GodotPlayerCostumeVisibility costume = player.CostumeVisibility;
        Func<int>? suit = player.Combat.Suit;
        Func<ushort, bool> keyboard = costume.SampleKeyboardAction;
        byte armor = player.Combat.State.SuitArmorEnabled;
        bool pressed = false;
        try
        {
            Assert(armor == 0 && player.Combat.State.FieldE18 == 0 && player.Scripts?.Active == false,
                "The live costume proof starts from the original unarmored, unscripted player.");
            player.Combat.Suit = () => 5;
            costume.SampleKeyboardAction = action => pressed && action is 0x40 or 0x100;
            pressed = true;
            await Until(() => player.Combat.State.SuitArmorEnabled == 1, "suit-5 keyboard actions0x40+0x100 toggle player57C on");
            Assert((player.ActorState.ItemFlags & 0x800) != 0 && player.PhysicalPad[5].Triggered == 0,
                "The actual AI frame sets CItem flag800 from player57C and clears the pad LeftTwo trigger.");
            await Frame();
            Assert((player.ActorState.ItemFlags & 0x800) != 0, "Player57C keeps flag800 through the next AI and camera frame.");
            player.Model.VerifySourceTranslucency(SourceItemTranslucency.ModelAlpha);
            pressed = false;
            await Until(() => true, "release");
            await Frame();
            Assert(player.Combat.State.SuitArmorEnabled == 1, "A held key is not a new source keyboard press.");
            pressed = true;
            await Until(() => player.Combat.State.SuitArmorEnabled == 0, "a second new press toggles player57C off");
            pressed = false;
            await Frame();
            player.Model.VerifySourceTranslucency(SourceItemTranslucency.Alpha(player.ActorState.ItemFlags));
            player.ActorState.ItemFlags &= 0xF7FF; player.SynchronizeSourceTranslucency();
            player.Model.VerifySourceTranslucency(null);
        }
        finally
        {
            pressed = false;
            player.Combat.Suit = suit; costume.SampleKeyboardAction = keyboard;
            player.Combat.State.SuitArmorEnabled = armor;
        }

        GodotEnvironmentVisibility visibility = hiding.Visibility;
        GodotActorWorldQuery world = hiding.World() ?? throw new InvalidDataException("Item hiding requires the Story line-query owner.");
        (GodotEnvironmentVisibilityItem Item, ImportedSceneMesh Mesh, FixedVector3 From, FixedVector3 To)? target = null;
        foreach (GodotEnvironmentVisibilityItem item in visibility.SourceItems)
        {
            if ((item.ItemFlags & 0x1801) != 0 || visibility.NativeMesh(item) is not { Visible: true } mesh) continue;
            Aabb box = mesh.GetAabb(); Transform3D transform = mesh.GlobalTransform;
            Vector3 centre = transform * box.GetCenter();
            FixedVector3 from = PsxCoordinates.OriginalWorldPosition(centre + Vector3.Up * (box.Size.Y + 2f));
            FixedVector3 to = PsxCoordinates.OriginalWorldPosition(centre);
            if (world.Trace(from, to) is { SupportId: null } hit && hit.InstanceIndex == item.InstanceIndex &&
                item.Source.AssetPath.Equals(hit.SourceAsset, StringComparison.OrdinalIgnoreCase))
            { target = (item, mesh, from, to); break; }
        }
        (GodotEnvironmentVisibilityItem envItem, ImportedSceneMesh envMesh, FixedVector3 rayFrom, FixedVector3 rayTo) =
            target ?? throw new InvalidDataException("No visible collidable L1A1 ENV item answered a downward mode1 ray.");
        ushort envFlags = envItem.ItemFlags;
        float envAlpha = envItem.ChunkModel is { } chunk && (chunk.RendererFlags & 0x4000) != 0
            ? SourceItemTranslucency.PsxModelAlpha : SourceItemTranslucency.ModelAlpha;
        CameraModeState mode = camera.ScriptRig.ModeState;
        FixedVector3 cameraPosition = mode.Position, tripodPosition = camera.Tripod.Position;
        try
        {
            visibility.WriteSourceItemFlags(envItem, (ushort)(envFlags | 0x800));
            await Frame();
            envMesh.VerifySourceTranslucency(envAlpha);
            visibility.WriteSourceItemFlags(envItem, envFlags);
            await Frame();
            envMesh.VerifySourceTranslucency(null);

            mode.Position = rayFrom; camera.Tripod.Position = rayTo;
            hiding.HideItems();
            mode.Position = cameraPosition; camera.Tripod.Position = tripodPosition;
            var hidden = Enumerable.Range(0, CameraHiddenItems<object>.Capacity).Select(hiding.Items.GetItem).OfType<object>().ToArray();
            Assert(hidden.Length > 0 && ReferenceEquals(hidden[0], envItem) && (envItem.ItemFlags & 0x800) != 0,
                $"Camera F9 hides the actual first mode1 ENV hit by setting its flag800; hidden={hidden.Length}.");
            await Frame();
            envMesh.VerifySourceTranslucency(envAlpha);
            mode.Position = camera.Tripod.Position = rayTo;
            hiding.HideItems();
            mode.Position = cameraPosition; camera.Tripod.Position = tripodPosition;
            Assert(hiding.Items.GetItem(0) is null && envItem.ItemFlags == envFlags,
                "The next F9 pass restores every same-region/model hidden item's original flags when its ray finds nothing.");
            await Frame();
            envMesh.VerifySourceTranslucency(null);
        }
        finally
        {
            mode.Position = cameraPosition; camera.Tripod.Position = tripodPosition;
            for (int index = 0; index < CameraHiddenItems<object>.Capacity; index++) hiding.Items.ClearItem(index);
            if (visibility.IsCurrentSourceItem(envItem)) visibility.WriteSourceItemFlags(envItem, envFlags);
        }
        GD.Print($"PASS: live suit-5 keyboard switch drives player57C/flag800 through real AI frames; player and ENV{envItem.InstanceIndex} render PC mode2 additive alpha{envAlpha} and restore opaque; camera F9 hides and restores the actual mode1 ENV hit.");

        async Task Frame()
        {
            if (scene.Failure is { } failure) throw new InvalidDataException("Translucency proof stopped the actual Story scene.", failure);
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (scene.Failure is { } stopped) throw new InvalidDataException("Translucency proof stopped the actual Story scene.", stopped);
        }
        async Task Until(Func<bool> complete, string phase)
        {
            var deadline = Stopwatch.StartNew();
            do
            {
                await Frame();
                if (deadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException($"Live translucency phase did not complete: {phase}.");
            } while (!complete());
        }
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
