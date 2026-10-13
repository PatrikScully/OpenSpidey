using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Source CBaddy::Die stages, supplied to an injury host's Die callback.</summary>
public static class ThugDeathLifecycle
{
    public static bool Step(ThugState state, ActorProcedureState actor, IThugInjuryHost host, int stage)
    {
        if ((state.BodyFlags & 0x40) != 0) return false;
        switch (stage)
        {
            case 0:
                if ((actor.BehaviorFlags & 0x4000) == 0) host.SendDeathPulse();
                goto case 3;
            case 3:
                state.BodyFlags |= 0x40;
                host.ItemFlags |= 1;
                host.MarkDead();
                break;
            case 1:
                if ((actor.BehaviorFlags & 0x4000) == 0) host.SendDeathPulse();
                host.ItemFlags |= 0x800;
                state.Transparency = 128;
                host.KillShadow();
                host.ItemFlags |= 0x400;
                state.TintColor = 0x404040;
                state.ActionCounter = 0;
                break;
            case 2:
                int previous = state.ActionCounter;
                state.ActionCounter = unchecked(previous + 1);
                if (previous >= 40) return true;
                int brightness = unchecked(6553 * unchecked(40 - state.ActionCounter)) >> 12;
                int packed = unchecked((brightness << 8) | brightness);
                state.TintColor = unchecked((uint)((packed << 8) | brightness));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown actor death stage.");
        }
        return false;
    }
}
