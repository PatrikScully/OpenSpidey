using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Story12 Hit/Exp_HitEnvItem host over the same canonical lists as story11.</summary>
internal sealed class GodotMissionLoudDeath(GodotMissionActorDeath death, Func<GodotActorSessionDirectory?> actors,
    Func<GodotGrenadeEnvironment> environment) : IMissionActorLoudDeathHost
{
    public object? ReadHead(SourceMissionDeathList list) => death.ReadHead(list);
    public object? ReadNext(SourceMissionDeathList list, object body) => death.ReadNext(list, body);
    public ushort ReadNode(object body) => death.ReadNode(body);
    public bool IsDead(object body) => death.IsDead(body);
    public FixedVector3 ReadPosition(object body) => death.ReadPosition(body);
    public void Die(object body) => death.Die(body);
    public void DeleteSuspendedBody(object body) => death.DeleteSuspendedBody(body);
    public void HideEnvironmentItem(uint checksum) => death.HideEnvironmentItem(checksum);
    public void KillSpecialDisplay(ushort node) => death.KillSpecialDisplay(node);

    /// <summary>CBody health +0xE2 as the unsigned word SendKillFromNode loads.</summary>
    public ushort ReadHealth(object body) => unchecked((ushort)(body switch
    {
        GodotThugActor thug => thug.State.Health,
        GodotHostageActor hostage => hostage.State.Health,
        GodotPlatformActor platform => platform.Variables.Health,
        GodotManipulationActor manipulation => manipulation.Variables.Health,
        _ => throw new NotSupportedException($"Source CBody health +0xE2 for {body.GetType().Name} has not been ported.")
    }));

    public void Hit(object body, ActorHit hit)
    {
        GodotActorSessionDirectory directory = actors() ?? throw new InvalidOperationException(
            "Source SendKillLoudly Hit requires the live actor session directory.");
        directory.ApplyHit(body, hit);
    }

    public void HitEnvironmentItem(uint checksum, ushort damage)
    {
        environment().SourceItems.FirstOrDefault(item => item.ModelChecksum == checksum)?.HitEnvironment(damage);
    }
}
