using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Authored L1A3 restart183 values enter the actual native lookaround state/path and placement owner, then restore the borrowed player state.</summary>
internal static class PlayerLookaroundCameraValueImportChecks
{
    public static void Verify(Node root, GodotPlayer player, PlayerFollowCamera camera, GodotPlayerCombatRuntime combat,
        GodotPlayerWebRuntime webs, PkrArchive archive)
    {
        TriggerFile file = TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
        TriggerNode restart = file.Nodes[1];
        TriggerInstruction[] commands = TriggerCommandReader.Read(restart).Where(instruction => instruction.Opcode == 183).ToArray();
        var target = new GodotScreenTarget { Name = "AuthoredLookaroundPitchProof", Camera = camera };
        root.AddChild(target);
        GodotPlayerLookaroundAim aim = webs.Lookaround ?? throw new InvalidDataException("Source183 native proof requires the real player lookaround owner.");
        var lookaround = new GodotPlayerLookaroundCamera(player, aim, camera, combat.World, target.SetTargetVisible);
        PlayerLookaroundState state = aim.State;
        GodotPlayerMelee melee = player.Melee!;
        if (state.Active || state.EntranceTicks != 0 || state.ExitTicks != 0)
            throw new InvalidDataException("Source183 native proof must borrow an inactive real lookaround owner.");
        var old = (state.FloorPitch, state.WallPitch, state.CeilingPitch, state.Pitch, state.Yaw, state.SmoothedPitch,
            state.SmoothedYaw, state.PreviousHeading, state.HoldTicks, state.TransitionFade, state.HeadingTurnTicksRemaining);
        byte wall = player.Combat.State.Field8E8, ceiling = player.Combat.State.Field8E9, surface = melee.Kick.SurfaceTransition;
        var neck = player.Model.GetSourceJoint(1); var head = player.Model.GetSourceJoint(3);
        Action<int>? oldBehind = player.Combat.CameraBehind;
        player.Combat.CameraBehind = camera.PutBehind;
        int checks = 0;
        try
        {
            Assert(commands.Length == 2 && Word(commands[0], 0) == 0 && Word(commands[0], 2) == 0 && Signed(commands[0], 4) == 100 &&
                Word(commands[1], 0) == 2 && Word(commands[1], 2) == 0 && Signed(commands[1], 4) == -800,
                "Original L1A3 restart1 must retain its genuine183 floor100 and ceiling−800 operands in authored order.");
            int pitch = state.Pitch;
            var handler = new PlayerLookaroundCameraValueCommandHandler(() => aim.State, () => GodotObject.IsInstanceValid(player));
            foreach (TriggerInstruction instruction in commands) handler.Execute(new(restart, 0), instruction);
            Assert(ReferenceEquals(lookaround.State.Angles, aim.State) && state.FloorPitch == 100 && state.CeilingPitch == -800 &&
                state.WallPitch == old.WallPitch && state.Pitch == pitch && !state.Active,
                "Authored183 must update the same native setup profile owner, retaining signed values without moving camera or changing current pitch.");
            player.Combat.State.Field8E8 = player.Combat.State.Field8E9 = 0;
            lookaround.EnterLookaroundMode();
            Assert(state.Active && state.Pitch == 100 && lookaround.State.Path is { Length: 24 } && camera.SourceControlMode == 7,
                "Genuine native floor entry must load authored100 from183 into the actual source7/24-sample lookaround transition.");
            handler.Execute(new(restart, 0), new(183, 0, new byte[] { 0, 0, 0, 0, 123, 0 }));
            Assert(state.FloorPitch == 123 && state.Pitch == 100 && state.Active,
                "A source183 profile change during active lookaround must affect the next entry while preserving the current camera pitch/path.");
            handler.Execute(new(restart, 0), commands[0]);
            PlayerLookaroundCameraSetupResult floor = lookaround.UpdateCameraPlacement(2);
            Assert(floor.Anchor == player.Controller.Position + lookaround.State.Offset && state.Pitch == 100 &&
                camera.SourceControlMode == 7 && lookaround.State.Path is { Length: 24 },
                "The actual native setup/world-clearance owner must consume the same floor100 pitch through its existing posed player anchor and transition.");
            lookaround.ExitLookaroundMode();
            player.Combat.State.Field8E9 = 1;
            lookaround.EnterLookaroundMode();
            Assert(state.Active && state.Pitch == -800 && camera.SourceControlMode == 7,
                "Genuine native ceiling-profile entry must retain raw signed−800 rather than masking it or reading a copied default170 profile.");
            lookaround.UpdateCameraPlacement(2);
            Assert(state.Pitch == -800 && lookaround.State.Path is { Length: 24 } && camera.SourceControlMode == 7,
                "Actual setup's scalar/quaternion/world-query phase must retain the authored ceiling pitch while advancing the genuine transition.");
            lookaround.ExitLookaroundMode();
            Assert(!state.Active && lookaround.State.Path is null && !target.State.Visible,
                "Both temporary native profile entries must retire their real paths and target through source exit.");
            GD.Print($"PASS: {checks} native source183 checks: original L1A3 floor100/ceiling−800, one actual setup owner, active-profile retention, genuine source7 entry/placement and path cleanup.");
        }
        finally
        {
            if (state.Active) lookaround.ExitLookaroundMode();
            (state.FloorPitch, state.WallPitch, state.CeilingPitch, state.Pitch, state.Yaw, state.SmoothedPitch,
                state.SmoothedYaw, state.PreviousHeading, state.HoldTicks, state.TransitionFade, state.HeadingTurnTicksRemaining) = old;
            player.Combat.State.Field8E8 = wall; player.Combat.State.Field8E9 = ceiling; melee.Kick.SurfaceTransition = surface;
            player.Model.SetSourceJoint(1, neck); player.Model.SetSourceJoint(3, head);
            player.Combat.CameraBehind = oldBehind; target.Free();
        }
        void Assert(bool condition, string evidence) { checks++; if (!condition) throw new InvalidDataException(evidence); }
        static ushort Word(TriggerInstruction instruction, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span[offset..]);
        static short Signed(TriggerInstruction instruction, int offset) => BinaryPrimitives.ReadInt16LittleEndian(instruction.Arguments.Span[offset..]);
    }
}
