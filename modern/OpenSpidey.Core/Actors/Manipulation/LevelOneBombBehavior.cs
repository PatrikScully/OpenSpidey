using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Manipulation;

/// <summary>CL1A3Bomb's additional fields; its underlying object remains the same original type401 CManipOb.</summary>
public sealed class LevelOneBombState(ManipulationState manipulation, ActorScriptState script)
{
    public ManipulationState Manipulation { get; } = manipulation ?? throw new ArgumentNullException(nameof(manipulation));
    public ActorScriptState Script { get; } = script ?? throw new ArgumentNullException(nameof(script));
    public byte Armed { get; set; }
    public byte ExpirationSignalSent { get; set; }
    public byte GroundContact { get; set; }
}

public readonly record struct LevelOneBombPlayerBasis(FixedVector3 Forward, FixedVector3 Up);

public interface ILevelOneBombHost
{
    int Difficulty { get; }
    uint Timer { get; }
    LevelOneBombPlayerBasis? PlayerBasis { get; }
    int GetGroundHeight(FixedVector3 position, int above, int below);
    ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode);
    void ShakeCamera(FixedVector3 position, int magnitude);
    void PlaySound(uint sound, FixedVector3 position, int pitch);
    void SignalLinkedActors(ushort node);
    void PulseLinkedActors(ushort node);
}

/// <summary>Exact first-level bomb AI, ground/wall response and virtual overrides from level_one_bomb.cpp.</summary>
public static class LevelOneBombBehavior
{
    public static void Construct(LevelOneBombState state, MissionCountdown countdown)
    {
        state.Armed = 0; state.ExpirationSignalSent = 0; countdown.Rate = 4096;
        // Source ctor leaves field12A untouched. Newly allocated modern owners begin with its safe zero default.
    }

    public static void Advance(LevelOneBombState state, MissionCountdown countdown, int elapsedTicks, ILevelOneBombHost host)
    {
        if (state.Armed != 0 && countdown.RemainingTicks == 0)
        {
            if (state.ExpirationSignalSent == 0)
            { host.SignalLinkedActors(state.Manipulation.Node); state.ExpirationSignalSent = 1; }
            return;
        }
        if ((state.Script.PendingSignals & 1) != 0)
        {
            state.Script.PendingSignals &= 0xFFFE;
            if (state.Armed == 0)
            {
                state.Armed = 1; countdown.DisplayEnabled = 1; countdown.Running = 1;
                countdown.RemainingTicks = host.Difficulty != 3 ? 7260u : 4260u;
                countdown.LastUpdateTick = host.Timer;
            }
        }
        if ((state.Manipulation.Flags & 1) != 0) DoPhysics(state, countdown, elapsedTicks, host);
    }

    public static void Die(LevelOneBombState state, MissionCountdown countdown, ILevelOneBombHost host)
    { host.PulseLinkedActors(state.Manipulation.Node); countdown.Stop(host.Timer); }

    public static void Dispose(MissionCountdown countdown, uint sourceClock) => countdown.Stop(sourceClock);

    public static void Smash(LevelOneBombState state, ILevelOneBombHost host, Func<int, int> random)
    {
        FixedVector3 velocity = host.PlayerBasis is { } player ? player.Forward * -24 + player.Up * 14 : new(0, -57344, 0);
        ManipulationBehavior.Throw(state.Manipulation, velocity, random);
    }

    public static void DoPhysics(LevelOneBombState state, MissionCountdown countdown, int elapsedTicks, ILevelOneBombHost host)
    {
        ManipulationState manipulation = state.Manipulation;
        ActorBodyMotion body = manipulation.Body;
        FixedVector3 previous = body.Position;
        for (int tick = 0; tick < elapsedTicks; tick++)
        { body.Velocity += body.Acceleration; body.Position += body.Velocity; }
        int ground = host.GetGroundHeight(body.Position, 100, 72);
        if (ground == -1) state.GroundContact = 0;
        else
        {
            if (state.GroundContact == 0)
            {
                host.ShakeCamera(body.Position, 2);
                if (countdown.Rate < 8192) countdown.Rate = 8192;
                host.PlaySound(0x819C, body.Position, 0); state.GroundContact = 1;
            }
            body.Position = body.Position with { Y = unchecked(ground - 294912) };
            body.Velocity = new(unchecked(220 * body.Velocity.X) >> 8, 0, unchecked(220 * body.Velocity.Z) >> 8);
            if (body.Position.X is >= -16384000 and <= -11673600 && body.Position.Z is >= 0x79E000 and <= 0xAF0000)
            {
                int x = body.Velocity.X;
                if (x > 0) { if (x < 8192) x = 8192; }
                else if (x > -8192) x = -8192;
                body.Velocity = body.Velocity with { X = x };
            }
        }
        if (GameWord.Absolute(body.Velocity.X) < 8192 && GameWord.Absolute(body.Velocity.Z) < 8192)
        {
            body.Velocity = body.Velocity with { X = 0, Z = 0 };
            manipulation.ItemFlags &= 0xFFDF; manipulation.Flags &= ~1;
        }
        FixedVector3 direction = FixedVectorMath.Normalize((body.Position - previous) >> 12);
        FixedVector3 start = previous - (direction << 7);
        // PC447302/44731A use current mPos for all endpoint axes; recovered C++ incorrectly uses captured X/Z.
        FixedVector3 end = body.Position + (direction << 6);
        if (host.LineToItem(start, end, 1) is not { } hit) return;
        host.ShakeCamera(body.Position, 2); body.Position = previous; host.PlaySound(0x819C, body.Position, 0);
        FixedVector3 normal = new(unchecked((short)hit.Normal.X), unchecked((short)hit.Normal.Y), unchecked((short)hit.Normal.Z));
        if (normal.Y < -3800) return;
        if (normal.Y > 3800)
        { if (body.Velocity.Y < 0) body.Velocity = body.Velocity with { Y = unchecked(-body.Velocity.Y) }; return; }
        FixedVector3 whole = body.Velocity >> 12;
        int halfLength = FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z)) / 2;
        FixedVector3 velocity = FixedVectorMath.Normalize(body.Velocity >> 6);
        int dot = unchecked(normal.Z * -velocity.Z + normal.X * -velocity.X + normal.Y * -velocity.Y) >> 12;
        int factor = dot << 1;
        body.Velocity = new(unchecked(((unchecked(normal.X * factor) >> 12) + velocity.X) * halfLength),
            unchecked(((unchecked(normal.Y * factor) >> 12) + velocity.Y) * halfLength),
            unchecked(((unchecked(normal.Z * factor) >> 12) + velocity.Z) * halfLength));
        if (body.Velocity.Y < 0) body.Velocity = body.Velocity with { Y = 0 };
    }
}
