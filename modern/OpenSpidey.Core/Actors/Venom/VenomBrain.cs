using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

public interface IVenomHost
{
    ActorBodyMotion Body { get; }
    int LevelId { get; }
    int ElapsedTicks { get; }
    int AnimationId { get; }
    bool AnimationFinished { get; }
    FixedVector3 PlayerPosition { get; }
    FixedVector3 PrimaryAimTarget { get; }
    FixedVector3 SecondaryAimTarget { get; }
    FixedVector3? MechListPosition { get; }
    void RunAnimation(int animation);
}

/// <summary>CVenom constructor (0x4E9600) and AI dispatch (0x4EC040) for walkabout/wait leaves.</summary>
public sealed class VenomBrain(VenomState state, IVenomHost host)
{
    public static void Construct(VenomState state, IVenomHost host, Action attachToBaddyList, Action<VenomState>? createHealthBar = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(attachToBaddyList);
        if (state.LevelId is 0x9904 or 0x601 or 0x602 or 0x501 or 0x503 or 0x504 or 0x505 or 0x506)
        {
            state.BodyFlags &= ~0x10u;
            state.MinorRadius = 0;
        }
        attachToBaddyList();
        state.AiState = 1;
        state.Substate = 0;
        switch (state.LevelId)
        {
            case 0x603:
                state.Health = 0x258;
                state.Field460 = 0xF0;
                state.Field464 = 0x2D0;
                state.Field39C = state.DifficultyLevel is 0 or 1 ? 0x3C : 0x14;
                state.BodyFlags |= 0x41;
                state.Field45C = 0;
                state.Substate = 1;
                break;
            case 0x604 or 0x601 or 0x602 or 0x501 or 0x502 or 0x503 or 0x504 or 0x505 or 0x506:
                state.AiState = 0x4000;
                state.Substate = 0;
                if (state.LevelId == 0x501) state.Field218 |= 0x8000;
                else if (state.LevelId == 0x502)
                {
                    state.Health = 0x258;
                    state.Field460 = 0x78;
                    state.Field464 = 0x168;
                    state.Field39C = state.DifficultyLevel is 0 or 1 ? 0x3C : 0x14;
                    createHealthBar?.Invoke(state);
                }
                else if (state.LevelId == 0x604)
                {
                    state.Health = 0x258;
                    state.Field460 = 0xF0;
                    state.Field464 = 0x2D0;
                    state.Field39C = state.DifficultyLevel is 0 or 1 ? 0x46 : 0x32;
                    state.Field33C = 1;
                    createHealthBar?.Invoke(state);
                    state.ResolveSwitchNodesPending = true;
                }
                break;
        }
    }

    private bool grounded;

    public void SnapToGround(Func<FixedVector3, int> groundHeight)
    {
        if (grounded) return;
        grounded = true;
        int ground = groundHeight(host.Body.Position);
        if (ground != -1)
            host.Body.Position = host.Body.Position with { Y = unchecked(ground - (state.SurfaceClearance << 12)) };
        if (state.ResolveSwitchNodesPending)
            throw new NotSupportedException("CVenom::ResolveSwitchNodes (0x4EB690) is not ported yet.");
    }

    public void Advance()
    {
        VenomPhysics.DoPhysics(state, host);
        switch (state.AiState)
        {
            case 0x4000:
                VenomWalkabout.Tick(state, host);
                break;
            case 1 when state.Substate == 0:
                break;
            default:
                throw new NotSupportedException(
                    $"CVenom::AI state 0x{state.AiState:X} substate {state.Substate} is not ported yet (0x4EC040).");
        }
    }
}
