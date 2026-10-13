using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Hostages;

public interface IHostageDeathHost
{
    void SendDeathPulse();
    void KillShadow();
    void MarkDead();
}

/// <summary>Original CBaddy::Die stages for CHostage, distinct from its field328 death flash counter.</summary>
public static class HostageDeathLifecycle
{
    public static bool Step(HostageState state, ActorProcedureState actor, IHostageDeathHost host, int stage)
    {
        if ((state.BodyFlags & 0x40) != 0) return false;
        switch (stage)
        {
            case 0:
                if ((actor.BehaviorFlags & 0x4000) == 0) host.SendDeathPulse();
                goto case 3;
            case 3:
                state.BodyFlags |= 0x40; state.ItemFlags |= 1; host.MarkDead(); break;
            case 1:
                if ((actor.BehaviorFlags & 0x4000) == 0) host.SendDeathPulse();
                state.ItemFlags |= 0x800; state.Transparency = 128; state.BodyFlags &= ~8u; host.KillShadow();
                state.ItemFlags |= 0x400; state.TintColor = 0x404040; state.DeathFadeUpdates = 0; break;
            case 2:
                int previous = state.DeathFadeUpdates; state.DeathFadeUpdates = unchecked(previous + 1);
                if (previous >= 40) return true;
                int brightness = unchecked(6553 * unchecked(40 - state.DeathFadeUpdates)) >> 12;
                int packed = unchecked((brightness << 8) | brightness);
                state.TintColor = unchecked((uint)((packed << 8) | brightness)); break;
            default: throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown source actor death stage.");
        }
        return false;
    }
}
