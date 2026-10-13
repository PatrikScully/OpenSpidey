using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public sealed class ThugMessage(int code, object? senderHandle = null)
{
    public int Code { get; } = code;
    public object? SenderHandle { get; } = senderHandle;
    public int Flags { get; set; }
    public int Age { get; set; }
    public FixedVector3 Vector0 { get; set; }
    public ThugMessage? Next { get; internal set; }
}

/// <summary>Source CMessage insertion order and CBaddy::CleanUpMessages deletion rules.</summary>
public sealed class ThugMessageQueue
{
    public ThugMessage? Head { get; private set; }
    public void Enqueue(ThugMessage message)
    {
        message.Next = Head;
        Head = message;
    }

    public void Cleanup(bool all = false, int code = 0)
    {
        ThugMessage? previous = null;
        for (ThugMessage? current = Head; current is not null;)
        {
            ThugMessage? next = current.Next;
            if ((current.Flags & 1) != 0 || all || current.Code == code)
            {
                if (previous is null) Head = next;
                else previous.Next = next;
                current.Next = null;
            }
            else previous = current;
            current = next;
        }
    }
}

public interface IThugMessageHost : IActorMotionHost
{
    ushort ItemFlags { get; set; }
    uint WaypointFlags { get; }
    bool HasJoints { get; }
    short GetJointRoll(int index);
    void SetJointRoll(int index, short roll);
    void ApplyMessagePose(int actorType);
    void TrySendToMessageSender(ThugMessage message, int code);
    void ReplyToStoredHostage(int code);
    void GetReadyToShootHostage(ThugMessage message);
    bool TryGetMessageSenderPosition(ThugMessage message, out FixedVector3 position);
    void RunToWhereTheActionIs(FixedVector3 position);
    void DeleteLaser();
    bool IsCurrentThugListOwner { get; }
    void ClearThugListOwner();
    void BurstTrapWeb();
    void BurstTugWeb();
    void PlayMessageSound(uint sound, FixedVector3 position, int pitchOffset);
    void SendSelfMessage(int code);
}

/// <summary>Recovered CThug::ProcessMessages, including persistent messages and deferred cleanup.</summary>
public static class ThugMessageProcessor
{
    public static void Process(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        ThugMessageQueue messages, IThugMessageHost host)
    {
        var stateFlags = new ThugStateFlags();
        for (ThugMessage? message = messages.Head; message is not null; message = message.Next)
        {
            if (state.State != 26 || message.Code >= 11)
            {
                switch (message.Code)
                {
                    case 10:
                        if (state.State is 2 or 1) host.RunToWhereTheActionIs(message.Vector0);
                        state.AlertTicks = 600;
                        break;
                    case 20:
                        int oldAge = message.Age;
                        if (state.State != 26)
                        {
                            message.Age = unchecked(oldAge + 1);
                            if (oldAge < 8)
                            {
                                host.ItemFlags |= 4;
                                if (host.HasJoints)
                                {
                                    int otherJoint = 6 + (state.Type == 304 ? 1 : 0);
                                    if (message.Age >= 4)
                                    {
                                        Roll(3, host.GetJointRoll(3) >> 1);
                                        Roll(otherJoint, host.GetJointRoll(otherJoint) >> 1);
                                    }
                                    else
                                    {
                                        Roll(3, host.GetJointRoll(3) + (800 >> message.Age));
                                        Roll(otherJoint, host.GetJointRoll(otherJoint) - (800 >> message.Age));
                                    }
                                }
                                host.ApplyMessagePose(state.Type);
                                continue;
                            }
                        }
                        if (!host.HasJoints) host.ApplyMessagePose(state.Type);
                        Roll(3, 0);
                        Roll(6 + (state.Type == 304 ? 1 : 0), 0);
                        host.ItemFlags = (ushort)(host.ItemFlags & ~4);
                        break;
                    case 19:
                        if (actor.Body.Angles.X != 0)
                        {
                            actor.Body.Angles = actor.Body.Angles with { X = 0 };
                            actor.Body.Position = actor.Body.Position with
                            { Y = unchecked(actor.Body.Position.Y + 6144 * state.YankOscillationTicks) };
                        }
                        state.YankOscillationTicks = 0;
                        state.CombatFlags &= ~0x20u;
                        break;
                    case 13:
                        if (state.State == 8 || (stateFlags.Check(state.State) & 1) != 0 || (host.WaypointFlags & 0x10) != 0)
                        {
                            host.TrySendToMessageSender(message, 1);
                            message.Flags |= 1;
                        }
                        else host.GetReadyToShootHostage(message);
                        continue;
                    case 16: host.ReplyToStoredHostage(1); break;
                    case 17: host.DeleteLaser(); break;
                    case 12:
                        host.ApplyMessagePose(state.Type);
                        if (state.State != 26)
                        {
                            Roll(1, host.GetJointRoll(1) >> 1);
                            if (host.GetJointRoll(1) > 1) continue;
                        }
                        Roll(1, 0);
                        if (host.IsCurrentThugListOwner && state.State != 9) host.ClearThugListOwner();
                        host.ItemFlags = (ushort)(host.ItemFlags & ~4);
                        break;
                    case 14:
                        if ((state.CombatFlags & 0x40) == 0)
                        {
                            host.BurstTrapWeb();
                            host.PlayMessageSound(0x802D, actor.Body.Position, 0);
                        }
                        break;
                    case 15:
                        if (state.State == 14) AttachStateMessage(15);
                        else host.BurstTugWeb();
                        break;
                    case 5:
                        if (state.State != 18)
                        {
                            host.Neutralize();
                            state.State = 18;
                            state.Substate = 0;
                        }
                        else
                        {
                            if (state.Substate is not (3 or 2)) continue;
                            state.ActionCounter = 0;
                            state.Substate = 2;
                        }
                        break;
                    case 6:
                        if (state.State != 19)
                        {
                            host.Neutralize();
                            host.PlayMessageSound(0x800E, actor.Body.Position, 0);
                            state.State = 19;
                            state.Substate = 0;
                            AttachStateMessage(15);
                        }
                        break;
                    case 7:
                        if ((state.State is 2 or 1) && host.TryGetMessageSenderPosition(message, out FixedVector3 position))
                            host.RunToWhereTheActionIs(position);
                        break;
                }
            }
            message.Flags |= 1;
        }
        messages.Cleanup();

        void Roll(int joint, int value) => host.SetJointRoll(joint, unchecked((short)value));
        void AttachStateMessage(int code) => procedures.Attach(new ThugStateSwitchMessageProcedure(state, code, host.SendSelfMessage));
    }
}
