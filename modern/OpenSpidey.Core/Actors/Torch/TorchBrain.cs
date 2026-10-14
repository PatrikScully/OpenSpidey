using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Torch;

/// <summary>Source CTorch constructor fields (source328, module "torch").</summary>
public sealed class TorchState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 328;
    public const int SurvivesFastForwardLevel = 0x803;
    public const uint LightAddress = 0x557FE8;
    public const uint PosePacketAddress = 0x557F20;
    public const string ModelName = "torch";
    public const string ModuleName = "torch";
    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short SurfaceClearance { get; } = 100;
    public ushort MinorRadius { get; } = 0;
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; }
    public uint CombatFlags { get; set; }
    public int Stage { get; set; } = 1;
    public int Substate { get; set; }
    public int GravityAcceleration { get; set; }
    public short GroundY { get; set; }
    public int StepScale { get; set; }
    public int PreviousTicks { get; set; } = 2;
    public int OlderTicks { get; set; } = 2;
    public bool DeathPulseSent { get; private set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
    public bool DiesOnFastForward => LevelId != SurvivesFastForwardLevel;

    public void Die(ITorchHost host)
    {
        if (IsDead) return;
        if ((CombatFlags & 0x4000) == 0 && !DeathPulseSent && SourceNode != ushort.MaxValue)
        { DeathPulseSent = true; host.SendDeathPulse(); }
        BodyFlags |= 0x40; host.ItemFlags |= 1; host.MarkDead();
    }
}

public interface ITorchHost : ICinematicActorScriptHost
{
    FixedVector3 PlayerPosition { get; }
    int AnimationFrame { get; }
    bool FastForwardRequested { get; }
    bool HasMessages { get; }
    bool HasSourceJoints { get; }
    AimAngles HeadAngles { get; set; }
    void DeleteAllMessages();
    void AdvanceTextureWibble();
    AimAngles LookTarget(int node);
    void ApplyPosePacket();
    void BuildTransform();
    void UpdateShadow();
    void SendDeathPulse();
    void MarkDead();
}

/// <summary>Original CTorch::DoPhysics: one step scaled by the three-frame tick history, not per-two-tick integration.</summary>
public static class TorchMotion
{
    public static void Advance(ActorBodyMotion body, int stepScale, int gravityAcceleration)
    {
        ArgumentNullException.ThrowIfNull(body);
        body.Acceleration = new(0, unchecked(gravityAcceleration - body.Velocity.Y / 16), 0);
        body.Velocity += (body.Acceleration * stepScale) >> 12;
        int squared = unchecked((int)((uint)(stepScale * stepScale) >> 12));
        body.Position += ((body.Acceleration * squared) / 2 >> 12) + ((body.Velocity * stepScale) >> 12);
        body.Angles = (body.Angles with { Y = unchecked((short)(body.Angles.Y + (stepScale * body.AngularVelocity.Y >> 12))) }).Mask();
        body.AngularVelocity = body.AngularVelocity with
        { Y = unchecked((short)(body.AngularVelocity.Y + (stepScale * body.AngularAcceleration.Y >> 12))) };
        body.AngularVelocity = new(Damp(body.AngularVelocity.X, body.AngularDamping.X),
            Damp(body.AngularVelocity.Y, body.AngularDamping.Y), Damp(body.AngularVelocity.Z, body.AngularDamping.Z));
    }

    private static short Damp(short value, byte shift)
    {
        short damped = unchecked((short)(value - (value >> shift)));
        return damped is >= -1 and <= 1 ? (short)0 : damped;
    }
}

/// <summary>Original CTorch::AI: landing, cinematic bytecode, tick history, head easing and MGS shadow.</summary>
public sealed class TorchBrain(TorchState state, ITorchHost host, CinematicActorScriptRuntime script)
{
    /// <summary>Constructor tail after squirting position/angles: run4, flags, BaddyList attach, then abort death.</summary>
    public static void Construct(TorchState state, ITorchHost host, Action attachToBaddyList)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(attachToBaddyList);
        host.RunAnimation(4);
        host.ItemFlags |= 0x480;
        attachToBaddyList();
        if (host.FastForwardRequested && state.DiesOnFastForward) state.Die(host);
    }

    public void Advance(int ticks)
    {
        if (host.FastForwardRequested && state.DiesOnFastForward) { state.Die(host); return; }
        if (host.HasMessages) host.DeleteAllMessages();
        host.AdvanceTextureWibble();
        if (host.AnimationId != 1) state.BehaviorFlags &= ~1u;
        else if ((state.BehaviorFlags & 1) == 0 && host.AnimationFrame >= 18) state.BehaviorFlags |= 1;
        state.StepScale = unchecked(1365 * (ticks + state.OlderTicks + state.PreviousTicks));
        TorchMotion.Advance(host.Body, state.StepScale, state.GravityAcceleration);
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
                            host.RunAnimation(5);
                            host.Body.Position = host.Body.Position with { Y = landing };
                            state.GravityAcceleration = 0; host.Body.Velocity = default; state.Substate++;
                        }
                        break;
                    case 2:
                        if (host.AnimationFinished) { state.Stage = 2; state.Substate = 0; }
                        break;
                    default: throw new InvalidDataException("Torch landing substate is outside the source state table.");
                }
                break;
            case 2:
                if (state.Substate == 0) { script.Begin(); state.Substate = 1; }
                if (state.Substate != 1) throw new InvalidDataException("Torch cinematic substate is outside the source state table.");
                if (script.Running) script.Update(ticks, host.FastForwardRequested);
                else if (state.LevelId == TorchState.SurvivesFastForwardLevel) { state.Stage = 4; state.Substate = 0; }
                else state.Die(host);
                break;
            case 4:
                if (host.AnimationId != 0 || host.AnimationFinished) host.RunAnimation(0);
                break;
            default: throw new InvalidDataException("Torch stage is outside the source state table.");
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
