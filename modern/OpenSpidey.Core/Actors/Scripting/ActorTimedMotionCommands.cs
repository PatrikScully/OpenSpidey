namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>Original4280 delay plus L1A2/L8A3 virtual-physics recording, including stationary402's extra40 ticks.</summary>
public static class ActorTimedMotionCommands
{
    public static bool Execute(ActorScriptState script, ActorBodyMotion body, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable, ActorScriptEnvironment environment, Action<int> physicsStep)
    {
        ushort word = operands.ReadWord();
        //405F9D sign-extends a variable result; literal words stay zero-extended.
        int duration = (word & 0x2000) != 0 ? readVariable(word, operands) : word;
        return Execute(script, body, duration, environment, physicsStep);
    }

    public static bool Execute(ActorScriptState script, ActorBodyMotion body, int duration,
        ActorScriptEnvironment environment, Action<int> physicsStep)
    {
        ArgumentNullException.ThrowIfNull(script); ArgumentNullException.ThrowIfNull(body); ArgumentNullException.ThrowIfNull(physicsStep);
        if (script.ActorType == 203 && script.SkipDuringDeath && environment.FastForwardDeath) return true;
        script.WaitTicks = duration; body.ScriptWaitTicks = duration;
        if (environment.LevelId is not (0x102 or 0x803)) return false;
        RecordedActorMotion recorded = body.Recorded;
        bool moving = body.Velocity.X != 0 || body.Velocity.Y != 0 || body.Velocity.Z != 0 ||
            body.AngularVelocity.X != 0 || body.AngularVelocity.Y != 0 || body.AngularVelocity.Z != 0;
        if (duration != 0 && moving)
        {
            recorded.StartPosition = body.Position; recorded.StartAngles = body.Angles;
            ActorAngles accumulated = body.Angles;
            // The original condition rereads field230 after each virtual physics call.
            for (int index = 0; index < body.ScriptWaitTicks; index++)
            {
                accumulated += Multiply(body.AngularVelocity, 2);
                physicsStep(2);
            }
            recorded.ElapsedTicks = 0; recorded.TotalTicks = 2 * duration;
            recorded.EndPosition = body.Position; recorded.EndAngles = body.Angles;
            recorded.VelocityPerTick = (recorded.EndPosition - recorded.StartPosition) / recorded.TotalTicks;
            ActorAngles difference = new(unchecked((short)(accumulated.X - recorded.StartAngles.X)),
                unchecked((short)(accumulated.Y - recorded.StartAngles.Y)), unchecked((short)(accumulated.Z - recorded.StartAngles.Z)));
            recorded.AngularVelocityPerTick = new(unchecked((short)(difference.X / recorded.TotalTicks)),
                unchecked((short)(difference.Y / recorded.TotalTicks)), unchecked((short)(difference.Z / recorded.TotalTicks)));
            // Velocity/acceleration and friction effects persist; only the original position/angles are restored.
            body.Position = recorded.StartPosition; body.Angles = recorded.StartAngles;
        }
        else recorded.ElapsedTicks = 2 * duration + (environment.LevelId == 0x102 && script.ActorType == 402 ? 40 : 0);
        script.WaitTicks = body.ScriptWaitTicks;
        return false;
    }
    private static ActorAngles Multiply(ActorAngles angles, int amount) => new(unchecked((short)(angles.X * amount)),
        unchecked((short)(angles.Y * amount)), unchecked((short)(angles.Z * amount)));
}
