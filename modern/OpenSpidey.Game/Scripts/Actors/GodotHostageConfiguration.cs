using OpenSpidey.Assets.Textures;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Actual model, player, speech, source ordering and session ownership required by a hostage.</summary>
public sealed class GodotHostageConfiguration
{
    private GodotPlayer? player;
    public required Func<string,ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<string,IReadOnlyList<ActorPartBox>> CollisionBoxes { get; init; }
    public GodotPlayer Player
    {
        get => SourcePlayer ?? throw new InvalidOperationException("Original hostage AI requires the post-census player.");
        init => player = value;
    }
    public Func<GodotPlayer?>? PlayerProvider { get; init; }
    public GodotPlayer? SourcePlayer => PlayerProvider is { } provider ? provider() : player;
    public required DecodedTexture ShadowTexture { get; init; }
    public required Func<IReadOnlyList<ActorCollisionBody>> SourceBaddyOrder { get; init; }
    public required GodotSpeechPlayer Speech { get; init; }
    public required IGodotHostageSessionHost Session { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<int,int> Random { get; init; }
    public required Func<int> SourceConstructionOrdinal { get; init; }
    public required Func<GodotHostageActor,bool> IsSourceCulled { get; init; }
}

/// <summary>Required original-list, mission signals, native weak actor handles and common constructor/waypoint effects.</summary>
public interface IGodotHostageSessionHost
{
    void AttachToSourceBaddyList(GodotHostageActor actor);
    void DetachFromSourceBaddyList(GodotHostageActor actor);
    void SendSignal(GodotHostageActor actor);
    void SendDeathPulse(GodotHostageActor actor);
    void SendMessage(GodotHostageActor sender,object target,int code);
    ActorProcedureState? RecoverActor(object? handle);
    ActorBodyMotion? RecoverBody(ActorProcedureState actor);
    bool ExecuteScriptCommand(GodotHostageActor actor,ushort opcode,ActorScriptCursor operands,ActorScriptEnvironment environment);
}
