using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

public sealed class GodotLevelOneBombConfiguration
{
    public required ReadOnlyMemory<byte> OriginalTriggerData { get; init; }
    public required MissionCountdown Countdown { get; init; }
    public required Func<uint> Clock { get; init; }
    public required GodotActorWorldQuery World { get; init; }
    public required GodotActorSessionDirectory Actors { get; init; }
    public required Action<uint, FixedVector3, int> PlayPositionSound { get; init; }

    internal TriggerActorDefinition BaseDefinition(TriggerNode node, TriggerActorDefinition definition)
    {
        if (definition.ActorType != 600 || node.SourceOffset < 0 || node.SourceOffset > OriginalTriggerData.Length - node.Data.Length ||
            !OriginalTriggerData.Span.Slice(node.SourceOffset, node.Data.Length).SequenceEqual(node.Data.Span))
            throw new InvalidDataException("Bomb constructor requires its exact original type600 TRG slice.");
        int offset = checked(node.SourceOffset + definition.ObjectDataOffset + 18);
        int length = checked(definition.BehaviorData.Length + 2);
        if (offset > OriginalTriggerData.Length - length)
            throw new InvalidDataException("Bomb constructor's original adjacent impact-sound word is absent.");
        // Genuine L1A4 node14 omits field104 from its record. CManipOb reads the following node15 kind1000.
        // Retain that exact adjacent word at this checked import boundary; no synthetic padding or model duplication.
        return definition with { ActorType = 401, BehaviorData = OriginalTriggerData.Slice(offset, length) };
    }
}

/// <summary>CL1A3Bomb specialization over one native CManipOb owner, its real ground/collision/audio and shared panel timer.</summary>
public sealed class GodotLevelOneBomb : ILevelOneBombHost, IDisposable
{
    private readonly GodotManipulationActor actor;
    private readonly GodotActorRegistry registry;
    private readonly GodotLevelOneBombConfiguration configuration;
    private bool disposed;
    private readonly ActorFrameTiming timing = new();
    public LevelOneBombState State { get; }
    public uint LastUpdateTick => timing.LastUpdateTick;
    public int ElapsedTicks => timing.ElapsedTicks;
    public int AccumulatedTicks => timing.AccumulatedTicks;
    internal GodotLevelOneBomb(GodotManipulationActor actor, GodotActorRegistry registry, GodotLevelOneBombConfiguration configuration)
    {
        this.actor = actor; this.registry = registry; this.configuration = configuration;
        State = new(actor.State, actor.ScriptState); LevelOneBombBehavior.Construct(State, configuration.Countdown);
    }
    internal int PrepareElapsedTicks()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        actor.State.BodyFlags = timing.PrepareClock(Timer, actor.State.BodyFlags);
        return ElapsedTicks;
    }
    public void Advance(int elapsedTicks) => LevelOneBombBehavior.Advance(State, configuration.Countdown, elapsedTicks, this);
    public void Smash() => LevelOneBombBehavior.Smash(State, this, registry.RandomSource);
    public void Die() => LevelOneBombBehavior.Die(State, configuration.Countdown, this);
    public int Difficulty => registry.Difficulty;
    public uint Timer => configuration.Clock();
    public LevelOneBombPlayerBasis? PlayerBasis => registry.Player is { } player && GodotObject.IsInstanceValid(player)
        ? new(player.Controller.Orientation.Forward, player.Controller.Orientation.Up) : null;
    public int GetGroundHeight(FixedVector3 position, int above, int below) => configuration.World.GroundHeight(position, above, below);
    public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode) => actor.LineToItem(start, end, mode);
    public void ShakeCamera(FixedVector3 position, int magnitude) => actor.ShakeCamera(position, magnitude);
    public void PlaySound(uint sound, FixedVector3 position, int pitch) => configuration.PlayPositionSound(sound, position, pitch);
    public void SignalLinkedActors(ushort node)
    { RequireNode(node); configuration.Actors.SendSignal(actor); }
    public void PulseLinkedActors(ushort node)
    { RequireNode(node); configuration.Actors.SendDeathPulse(actor); }
    private void RequireNode(ushort node)
    { if (node != actor.SourceNodeId) throw new InvalidDataException("Bomb virtual override lost its actual source link owner."); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; LevelOneBombBehavior.Dispose(configuration.Countdown, Timer);
    }
}
