using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

/// <summary>Binds the source web-tug virtual call to the actor's real path, physics and procedure owners.</summary>
public sealed class GodotThugWebTug(GodotThugActor actor) : IThugWebTugHost
{
    public bool TugImpulse(FixedVector3 direction, IReadOnlyList<FixedVector3>? path) =>
        ThugWebTugBehavior.TugImpulse(actor.State, actor.Actor, direction, path, this);
    public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit) =>
        actor.PathCheckWithHit(from, to, clearance, out hit);
    public bool AddPointToPath(FixedVector3 position, int argument) => actor.AddPointToPath(position, argument);
    public void Neutralize() => actor.Neutralize();
    public void LookAtPoint(FixedVector3 position, uint completionMask, int rate, int tolerance) =>
        actor.Procedures.Attach(ActorLookAtProcedure.ForPoint(actor.Actor, position, completionMask, rate, tolerance));
    public void ReplaceThrowPoints(IReadOnlyList<FixedVector3> points) => actor.SetThrowPoints(points);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) => actor.PlayPositionSound(sound, position, pitchOffset);
}
