using OpenSpidey.Core.Combat;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>The shipped Jonah Hit entry, including its always-zero return and signed-short health wrapping.</summary>
public static class JonahDamage
{
    public static bool Hit(JonahLocomotionState state, IJonahBrainHost host, ActorHit hit)
    {
        JonahState jonah = state.Jonah;
        short previousHealth = jonah.Health;
        if (previousHealth < 0) return false;
        jonah.SignalFlags &= ~0x80u;
        state.HitReactionContext = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        if (state.HitReactionContext == 6) return false;
        if ((hit.Flags & 4) != 0) jonah.Health = unchecked((short)(jonah.Health - hit.Damage));
        if ((hit.Flags & 4) == 0 || jonah.Health > 0)
            host.PlaySound(unchecked((uint)(host.Random(2) + 179)) | 0x8000, state.Brain.Position, 0);
        bool pleading = jonah.State == 3;
        bool pleaMode = unchecked((byte)(jonah.IdleAnimations >> 8)) == 1;
        if (jonah.Health <= 0)
        {
            host.PlaySound(0x80B2, state.Brain.Position, 0);
            state.Brain.Neutralize();
            ushort clip;
            if (pleading) clip = 13;
            else if (pleaMode) clip = 12;
            else clip = 14;
            host.RunAnimation(clip, 0, -1);
            state.Brain.EnterState(8);
        }
        else
        {
            if ((jonah.SignalFlags & 0x400) == 0)
                state.ObstructionUpdates = unchecked(state.ObstructionUpdates + previousHealth - jonah.Health);
            state.Brain.Neutralize();
            ushort clip;
            if (pleading) clip = 17;
            else if (pleaMode) clip = 16;
            else clip = 18;
            host.RunAnimation(clip, 0, -1);
            state.Brain.EnterState(6);
        }
        return false;
    }
}
