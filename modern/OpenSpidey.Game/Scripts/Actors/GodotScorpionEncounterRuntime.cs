using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.Actors;

public sealed class GodotScorpionEncounterContext
{
    public required PkrArchive Archive { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required ReadOnlyMemory<byte> OriginalExecutable { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<bool> JoelJewettCheat { get; init; } // Source JoelJewCheatCode, read by CJonah's constructor.
    public GodotCombatSceneOwnership? SceneOwnership { get; init; }
    public Func<GodotPlayer?>? Player { get; init; }
    public Func<uint>? Clock { get; init; }
    public Func<int>? AttackClock { get; init; }
    public Func<int>? Difficulty { get; init; }
    public SourceMusicState? Music { get; init; }
    public Func<int, int, int, bool>? PositionSpeech { get; init; } // Redbook_XAPlayPos(bank, slot, position, priority) result.
    public required PsxAssetRegions Regions { get; init; }
}

/// <summary>CCamera and CPlayer camera transports read by the Scorpion and Jonah AIs.</summary>
public interface IGodotEncounterCamera : IScorpionBossCameraHost
{
    short Angle { get; }
    void SetAngle(short angle, ushort ticks);
    void SetXOffset(short offset, ushort ticks);
    void SetYOffset(short offset, ushort ticks);
    void SetZOffset(short offset, ushort ticks);
    void TrackJonahPositionAndOrientation(JonahState jonah);
    void PutPlayerCameraBehind(int argument); // CPlayer::SetCeilingCamera 4BF820.
}

/// <summary>Source "scorpion"/"jonah" relocatable modules (482D20/444250 clears, slot0 factories) and the shared Panel health bars.</summary>
public partial class GodotScorpionEncounterRuntime : Node
{
    private GodotScorpionEncounterContext context = null!;
    private GodotScorpionEncounterResources? resources;
    private GodotScorpionHealthBarResources? panelResources;
    private GodotScorpionHealthBarPresenter? presenter;
    private bool factoriesRegistered;
    private SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor>? modules;
    private DecodedTexture? shadowTexture;
    private PlayerFollowCamera? camera;
    private ScorpionBossCamera? bossCamera;
    private GodotEncounterCameraBridge? encounterCamera;
    private Func<object?>? heldActor;
    /// <summary>Source gBossRelated0x56E998, read by the camera's boss modes15/16/17.</summary>
    public CameraFocusTarget? GlobalBoss { get; private set; }
    internal Memory<FixedVector3> TailTrail { get; } = new FixedVector3[6]; // Source6A73F8.
    internal BossHealthBars<ScorpionEncounterBodyState, GodotBossHealthBarTexture> HealthBars { get; } = new(body => body.Health);
    internal GodotScorpionHealthBarPresenter? HealthBarPresenter => presenter;
    public GodotActorRegistry Registry => context.Registry;
    public SourceVertexLightingState LightingState => context.LightingState;
    public bool JoelJewettCheat => context.JoelJewettCheat();
    public GodotScorpionEncounterResources Resources =>
        resources ??= new(context.Archive, context.Repository, context.OriginalExecutable, requireMissionBanks: true);
    internal SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> Modules =>
        modules ?? throw new InvalidOperationException("Source Scorpion/Jonah modules are not registered.");
    internal PkrArchive Archive => context.Archive;
    internal PsxLibraryRepository Repository => context.Repository;
    internal ReadOnlyMemory<byte> OriginalExecutable => context.OriginalExecutable;
    internal PsxAssetRegions Regions => context.Regions;
    internal GodotCombatSceneOwnership SceneOwnership => context.SceneOwnership ?? throw Missing("scene combat ownership");
    internal GodotPlayer Player => (context.Player ?? throw Missing("scene player"))() ?? throw Missing("constructed scene player");
    internal uint Clock => (context.Clock ?? throw Missing("source vblank clock"))();
    internal int AttackClock => (context.AttackClock ?? throw Missing("attack clock"))();
    internal int Difficulty => (context.Difficulty ?? throw Missing("difficulty"))();
    internal SourceMusicState Music => context.Music ?? throw Missing("music state");
    internal bool PlayPositionSpeech(int bank, int slot, int priority) => (context.PositionSpeech ?? throw Missing("positional speech"))(bank, slot, priority);
    /// <summary>Source camera global0x56F3B8 as CScorpion reaches it; null while no player camera exists.</summary>
    internal IScorpionBossCameraHost? BossCamera => camera is not null && IsInstanceValid(camera) ? bossCamera : null;
    internal PlayerFollowCamera? BoundCamera => camera is not null && IsInstanceValid(camera) ? camera : null;
    internal IGodotEncounterCamera EncounterCamera => encounterCamera ?? throw new NotSupportedException(
        "The boss AIs need CameraList: boss mode16, BossCamSpinRate/StationaryRadius, XZ/Y distances, hidden items and Jonah tracking. Call BindCamera after the player camera is composed.");
    internal DecodedTexture ShadowTexture => shadowTexture ??=
        context.Repository.RequireLoaded("shell_dc").Textures.Single(texture => texture.Checksum == GodotActorShadow.ShadowTextureChecksum);
    private static InvalidOperationException Missing(string owner) => new($"The source Scorpion encounter AI requires its {owner}.");

    public static GodotScorpionEncounterRuntime Create(GodotScorpionEncounterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new() { Name = "OriginalScorpionEncounterModules", context = context };
    }

    /// <summary>Publishes the session's player camera as camera global0x56F3B8 and feeds its boss modes from0x56E998.</summary>
    public void BindCamera(PlayerFollowCamera camera, GodotCameraItemHiding hiding)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(hiding);
        this.camera = camera;
        bossCamera = new(camera.Ai, hiding.GetBossHiddenItem, hiding.Items.ClearItem);
        encounterCamera = new(camera, this);
        camera.SourceBoss = () => GlobalBoss;
    }

    /// <summary>Source player0xE48 held CItem identity for boss camera hide-item scans.</summary>
    internal void BindHeldActor(Func<object?> readHeldActor) => heldActor = readHeldActor ?? throw new ArgumentNullException(nameof(readHeldActor));
    internal object? ReadHeldActor() => heldActor?.Invoke();

    /// <summary>CScorpion AI's0x56E998 store; its body position is source mPos.</summary>
    internal void SetGlobalBoss(GodotScorpionEncounterActor? boss) =>
        PublishGlobalBoss(boss is null ? null : new CameraFocusTarget(boss, () => boss.SourcePosition));

    /// <summary>Shared gBossRelated0x56E998 publish used by CScorpion and CRhino.</summary>
    public void PublishGlobalBoss(CameraFocusTarget? boss) =>
        GlobalBoss = boss is null ? null : ReferenceEquals(GlobalBoss?.Identity, boss.Identity) ? GlobalBoss : boss;

    /// <summary>CScorpion destructor4836D0: clears0x56E998, then sets mode3 whenever a camera exists.</summary>
    internal void ReleaseBossCamera()
    {
        GlobalBoss = null;
        if (BossCamera is { } host && host.Mode.Mode != 3) host.SetMode(3);
    }

    public void Register(SourceModuleRegistry<GodotActorModuleArguments, GodotScriptActor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (factoriesRegistered) throw new InvalidOperationException("Source Scorpion/Jonah factories are already registered.");
        modules.Register("scorpion", module =>
        {
            module.SetClear(() => ClearSourceBaddies(ScorpionState.ActorType));
            module.SetUserFunction(0, arguments =>
            {
                GodotScorpionEncounterActor actor = GodotScorpionEncounterActor.Create(Validate(arguments).Node, this, ScorpionState.ActorType);
                actor.AddChild(new GodotNativeDestructionObserver { Name = "SourceScorpionCameraDestructor", Destroyed = ReleaseBossCamera });
                actor.AddChild(new GodotNativeDestructionObserver { Name = "SourceScorpionTailDestructor", Destroyed = actor.ReleaseTailRegions });
                return actor;
            });
            module.SetTypedUserFunction<GodotScorpionEncounterActor, object?>(1, scorpion => scorpion.RequireScorpionAi().CurrentTargetForModule());
        });
        modules.Register("jonah", module =>
        {
            module.SetClear(() => ClearSourceBaddies(JonahState.ActorType));
            module.SetUserFunction(0, arguments => GodotScorpionEncounterActor.Create(Validate(arguments).Node, this, JonahState.ActorType));
            module.SetTypedUserFunction<GodotScorpionEncounterActor, uint>(1, jonah => JonahState.WhatShouldScorpionDo(RequireJonah(jonah).State));
            module.SetUserAction<GodotScorpionEncounterActor>(2, jonah => RequireJonah(jonah).ShouldPlead());
        });
        this.modules = modules;
        context.Registry.RegisterFactory(ScorpionState.ActorType, (node, owner) => modules.CallUserFunction("scorpion", 0, new(node, owner)));
        context.Registry.RegisterFactory(JonahState.ActorType, (node, owner) => modules.CallUserFunction("jonah", 0, new(node, owner)));
        factoriesRegistered = true;
    }

    /// <summary>Panel_CreateHealthBar: the panel textures come from the L2A2_G and bits banks already loaded by autoexec.</summary>
    internal void CreateHealthBar(ScorpionEncounterBodyState body, short actorType, Func<ScorpionEncounterBodyState, int> readHitFlashUpdates)
    {
        panelResources ??= GodotScorpionHealthBarResources.Create(context.Archive, context.Repository);
        HealthBars.Create(body, actorType, panelResources.FindTexture);
        if (presenter is not null) return;
        presenter = GodotScorpionHealthBarPresenter.Create(panelResources, HealthBars, readHitFlashUpdates);
        AddChild(presenter);
    }

    public void DrawHealthBars(CanvasItem canvas, HudViewportTransform viewport, int screenOffsetY)
    {
        if (presenter is not null && HealthBars.Primary is not null) presenter.DrawOn(canvas, viewport, screenOffsetY);
    }

    private GodotActorModuleArguments Validate(GodotActorModuleArguments arguments) => ReferenceEquals(arguments.Registry, context.Registry) ? arguments :
        throw new InvalidOperationException("Source module factory cannot transfer ownership to a different scene registry.");

    private static JonahState RequireJonah(GodotScorpionEncounterActor actor) => actor.State as JonahState ??
        throw new InvalidOperationException("Source module jonah user functions require a CJonah argument.");

    private void ClearSourceBaddies(short type)
    {
        foreach (object identity in context.Registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder)
            if (identity is GodotScriptActor actor && context.Registry.Owns(actor) && actor.SourceActorType == type)
                context.Registry.DestroyImmediately(actor);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (presenter is null) panelResources?.Dispose();
    }

    private sealed class GodotEncounterCameraBridge(PlayerFollowCamera camera, GodotScorpionEncounterRuntime runtime) : IGodotEncounterCamera
    {
        public CameraModeState Mode => camera.Ai.Mode;
        public int SpinRate { get => camera.Ai.Boss.SpinRate; set => camera.Ai.Boss.SpinRate = value; }
        public int StationaryRadius { get => camera.Ai.Boss.StationaryRadius; set => camera.Ai.Boss.StationaryRadius = value; }
        public ushort GetHorizontalDistance() => unchecked((ushort)camera.Ai.Rig.Parameter(CameraParameter.HorizontalDistance).Value);
        public short GetVerticalDistance() => unchecked((short)camera.Ai.Rig.Parameter(CameraParameter.VerticalDistance).Value);
        public void SetHorizontalDistance(ushort distance, ushort duration) => camera.SetEncounterHorizontalDistance(distance, duration);
        public void SetVerticalDistance(short distance, ushort duration) => camera.SetEncounterCameraYOffset(distance, duration);
        public void SetMode(uint mode) => camera.ScriptRig.SetMode(checked((ushort)mode));
        public IScorpionCameraHiddenItem? GetHiddenItem(int index) => runtime.bossCamera?.GetHiddenItem(index);
        public void ClearHiddenItem(int index) => runtime.bossCamera?.ClearHiddenItem(index);
        public short Angle => camera.EncounterCameraAngle;
        public void SetAngle(short angle, ushort ticks) => camera.SetEncounterCameraAngle(angle, ticks);
        public void SetXOffset(short offset, ushort ticks) => camera.SetEncounterCameraXOffset(offset, ticks);
        public void SetYOffset(short offset, ushort ticks) => camera.SetEncounterCameraYOffset(offset, ticks);
        public void SetZOffset(short offset, ushort ticks) => camera.SetEncounterCameraZOffset(offset, ticks);
        public void PutPlayerCameraBehind(int argument) => camera.PutBehind(argument);
        public void TrackJonahPositionAndOrientation(JonahState jonah)
        {
            GodotScorpionEncounterActor? actor = FindJonahActor(jonah);
            if (actor is null) return;
            camera.TrackJonahEncounterView(jonah, actor.Motion.Body.Position, actor.Motion.Body.Angles.Y);
        }

        private GodotScorpionEncounterActor? FindJonahActor(JonahState jonah)
        {
            foreach (object identity in runtime.Registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder)
                if (identity is GodotScorpionEncounterActor { State: JonahState state } actor && ReferenceEquals(state, jonah))
                    return actor;
            return null;
        }
    }
}
