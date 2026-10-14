using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Owns the player model and schedules the source's two-tick movement updates.</summary>
public partial class GodotPlayer : Node3D
{
    private int physicsTicks;
    private bool destroyed;
    private IReadOnlyList<byte>? fistVariants;
    private byte fistAnimation;
    private PlayerButton pendingButtons;
    private readonly byte[] pendingPhysicalTriggers = new byte[20];
    private GodotZipWebStrand? activeStrand;
    private readonly List<GodotZipWebStrand> strands = [];
    private readonly List<GodotWebImpactSplat> splats = [];
    private PlayerEffectTextures? effectTextures;
    public PlayerTerrainController Controller { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public PlayerFrameSounds FrameSounds { get; private set; } = new();
    public PlayerAnimationSoundDriver Animation { get; private set; } = null!;
    public GodotPlayerCombat Combat { get; private set; } = null!;
    public GodotPlayerMelee? Melee {get;private set;}
    public GodotPlayerInventory? Inventory { get; set; }
    public PlayerActorState ActorState { get; } = new();
    public PlayerPowerUpCollectionState PowerUpCollection { get; } = new();
    public IMissionContactFrameHost? MissionContacts { get; set; }
    public Action<PlayerFrameSound>? SoundOutput { get; set; }
    public Action? ResetCinematicScreen { get; set; }
    public uint SourceFootstepBank { get; private set; }
    public bool UseLevelFootstepAliases { get => SourceFootstepBank != 0; set => SourceFootstepBank = value ? 1u : 0u; }
    public void SetSourceFootstepBank(ushort value) => SourceFootstepBank = value;
    public int SourceUpdates { get; private set; }
    public int LiveZipStrands => strands.Count;
    public int LiveWebSplats => splats.Count;
    public int LiveWebKnots => strands.Sum(strand => strand.VisibleKnots);
    public Func<short>? CameraHeading { get; set; }
    public Func<PlayerInputSample> SampleInput { get; set; } = PlayerControls.Read;
    public Func<PlayerButtonInput> SampleButtons { get; set; } = PlayerControls.ReadButtons;
    public GodotPlayerScripts? Scripts { get; set; }
    public GodotPlayerCameraStartup? CameraStartup { get; set; }
    public Action? DeleteAdditionalPlayerBodies { get; set; }
    public PlayerButtonState[] PhysicalPad { get; } = new PlayerButtonState[20];
    public event Action<Exception>? Faulted;

    public static GodotPlayer Create(FixedVector3 position, short yaw, ImportedAnimatedModel model,
        PlayerTerrainPolicy policy, PlayerEffectTextures? effectTextures = null, short health = 100, int? maxHealth = null, Func<int, int>? random = null)
    {
        PlayerControls.Register();
        var player = new GodotPlayer { Name = "Player", Model = model, effectTextures = effectTextures, FrameSounds = new(random) };
        model.AdvanceAutomatically = false;
        player.AddChild(model);
        player.Animation = new(model, player.FrameSounds);
        player.Controller = new(position, yaw, new GodotPlayerWorldQuery(player), player.Animation, policy, random);
        player.Controller.BeginPlayerAi += player.BeginSourceAi;
        player.Combat = new(player, health, maxHealth ?? health);
        player.Combat.HiddenParts=model.SourceHiddenParts;
        player.CacheBodyHook();
        player.ApplyTransform();
        return player;
    }

    public override void _PhysicsProcess(double delta)
    {
        PlayerButtonInput buttons = SampleButtons();
        PlayerControls.ReadPhysicalPad(PhysicalPad);
        for (int index = 0; index < PhysicalPad.Length; index++)
        {
            pendingPhysicalTriggers[index] |= PhysicalPad[index].Triggered;
            PhysicalPad[index] = PhysicalPad[index] with { Triggered = pendingPhysicalTriggers[index] };
        }
        pendingButtons |= buttons.Triggered;
        if (++physicsTicks % 2 != 0) return;
        try
        {
            AdvanceSourceUpdate(SampleInput(), CameraHeading?.Invoke() ?? Controller.Orientation.Heading, 2,
                buttons with { Triggered = pendingButtons });
            pendingButtons = PlayerButton.None;
            Array.Clear(pendingPhysicalTriggers);
        }
        catch (Exception exception)
        {
            SetPhysicsProcess(false);
            if (Faulted is null) GD.PushError(exception.ToString());
            else Faulted(exception);
        }
    }

    private void BeginSourceAi(uint sourceTime)
    {
        CameraStartup?.Advance();
        Scripts?.PreparePlayerAi();
    }

    /// <summary>One complete source update, also used by native checks with automatic processing disabled.</summary>
    public void AdvanceSourceUpdate(PlayerInputSample sample, short cameraHeading, int ticks = 2,
        PlayerButtonInput buttons = default)
    {
        MissionContacts?.BeginContactFrame();
        CacheBodyHook();
        Controller.Step(sample, cameraHeading, ticks, buttons, Scripts?.Active == true ? Scripts.ReadControls : null);
        if (Melee is null) Model.ApplySourceJointPose(Model.SourcePoseEnabled);
        SynchronizeHandParts();
        SourceUpdates++;
        if (FrameSounds.Update(Model.ClipId, Model.Playback.Frame, UseLevelFootstepAliases) is { } sound) SoundOutput?.Invoke(sound);
        Melee?.ProcessAnimationSoundWords(Model.ClipId,Model.Playback.Frame);
        ApplyTransform();
        UpdateZipEffects();
        MissionContacts?.EndContactFrame();
        Controller.CompletePlayerAi();
    }

    public void ConfigureHandVariants(IReadOnlyList<byte> variants)
    {
        if(variants.Count!=300 || variants.Any(variant=>variant>3))throw new ArgumentException("Player hand selection requires the original300-slot fists lookup.",nameof(variants));
        fistVariants=Array.AsReadOnly(variants.ToArray());
        SynchronizeHandParts(true);
    }
    private void SynchronizeHandParts(bool force=false)
    {
        if(fistVariants is not null && (force || Model.ClipId!=fistAnimation))
        {
            Combat.HiddenParts=PlayerHandParts.Select(Combat.HiddenParts,fistVariants[Model.ClipId]);
            fistAnimation=unchecked((byte)Model.ClipId); // Source fieldAB4 is a byte, including clips above255.
        }
        Model.SetSourceHiddenParts(Combat.HiddenParts);
    }

    private void UpdateZipEffects()
    {
        if (!Controller.ZipWebVisible && strands.Count == 0 && splats.Count == 0) return;
        FixedVector3 hook = PlayerBodyHook.Evaluate(Controller.Position, Controller.Position,
            Controller.Orientation.Basis, Model.GetSourcePartPose(PlayerRigParts.WebHand));
        Camera3D? camera = GetViewport().GetCamera3D();
        FixedVector3 cameraPosition = camera is null ? Controller.Position : PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition);
        if (Controller.ZipWebVisible && activeStrand is null && Controller.ZipTarget is { } target)
        {
            activeStrand = GodotZipWebStrand.Create(target.Contact.Position, hook, effectTextures);
            AddChild(activeStrand);
            strands.Add(activeStrand);
            if (effectTextures is not null)
            {
                GodotWebImpactSplat splat = GodotWebImpactSplat.Create(target.Contact.Position,
                    target.Contact.Normal, effectTextures.ImpactSplat);
                AddChild(splat);
                splats.Add(splat);
            }
        }
        if (!Controller.ZipWebVisible && activeStrand is not null)
        {
            activeStrand.Strand.Release();
            activeStrand = null;
        }
        for (int index = strands.Count - 1; index >= 0; index--)
        {
            GodotZipWebStrand strand = strands[index];
            strand.Advance(hook, cameraPosition, 2);
            if (strand.Strand.Finished) strands.RemoveAt(index);
        }
        for (int index = splats.Count - 1; index >= 0; index--)
        {
            GodotWebImpactSplat splat = splats[index];
            splat.Advance();
            if (splat.Splat.Finished) splats.RemoveAt(index);
        }
    }

