using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;

namespace OpenSpidey.Game.Actors;

/// <summary>Concrete inherited CBaddy leaves for live thug scripts; flow and waypoint commands retain their existing interpreters.</summary>
public sealed class GodotThugScriptCommands(GodotActorRegistry registry)
{
    public bool TryExecute(GodotThugActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(registry);
        switch (opcode)
        {
            case 0x4201:
                actor.CycleAnimation(operands.ReadWord(), unchecked((sbyte)operands.ReadWord())); return true;
            case 0x4202:
                actor.RunAnimation(operands.ReadWord(), 0, -1); return true;
            case 0x4203: actor.ItemFlags &= 0xFFFE; return true;
            case 0x4204: actor.ItemFlags |= 1; return true;
            case 0x4205:
                if ((actor.State.BodyFlags & 0x40) == 0)
                {
                    if ((actor.Actor.BehaviorFlags & 0x4000) == 0) actor.SendDeathPulse();
                    actor.State.BodyFlags |= 0x40; actor.ItemFlags |= 1; actor.Lifecycle.Kill();
                }
                return true;
            case 0x4226: actor.Actor.Body.Velocity = default; return true;
            case 0x4227:
                byte interval = unchecked((byte)operands.ReadWord());
                actor.Route.Flags |= 4; actor.State.Field212 = interval;
                actor.Height.QueryInterval = interval; actor.Height.QueryElapsed = 0; return true;
            case 0x428E: case 0x4290:
                (registry.Sounds ?? throw new InvalidOperationException("Live thug sound commands require the actual scene channels."))
                    .Play(ActorScriptSoundCode.Global(operands.ReadWord(), opcode == 0x428E)); return true;
            default: return false;
        }
    }
}
