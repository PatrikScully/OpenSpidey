using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public interface IPunchObHost
{
    ActorBodyMotion Body { get; }
    ushort ItemFlags { get; set; }
    ushort InputFlags { get; set; }
    int AnimationId { get; }
    bool AnimationFinished { get; }
    bool HasMessages { get; }
    int ElapsedTicks { get; }
    int SourceScale { get; set; }
    bool HasTrapWeb { get; }
    void InitializeModel(string name);
    void AttachToSourceBaddyList();
    void RunAnimation(int animation);
    void ParseAuthoredScript();
    void DeleteAllMessages();
    void BuildTransform();
    void BurstTrapWeb();
    void HitFromInput(short health);
    void MarkSourceBodyDead();
}

/// <summary>CPunchOb constructor 0x4A73B0 after SquirtPos/SquirtAngles.</summary>
public static class PunchObConstruction
{
    public static void Construct(PunchObState state, IPunchObHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        host.InitializeModel(PunchObState.ModelName);
        host.AttachToSourceBaddyList();
        state.BodyFlags |= 0x10;
        state.CombatFlags |= 2;
        state.MinorRadius = 100;
        host.RunAnimation(0);
        host.ParseAuthoredScript();
    }
}

/// <summary>CPunchOb::AI (0x4A75E0).</summary>
public sealed class PunchObBrain(PunchObState state, IPunchObHost host)
{
    public void Advance(int attackClock)
    {
        if (host.HasMessages) host.DeleteAllMessages();
        if (host.AnimationFinished) host.RunAnimation(0);
        host.BuildTransform();
        if ((attackClock & 0xF) == 0 && state.Stage != 1 && host.HasTrapWeb)
        {
            state.Stage = 1;
            state.Substate = 0;
        }
        if (state.Stage == 1)
        {
            switch (state.Substate)
            {
                case 0:
                    state.Elapsed += host.ElapsedTicks;
                    if (state.Elapsed > 180)
                    {
                        host.ItemFlags |= 0x200;
                        state.Substate = 1;
                    }
                    break;
                case 1:
                    host.SourceScale += 512;
                    if (host.SourceScale >= 6144)
                    {
                        host.BurstTrapWeb();
                        state.Substate++;
                    }
                    break;
                case 2:
                    host.SourceScale -= 256;
                    if (host.SourceScale <= 4096)
                    {
                        host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~0x200));
                        state.Stage = 0;
                        state.Substate = 0;
                    }
                    break;
                default:
                    throw new InvalidDataException("CPunchOb substate is outside the source state table.");
            }
        }
        if ((host.InputFlags & 1) != 0) host.HitFromInput(state.Health);
    }
}
