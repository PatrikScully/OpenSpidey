using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Actors.Choppers;

public interface IChopperMissileHost
{
    ChopperMissileState State { get; }
    int ElapsedTicks { get; }
    int Difficulty { get; }
    TriggerFile Triggers { get; }
    void SetSmokePosition(FixedVector3 position);
    void ModifySoundPosition(FixedVector3 position);
    void SendPulse(ushort node);
    void Explode();
}

/// <summary>CChopperMissile::AI (0x425400) homing, smoke ribbon trailing offset and waypoint chaining.</summary>
public static class ChopperMissileBrain
{
    private const int EasySpeedCap = 0x2A000;
    private const int HardSpeedCap = 0x40000;

    public static void Advance(IChopperMissileHost host)
    {
        ChopperMissileState state = host.State;
        if (!state.Alive) return;
        int elapsed = host.ElapsedTicks;
        if (state.Phase == 2) return;

        if (state.DelayTicks != 0)
        {
            if (state.DelayTicks <= elapsed) state.DelayTicks = 0;
            else state.DelayTicks -= elapsed;
        }

        int cap = host.Difficulty is 0 or 1 ? EasySpeedCap : HardSpeedCap;
        if (state.Speed < cap)
        {
            state.Speed += elapsed << 13;
            if (state.Speed > cap) state.Speed = cap;
        }

        AimAngles aim = FixedAim.Calculate(state.Position, state.TargetPosition);
        if (FixedVectorMath.OriginalDistance(state.Position, state.TargetPosition) > 0x200)
            TurnTowards(state, aim, state.Speed >> 12);
        else
            state.Angles = new ActorAngles(aim.Pitch, aim.Yaw, 0);

        state.Velocity = FixedAim.Velocity(state.Speed >> 12, aim);
        bool hitTarget = false;
        for (int step = 0; step < elapsed; step += 2)
        {
            state.Position += state.Velocity * 2;
            state.Angles = (state.Angles + state.AngularVelocity).Mask();
            IntegrateAngularVelocity(state);
            if (FixedVectorMath.OriginalDistance(state.Position, state.TargetPosition) < 0x80)
            {
                hitTarget = true;
                break;
            }
        }

        FixedVector3 smokeOffset = FixedAim.Velocity(128, new AimAngles(state.Angles.X, state.Angles.Y));
        host.SetSmokePosition(state.Position - smokeOffset);
        host.ModifySoundPosition(state.Position);
        if (!hitTarget) return;

        if (state.Phase is not (0 or 1)) return;
        IReadOnlyList<ushort> links = MissionRuntime.ReadLinks(host.Triggers.Nodes[state.WaypointNode]);
        if (state.Phase == 0)
        {
            foreach (ushort link in links) host.SendPulse(link);
            host.Explode();
            return;
        }

        if (links.Count > 0)
        {
            int next = links[0];
            if ((uint)next < host.Triggers.Nodes.Count && host.Triggers.Nodes[next].TypeCode == 1002)
            {
                state.WaypointNode = next;
                state.TargetPosition = ChopperMissileConstruction.WaypointPosition(host.Triggers, next);
                return;
            }
        }
        host.Explode();
    }

    private static void TurnTowards(ChopperMissileState state, AimAngles ideal, int factor)
    {
        short pitch = WrappedDifference(ideal.Pitch, state.Angles.X);
        short yaw = WrappedDifference(ideal.Yaw, state.Angles.Y);
        if ((pitch | yaw) == 0)
            state.AngularVelocity = state.AngularVelocity with { X = 0, Y = 0 };
        else
            state.AngularAcceleration = state.AngularAcceleration with
            {
                X = unchecked((short)(factor * pitch >> 8)),
                Y = unchecked((short)(factor * yaw >> 8))
            };
    }

    private static void IntegrateAngularVelocity(ChopperMissileState state)
    {
        short vx = unchecked((short)(state.AngularVelocity.X + state.AngularAcceleration.X));
        state.AngularVelocity = state.AngularVelocity with { X = unchecked((short)(vx - (vx >> state.AngularFriction.X))) };
        short vy = unchecked((short)(state.AngularVelocity.Y + state.AngularAcceleration.Y));
        state.AngularVelocity = state.AngularVelocity with { Y = unchecked((short)(vy - (vy >> state.AngularFriction.Y))) };
        state.AngularVelocity = state.AngularVelocity.KillSmall();
    }

    private static short WrappedDifference(short ideal, short current)
    {
        short difference = unchecked((short)(ideal - current));
        if (difference < -2048) difference = unchecked((short)(difference + 4096));
        if (difference > 2048) difference = unchecked((short)(difference - 4096));
        return difference;
    }
}
