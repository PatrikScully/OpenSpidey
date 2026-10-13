using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cinematics;

/// <summary>Source CBlackCat constructor fields; cinematic state remains separate from general CBaddy scripts.</summary>
public sealed class BlackCatState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 319;
    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public byte ActorPhase => unchecked((byte)SourceConstructionOrdinal);
    public string ModelName => LevelId == 0x803 ? "bc2" : "blackcat";
    public short SurfaceClearance { get; } = 100;
    public ushort MinorRadius { get; } = 0;
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; }
    public uint CombatFlags { get; set; }
    public int Stage { get; set; } = 1;
    public int Substate { get; set; }
    public int GravityAcceleration { get; set; }
    public short GroundY { get; set; }
    public bool DeathPulseSent { get; private set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    public void Die(ICinematicBlackCatHost host)
    {
        if (IsDead) return;
        if ((CombatFlags & 0x4000) == 0 && !DeathPulseSent && SourceNode != ushort.MaxValue)
        { DeathPulseSent = true; host.SendDeathPulse(); }
        BodyFlags |= 0x40; host.ItemFlags |= 1; host.MarkDead();
    }
}

public interface ICinematicBlackCatHost : ICinematicActorScriptHost
{
    FixedVector3 PlayerPosition { get; }
    int AnimationFrame { get; }
    bool FastForwardRequested { get; }
    void PlayPositionSound(uint sound, FixedVector3 position, int pitch);
    void DeleteAllMessages();
    void SendDeathPulse();
    void MarkDead();
    void UpdatePoseAndShadow(int lookTargetNode, bool scriptRunning);
}

/// <summary>Original CBlackCat::AI: physics precedes landing/script work, and pose/shadow follow even a scripted death.</summary>
public sealed class BlackCatBrain(BlackCatState state, ICinematicBlackCatHost host, CinematicActorScriptRuntime script)
{
    public void Advance(int ticks)
    {
        if (host.FastForwardRequested && state.LevelId != 0x803) { state.Die(host); return; }
        if (host.AnimationId == 8)
        {
            if ((state.BehaviorFlags & 1) == 0 && host.AnimationFrame >= 18)
            { host.PlayPositionSound(0x819B, host.Body.Position, 0); state.BehaviorFlags |= 1; }
        }
        else state.BehaviorFlags &= ~1u;
        CinematicActorMotion.Advance(host.Body, ticks, state.GravityAcceleration);
        host.DeleteAllMessages();
        switch (state.Stage)
        {
            case 1:
                switch (state.Substate)
                {
                    case 0:
                        AimAngles aim = FixedAim.Calculate(host.Body.Position, host.PlayerPosition with { Y = host.Body.Position.Y });
                        host.Body.Angles = new(aim.Pitch, aim.Yaw, 0);
                        int ground = host.GroundHeight(host.Body.Position, 0, 0x800, 0);
                        if (ground != -1)
                        { state.GravityAcceleration = 0x2000; state.GroundY = unchecked((short)(ground >> 12)); state.Substate++; }
                        break;
                    case 1:
                        int landing = unchecked((state.GroundY - state.SurfaceClearance) << 12);
                        if (host.Body.Position.Y > landing)
                        {
                            host.PlayPositionSound(0x819A, host.Body.Position, 0); host.RunAnimation(7);
                            host.Body.Position = host.Body.Position with { Y = landing };
                            state.GravityAcceleration = 0; host.Body.Velocity = default; state.Substate++;
                        }
                        break;
                    case 2:
                        if (host.AnimationFinished) { state.Stage = 2; state.Substate = 0; }
                        break;
                    default: throw new InvalidDataException("Black Cat landing substate is outside the source state table.");
                }
                break;
            case 2:
                if (state.Substate == 0) { script.Begin(); state.Substate = 1; }
                if (state.Substate != 1) throw new InvalidDataException("Black Cat cinematic substate is outside the source state table.");
                if (script.Running) script.Update(ticks, host.FastForwardRequested);
                else if (state.LevelId == 0x803) { state.Stage = 4; state.Substate = 0; }
                else state.Die(host);
                break;
            case 4:
                if (host.AnimationId != 0 || host.AnimationFinished) host.RunAnimation(0);
                break;
            default: throw new InvalidDataException("Black Cat stage is outside the source state table.");
        }
        host.UpdatePoseAndShadow(script.LookTargetNode, script.Running);
    }
}
