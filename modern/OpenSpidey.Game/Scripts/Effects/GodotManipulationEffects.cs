using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Effects;

/// <summary>Required source world/game effects; no collision, message, damage or explosion callback is synthesized.</summary>
public sealed class GodotManipulationEffectsConfiguration
{
    public required GodotActorRegistry Registry { get; init; }
    public required GodotManipulationDebrisConfiguration Debris { get; init; }
    public required Func<IReadOnlyList<ManipulationTargetBody>> SourceBaddyOrder { get; init; }
    public required Func<FixedVector3> PlayerPosition { get; init; }
    public required Func<FixedVector3> CameraPositionWholeUnits { get; init; }
    public required Func<FixedVector3, int, int, int, int> GroundHeight { get; init; }
    public required Action<ushort, FixedVector3, int> PlayPositionSound { get; init; }
    public required Action<object, FixedVector3> SendExplosionMessage { get; init; }
    public required Action<FixedVector3> CreateBigExplosion { get; init; }
    public required Action<ActorHit> HitPlayer { get; init; }
    public required Action<object, ActorHit> HitBaddy { get; init; }
    public required Action<object> ReactSymbioteToExplosion { get; init; }
}

/// <summary>Native composition of real CManipOb Chunk effects: transient OBJ bodies and original Misc flat bits.</summary>
public partial class GodotManipulationEffects : Node3D, IManipulationImpactHost
{
    private GodotManipulationEffectsConfiguration configuration = null!;
    public GodotManipulationSmoke Smoke { get; private set; } = null!;
    public static GodotManipulationEffects Create(byte[] originalBits, GodotManipulationEffectsConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var effect = new GodotManipulationEffects { Name = "OriginalManipulationEffects", configuration = configuration };
        try { effect.Smoke = GodotManipulationSmoke.Create(originalBits); effect.AddChild(effect.Smoke); return effect; }
        catch { effect.Free(); throw; }
    }
    /// <summary>One original bit Move per source update; rendering alone never advances lifetime.</summary>
    public void AdvanceBits() => Smoke.Advance();
    public IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder => configuration.SourceBaddyOrder();
    public FixedVector3 PlayerPosition => configuration.PlayerPosition();
    public FixedVector3 CameraPositionWholeUnits => configuration.CameraPositionWholeUnits();
    public int GroundHeight(FixedVector3 p, int above, int below, int flags) => configuration.GroundHeight(p, above, below, flags);
    public void PlayPositionSound(ushort sound, FixedVector3 p, int volume) => configuration.PlayPositionSound(sound, p, volume);
    public void SendExplosionMessage(object target, FixedVector3 p) => configuration.SendExplosionMessage(target, p);
    public void CreateBigExplosion(FixedVector3 p) => configuration.CreateBigExplosion(p);
    public void HitPlayer(ActorHit hit) => configuration.HitPlayer(hit);
    public void HitBaddy(object target, ActorHit hit) => configuration.HitBaddy(target, hit);
    public void ReactSymbioteToExplosion(object target) => configuration.ReactSymbioteToExplosion(target);
    public void TrySpawnDebris(Func<ManipulationDebrisRequest> factory)
    {
        GodotManipulationDebris actor = GodotManipulationDebris.Create(factory, configuration.Registry, configuration.Debris);
        try { configuration.Registry.Adopt(actor); }
        catch { actor.Free(); throw; }
    }
    public void SpawnChunkSmoke(ManipulationSmokeRequest request) => Smoke.Emit(new ManipulationChunkSmoke(request, configuration.Registry.RandomSource));
    public void SendAttachedPulse(ushort node)
    {
        TriggerActorDefinition definition = TriggerActorDefinition.Read(configuration.Registry.Triggers.Nodes[node]);
        if (definition.ActorType is not 401 and not 600) throw new InvalidDataException("Manipulation impact pulse references another source actor family.");
        foreach (ushort link in definition.Links) configuration.Registry.ReceivePulse(link);
    }
}
