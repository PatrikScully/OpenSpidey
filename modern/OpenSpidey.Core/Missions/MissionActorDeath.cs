using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Missions;

public enum SourceMissionDeathList { Baddy, Control, Environment, PowerUp, Suspended }

/// <summary>Actual source body pointers and virtual/deleting-destructor boundaries; no scene deletion substitute for Die.</summary>
public interface IMissionActorDeathHost
{
    object? ReadHead(SourceMissionDeathList list);
    object? ReadNext(SourceMissionDeathList list, object body);
    ushort ReadNode(object body);
    bool IsDead(object body);
    FixedVector3 ReadPosition(object body);
    void Die(object body);
    void DeleteSuspendedBody(object body);
    void HideEnvironmentItem(uint checksum);
    void KillSpecialDisplay(ushort node);
}

/// <summary>SendKillFromNode How1: CBody vtable slot3 Hit and Exp_HitEnvItem instead of Die and ENV flag1.</summary>
public interface IMissionActorLoudDeathHost : IMissionActorDeathHost
{
    /// <summary>Unsigned CBody health word +0xE2.</summary>
    ushort ReadHealth(object body);
    void Hit(object body, ActorHit hit);
    /// <summary>Spool_FindEnviroItem then Exp_HitEnvItem(item, null, damage); a missing checksum is ignored.</summary>
    void HitEnvironmentItem(uint checksum, ushort damage);
}

/// <summary>Original11 SendKill and133 box sweep, preserving distinct pointer-read and virtual-death policies.</summary>
public static class MissionActorDeath
{
    /// <summary>Original12 (0x4E3190 How1): only SHitInfo flag byte4, damage = health, zero direction are written.</summary>
    public static void SendKillLoudly(TriggerFile triggers, TriggerNode source, IMissionActorLoudDeathHost host)
    {
        foreach (ushort nodeId in MissionRuntime.ReadLinks(source))
        {
            if (nodeId >= triggers.Nodes.Count || triggers.Nodes[nodeId].Id != nodeId)
                throw new InvalidDataException($"Source SendKillLoudly references missing node{nodeId}.");
            TriggerNode target = triggers.Nodes[nodeId];
            switch (target.TypeCode)
            {
                case 1:
                    if (target.Data.Length < 4) throw new InvalidDataException("Source SendKillLoudly actor header is truncated.");
                    if (BinaryPrimitives.ReadInt16LittleEndian(target.Data.Span[2..]) == 409) host.KillSpecialDisplay(nodeId);
                    else foreach (SourceMissionDeathList list in ActiveActorLists) HitInList(nodeId, list, host);
                    break;
                case 2: case 9: host.HitEnvironmentItem(TriggerEnvironmentReference.ReadChecksum(target), ushort.MaxValue); break;
                case 5: case 20: HitInList(nodeId, SourceMissionDeathList.PowerUp, host); break;
            }
        }
    }

    private static void HitInList(ushort node, SourceMissionDeathList list, IMissionActorLoudDeathHost host)
    {
        object? body = host.ReadHead(list);
        while (body is not null)
        {
            if (host.ReadNode(body) == node) host.Hit(body, new(4, 0, 0, host.ReadHealth(body), default));
            body = host.ReadNext(list, body);
        }
    }

    private static readonly SourceMissionDeathList[] ActiveActorLists =
        [SourceMissionDeathList.Baddy, SourceMissionDeathList.Control, SourceMissionDeathList.Environment];
    private static readonly SourceMissionDeathList[] BoxLists =
        [SourceMissionDeathList.Baddy, SourceMissionDeathList.Control, SourceMissionDeathList.Environment,
            SourceMissionDeathList.PowerUp, SourceMissionDeathList.Suspended];

    public static void SendKill(TriggerFile triggers, TriggerNode source, IMissionActorDeathHost host)
    {
        foreach (ushort nodeId in MissionRuntime.ReadLinks(source))
        {
            if (nodeId >= triggers.Nodes.Count || triggers.Nodes[nodeId].Id != nodeId)
                throw new InvalidDataException($"Source SendKill references missing node{nodeId}.");
            TriggerNode target = triggers.Nodes[nodeId];
            switch (target.TypeCode)
            {
                case 1:
                    if (target.Data.Length < 4) throw new InvalidDataException("Source SendKill actor header is truncated.");
                    if (BinaryPrimitives.ReadInt16LittleEndian(target.Data.Span[2..]) == 409) host.KillSpecialDisplay(nodeId);
                    else foreach (SourceMissionDeathList list in ActiveActorLists) KillInList(nodeId, list, host);
                    break;
                case 2: case 9: host.HideEnvironmentItem(TriggerEnvironmentReference.ReadChecksum(target)); break;
                case 5: case 20: KillInList(nodeId, SourceMissionDeathList.PowerUp, host); break;
            }
        }
    }
    public static void KillInList(ushort node, SourceMissionDeathList list, IMissionActorDeathHost host)
    {
        object? body = host.ReadHead(list);
        while (body is not null)
        {
            if (host.ReadNode(body) == node) host.Die(body);
            // PC4E3335/338E/33EB/3453 reads current+20 AFTER the virtual call. Dead bodies still receive that call.
            body = host.ReadNext(list, body);
        }
    }
    public static int KillEverythingInBox(FixedVector3 minimum, FixedVector3 maximum, IMissionActorDeathHost host)
    {
        int count = 0;
        // PC4E69D0 evaluates the recovered C++ '+' operands right-to-left: active heads first, suspended last.
        foreach (SourceMissionDeathList list in BoxLists)
            count = unchecked(count + KillObjectsInBox(minimum, maximum, list, host));
        return count;
    }
    public static int KillObjectsInBox(FixedVector3 minimum, FixedVector3 maximum, SourceMissionDeathList list, IMissionActorDeathHost host)
    {
        int count = 0;
        object? body = host.ReadHead(list);
        while (body is not null)
        {
            // PC4E695B captures next BEFORE IsDead and either the virtual Die or scalar deleting destructor.
            object? next = host.ReadNext(list, body);
            if (!host.IsDead(body))
            {
                FixedVector3 position = host.ReadPosition(body);
                if (position.X >= minimum.X && position.X <= maximum.X && position.Y >= minimum.Y && position.Y <= maximum.Y &&
                    position.Z >= minimum.Z && position.Z <= maximum.Z)
                {
                    if (list == SourceMissionDeathList.Suspended) host.DeleteSuspendedBody(body);
                    else host.Die(body);
                    count = unchecked(count + 1);
                }
            }
            body = next;
        }
        return count;
    }
}
