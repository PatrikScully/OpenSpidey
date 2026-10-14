using System.IO.Compression;
using System.Text;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.World;
using OpenSpidey.Assets.Audio;

int assertions = 0;
void Assert(bool value, string message)
{
    assertions++;
    if (!value) throw new Exception(message);
}
void Reject<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { assertions++; return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

Assert(Environment.Is64BitProcess, "Tests must execute in a 64-bit process.");
GameRandomTests.Run(Assert);
VectorNormalTests.Run(Assert);
ParticleSchedulerTests.Run(Assert);
ActorListTests.Run(Assert);
PlayerWebInputTests.Run(Assert);
PlayerInputFinalizerTests.Run(Assert);
PlayerLookaroundTests.Run(Assert);
PlayerLookaroundEntryTests.Run(Assert);
PlayerLookaroundSetupTests.Run(Assert);
PlayerLookaroundTargetTests.Run(Assert);
PlayerSwingWebAvailabilityTests.Run(Assert);
FireSparkTests.Run(Assert);
FireEmberTests.Run(Assert);
WebDomeModelTests.Run(Assert);
WebDomeShockTests.Run(Assert);
WebDomeTests.Run(Assert);
PlayerDomeActionTests.Run(Assert);
ItemBurstFragmentTests.Run(Assert);
ItemBurstTests.Run(Assert);
ExplosionFragmentTests.Run(Assert);
CrateExplosionTests.Run(Assert);
PsxAssetRegionTests.Run(Assert);
PlayerWebDripTests.Run(Assert);
KnottedWebSplatTests.Run(Assert);
FlamingWebTests.Run(Assert);
TrapWebTests.Run(Assert);
HudLayoutTests.Run(Assert);
ScreenTargetTests.Run(Assert);
PlayerHudTests.Run(Assert);
BitmapFontTests.Run(Assert);
MissionRuntimeTests.Run(Assert);
MissionEnvironmentPlanTests.Run(Assert, args.Length == 0 ? null : args[0]);
MissionRenderCompatibilityTests.Run(Assert);
ActorSpeechCommandTests.Run(Assert);
ActorPlayerScriptCommandTests.Run(Assert);
MissionActorDeathTests.Run(Assert);
MissionTextBoxTests.Run(Assert);
PlayerScriptDeathTests.Run(Assert);
PlayerFightMusicFadeTests.Run(Assert);
SourceSparkTests.Run(Assert);
SourceMusicTests.Run(Assert);
ActorCameraShakeCommandTests.Run(Assert);
ScorpionApproachCountdownTests.Run(Assert);
ScorpionApproachProgressTests.Run(Assert);
ScorpionConstructionTests.Run(Assert);
ScorpionTailPathTests.Run(Assert);
ScorpionTailGeometryTests.Run(Assert);
BossHealthBarTests.Run(Assert);
ScorpionHealthBarFrameTests.Run(Assert);
NamedTextureSourceNameTests.Run(Assert);
ScorpionTailSweepLayoutTests.Run(Assert);
ScorpionDamageTests.Run(Assert);
ScorpionBrainTests.Run(Assert);
JonahBrainTests.Run(Assert);
JonahLocomotionTests.Run(Assert);
JonahScorpionContactTests.Run(Assert);
JonahFootprintGeometryTests.Run(Assert);
ActorVisualEffectCommandTests.Run(Assert);
MissionTransitionTests.Run(Assert);
CampaignCompletionTests.Run(Assert, args.Length == 0 ? null : args[0]);
SourceGameFadeTests.Run(Assert);
SourceWaterPostTests.Run(Assert);
PlayerPowerUpTests.Run(Assert);
PowerUpTests.Run(Assert);
PlayerStartOrientationTests.Run(Assert);
PlayerBrightnessRenderDepthTests.Run(Assert);
ControlActorEffectsTests.Run(Assert);
PlayerCutsceneSkipTests.Run(Assert);
CameraStartPositionTests.Run(Assert);
ContactCommandTests.Run(Assert);
EnvironmentChunkTests.Run(Assert);
EnvironmentChunkProducerTests.Run(Assert);
PlayerDistanceAttackPreparationTests.Run(Assert);
PlayerHandTrailTests.Run(Assert);
PlayerAdditionalEffectTests.Run(Assert);
PlayerFlamingFistTests.Run(Assert);
PlayerAdditionalPartAngleTests.Run(Assert);
MissionCheckpointSessionTests.Run(Assert);
MissionMessageTests.Run(Assert);
MissionTextTests.Run(Assert);
MissionCountdownTests.Run(Assert);
EnvironmentVisibilityTests.Run(Assert);
CommandPointDispatchTests.Run(Assert);
CinemaTests.Run(Assert);
SpeechTests.Run(Assert);
ActorSpeechAttachmentTests.Run(Assert);
PlayerSoundTests.Run(Assert);
SoundChannelTests.Run(Assert);
ActorScriptSoundTests.Run(Assert);
PcmWaveTests.Run(Assert);
InputSuppressionTests.Run(Assert);
PlatformMotionTests.Run(Assert);
PlatformDamageTests.Run(Assert);
BaddyConstructionTests.Run(Assert);
ActorWebReactionTests.Run(Assert);
EnvironmentImpactTests.Run(Assert);
PlayerFireWebTests.Run(Assert);
PlayerWebAttackTests.Run(Assert);
WebEffectTests.Run(Assert);
ShatterFragmentTests.Run(Assert);
ShatterItemTests.Run(Assert);
KnottedWebTests.Run(Assert);
KnottedFragmentTests.Run(Assert);
WebOwnerTests.Run(Assert);
WebChainTests.Run(Assert);
WebSnapTests.Run(Assert);
WebBreakTests.Run(Assert);
PlayerWebTugTests.Run(Assert);
ThugWebTugTests.Run(Assert);
ActorBodyMotionTests.Run(Assert);
ActorSteeringTests.Run(Assert);
ActorProcedureListTests.Run(Assert);
ActorMoveToTests.Run(Assert);
ThugTests.Run(Assert);
ThugInitializationTests.Run(Assert);
ThugPatrolTests.Run(Assert);
ThugFightTests.Run(Assert);
ThugAwarenessTests.Run(Assert);
ThugRangedTests.Run(Assert);
ThugAttackTests.Run(Assert);
ThugDamageTests.Run(Assert);
ThugInjuryTests.Run(Assert);
ThugReactionTests.Run(Assert);
ThugBrainTests.Run(Assert);
ThugHostageTests.Run(Assert);
ThugConversationTests.Run(Assert);
ThugMessageTests.Run(Assert);
ThugMessageReactionTests.Run(Assert);
HostageTests.Run(Assert);
HostageDeathTests.Run(Assert);
SwitchTests.Run(Assert);
ManipulationTests.Run(Assert);
LevelOneBombTests.Run(Assert);
ManipulationChunkTests.Run(Assert);
BigExplosionTests.Run(Assert);
ThugTorsoAimTests.Run(Assert);
ThugChaseTests.Run(Assert);
ThugOwnershipTests.Run(Assert);
ThugSpitTests.Run(Assert);
ThugFrameSoundTests.Run(Assert);
ThugBulletTests.Run(Assert);
ThugImpactTests.Run(Assert);
ActorShadowTests.Run(Assert);
ThugCombatRingTests.Run(Assert);
PlayerCombatImpactTests.Run(Assert);
BouncingRockTests.Run(Assert);
CompassTests.Run(Assert);
ActorRecoilTests.Run(Assert);
PlayerKillQuipTests.Run(Assert);
PlayerGrabTests.Run(Assert);
PlayerWebInventoryTests.Run(Assert);
PlayerCarriedSaveTests.Run(Assert);
PlayerMeleeTests.Run(Assert);
PlayerKickTests.Run(Assert);
PlayerInteractionTests.Run(Assert);
PlayerTargetTests.Run(Assert);
PlayerAutoAimTests.Run(Assert);
PlayerSenseTests.Run(Assert);
PlayerTorsoTests.Run(Assert);
LineParticleTests.Run(Assert);
ActorSpriteRingTests.Run(Assert);
ScreenFlashTests.Run(Assert);
ActorScreenFlashCommandTests.Run(Assert);
ActorKineticProcedureTests.Run(Assert);
ActorAttackMonitorTests.Run(Assert);
ActorHeightControllerTests.Run(Assert);
ActorPathControllerTests.Run(Assert);
ActorHookTests.Run(Assert);
ActorJointPoseTests.Run(Assert);
ActorPartCollisionTests.Run(Assert);
ActorLaserCollisionTests.Run(Assert);
GrenadeProjectileTests.Run(Assert);
GlowStateTests.Run(Assert);
GrenadeEffectTests.Run(Assert);
ProjectileBlastDamageTests.Run(Assert);
SceneLibraryTests.Run(Assert);
TriggerActorTests.Run(Assert);
TriggerPlacementTests.Run(Assert);
ActorScriptTests.Run(Assert);
ActorLinkedBodyCommandsTests.Run(Assert);
ActorLinkEventCommandsTests.Run(Assert);
ActorDirectEventCommandsTests.Run(Assert);
ActorTimedMotionCommandsTests.Run(Assert);
ActorEnvironmentChunkCommandTests.Run(Assert);
EnvironmentPreparationTests.Run(Assert);
ActorWaypointTests.Run(Assert);
ActorLifecycleTests.Run(Assert);
CameraParameterTests.Run(Assert);
CameraModeTests.Run(Assert);
CameraCommandModeTests.Run(Assert);
PlayerLookaroundCameraValueTests.Run(Assert);
CameraViewHeadingTests.Run(Assert);
CameraTrackingTests.Run(Assert);
CinematicActorTests.Run(Assert);
BlackCatTests.Run(Assert);
PlayerLookaroundReticleTests.Run(Assert);
WebSwingAnchorTests.Run(Assert);
PlayerSwingBasisTests.Run(Assert);
SourceModuleTests.Run(Assert);
LinkedEnvironmentVisibilityTests.Run(Assert);
CinematicShadowTests.Run(Assert);
PlayerSwingPhysicsTests.Run(Assert);
ReleasedSwingWebTests.Run(Assert);
PlayerSwingActionTests.Run(Assert);
PlayerAirborneAttackSelectionTests.Run(Assert);
VertexLightingTests.Run(Assert);
PlayerSurfaceTransitionTests.Run(Assert);
CameraTripodMotionTests.Run(Assert);
PlayerSmashAttackTests.Run(Assert);
SmokeTrailTests.Run(Assert);
SwingBackCreationTests.Run(Assert);
MissionStartupTests.Run(Assert);
MissionStartupSequenceTests.Run(Assert);
MissionTimerDisplayTests.Run(Assert);
ActorFrameTimingTests.Run(Assert);
PlayerLandingRecoveryTests.Run(Assert);
PlayerForwardSelectionTests.Run(Assert);
PlayerSwingSelectionTests.Run(Assert);
PlayerCameraFacingTests.Run(Assert);
OptimumCameraTests.Run(Assert);
PlayerWalkingTests.Run(Assert);
PlayerCeilingSmashTests.Run(Assert);
PlayerAirborneMovementTests.Run(Assert);
PlayerAirborneEntryTests.Run(Assert);
PlayerTriggerZoneTests.Run(Assert);
PlayerAirborneWallTests.Run(Assert);
PlayerTorsoAimFinalizerTests.Run(Assert);
PlayerSurfaceEntryTests.Run(Assert);
FixedCameraTests.Run(Assert);
FixedQuaternionTests.Run(Assert);
CameraRotationTests.Run(Assert);
CameraShakeTests.Run(Assert);
RenderEnvironmentCommandTests.Run(Assert);
RenderFogTests.Run(Assert);
TextureDecoderTests.Run(Assert);
PsxPaletteTests.Run(Assert);
AnimationChannelTests.Run(Assert);
CompressedAnimationTests.Run(Assert);
PoseEvaluatorTests.Run(Assert);
MatrixAnimationTests.Run(Assert);
CollisionTests.Run(Assert);
RestartPointTests.Run(Assert);
MissionCheckpointTests.Run(Assert);
PlayerMotionTests.Run(Assert);
PlayerMovementInputTests.Run(Assert);
PlayerStandTests.Run(Assert);
PlayerHandPartsTests.Run(Assert);
PlayerZipDiveTests.Run(Assert);
PlayerWebAttachTests.Run(Assert);
ImpactWebTests.Run(Assert);
PsxRenderPolicyTests.Run(Assert);
PlayerScriptTests.Run(Assert);
ActorAnimationPlaybackTests.Run(Assert);
PlayerFlatMovementTests.Run(Assert);
GroundProbeTests.Run(Assert);
PlayerGroundResponseTests.Run(Assert);
PlayerGroundSolverTests.Run(Assert);
PlayerMovementSweepTests.Run(Assert);
PlayerTerrainMotionTests.Run(Assert);
PlayerCrawlMotionTests.Run(Assert);
PlayerWallMovementTests.Run(Assert);
PlayerWallAttachmentTests.Run(Assert);
PlayerTerrainControllerTests.Run(Assert);
PlayerSupportTests.Run(Assert);
PlayerSurfaceControllerTests.Run(Assert);
PlayerZipWebTests.Run(Assert);
FixedAimTests.Run(Assert);
ZipWebStrandTests.Run(Assert);
WebImpactSplatTests.Run(Assert);
SpriteAnimationTests.Run(Assert);
PlayerJumpTests.Run(Assert);
PlayerHitTests.Run(Assert);
PlayerDeathProgressTests.Run(Assert);
PlayerCombatTimerTests.Run(Assert);
string temporary = Path.Combine(Path.GetTempPath(), "openspidey-" + Guid.NewGuid());
Directory.CreateDirectory(temporary);
try
{
    byte[] payload = Encoding.ASCII.GetBytes("123456789");
    foreach (bool compressed in new[] { false, true })
    {
        string archivePath = Path.Combine(temporary, $"{compressed}.pkr");
        WriteArchive(archivePath, payload, compressed);
        using var archive = new PkrArchive(archivePath);
        Assert(archive.Entries.Count == 1, "Archive index count.");
        Assert(archive.Read("DATA\\Test.bin").SequenceEqual(payload), "Stored/zlib decode and standard CRC vector.");
        Reject<FileNotFoundException>(() => archive.Read("data/missing.bin"));
        Reject<InvalidDataException>(() => archive.Read("data/../test.bin"));
    }

    string invalid = Path.Combine(temporary, "bad.pkr");
    WriteArchive(invalid, payload, false);
    byte[] corrupted = File.ReadAllBytes(invalid);
    corrupted[8] ^= 1;
    File.WriteAllBytes(invalid, corrupted);
    using (var archive = new PkrArchive(invalid))
        Reject<InvalidDataException>(() => archive.Read("data/test.bin"));
    corrupted[4] = 255; corrupted[5] = 255; corrupted[6] = 255; corrupted[7] = 127;
    File.WriteAllBytes(invalid, corrupted);
    Reject<InvalidDataException>(() => { using var archive = new PkrArchive(invalid); });

    byte[] geometry = BuildTriangle();
    PsxScene triangle = PsxSceneReader.Read(geometry);
    Assert(triangle.Models.Count == 1 && triangle.Instances.Count == 1, "Geometry tables.");
    Assert(triangle.Models[0].Faces[0].IsTriangle, "Triangle flag.");
    Assert(triangle.Models[0].Vertices[1].Position.X == 100, "Signed position decoding.");
    Assert(triangle.Models[0].Faces[0].TextureChecksum == null, "Untextured packet.");
    PsxScene collisionPacket = PsxSceneReader.Read(BuildTriangle(collision: true));
    Assert(collisionPacket.Models[0].Normals[0].Direction.Z == 4096 && collisionPacket.Models[0].Normals[0].Flags == 7,
        "Original fixed-point normals and their flags survive parsing.");
    Assert(collisionPacket.Models[0].Faces[0].CollisionWord == 0x100000, "Collision word is read separately from face color.");
    Assert(CollisionGeometry.Read(collisionPacket.Models[0]).Count == 1, "Parsed collision packet produces a triangle.");
    Reject<InvalidDataException>(() => PsxSceneReader.Read(geometry.AsSpan(0, geometry.Length - 1)));
    geometry[^8] = 255;
    Reject<InvalidDataException>(() => PsxSceneReader.Read(geometry));

    string? assetDirectory = args.Length > 0 ? args[0] : null;
    OriginalPlayerDataTests.Run(Assert, assetDirectory);
    if (assetDirectory is not null)
    {
        foreach (string name in new[] { "data.pkr", "media.pkr" })
        {
            using var archive = new PkrArchive(Path.Combine(assetDirectory, name));
            RenderEnvironmentCommandTests.RunOriginalAssets(archive, Assert);
            PsxAssetRegionTests.RunOriginalAssets(archive, Assert);
            int scenes = 0, models = 0, faces = 0, triggerFiles = 0, triggerNodes = 0, instructions = 0, textures = 0, restarts = 0;
            int playerScripts = 0, playerInstructions = 0;
            int actorDefinitions = 0, startupActors = 0, placements = 0, fixedCameras = 0, quaternionCameras = 0;
            long decodedBytes = 0;
            int animationLibraries = 0, animationClips = 0, animationFrames = 0;
            int matrixLibraries = 0, matrixClips = 0, matrixFrames = 0;
            int spriteLibraries = 0, spriteAnimations = 0, spriteFrames = 0;
            int collisionScenes = 0, collisionTriangles = 0;
            int soundBanks = 0, soundDefinitions = 0;
            foreach (PkrEntry entry in archive.Entries.OrderBy(entry => entry.Path))
            {
                byte[] bytes = archive.Read(entry.Path);
                decodedBytes += bytes.Length;
                if (entry.Path.Equals("data/spidey.psx", StringComparison.OrdinalIgnoreCase))
                    ActorPartCollisionTests.VerifyOriginalPlayer(bytes, Assert);
                if (entry.Path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase))
                    BitmapFontTests.VerifyOriginal(bytes, entry.Path, Assert);
                if (entry.Path.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase))
                {
                    SoundDefinitionBank bank = SoundDefinitionBank.Read(bytes);
                    Assert(bank.Sounds.Count <= 64, "Original sound definitions retain their source bank capacity.");
                    soundBanks++;
                    soundDefinitions += bank.Sounds.Count;
                }
                if (entry.Path.EndsWith(".trg", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        TriggerFile triggers = TriggerFile.Read(bytes);
                        if (entry.Path.Equals("data/L1A1_T.trg", StringComparison.OrdinalIgnoreCase))
                            ActorWaypointTests.VerifyFirstLevel(triggers, Assert);
                        triggerFiles++;
                        triggerNodes += triggers.Nodes.Count;
                        foreach (TriggerNode node in triggers.Nodes)
                        {
                            if (TriggerPlacement.IsSupported(node.TypeCode))
                            {
                                TriggerPlacement placement = TriggerPlacement.Read(node);
                                Assert((node.SourceOffset + placement.PositionOffset) % 4 == 0,
                                    "Original position-bearing records retain their absolute alignment.");
                                placements++;
                            }
                            if (node.TypeCode is 1 or 7)
                            {
                                TriggerActorDefinition actor = TriggerActorDefinition.Read(node);
                                Assert((node.SourceOffset + actor.ObjectDataOffset) % 4 == 0 && actor.Links.All(link => link < triggers.Nodes.Count),
                                    "Original actor factory records retain bounded links and aligned constructor data.");
                                actorDefinitions++;
                                if (actor.InitializeOnLoad) startupActors++;
                                if (entry.Path.Equals("data/L1A1_T.trg", StringComparison.OrdinalIgnoreCase) && node.Id == 36)
                                    ActorScriptTests.VerifyOriginalFirstLevelControl(node, Assert);
                            }
                            IReadOnlyList<TriggerInstruction> nodeInstructions = TriggerCommandReader.Read(node);
                            instructions += nodeInstructions.Count;
                            foreach (TriggerInstruction instruction in nodeInstructions)
                            {
                                if (instruction.Opcode is (ushort)MissionOpcode.SetFixedCameraPosition or
                                    (ushort)MissionOpcode.SetFixedCameraPositionAndRotation)
                                {
                                    FixedCameraTests.VerifyOriginal(triggers, node, instruction, Assert);
                                    if (instruction.Opcode == (ushort)MissionOpcode.SetFixedCameraPosition) fixedCameras++;
                                    else quaternionCameras++;
                                }
                                if (instruction.Opcode != 199) continue;
                                PlayerScriptProgram script = PlayerScriptProgram.Read(instruction.Arguments.Span, instruction.SourceOffset + 2);
                                playerScripts++;
                                playerInstructions += script.Instructions.Count;
                                Assert(script.Instructions[^1].Opcode == PlayerScriptOpcode.Stop,
                                    "Original player-control script decodes through its Stop instruction.");
                            }
                            if (node.TypeCode == (ushort)TriggerNodeKind.Restart)
                            {
                                RestartPoint restart = RestartPoint.Read(node);
                                Assert((node.SourceOffset + restart.CommandOffset) % 2 == 0,
                                    "Original checkpoint command lists remain word-aligned.");
                                restarts++;
                                if (entry.Path.Contains("L1A1", StringComparison.OrdinalIgnoreCase))
                                    Console.WriteLine($"{entry.Path} restart {restart.NodeId}: {restart.Name}, position={restart.Position}, orientation={restart.Orientation}");
                            }
                        }
                        _ = new MissionRuntime(triggers, new MissionRuntimeTests.ActorFactory(), []);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidDataException($"Trigger decode failed: {entry.Path}", exception);
                    }
                }
                if (!entry.Path.EndsWith(".psx", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    PsxScene scene = PsxSceneReader.Read(bytes);
                    Assert(scene.Models.All(model => model.Checksum is not null), $"{entry.Path}: every original model retains its script checksum.");
                    IReadOnlyList<SpriteAnimation> sprites = SpriteAnimationReader.Read(bytes);
                    if (sprites.Count != 0)
                    {
                        spriteLibraries++;
                        spriteAnimations += sprites.Count;
                        spriteFrames += sprites.Sum(animation => animation.Frames.Count);
                        var textureKeys = PsxTextureReader.Read(bytes).Select(texture => texture.Checksum).ToHashSet();
                        foreach (SpriteAnimation sprite in sprites)
                            Assert(sprite.Frames.All(frame => textureKeys.Contains(frame.TextureChecksum)),
                                $"{entry.Path}: sprite {sprite.Name} resolves every frame to an imported texture.");
                    }
                    if (entry.Path.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase) ||
                        entry.Path.EndsWith("_O.psx", StringComparison.OrdinalIgnoreCase))
                    {
                        collisionScenes++;
                        collisionTriangles += scene.Models.Sum(model => CollisionGeometry.Read(model).Count);
                    }
                    CompressedAnimationLibrary? animations = PsxCompressedAnimationReader.Read(bytes);
                    MatrixAnimationLibrary? matrices = PsxMatrixAnimationReader.Read(bytes);
                    if (matrices is not null)
                    {
                        matrixLibraries++;
                        var pose = new FixedJointTransform[matrices.Parents.Count];
                        foreach (MatrixAnimationClip clip in matrices.Clips)
                        {
                            matrixClips++;
                            matrixFrames += clip.FrameCount;
                            for (int frame = 0; frame < clip.FrameCount; frame++)
                            {
                                clip.SampleFrame(frame, loop: false, pose);
                                clip.SampleFrame(frame, loop: true, pose);
                            }
                        }
                    }
                    if (animations is not null)
                    {
                        _ = StitchedVertexBindings.Read(scene, animations.Parents.Count);
                        animationLibraries++;
                        var pose = new FixedJointTransform[animations.Parents.Count];
                        foreach (CompressedAnimationClip clip in animations.Clips)
                        {
                            DecodedAnimationClip decoded = clip.Decode();
                            animationClips++;
                            animationFrames += decoded.FrameCount;
                            for (int frame = 0; frame < decoded.FrameCount; frame++)
                                CompressedPoseEvaluator.Evaluate(animations, decoded, frame, pose);
                            Assert(decoded.EncodedSize - decoded.BytesConsumed is >= 0 and <= 3,
                                $"{entry.Path} animation {clip.Id} consumes its bounded stream except alignment padding.");
                        }
                    }
                    textures += PsxTextureReader.Read(bytes).Count;
                    scenes++;
                    models += scene.Models.Count;
                    faces += scene.Models.Sum(model => model.Faces.Count);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException($"Scene decode failed: {entry.Path}", exception);
                }
            }
            Console.WriteLine($"{name}: {archive.Entries.Count} entries, {decodedBytes} decoded bytes, {scenes} scenes, {models} models, {faces} faces, {textures} textures, {triggerFiles} trigger files, {triggerNodes} trigger nodes, {restarts} restart points, {instructions} mission instructions; all entry CRCs passed.");
            Console.WriteLine($"{animationLibraries} compressed skeletons, {animationClips} clips, {animationFrames} frames decoded.");
            Console.WriteLine($"{matrixLibraries} matrix animation libraries, {matrixClips} clips, {matrixFrames} frames sampled in loop and one-shot modes.");
            Console.WriteLine($"{collisionScenes} world/object geometry files, {collisionTriangles} collision triangles decoded.");
            Console.WriteLine($"{spriteLibraries} sprite sequencer libraries, {spriteAnimations} animations, {spriteFrames} frames decoded.");
            Console.WriteLine($"{playerScripts} player-control streams, {playerInstructions} scripted input instructions decoded.");
            Console.WriteLine($"{actorDefinitions} actor factory records, {startupActors} startup creations decoded.");
            Console.WriteLine($"{placements} shared node placements decoded.");
            Console.WriteLine($"{fixedCameras} original fixed-position camera commands resolved.");
            Console.WriteLine($"{quaternionCameras} original quaternion camera commands resolved.");
            Console.WriteLine($"{soundBanks} SFX banks, {soundDefinitions} sound definitions decoded.");
        }
    }
    Console.WriteLine($"Passed {assertions} assertions in {IntPtr.Size * 8}-bit .NET.");
}
finally { Directory.Delete(temporary, recursive: true); }

