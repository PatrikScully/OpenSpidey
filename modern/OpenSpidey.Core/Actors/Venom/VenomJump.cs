using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::ScanNodesForJumpTarget (0x4ECC60), CalculateJumpPositionArray (0x4ECD80) and Jump (0x4ED410).</summary>
public static class VenomJump
{
    /// <summary>Nearest ID_JUMP_TO node (type 1002, subtype 3) 256..4096 away that is closer to the player; 0 when none.</summary>
    public static int ScanNodesForJumpTarget(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        int result = 0;
        int best = VenomNodeStates.CrapDistance(host.Body.Position, host.PlayerPosition) - 256;
        if (best <= 0) return 0;
        for (int node = 1; node < host.TriggerNodeCount; node++)
        {
            if (host.TriggerNodeTypeCode(node) != 1002 || host.TriggerNodeSubtype(node) != 3) continue;
            FixedVector3 position = host.TriggerPosition(node);
            int ground = host.GroundHeight(position, 0, 0x2000, 0);
            if (ground == -1) continue;
            position = position with { Y = unchecked(ground - (state.SurfaceClearance << 12)) };
            int distance = VenomNodeStates.CrapDistance(position, host.Body.Position);
            if (distance < 256 || distance > 4096) continue;
            int toPlayer = VenomNodeStates.CrapDistance(position, host.PlayerPosition);
            if (toPlayer >= best) continue;
            best = toPlayer;
            result = node;
            state.JumpTarget = position;
        }
        return result;
    }

    public static void CalculateJumpPositionArray(VenomState state, IVenomHost host, FixedVector3 target, int count)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        FixedVector3 origin = host.Body.Position;
        state.JumpCount = count;
        int dx = unchecked(target.X - origin.X) / count;
        int dy = unchecked(target.Y - origin.Y) / count;
        int dz = unchecked(target.Z - origin.Z) / count;
        int x = unchecked(origin.X + dx), y = unchecked(origin.Y + dy), z = unchecked(origin.Z + dz);
        int step = 2048 / (count - 1);
        FixedVector3[] path = new FixedVector3[Math.Max(count, 0)];
        int t = 0;
        for (int i = 0; i < count; i++)
        {
            path[i] = new(x, unchecked(y - (GameAngle.Heading(t & 0xFFF).Sine << 8)), z);
            x = unchecked(x + dx);
            y = unchecked(y + dy);
            z = unchecked(z + dz);
            t += step;
        }
        state.JumpPath = path;
        state.JumpIndex = 0;
    }

    public static void Jump(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(combat);
        switch (state.Substate)
        {
            case 0:
                host.Body.Velocity = default;
                state.Field218 = (state.Field218 & 0xFFFFFFFC) | 2;
                if (host.AnimationId == 7)
                {
                    if (host.AnimationFrame >= 4)
                    {
                        state.BodyFlags &= ~0x10u;
                        state.MinorRadius = 0;
                        state.Substate++;
                    }
                }
                else host.RunAnimation(7);
                break;
            case 1:
            {
                uint count = unchecked((uint)state.JumpCount);
                int index = unchecked(state.JumpIndex + host.ElapsedTicks);
                bool done = false;
                state.JumpIndex = index;
                if (unchecked((uint)index) >= count)
                {
                    done = true;
                    state.JumpIndex = unchecked((int)count - 1);
                }
                host.Body.Position = state.JumpPath![state.JumpIndex];
                if (done)
                {
                    state.Field218 &= ~2u;
                    state.JumpPath = null;
                    state.BodyFlags |= 0x10;
                    state.MinorRadius = 0xC0;
                    state.Substate++;
                }
                else if (host.AnimationFrame > 14) combat.SetAnimationFrame(14);
                break;
            }
            case 2:
                if (host.AnimationFinished)
                {
                    state.AiState = state.ReturnAiState;
                    state.Substate = state.ReturnSubstate;
                }
                break;
        }
    }
}
