using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Gameplay;

public interface ISubwayCarWiggleHost
{
    bool PlayerPresent { get; }
    FixedVector3 PlayerPosition { get; set; }
    uint PlayerBrightness { get; set; }
    int Random(int range);
    int NodeCount { get; }
    ushort NodeKind(int node);
    FixedVector3 NodePosition(int node);
    void SendPulse(int node);
    void ReportMissingDrowningNode();
    int EnvironmentCount { get; }
    int ReadEnvironmentY(int index);
    void WriteEnvironmentY(int index, int y);
    int EnvironmentSpringVelocity(int index);
    void SetEnvironmentSpringVelocity(int index, int velocity);
    int EnvironmentSpringFlag(int index);
    void SetEnvironmentSpringFlag(int index, int flag);
    int BaddyCount { get; }
    short BaddyType(int index);
    int BaddyAiState(int index);
    void SetBaddyAiState(int index, int state, int substate);
    int PowerUpCount { get; }
    FixedVector3 PowerUpWorkingPosition(int index);
    void SetPowerUpWorkingPosition(int index, FixedVector3 position);
}

/// <summary>Original L5A5LSC_WiggleSubwayCars (0x4487A0): brightness flicker, drown relocate, enviro springs, LizMan handoff, power-up Z drift.</summary>
public static class SubwayCarWiggle
{
    private static readonly FixedVector3 DrowningPosition =
        new(unchecked((int)0xFEAB8000), 0x532000, unchecked((int)0xFE330000));

    public static void Wiggle(ISubwayCarWiggleHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.PlayerPresent)
        {
            if (host.Random(4) == 0)
                host.PlayerBrightness = unchecked((uint)(host.Random(0x80) + 0x20));

            if (host.PlayerPosition.Y > 0xCB2000)
            {
                bool found = false;
                for (int node = 1; node < host.NodeCount; node++)
                {
                    if (host.NodeKind(node) != 1) continue;
                    FixedVector3 position = host.NodePosition(node);
                    if (position.X != DrowningPosition.X || position.Y != DrowningPosition.Y || position.Z != DrowningPosition.Z)
                        continue;
                    found = true;
                    host.SendPulse(node);
                    host.PlayerPosition = position;
                    break;
                }
                if (!found) host.ReportMissingDrowningNode();
            }
        }

        for (int index = 0; index < host.EnvironmentCount && index < 8; index++)
        {
            int flag = 0;
            int vel = host.EnvironmentSpringVelocity(index);
            int y = host.ReadEnvironmentY(index);
            if (vel != flag) y = unchecked(y - (vel << 12));

            flag = host.EnvironmentSpringFlag(index);
            if (flag == 0) vel = unchecked(vel - (3 + host.Random(3)));
            else vel = unchecked(vel + host.Random(3) + 3);

            if (vel > 11) { vel = 11; flag = 0; }
            else if (vel < -11) { vel = -11; flag = 1; }

            y = unchecked(y + (vel << 12));
            host.WriteEnvironmentY(index, y);
            host.SetEnvironmentSpringVelocity(index, vel);
            host.SetEnvironmentSpringFlag(index, flag);
        }

        for (int index = 0; index < host.BaddyCount; index++)
        {
            if (host.BaddyType(index) != 0x13D || host.BaddyAiState(index) != 0x12) continue;
            host.SetBaddyAiState(index, 0x18, 0);
        }

        for (int index = 0; index < host.PowerUpCount; index++)
        {
            FixedVector3 working = host.PowerUpWorkingPosition(index);
            host.SetPowerUpWorkingPosition(index, working with { Z = unchecked(working.Z - 0x40000) });
        }
    }
}
