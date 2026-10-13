using Godot;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual held/edge button routing through the original L1A1 player's live web composition.</summary>
internal static class PlayerWebManualInputImportChecks
{
    public static void Verify(GodotPlayer player, GodotPlayerWebRuntime webs, GodotPlayerCombatRuntime combat,
        Action<PlayerInputSample, PlayerButtonInput> step)
    {
        GodotPlayerMelee melee = player.Melee ?? throw new InvalidOperationException("Manual web input proof requires native melee.");
        webs.ConfigureAttack(_ => throw new InvalidOperationException("This bounded ordinary input proof excludes the separate source dome constructor."));
        GodotPlayerWebAttack attack = webs.Attack!;
        GodotPlayerLookaroundAim lookaround = webs.Lookaround!;
        player.Controller.Web = webs;
        player.Inventory!.State.FireWebbing = 0;
        PlayerInputSample up = new(true, new(-127, 0), false, false, false, false, false, false);
        step(up, new(PlayerButton.Web | PlayerButton.Jump, PlayerButton.Web | PlayerButton.Jump));
        Assert(player.Controller.Action == PlayerAction.WebAttach && player.Model.ClipId == 0xFF &&
            melee.State.WebAttackMode == 4 && melee.State.Input.Pressed[16] == 1 && melee.State.Input.Pressed[17] == 1 &&
            melee.State.Input.Sideways == -127 && melee.State.Input.Forward == 0,
            "Actual held Web slot17 and physical forward E2D must select source10000/clip255 before the simultaneously triggered Jump slot16.");
        Assert(melee.State.Input.Triggered[16] == 0 && melee.State.Input.Mirrors[16] == 0,
            "The final source frame reset must clear the competing Jump edge after the earlier directional web branch wins.");
        for (int frame = 0; frame < 300 && player.Controller.Action != PlayerAction.Idle; frame++) step(default, default);
        Assert(player.Controller.Action == PlayerAction.Idle, "Actual attach/recovery and any retained Jump must complete through the real terrain/action owners.");
        step(default, default);
        Assert(attack.State.ButtonLatched == 0 && melee.State.Input.Pressed[16] == 0 && melee.State.Input.Pressed[17] == 0,
            "Physical release must clear held native slots and the original web edge latch on idle CheckWebShot.");
        Assert(webs.Actions.State.CurrentStrand is null && !player.Combat.State.WebPresent,
            "Manual attach recovery must not create a second player current-web owner.");
        for (int frame = 0; frame < 180 && combat.Particles.Count != 0; frame++) step(default, default);
        Assert(combat.Particles.Count == 0, "Actual directional web projectile/drips must retire in the production source particle phases.");
        VerifyScalarAim(player, melee, attack, lookaround);
        GD.Print("Verified manual native Web17/Jump16 routing, physical forward E2D attach priority, real release/recovery, particle retirement and source lookaround scalar torso/head aim.");
    }

    private static void VerifyScalarAim(GodotPlayer player, GodotPlayerMelee melee, GodotPlayerWebAttack attack,
        GodotPlayerLookaroundAim lookaround)
    {
        PlayerCombatState combat = player.Combat.State;
        bool active = combat.LookaroundActive;
        byte surface = melee.Kick.SurfaceTransition, ceiling = combat.Field8E9;
        var angles = (lookaround.State.Pitch, lookaround.State.Yaw, lookaround.State.SmoothedPitch,
            lookaround.State.SmoothedYaw, lookaround.State.PreviousHeading, lookaround.State.TransitionFade);
        var neck = player.Model.GetSourceJoint(1); var head = player.Model.GetSourceJoint(3);
        try
        {
            combat.LookaroundActive = true; melee.Kick.SurfaceTransition = 1; combat.Field8E9 = 0;
            short heading = player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
            lookaround.State.PreviousHeading = heading; lookaround.State.Pitch = 170; lookaround.State.Yaw = 1025;
            lookaround.State.SmoothedPitch = 0; lookaround.State.SmoothedYaw = 0;
            Assert(lookaround.AdvanceAngleSlice(2), "The genuine source active steady angle slice must remain active.");
            Assert(player.Model.GetSourceJoint(1).RotationX == 85 && player.Model.GetSourceJoint(1).RotationY == 512 &&
                player.Model.GetSourceJoint(3).RotationX == 85 && player.Model.GetSourceJoint(3).RotationY == 512 &&
                lookaround.State.SmoothedPitch == 0 && lookaround.State.SmoothedYaw == 833,
                "Actual original joints1/3 must use raw half pitch/yaw, retaining the real smoothing192 dead zone.");
            attack.AimTorso(melee.Kick);
            ushort target = (ushort)((heading + 1025) & 4095);
            Assert(player.Controller.HeadingTurn.Target == target && player.Controller.HeadingTurn.RemainingTicks > 0,
                "Native lookaround web aim must call the genuine short/bool torso overload with effective heading plus raw yaw.");
            combat.Field8E9 = 1; attack.AimTorso(melee.Kick);
            Assert(player.Controller.HeadingTurn.Target == (ushort)((heading - 1025) & 4095),
                "Actual ceiling lookaround aim must subtract raw yaw from effective heading before the source torso target mask.");
        }
        finally
        {
            combat.LookaroundActive = active; melee.Kick.SurfaceTransition = surface; combat.Field8E9 = ceiling;
            (lookaround.State.Pitch, lookaround.State.Yaw, lookaround.State.SmoothedPitch,
                lookaround.State.SmoothedYaw, lookaround.State.PreviousHeading, lookaround.State.TransitionFade) = angles;
            player.Model.SetSourceJoint(1, neck); player.Model.SetSourceJoint(3, head);
            melee.LockTargetTorsoAngle();
        }
    }

    private static void Assert(bool condition, string evidence)
    {
        if (!condition) throw new InvalidDataException(evidence);
    }
}
