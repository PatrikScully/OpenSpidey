using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>The original script switch uses genuine smoke, CDome and held401 destructors, without substituting combat-hit effects.</summary>
internal sealed class GodotPlayerSynthesizedInput(GodotPlayer player) : IPlayerSynthesizedInputHost
{
    private GodotPlayerWebRuntime? web;
    private GodotPlayerCombatRuntime? combat;

    public void BindSourceOwners(GodotPlayerWebRuntime sourceWeb, GodotPlayerCombatRuntime sourceCombat)
    {
        ArgumentNullException.ThrowIfNull(sourceWeb); ArgumentNullException.ThrowIfNull(sourceCombat);
        if (web is not null || combat is not null) throw new InvalidOperationException("The player script switch already owns its native effect services.");
        if (!ReferenceEquals(sourceCombat.Actors.Registry.Player, player) || player.Melee is null ||
            sourceWeb.Attack is null || sourceWeb.Domes is null || sourceWeb.Smash is null)
            throw new ArgumentException("Script switching requires this CPlayer and its fully composed source melee/web owners.");
        web = sourceWeb; combat = sourceCombat;
    }

    public void SetVelocity(FixedVector3 velocity) => player.Controller.SetScriptVelocity(velocity);
    public bool SmokeTrailPresent(int slot)
    {
        if (web is not null) return web.Smash!.CurrentTrails[slot] is not null;
        bool present = slot == 0 ? player.Combat.State.FirstSmokeTrailActive : player.Combat.State.SecondSmokeTrailActive;
        if (present) throw new InvalidOperationException("The original smoke trail pointer has no bound native script-switch owner.");
        return false;
    }
    public void FadeSmokeTrail(int slot) => web!.Smash!.FadeSmokeTrail(slot);
    public object? RecoverDome()
    {
        if (web is null) return null;
        var handle = web.Attack!.State.DomeHandle;
        object? dome = web.Domes!.Recover(ref handle);
        web.Attack.State.DomeHandle = handle;
        return dome;
    }
    public void DestroyDome(object dome)
    {
        if (!web!.Domes!.DestroySourceBody(dome))
            throw new InvalidOperationException("The recovered original AB8 dome lost its genuine deleting destructor.");
    }
    public void ClearDomeHandle() => web!.Attack!.State.DomeHandle = default;
    public object? HeldObject
    {
        get
        {
            if (player.Melee?.Kick.HeldObject is { } held) return held.Identity;
            if (combat?.Session.HeldActor is { } actor) return actor;
            if (player.Combat.State.HeldObjectPresent)
                throw new InvalidOperationException("The original held-object pointer has no actual401 owner.");
            return null;
        }
    }
    public void DestroyHeldObject(object item)
    {
        if (combat is null || item is not GodotManipulationActor actor || !GodotObject.IsInstanceValid(actor) ||
            !ReferenceEquals(combat.Session.HeldActor, actor) || !combat.Actors.Registry.Owns(actor))
            throw new InvalidOperationException("The source player script switch must delete its actual held401 body.");
        combat.Actors.Registry.DestroyImmediately(actor);
    }
    public void ClearHeldObject()
    {
        player.Melee!.Kick.HeldObject = null;
        player.Combat.State.HeldObjectPresent = false;
        if (combat!.Session.HeldActor is not null)
            throw new InvalidOperationException("The genuine held401 destructor did not clear its session pointer.");
    }
    public void SelectAndResetAnimationSounds(ushort animation) => player.Melee!.SelectAndResetAnimationSounds(animation);
    public void RunAnimation(ushort animation, int firstFrame, int lastFrame) => player.Melee!.RunAnimation(animation, firstFrame, lastFrame);
    public void ClearInputAliases() => player.ClearSourceScriptInput();
}
