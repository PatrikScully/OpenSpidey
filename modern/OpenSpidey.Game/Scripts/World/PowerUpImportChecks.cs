using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Authored power-up records, actual banks/native terrain, shared player resources, canonical source lists and protected-child teardown.</summary>
internal static class PowerUpImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, GodotPlayer player,
        GodotPlayerCombatRuntime combat, GodotSoundChannels sounds, GodotSpeechPlayer speech, Func<uint> sourceClock)
    {
        PlayerCombatState state = player.Combat.State; PlayerWebInventory inventory = player.Inventory!.State;
        var saved = (state.Position, state.ActionState, state.Health, state.ArmorActive, state.ArmorTexturesActive, state.ArmorAmount,
            state.ArmorAnimationVariant, state.SuitArmorEnabled, inventory.Webbing, inventory.Cartridges, inventory.FireWebbing, inventory.PickupRevision, inventory.LastCartridgeClock);
        var collectionState = player.PowerUpCollection;
        var savedCollection = (collectionState.PickupJingleReady, collectionState.LastHealthPickupClock);
        var resources = new GodotPlayerPowerUpResources(player, archive, collectionState, () => 2);
        var progress = new CampaignProgress(); int originalParticles = combat.Particles.Count; int models = 0, sprites = 0, pulsePickups = 0;
        var emittedGlows = new List<OpenSpidey.Core.Effects.IGlowShape>();
        void RememberGlow(OpenSpidey.Core.Effects.SourceParticleRegistration bit)
        { if (bit.Identity is OpenSpidey.Core.Effects.IGlowShape glow) emittedGlows.Add(glow); }
        combat.Glows.SourceBitCreated += RememberGlow;
        try
        {
            ImportedGeometryOwnershipImportChecks.Verify(owner, repository.Load("data/l1a1_g.psx"), repository.Load("items"));
            await VerifyBank("l1a1", [41, 42, 52, 53]);
            await VerifyBank("l2a1", [149]);
            await VerifyBank("l1a4", [286, 349]);
            await VerifyBank("l7a3", [144]);
            await VerifyBank("lga1", [2]);
            await VerifyBank("lha1", [6]);
            await VerifyArmorHud();
            foreach (var glow in emittedGlows.ToArray()) combat.Glows.Remove(glow);
            Assert(combat.Particles.Count == originalParticles, "Every actual model/shadow/training flat and glow must release its real shared bit identity after the bounded proof.");
            GD.Print($"Verified native power-ups: {models} genuine items models, {sprites} current-level TrainingPick-Up_01 sprites, {pulsePickups} real mission-pulse pickup, source health/web/fire/comic/armor effects, rejection latch, original difficulty suppression, already-collected comic death, actual costarm HUD and canonical protected-child cleanup.");
        }
        finally
        {
            combat.Glows.SourceBitCreated -= RememberGlow;
            foreach (var glow in emittedGlows.ToArray()) combat.Glows.Remove(glow);
            (state.Position, state.ActionState, state.Health, state.ArmorActive, state.ArmorTexturesActive, state.ArmorAmount,
                state.ArmorAnimationVariant, state.SuitArmorEnabled, inventory.Webbing, inventory.Cartridges, inventory.FireWebbing, inventory.PickupRevision, inventory.LastCartridgeClock) = saved;
            (collectionState.PickupJingleReady, collectionState.LastHealthPickupClock) = savedCollection;
            player.Controller.ApplyCombatState();
        }

        async Task VerifyBank(string bank, ushort[] nodes)
        {
            var viewport = new SubViewport { Name = $"OriginalPowerUp_{bank}", OwnWorld3D = true, Size = new(640, 480),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
            var root = new Node3D(); viewport.AddChild(root);
            var actors = new List<GodotPowerUpActor>();
            GodotActorRegistry? observedRegistry = null;
            try
            {
                var proofLibraries = new PsxLibraryRepository(archive);
                ImportedLevelWorld world = ImportedLevelWorld.Load(proofLibraries, $"data/{bank}_g.psx"); root.AddChild(world);
                var camera = new Camera3D { Current = true }; root.AddChild(camera); camera.MakeCurrent();
                TriggerFile triggers = TriggerFile.Read(archive.Read($"data/{bank}_t.trg"));
                var registry = new GodotActorRegistry { Name = "OriginalPowerUpList", Triggers = triggers, Player = player, Difficulty = 2,
                    Environment = new(LevelId: MissionLevelId.FromAssetName(bank)), RandomSource = new GameRandom(0x46AFC0).Next };
                observedRegistry = registry; registry.Spawned += RecordPowerUp;
                root.AddChild(registry); registry.SetPhysicsProcess(false);
                var mission = new MissionRuntime(triggers, registry, []); registry.ReceivePulse = mission.ReceivePulse;
                var config = new GodotPowerUpConfiguration
                {
                    Items = proofLibraries.Load("items"), ShadowTexture = proofLibraries.Load("shell_dc").Textures.Single(texture => texture.Checksum == GodotActorShadow.ShadowTextureChecksum),
                    TrainingPickupTexture = () => proofLibraries.Load($"data/{bank}_g.psx").Textures.Single(texture => texture.Checksum ==
                        NamedTextureTable.Find(archive.Read($"data/{bank}_g.tex"), "TrainingPick-Up_01")),
                    World = new(root), Glows = combat.Glows, Progress = progress, Collection = collectionState,
                    SourceClock = sourceClock, LogicFrame = () => 1, SpeechBusy = () => speech.Controller.Busy,
                    SendSignal = registry.SignalActiveActors, PlaySound = (sound, volume, pitch) => { Assert(pitch == 0, "Original pickup sound pitch must remain zero."); sounds.Controller.Play(sound, unchecked((short)volume)); },
                    PlaySpeech = (speechBank, slot, priority) => speech.Play(speechBank, slot, priority), SetArmor = resources.SetArmor,
                    SourceBitCreated = combat.Particles.Attach, SourceBitRemoved = identity => combat.Particles.Detach(identity)
                };
                var factory = new GodotPowerUpFactory(config);
                registry.PowerUps = factory;
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                foreach (ushort id in nodes)
                {
                    TriggerNode node = triggers.Nodes[id]; TriggerPowerUpDefinition definition = TriggerPowerUpDefinition.Read(node);
                    mission.ReceivePulse(node.Id);
                    GodotPowerUpActor actor = registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder.OfType<GodotPowerUpActor>().FirstOrDefault() ??
                        throw new InvalidDataException("The real mission pulse did not construct its authored pickup.");
                    Assert(registry.Owns(actor) && registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder[0] == actor && actor.State.HasNode == 1 && actor.State.NodeId == id,
                        "Actual ctor must adopt its native identity once at the original head insertion, before factory SetNode.");
                    state.Position = actor.State.Position; state.ActionState = 1; player.Controller.ApplyCombatState();
                    registry.SourceList(ActorUpdateGroup.PowerUp).Update();
                    Assert(actor.SourceUpdates == 1 && actor.State.Timing.ElapsedTicks == 2 && actor.State.AngularVelocityY == 24 && actor.State.Yaw == 48,
                        "Genuine power-up EveryFrame first tick must feed its actual yaw/AI owner.");
                    camera.GlobalPosition = PsxCoordinates.FixedWorldPosition(actor.State.Position) + new Vector3(0, 3, 12);
                    camera.LookAt(PsxCoordinates.FixedWorldPosition(actor.State.Position));
                    PowerUpVisibilityImportChecks.Verify(registry, world, proofLibraries, actor, Assert);
                    if (definition.Type == 18)
                    {
                        GodotPowerUpSprite bit = actor.SourcePickupBit ?? throw new InvalidDataException("Actual authored type18 has no protected CFlatBit.");
                        uint checksum = NamedTextureTable.Find(archive.Read($"data/{bank}_g.tex"), "TrainingPick-Up_01");
                        Assert(bit.SourceFrame.TextureChecksum == checksum && bit.SourceFrame.Width == 127 && bit.SourceFrame.Height == 127 &&
                            bit.SourceFrame.OffsetX == -63 && bit.SourceFrame.OffsetY == -63 && bit.SourceSpriteScale == 400 && bit.Protected,
                            "Source named training texture, synthetic rectangle, scale and protection must survive native construction.");
                        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); bit.Rebuild();
                        Assert(bit.RenderMesh.GetSurfaceCount() == 1, "The genuine protected training flat must project its original current-level texture into the native viewport.");
                        state.Position = actor.State.Position + new FixedVector3(14000 << 12, 0, 0); player.Controller.ApplyCombatState();
                        registry.SourceList(ActorUpdateGroup.PowerUp).Update();
                        Assert(actor.Lifecycle.Phase == ActorLifePhase.Suspended && (actor.BodyFlags & 1) != 0 && actor.State.PlayerDistance == 14000 &&
                            actor.SourcePickupBit is null && actor.SourceShadow is null && registry.SuspendedSourceList.SourceOrder.Contains(actor),
                            "Genuine radial suspension must call DeleteStuff, retire protected child bits and move the actual body into the shared suspended list.");
                        state.Position = actor.State.Position; player.Controller.ApplyCombatState(); registry.SuspendedSourceList.Update();
                        Assert(actor.Lifecycle.Phase == ActorLifePhase.Active && (actor.BodyFlags & 1) == 0 && registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder.Contains(actor),
                            "Source UnSuspend must clear body bit1 and reattach the same actual PowerUp identity.");
                        registry.SourceList(ActorUpdateGroup.PowerUp).Update();
                        Assert(actor.SourcePickupBit is { } recreated && !ReferenceEquals(recreated, bit) && actor.SourceUpdates == 2,
                            "The next genuine source AI must recreate its owned training flat without changing body identity or advancing AI while suspended.");
                        sprites++;
                    }
                    else
                    {
                        Assert(actor.ModelIndex == config.Items.FindModel(actor.State.ModelChecksum!.Value) && actor.RenderModel?.Mesh is ArrayMesh mesh && mesh.GetSurfaceCount() > 0,
                            "Actual power-up must retain its selected items checksum and genuine GPU geometry."); models++;
                    }
                    int soundCount = sounds.StartedCount;
                    var collection = new GodotPlayerPowerUpCollection(player, registry, collectionState, config.PlaySound);
                    if (definition.Type is 15 or 16)
                    {
                        state.Health = (short)state.MaxHealth; collectionState.PickupJingleReady = 1;
                        collection.CollectPowerUps();
                        Assert(actor.State.FailedCollectionFlash == 1 && collectionState.PickupJingleReady == 0 && sounds.StartedCount == soundCount + 1 && (actor.BodyFlags & 0x40) == 0,
                            "Full health rejects the actual nearby pickup, flashes it and consumes the genuine one-shot jingle latch.");
                        registry.SourceList(ActorUpdateGroup.PowerUp).Update();
                        Assert(actor.State.Tint == 0x101010 && (actor.State.ItemFlags & 0xC00) == 0xC00, "Rejected health must update the actual imported model's source tint/transparency flags.");
                        state.Health = 1; collection.CollectPowerUps();
                        int gain = actor.State.Health * state.MaxHealth / 100;
                        Assert(state.Health == Math.Min(state.MaxHealth, 1 + gain) && collectionState.LastHealthPickupClock == sourceClock() && (actor.BodyFlags & 0x40) != 0,
                            "Accepted health uses its real difficulty-remapped percentage, shared clock and source death.");
                    }
                    else if (definition.Type == 8)
                    {
                        state.Health = 100; inventory.Webbing = 0; inventory.Cartridges = 0; inventory.FireWebbing = 0;
                        int pending = mission.PendingCount; collection.CollectPowerUps();
                        Assert(inventory.Webbing == 4096 && (actor.BodyFlags & 0x40) != 0 && sounds.StartedCount == soundCount + 1,
                            "Actual web cartridge must fill the shared source inventory and play its original collection sound.");
                        if (definition.Links.Count != 0) Assert(mission.PendingCount > pending, "Authored cartridge death must pulse its real linked mission node.");
                    }
                    else if (definition.Type == 11)
                    {
                        int pending = mission.PendingCount; collection.CollectPowerUps(); pulsePickups++;
                        Assert(actor.State.HasNode == 0 && (actor.BodyFlags & 0x40) != 0 && mission.PendingCount > pending && combat.Glows.Count > 0,
                            "Actual mission pickup must send genuine node links once, clear its latch, die and emit the original glow.");
                    }
                    else if (definition.Type == 10)
                    {
                        collection.CollectPowerUps(); Assert((progress.CollectedComicMask & (1u << actor.State.Health)) != 0 && (actor.BodyFlags & 0x40) != 0,
                            "Authored comic cover must write the real persistent mask and retire its native body.");
                        mission.ReceivePulse(node.Id);
                        var duplicate = registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder.OfType<GodotPowerUpActor>().First();
                        Assert((duplicate.BodyFlags & 0x40) != 0 && duplicate.State.HasNode == 1 && duplicate.State.NodeId == node.Id,
                            "Already-collected comic must die inside the ctor before source factory SetNode, without duplicate pulse bookkeeping.");
                    }
                    else if (definition.Type == 13)
                    {
                        state.ArmorActive = state.ArmorTexturesActive = false; collection.CollectPowerUps();
                        Assert(state.ArmorActive && state.ArmorTexturesActive && state.ArmorAmount == 100 && resources.ArmorHealthIcon?.Name == "costarm",
                            "Authored armor must update the shared player fields and genuine costarm icon owner.");
                    }
                    else if (definition.Type == 12)
                    {
                        inventory.FireWebbing = 0; collection.CollectPowerUps(); Assert(inventory.FireWebbing != 0 && (actor.BodyFlags & 0x40) != 0,
                            "Authored fire-web pickup must activate the shared genuine fire inventory variant.");
                        if (bank == "l7a3" && id == 144)
                        {
                            GodotPowerUpActor[] linked = actors.Where(pickup => !ReferenceEquals(pickup, actor)).ToArray();
                            Assert(definition.Links.SequenceEqual(new ushort[] { 392, 393 }) && linked.Length == 2 &&
                                linked.Select(pickup => pickup.SourceNodeId).SequenceEqual(definition.Links) &&
                                registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder.OfType<GodotPowerUpActor>()
                                    .Where(pickup => !ReferenceEquals(pickup, actor)).Select(pickup => pickup.SourceNodeId)
                                    .SequenceEqual(definition.Links.Reverse()) && linked.All(pickup =>
                                {
                                    TriggerPowerUpDefinition original = TriggerPowerUpDefinition.Read(triggers.Nodes[pickup.SourceNodeId]);
                                    return original.Type == 12 && original.Links.Count == 0 && pickup.Type == 12 &&
                                        pickup.State.HasNode == 1 && pickup.State.ModelChecksum == 0x12820A41 &&
                                        pickup.Definition.Position == original.Position && pickup.SourceUpdates == 0 &&
                                        (pickup.BodyFlags & 0x40) == 0 && registry.Owns(pickup);
                                }),
                                "Actual L7A3 fire pickup144 death must construct both authored392/393 pickups in link order, prepend them393/392 and retain their genuine metadata before the separate actor pass.");
                        }
                    }
                    else if (definition.Type == 18)
                    {
                        int revision = inventory.PickupRevision; collection.CollectPowerUps(); Assert(inventory.PickupRevision == unchecked(revision + actor.State.Health) && (actor.BodyFlags & 0x40) != 0,
                            "Authored training pickup must add its actual lifetime-encoded score/revision amount.");
                        GodotPowerUpSprite aliveChild = actor.SourcePickupBit!; combat.Particles.RemoveDead();
                        Assert(ReferenceEquals(actor.SourcePickupBit, aliveChild) && aliveChild.Attached,
                            "Protected flat lifetime must remain independent of parent zombie40 until the real source destructor releases it.");
                    }
                    if (bank != "l1a1" && id == nodes[^1])
                        PowerUpVisibilityImportChecks.VerifyUnloadedRegion(registry, world, proofLibraries, Assert);
                    string beforeDestruction = string.Join("; ", actors.Select(Describe));
                    object[] ownedCompanions = actors.SelectMany(pickup => new object?[] { pickup.SourceShadow, pickup.SourcePickupBit })
                        .Where(bit => bit is not null).Cast<object>().ToArray();
                    int glowsBeforeDestruction = combat.Glows.Count;
                    foreach (GodotPowerUpActor live in actors.Where(registry.Owns).ToArray()) registry.DestroyImmediately(live);
                    object[] remaining = registry.SourceList(ActorUpdateGroup.PowerUp).SourceOrder.ToArray();
                    string afterDestruction = string.Join("; ", actors.Select(Describe));
                    Assert(actors.All(pickup => pickup.ResourcesDisposed && pickup.SourceShadow is null && pickup.SourcePickupBit is null) && remaining.Length == 0 &&
                        !Enum.GetValues<OpenSpidey.Core.Effects.SourceParticleList>().SelectMany(combat.Particles.Identities)
                            .Any(bit => ownedCompanions.Any(owned => ReferenceEquals(bit, owned))),
                        $"Immediate source destruction must release genuine models, Flat/Quad companions and canonical PowerUp registration. " +
                        $"Bank={bank}, requested node={id}, before=[{beforeDestruction}], after=[{afterDestruction}], " +
                        $"remaining=[{string.Join(';', remaining.OfType<GodotPowerUpActor>().Select(Describe))}], " +
                        $"PowerUp count={registry.SourceList(ActorUpdateGroup.PowerUp).Count}, suspended={registry.SuspendedSourceList.Count}, " +
                        $"particles={combat.Particles.Count}, glows={glowsBeforeDestruction}->{combat.Glows.Count}.");
                    Assert(combat.Glows.Count == glowsBeforeDestruction,
                        "Standalone Exp_GlowFlash identities must survive PowerUp destruction; CPowerUp DeleteStuff owns only its actual optional pGlow and protected companions.");
                    actors.Clear();

                    string Describe(GodotPowerUpActor pickup)
                    {
                        bool valid = GodotObject.IsInstanceValid(pickup);
                        return $"node{pickup.SourceNodeId}/type{pickup.Type}/managed{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pickup)}" +
                            $"/native{(valid ? pickup.GetInstanceId() : 0)}/valid{valid}/queued{(valid && pickup.IsQueuedForDeletion())}" +
                            $"/owned{registry.Owns(pickup)}/phase{pickup.Lifecycle.Phase}/flags{pickup.BodyFlags:X4}" +
                            $"/disposed{pickup.ResourcesDisposed}/shadow{pickup.SourceShadow is not null}/flat{pickup.SourcePickupBit is not null}";
                    }
                }
                if (bank == "l1a1")
                {
                    registry.Difficulty = 3;
                    TriggerFile hardTriggers = TriggerFile.Read(archive.Read("data/l3a1_t.trg")); TriggerNode small = hardTriggers.Nodes[11];
                    Assert(TriggerPowerUpDefinition.Read(small).Type == 14 && factory.Create(small, registry) is null && registry.LiveCount == 0,
                        "The genuine hardest-difficulty factory must suppress actual health14 before constructing/adopting a body.");
                    var reduced = (GodotPowerUpActor)factory.Create(triggers.Nodes[42], registry)!;
                    Assert(reduced.State.Type == 14 && reduced.State.Health == 20 && reduced.SourceNodeId == 42,
                        "Hardest difficulty must reduce the genuine authored health15 to source type14 while retaining its actual mission node.");
                    registry.DestroyImmediately(reduced);
                }
            }
            finally
            {
                if (observedRegistry is not null) observedRegistry.Spawned -= RecordPowerUp;
                foreach (GodotPowerUpActor actor in actors) if (GodotObject.IsInstanceValid(actor)) actor.Free();
                viewport.Free();
            }

            void RecordPowerUp(GodotScriptActor actor)
            { if (actor is GodotPowerUpActor pickup) actors.Add(pickup); }
        }
        async Task VerifyArmorHud()
        {
            resources.SetArmor(true);
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
                var hud = GodotPlayerHud.Create(archive, player, sourceClock); viewport.AddChild(hud); hud.BindPowerUpResources(resources);
                try
                {
                    SpriteAnimationFrame icon = resources.HealthIcon!.Value;
                    Assert(hud.CurrentFrame().OfType<OpenSpidey.Core.Presentation.PlayerHudSprite>().Any(sprite => sprite.Frame == icon),
                        "HUD must choose the actual armor frame before the original suit/default icon.");
                    DecodedTexture decoded = resources.ArmorIconTextures.Single(texture => texture.Checksum == icon.TextureChecksum);
                    using Image image = hud.Textures[icon.TextureChecksum].GetImage();
                    Assert(image.GetData().SequenceEqual(decoded.RgbaPixels), "Actual costarm palette/texture pixels must reach the native HUD unchanged.");
                    await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using Image capture = viewport.GetTexture().GetImage();
                    Assert(capture.GetData().Where((_, index) => (index & 3) == 3).Any(value => value != 0), "Actual armor HUD must render in both source and wide viewports.");
                    resources.SetArmor(false);
                    Assert(resources.ArmorHealthIcon is null, "Armor removal must clear the actual source animation pointer before suit/default fallback.");
                    resources.SetArmor(true);
                }
                finally { viewport.Free(); }
                Assert(hud.ResourcesDisposed, "Actual armor icon uploads must retire with their HUD owner.");
            }
            PlayerCarriedConstructorState constructor = PlayerCarriedSavePolicy.CreatePlayer(new(0, 0, 37, 1, 0), 2,
                player.Combat.CurrentSuit, 0x101, false, armourTexturesActive: 1);
            resources.ApplyConstructorState(constructor);
            Assert(state.ArmorAmount == 37 && resources.ArmorHealthIcon is not null && !state.ArmorActive && state.ArmorTexturesActive && state.SuitArmorEnabled == 0,
                "Source constructor must load costarm independently of5E9, retain the existing shared armor global and restore the distinct57C gate.");
        }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native power-up: {evidence}"); }
}