static void WriteArchive(string path, byte[] payload, bool compressed)
{
    byte[] stored = payload;
    if (compressed)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(payload);
        stored = output.ToArray();
    }
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write(0x33524b50u);
    writer.Write((uint)(8 + stored.Length));
    writer.Write(stored);
    writer.Write(4u); writer.Write(1u); writer.Write(1u);
    WriteName("data\\"); writer.Write(0u); writer.Write(1u);
    WriteName("test.bin"); writer.Write(0xcbf43926u); writer.Write(compressed ? 2 : -2);
    writer.Write(8u); writer.Write((uint)payload.Length); writer.Write((uint)stored.Length);

    void WriteName(string name)
    {
        byte[] bytes = new byte[32];
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
        writer.Write(bytes);
    }
}

static byte[] BuildTriangle(bool collision = false)
{
    using var output = new MemoryStream();
    using var writer = new BinaryWriter(output);
    writer.Write(0x20006u); writer.Write(0u); writer.Write(1u);
    writer.Write(new byte[36]); // One instance, model zero at origin.
    writer.Write(1u); writer.Write(56u);
    writer.Write((ushort)0); writer.Write((ushort)3); writer.Write((ushort)(collision ? 1 : 0)); writer.Write((ushort)1);
    writer.Write(new byte[18]); writer.Write(ushort.MaxValue);
    foreach (var vertex in new[] { (0, 0, 0), (100, 0, 0), (0, -100, 0) })
    {
        writer.Write((short)vertex.Item1); writer.Write((short)vertex.Item2); writer.Write((short)vertex.Item3); writer.Write((ushort)0);
    }
    if (collision)
    {
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)4096); writer.Write((ushort)7);
    }
    writer.Write(((collision ? 4u : 3u) << 18) | 0x10u); writer.Write(new byte[] { 0, 1, 2, 2 }); writer.Write(0x00ffffffu);
    if (collision) writer.Write(0x100000u);
    return output.ToArray();
}
