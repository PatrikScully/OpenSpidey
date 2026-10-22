using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>CLizMan guard (state 1) and node jump (state 23) leaves.</summary>
public static class LizManGuard
{
    /// <summary>CLizMan::Guard (0x4500E0).</summary>
    public static void Guard(LizManState state, ILizManHost host)
    {
        switch (state.Substate)
        {
            case 0:
                state.Field1F8 = 0;
                host.Neutralize();
                if (unchecked((uint)(host.TotalTime - state.Field374)) < 0x12C)
                    host.Procedures.Attach(ActorLookAtProcedure.ForBody(host.Actor, host.RecoverPlayerBody, 0, 128, 200));
                state.Substate++;
                break;
            case 1:
                if (LizManAnimationFollowOn.PendingAnimation != 0xFFFF && host.AnimationId != 5)
                {
                    state.Field340 = 0;
                    host.RunAnimation(5);
                }
                state.Field1F8 = unchecked(state.Field1F8 + host.ElapsedTicks);
                if (state.Field1F8 <= 120) break;
                state.Field1F8 = 0;
                int target = host.ScanNearbyNodesForJumpTarget();
                if (target == 0) break;
                state.Field1F0 = 0;
                host.PlayPositionalSound((host.Random(2) + 134) | 0x8000);
                FixedVector3 destination = host.TriggerPosition(target);
                state.Field1F4 = target;
                CalculateJumpPositionArray(state, host, destination);
                state.State = 23;
                state.Substate = 0;
                break;
        }
    }

    /// <summary>CLizMan::CalculateJumpPositionArray (0x44F2E0): 48 steps with a sine-table arc.</summary>
    public static void CalculateJumpPositionArray(LizManState state, ILizManHost host, FixedVector3 target)
    {
        int ground = host.GroundHeight(target, 0, 512, 0);
        FixedVector3 position = host.Body.Position;
        int stepX = unchecked(target.X - position.X) / 48;
        int stepY = unchecked(ground - (state.SurfaceClearance << 12) - position.Y) / 48;
        int stepZ = unchecked(target.Z - position.Z) / 48;
        var positions = new FixedVector3[48];
        int x = unchecked(position.X + stepX), y = unchecked(position.Y + stepY), z = unchecked(position.Z + stepZ);
        for (int index = 0, angle = 0; angle < 2064; index++, angle += 43)
        {
            positions[index] = new(x, unchecked(y - (GameAngle.Heading(angle & 0xFFF).Sine << 8)), z);
            x = unchecked(x + stepX);
            y = unchecked(y + stepY);
            z = unchecked(z + stepZ);
        }
        state.JumpPositions = positions;
        state.JumpPositionIndex = 0;
    }

    /// <summary>CLizMan::Jump (0x44F420), state 23.</summary>
    public static void Jump(LizManState state, ILizManHost host)
    {
        state.Field394 |= 1;
        FixedVector3[] positions = state.JumpPositions ?? throw new InvalidOperationException("CLizMan::Jump has no position array.");
        switch (state.Substate)
        {
            case 0:
                host.Procedures.Attach(ActorLookAtProcedure.ForPoint(host.Actor, positions[47], 2, 128, 200));
                if (host.AnimationId != 11)
                {
                    state.Field340 = 0;
                    host.RunAnimation(11);
                    return;
                }
                if (host.AnimationFrame < 12) return;
                host.PlayPositionalSound((host.Random(2) + 130) | 0x8000);
                host.KillShadow();
                state.MinorRadius = 0;
                state.Substate++;
                break;
            case 1:
            {
                state.Field394 |= 2;
                state.JumpPositionIndex = unchecked(state.JumpPositionIndex + host.ElapsedTicks);
                bool landed = false;
                if (state.JumpPositionIndex >= 48)
                {
                    state.JumpPositionIndex = 47;
                    landed = true;
                }
                host.Body.Position = positions[state.JumpPositionIndex];
                if (landed)
                {
                    host.PlayPositionalSound(32900);
                    state.MinorRadius = 128;
                    host.SetHeight(true, 100, 600);
                    state.Substate++;
                }
                else if (host.AnimationFrame > 32)
                    host.SetAnimationFrame(32);
                break;
            }
            case 2:
                if (LizManAnimationFollowOn.PendingAnimation == 0xFFFF) break;
                state.JumpPositions = null;
                state.State = 25;
                state.Substate = 0;
                break;
        }
    }

}
