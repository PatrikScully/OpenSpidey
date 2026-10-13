using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Required source world/effect ownership for a live enemy. No fallback AI or effect implementations.</summary>
public sealed class GodotThugConfiguration
{
    private GodotPlayer? player;
    public required Func<string, ImportedAnimatedModel> ModelFactory { get; init; }
    public GodotPlayer Player
    {
        get => SourcePlayer ?? throw new InvalidOperationException("Source thug AI requires the actual constructed scene player.");
        init => player = value ?? throw new ArgumentNullException(nameof(value));
    }
    public Func<GodotPlayer?>? PlayerProvider { get; init; }
    public GodotPlayer? SourcePlayer => PlayerProvider is { } provider ? provider() : player;
    public required ThugCombatCoordinator Combat { get; init; }
    public required Func<IReadOnlyList<GodotThugActor>> SourceBaddyOrder { get; init; }
    public required IGodotThugNativeEffects Effects { get; init; }
    public required GodotThugSpitParticles SpitParticles { get; init; }
    public required GodotActorSpriteRing SpriteRings { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<uint> Timer { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<int> SourceConstructionOrdinal { get; init; }
    public required Func<GodotThugActor, bool> IsSourceCulled { get; init; }
}

/// <summary>Native effects and interactions requiring scene-owned source assets or other live actors.</summary>
public interface IGodotThugNativeEffects
{
    int DomeCount { get; }
    bool IgnoreSphereRadius { get; }
    int PlayerWebComboValue { get; }
    void PlayPositionSound(uint sound, FixedVector3 position, int pitchOffset);
    void PlayGlobalSound(ushort sound, int volume, int pitchOffset);
    void CreateCombatImpact(GodotThugActor actor, FixedVector3 position, int flags);
    void CreateShadow(GodotThugActor actor);
    void KillShadow(GodotThugActor actor);
    void UpdateAppearanceAndShadow(GodotThugActor actor);
    void ClearLaser(GodotThugActor actor);
    void DeleteBarrelLine(GodotThugActor actor);
    void SetLaser(GodotThugActor actor, FixedVector3 start, FixedVector3 end);
    void DrawTracer(GodotThugActor actor, ThugShotLine line, object? attachment, byte red, byte green, byte blue);
    void DrawBarrelFlash(GodotThugActor actor, ThugShotLine line, byte red, byte green, byte blue);
    void DrawGlowFlash(FixedVector3 position, IReadOnlyList<int> constructorArguments);
    void SpawnGrenade(GodotThugActor actor, ThugGrenadeRequest request);
    void NotifyKill(int actorType);
    void PlayDialogue(GodotThugActor actor, int bank, int clip, FixedVector3 position, int attenuation);
    bool GrabUpdate(GodotThugActor actor, ref FixedVector3 position, ref short yaw);
    IThugInjuryBody? StruckGameObject(GodotThugActor actor, int first, int second);
    void ElasticCollision(GodotThugActor actor, IThugInjuryBody target, FixedVector3 velocity, int duration);
    void CheckFallBack(GodotThugActor actor);
    void ApplyHitToBody(IThugInjuryBody body, ActorHit hit);
    IThugTrapWeb? RecoverWeb(GodotThugActor actor, bool tugWeb);
    void SetPlayerWebCombo(int duration, int amount);
    ActorProcedureState? RecoverActor(object? handle);
    ActorBodyMotion? RecoverBody(ActorProcedureState actor);
    void SendMessage(GodotThugActor sender, object targetHandle, int code);
    void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition);
    void SendSignal(GodotThugActor actor);
    bool ExecuteScriptCommand(GodotThugActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment);
}
