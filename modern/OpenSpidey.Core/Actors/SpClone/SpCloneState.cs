using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Torch;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.SpClone;

/// <summary>Source CSpClone (type 327, module "spclone") constructor fields.</summary>
public sealed class SpCloneState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 327;
    public const string ModuleName = "spclone";
    public const uint LightAddress = 0x555920;
    /// <summary>G_UNK_POSE, the CSuper::ApplyPose packet both AI calls use.</summary>
    public const uint PosePacketAddress = 0x555888;
    public const string ModelName = "spidey";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short Health { get; set; }
    public ushort MinorRadius { get; set; }
    public uint BodyFlags { get; set; } = 0x216;
    /// <summary>CBaddy+0x2A8; bit 0x4000 suppresses the Die0 death pulse.</summary>
    public uint CombatFlags { get; set; }
    public int AiState { get; set; } = 1;
    public int AiSubstate { get; set; }
    public short Field21E { get; set; } = 100;
    public short ShadowScale { get; set; } = 48;
    /// <summary>realRegisterArr[0] (0x222): landing ground Y, also the MGS shadow plane.</summary>
    public short GroundY { get; set; }
    public int GravityAcceleration { get; set; }
    public int PreviousTicks { get; set; } = 2;
    public int OlderTicks { get; set; } = 2;
    public int StepScale { get; set; }
    public bool DeathPulseSent { get; private set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    /// <summary>CBaddy::Die(0) 0x404320.</summary>
    public void Die(ISpCloneHost host)
    {
        if (IsDead) return;
        if ((CombatFlags & 0x4000) == 0 && !DeathPulseSent && SourceNode != ushort.MaxValue)
        { DeathPulseSent = true; host.SendDeathPulse(); }
        BodyFlags |= 0x40; host.ItemFlags |= 1; host.MarkDead();
    }
}

public interface ISpCloneHost : ICinematicActorScriptHost
{
    FixedVector3 PlayerPosition { get; }
    /// <summary>Byte 0x60CFC4.</summary>
    bool FastForwardRequested { get; }
    bool HasMessages { get; }
    bool HasSourceJoints { get; }
    AimAngles HeadAngles { get; set; }
    void DeleteAllMessages();
    AimAngles LookTarget(int node);
    void ApplyPosePacket();
    void BuildTransform();
    void UpdateShadow();
    void SendDeathPulse();
    void MarkDead();
}

/// <summary>Original CSpClone::AI 0x4AFF30: tick-history physics, landing, analogue-input bytecode, head look and MGS shadow.</summary>
public sealed class SpCloneBrain(SpCloneState state, ISpCloneHost host, CinematicActorScriptRuntime script)
{
    /// <summary>Constructor 0x4AFD60 after squirting position/angles.</summary>
    public static void Construct(SpCloneState state, ISpCloneHost host, Action attachToBaddyList)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(attachToBaddyList);
        state.BodyFlags |= 8; // ShadowOn
        host.RunAnimation(0xCA);
        host.ItemFlags |= 0x480;
        attachToBaddyList();
        if (host.FastForwardRequested) state.Die(host);
    }

    public void Advance(int ticks)
    {
        if (host.HasMessages) host.DeleteAllMessages();
        if (host.FastForwardRequested) { state.Die(host); return; }
        state.StepScale = unchecked(1365 * (ticks + state.OlderTicks + state.PreviousTicks));
        TorchMotion.Advance(host.Body, state.StepScale, state.GravityAcceleration);
        switch (state.AiState)
        {
            case 1:
                switch (state.AiSubstate)
                {
                    case 0:
                        AimAngles aim = FixedAim.Calculate(host.Body.Position, host.PlayerPosition with { Y = host.Body.Position.Y });
                        host.Body.Angles = new(aim.Pitch, aim.Yaw, 0);
                        int ground = host.GroundHeight(host.Body.Position, 0, 0x800, 0);
                        if (ground != -1)
                        { state.GravityAcceleration = 0x2000; state.GroundY = unchecked((short)(ground >> 12)); state.AiSubstate++; }
                        break;
                    case 1:
                        int landing = unchecked((state.GroundY - state.Field21E) << 12);
                        if (host.Body.Position.Y > landing)
                        {
                            host.RunAnimation(203);
                            host.Body.Position = host.Body.Position with { Y = landing };
                            state.GravityAcceleration = 0; host.Body.Velocity = default; state.AiSubstate++;
                        }
                        break;
                    case 2:
                        if (host.AnimationFinished) { state.AiState = 2; state.AiSubstate = 0; }
                        break;
                    default: throw new InvalidDataException("SpClone landing substate is outside the source state table.");
                }
                break;
            case 2:
                if (state.AiSubstate == 0) { script.Begin(); state.AiSubstate = 1; }
                if (state.AiSubstate != 1) throw new InvalidDataException("SpClone script substate is outside the source state table.");
                if (script.Running) script.Update(ticks, abort: false);
                else state.Die(host);
                break;
            default: throw new InvalidDataException("SpClone state is outside the source state table.");
        }
        state.OlderTicks = state.PreviousTicks;
        state.PreviousTicks = ticks;
        AimAngles look = script.LookTargetNode != 0 ? host.LookTarget(script.LookTargetNode) : default;
        if (!host.HasSourceJoints) host.ApplyPosePacket();
        host.HeadAngles = CinematicActorPose.AdvanceHead(host.HeadAngles, look);
        if ((host.ItemFlags & 4) != 0 && script.Running && (host.HeadAngles.Pitch | host.HeadAngles.Yaw) == 0)
            host.ItemFlags &= 0xFFFB;
        if ((host.ItemFlags & 4) != 0) host.ApplyPosePacket();
        else host.BuildTransform();
        host.UpdateShadow();
    }
}
