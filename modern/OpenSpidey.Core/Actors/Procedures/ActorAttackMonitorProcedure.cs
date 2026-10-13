using System.Numerics;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Combat;

namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Original animated hook collision and player damage/effect boundaries.</summary>
public interface IActorAttackMonitorHost
{
    int AnimationId { get; }
    int AnimationFrame { get; }
    int DomeCount { get; }
    ushort AttackDamage { get; }
    FixedVector3 PlayerPosition { get; }
    short PlayerHealth { get; }
    FixedVector3 GetActorHookPosition(ActorHook hook);
    /// <summary>Web_CollideWithSuper against the player's current part poses and hook boxes, at the supplied Q12 box scale.</summary>
    bool CollideWithPlayer(FixedVector3 from, FixedVector3 to, int collisionScale, out ActorHook hitHook);
    void CreateCombatImpactEffect(FixedVector3 position, int flags);
    void PlayPositionSound(ushort sound, FixedVector3 position, int volume);
    void HitPlayer(ActorHit hit);
    void Victorious();
}

/// <summary>Recovered CAIProc_MonitorAttack and its CBaddy::SmackSpidey hook sweep and hit behavior.</summary>
public sealed class ActorAttackMonitorProcedure : IActorProcedure
{
    private readonly ActorProcedureState actor;
    private readonly IActorAttackMonitorHost host;
    private readonly FixedVector3[] previousPoints;
    public int SourceType => 1281;
    public int Flags { get; set; }
    public Action? ExecuteNext { get; set; }
    public int AnimationId { get; }
    public int StartFrame { get; }
    public int EndFrame { get; }
    public uint HookMask { get; }
    public uint CompletionMask { get; private set; }
    public bool PreviousSampleValid { get; private set; }
    public ReadOnlySpan<FixedVector3> PreviousHookPositions => previousPoints;

    public ActorAttackMonitorProcedure(ActorProcedureState actor, IActorAttackMonitorHost host,
        int startFrame, uint hookMask, int frameCount, uint completionMask)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(host);
        this.actor = actor;
        this.host = host;
        AnimationId = host.AnimationId;
        StartFrame = startFrame;
        EndFrame = unchecked(startFrame + frameCount - 1);
        HookMask = hookMask;
        CompletionMask = completionMask;
        previousPoints = new FixedVector3[BitOperations.PopCount(hookMask)];
        actor.CompletionFlags &= ~completionMask;
    }

    public void Execute(int elapsedTicks)
    {
        if (host.AnimationId != AnimationId) Flags |= 1;
        else if (host.AnimationFrame >= StartFrame && host.AnimationFrame <= EndFrame)
        {
            if (SmackPlayer())
            {
                if (CompletionMask != 0)
                {
                    actor.CompletionFlags |= CompletionMask;
                    CompletionMask &= 0xF0F0;
                }
                Flags |= 1;
            }
            // The source sets this even when dome interference suppressed all hook samples.
            PreviousSampleValid = true;
        }
        ExecuteNext?.Invoke();
    }

    private bool SmackPlayer()
    {
        if (host.DomeCount != 0) return false;
        int slot = 0;
        for (int part = 0; part < 32; part++)
        {
            if ((HookMask & (1u << part)) == 0) continue;
            FixedVector3 previous = previousPoints[slot];
            FixedVector3 current = host.GetActorHookPosition(new(0, 0, 0, (ushort)part));
            previousPoints[slot++] = current;
            if (!PreviousSampleValid) continue;
            FixedVector3 extended = current + ((current - previous) >> 1);
            if (!host.CollideWithPlayer(previous, extended, 0x2000, out ActorHook hitHook)) continue;

            host.CreateCombatImpactEffect(current, 0);
            ushort damage = host.AttackDamage;
            ushort sound = 17;
            if (damage < 10) sound = 15;
            else if (damage < 20) sound = 16;
            host.PlayPositionSound(sound, host.PlayerPosition, 0);
            // The source uses this same mask both for procedure completion and to suppress the damaging hit.
            if (CompletionMask == 0)
            {
                FixedVector3 difference = (host.PlayerPosition - actor.Body.Position) >> 12;
                FixedVector3 direction = FixedVectorMath.Normalize(new(difference.X, 0, difference.Z));
                host.HitPlayer(new(15, unchecked((byte)hitHook.PartIndex), 8, damage, direction));
                if (host.PlayerHealth <= 0) host.Victorious();
            }
            return true;
        }
        return false;
    }
}