    public void VerifyZipEffects()
    {
        foreach (GodotZipWebStrand strand in strands) strand.VerifyMesh();
        foreach (GodotWebImpactSplat splat in splats) splat.VerifyMesh();
    }

    public void AttachToSurface(PlayerWallAttachment attachment)
    {
        Controller.AttachToSurface(attachment);
        ApplyTransform();
    }

    private void CacheBodyHook()
    {
        FixedVector3 previousPosition = Controller.Position;
        var previousBasis = Controller.Orientation.Basis;
        var previousBodyPose = Model.GetSourcePartPose(PlayerRigParts.Body);
        Controller.BodyHook = position => PlayerBodyHook.Evaluate(position, previousPosition, previousBasis, previousBodyPose);
    }

    private void ApplyTransform()
    {
        Transform3D transform = PsxCoordinates.CharacterTransform(Controller.Orientation.Basis);
        transform.Origin = PsxCoordinates.FixedWorldPosition(Controller.Position);
        Transform = transform;
    }

    public void RefreshCombatTransform() => ApplyTransform();
    public PlayerCarriedSaveState ReadCarriedSaveState()
    {
        PlayerWebInventory inventory = Inventory?.State ?? throw new InvalidOperationException("The source player has no web inventory.");
        return new(inventory.Webbing, inventory.Cartridges, Combat.State.ArmorAmount,
            Combat.State.ArmorTexturesActive ? (byte)1 : (byte)0, Combat.State.SuitArmorEnabled);
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || destroyed) return;
        destroyed = true;
        Combat?.StopFightMusic();
        DeleteAdditionalPlayerBodies?.Invoke();
        DeleteAdditionalPlayerBodies = null;
        Melee?.Dispose();
        Controller.BeginPlayerAi -= BeginSourceAi;
        Controller.Melee=null;
        Animation.SourceScriptsSelected=null;
        ResetCinematicScreen?.Invoke();
        ResetCinematicScreen = null;
    }
    public void ConfigureMelee(GodotPlayerMeleeConfiguration configuration)
    {
        if(Melee is not null)throw new InvalidOperationException("Player melee already has its source owner.");
        var melee=new GodotPlayerMelee(this,configuration);
        Melee=melee;
        Controller.Melee=melee;
        Animation.SourceScriptsSelected=clip=>melee.SelectAndResetAnimationSounds(checked((ushort)clip));
    }
}
