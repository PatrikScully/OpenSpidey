using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A1 world/rig, source100/200/400 and the production swing/particle composition.</summary>
internal static class WebSwingImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory)
    {
        foreach (bool fire in new[] { false, true })
        {
            var viewport = new SubViewport { OwnWorld3D = true, Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            try
            {
                var root = new Node3D { Name = fire ? "GenuineFireSwingProof" : "GenuineSwingProof" }; viewport.AddChild(root);
                ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A1_G.psx"); root.AddChild(world);
                TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
                GodotPlayer player = playerFactory(); root.AddChild(player); player.SetPhysicsProcess(false);
                uint clock = 1000;
                var camera = new PlayerFollowCamera { Target = player, Current = true, SourceTotalTime = () => clock }; root.AddChild(camera);
                camera.SetPhysicsProcess(false); camera.MakeCurrent();
                player.Combat.SourceTimer = () => clock;
                player.Combat.CameraBehind = camera.PutBehind;
                player.Combat.CameraField12C = camera.SetFocusHook;
                player.Combat.CameraFlag = () => camera.TerrainObstructed;
                var random = new GameRandom(0x4F6F00);
                var registry = new GodotActorRegistry { Triggers = triggers, Player = player, Difficulty = 2,
                    Environment = new(LevelId: 0x101), RandomSource = random.Next };
                root.AddChild(registry); registry.SetPhysicsProcess(false);
                var combat = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry,
                    () => world, camera, owner.GetNode<GodotSpeechPlayer>("Speech"), () => clock);
                root.AddChild(combat); combat.SetPhysicsProcess(false);
                var channels = owner.GetNode<GodotSoundChannels>("SoundChannels");
                var webs = GodotPlayerWebRuntime.Create(archive, repository, player, combat, camera, () => clock, random.Next,
                    (sound, position, _) => unchecked((int)channels.Controller.PlayAt(sound, position, new(camera.SourcePosition >> 12))));
                root.AddChild(webs); webs.ConfigureDomes(registry, combat, repository, camera, () => clock, random.Next);
                var target = new GodotScreenTarget { Name = "OriginalSwingTarget", Camera = camera }; root.AddChild(target);
                var lookaround = new GodotPlayerLookaroundRuntime(player, webs, combat, camera, target, new()
                {
                    PracticeMode = () => false, InvertPitch = () => false,
                    SwingWebPresent = () => webs.Swing?.SwingPresent == true,
                    BeginZip = player.Controller.BeginLookaroundZip,
                    // The source target leaf already writes action100 before this shared controller bridge.
                    BeginSwing = _ => player.Controller.ApplyCombatState()
                });
                player.Combat.LookaroundExited = lookaround.Camera.ExitLookaroundMode;
                webs.ConfigureSwing(camera, combat, lookaround.Targets);
                GodotPlayerSwing swing = webs.Swing ?? throw new InvalidDataException("Production runtime omitted its real swing owner.");
                player.Controller.Web = webs; player.Controller.Lookaround = lookaround;
                await Frame(); await Frame();
                for (int frame = 0; frame < 40 && !player.Controller.IsGrounded; frame++) Step();
                Assert(player.Controller.IsGrounded && player.Controller.Action == PlayerAction.Idle,
                    "Native swing must start on the actual first-level restart roof.");
                PlayerLandingRecoveryImportChecks.Verify(player, camera, webs);
                PlayerWalkingImportChecks.Verify(player, camera, webs, world);
                PlayerAirborneMovementImportChecks.Verify(player, webs, Step);
                PlayerTriggerZoneImportChecks.Verify(player, world, registry, archive);
                FixedVector3 restartRoofPosition = player.Controller.Position;
                for (int frame = 0; frame < 16; frame++) Step(new(true, new(0, 127), false, false, false, false, false, false));
                for (int frame = 0; frame < 12; frame++) Step();
                Assert(player.Controller.IsGrounded && player.Controller.Action == PlayerAction.Idle &&
                    FixedVectorMath.OriginalDistance(restartRoofPosition, player.Controller.Position) > 0,
                    $"The longer swing target must be reached by actual physical traversal on the imported restart roof. Before={restartRoofPosition}, after={player.Controller.Position}, distance={FixedVectorMath.OriginalDistance(restartRoofPosition, player.Controller.Position)}, grounded={player.Controller.IsGrounded}, action={player.Controller.Action}, clip={player.Model.ClipId}, speedRamp={player.Controller.SpeedRamp}, heading={player.Controller.Orientation.Heading}.");
                // Face across the same roof with the actual source orientation leaf. At its
                // centre the first shallow ray already passes beyond the finite roof bounds.
                player.Melee!.State.HeadingDelta = 2048;
                player.Melee.OrientToNormal(false, default);
                player.Melee.State.HeadingDelta = 0;
                player.Controller.CaptureCombatState(2);
                player.Inventory!.State.Webbing = 4096; player.Inventory.State.FireWebbing = 0;
                if (fire) Assert(player.Inventory.SetFireWebbing(), "Fire swing must use the actual inventory fire conversion.");
                int beforeWebbing = player.Inventory.State.Webbing;
                lookaround.Camera.EnterLookaroundMode();
                for (int frame = 0; frame < 12; frame++)
                {
                    clock += 2; lookaround.Camera.UpdateCameraPlacement(2);
                    camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
                }
                Assert(lookaround.Aim.State.Active && lookaround.Aim.State.EntranceTicks == 0,
                    "Actual24-tick posed lookaround entry must finish before the native world-target search.");
                PlayerLookaroundCameraSetupResult selected = FindActualSwingTarget();
                FixedVector3 near = lookaround.Targets.Swing.NearCentre, normal = lookaround.Targets.Swing.PlaneNormal;
                lookaround.Aim.State.ActivateTarget = 1; lookaround.Targets.Update(selected);
                Assert(player.Combat.State.ActionState == 0x100 && player.Model.ClipId == 0x111 && !lookaround.Aim.State.Active &&
                    player.Melee!.State.WebAttackStarted == 1 && ReferenceEquals(swing.State.Availability, lookaround.Targets.Swing) &&
                    ReferenceEquals(swing.State.Targets, lookaround.Targets.State),
                    "A genuine imported face must enter100/clip273, share fullDA0/D64/DAC and exit the real camera7 owner.");
                player.Controller.ApplyCombatState();
                for (int frame = 0; frame < 100 && player.Combat.State.ActionState != 0x400; frame++) Step();
                WebSwingAnchor anchor = swing.State.Current ?? throw new InvalidDataException("Actual swing windup/release failed to create CSwinger.");
                Assert(player.Combat.State.ActionState == 0x400 && player.Model.ClipId == 0x113 && anchor.Anchor == near &&
                    anchor.Phase == 2048 && anchor.LastUpdateTick == clock && anchor.FireVariant == (fire ? 1 : 0) &&
                    anchor.Strand.WobbleKnots == 1 && anchor.Strand.FlickerKnots == 0 &&
                    player.Inventory.State.Webbing == beforeWebbing - 128 && camera.FocusHookIndex == 2,
                    "Source200 must set genuine focus2/behind16, charge128, construct its player-owned arc at currentclock and defer directAI until next source frame.");
                Assert(anchor.BaseMatrix == PlayerSwingBasis.Calculate(player.Combat.State.Position, near, normal, anchor.BaseMatrix).Matrix,
                    "Native CSwinger must use the actual full shared anchor/normal basis.");
                Assert(combat.Particles.Identities(SourceParticleList.GPolyLine).Contains(anchor.Strand) &&
                    combat.Particles.Identities(SourceParticleList.GPolyLine).Contains(anchor.Strand.InnerPolyline) &&
                    anchor.Strand.Knots.All(knot => combat.Particles.Identities(SourceParticleList.Flat).Contains(knot)),
                    "Actual CSwinger must register one outer, one protected inner and its original protected knot sprites in the production lists.");
                PlayerWebRuntimeLifetimeImportChecks.Verify(webs, combat);
                uint previousClock = clock;
                int previousPhase = anchor.Phase;
                Step();
                Assert(ReferenceEquals(swing.State.Current, anchor) && anchor.LastUpdateTick == clock &&
                    anchor.Phase == unchecked(previousPhase + (int)(clock - previousClock) * anchor.PhaseSpeed),
                    "Player-owned swing must advance once directly before physics, without actor EveryFrame seed/clamp.");
                VerifyAttachment(anchor);
                await Frame(); await Frame();
                uint stableClock = anchor.LastUpdateTick; int stablePhase = anchor.Phase;
                webs.Strands.Rebuild();
                Assert(webs.Strands.LineMesh.GetSurfaceCount() > 0 && webs.Strands.KnotMesh.GetSurfaceCount() > 0,
                    "Real source swing must render its paired native polylines and original WebKnot texture.");
                int narrowSurfaces = VerifyLineGeometry(webs.Strands, camera);
                viewport.Size = new(1280, 720); await Frame(); await Frame(); webs.Strands.Rebuild();
                int wideSurfaces = VerifyLineGeometry(webs.Strands, camera);
                Assert(anchor.LastUpdateTick == stableClock && anchor.Phase == stablePhase && narrowSurfaces > 0 && wideSurfaces > 0,
                    "Widescreen must upload the exact source two-pixel web caps through the real resized camera and never advance the player-owned arc.");
                viewport.Size = new(640, 480);
                Step(default, new(PlayerButton.Jump, PlayerButton.Jump));
                Assert(swing.State.Current is null && anchor.IsDisposed && !combat.Particles.Identities(SourceParticleList.GPolyLine).Contains(anchor.Strand) &&
                    webs.Strands.ReleasedSwings.Count == 1 && webs.Strands.ReleasedSwings.Single().Age == 1 &&
                    lookaround.Aim.State.JumpReleased == 0 && player.Controller.PendingWebReleaseAnimation == 1 && camera.FocusHookIndex == -1,
                    "Actual Jump16 release must destroy directE64, clear E8D, set54D and move one new CSwingBack in the same later GPolyLine phase.");
                ReleasedSwingWeb released = webs.Strands.ReleasedSwings.Single();
                Assert((released.Strand.StartColour & 0x02000000) != 0 && released.Strand.Colours.All(colour => (colour & 0x02000000) != 0) &&
                    webs.Strands.SourceTransparentLineMaterial.Transparency == BaseMaterial3D.TransparencyEnum.Alpha &&
                    webs.Strands.SourceTransparentLineMaterial.BlendMode == BaseMaterial3D.BlendModeEnum.Add &&
                    webs.Strands.SourceTransparentLineMaterial.AlbedoColor.A == 128 / 255f,
                    "Released source paired web polylines must retain actual code2 and native half-alpha additive rendering.");
                for (int frame = 0; frame < 180 && (combat.Particles.Count != 0 || webs.Strands.ReleasedSwings.Count != 0); frame++) Step();
                Assert(webs.Strands.ReleasedSwings.Count == 0 && released.Strand.IsDisposed && combat.Particles.Count == 0 &&
                    webs.Actions.State.CurrentStrand is null && !swing.SwingPresent,
                    "Source retraction/age cleanup must remove both paired lines, every protected knot and all genuine fire embers through the shared lists.");
                bool previousLowMemory = combat.ItemShatter.LowMemory;
                try
                {
                    PlayerSwingBasisResult basis = PlayerSwingBasis.Calculate(player.Combat.State.Position, near, normal, swing.State.Basis);
                    WebSwingAnchor suppressed = swing.CreateSwing(near, FixedVectorMath.OriginalDistance(player.Combat.State.Position, near), basis.Matrix, normal);
                    swing.State.Current = suppressed;
                    combat.ItemShatter.LowMemory = true; swing.ReleaseCurrent(createVisual: true);
                    Assert(suppressed.IsDisposed && swing.State.Current is null && webs.Strands.ReleasedSwings.Count == 0 && combat.Particles.Count == 0,
                        "Native SwingBack must read the same real G_LOW_MEMORY owner as ItemShatter, suppress its entire release allocation and still destroy the held protected bits.");
                }
                finally { combat.ItemShatter.LowMemory = previousLowMemory; }

                PlayerLookaroundCameraSetupResult FindActualSwingTarget()
                {
                    var contacts = new List<string>();
                    // The genuine standing check shifts its origin up580 and forward320 before
                    // enforcing its angle limit. Include the shallow rays between coarse angle
                    // steps, since the actual finite roof does not span every longer ray.
                    foreach (int pitch in Enumerable.Range(0, 49).Select(index => -768 + index * 32))
                        foreach (int yaw in Enumerable.Range(0, 17).Select(index => -1024 + index * 128))
                        {
                            lookaround.Aim.State.Pitch = pitch; lookaround.Aim.State.Yaw = yaw;
                            clock += 2;
                            PlayerLookaroundCameraSetupResult setup = lookaround.Camera.UpdateCameraPlacement(2);
                            if (!setup.SelectTarget) continue;
                            var rotation = PlayerLookaroundCameraSetup.MultiplyRotation(player.Controller.Orientation.Basis, setup.RawLookRotation);
                            FixedVector3 end = setup.Anchor - (new FixedVector3(rotation.M02, rotation.M12, rotation.M22) << 12);
                            if (lookaround.Targets.TraceTargetWorld(setup.Anchor, end) is not { } contact) continue;
                            contacts.Add($"pitch={pitch}, yaw={yaw}, distance={contact.Distance}, normal={contact.Normal}, flags={contact.FaceFlags:X}, point={contact.Position}");
                            if ((contact.FaceFlags & 0x2000000) != 0) continue;
                            lookaround.Targets.State.TargetPosition = contact.Position; lookaround.Targets.State.SurfaceNormal = contact.Normal;
                            if (lookaround.Targets.CheckSwingAvailability(lookaround.Targets.State, contact)) return setup;
                        }
                    PlayerCombatState state = player.Combat.State;
                    string bounds = DescribeRoof();
                    throw new InvalidDataException($"The actual first-level roof lookaround rays did not find an eligible original swing face. Player={state.Position}, action={state.ActionState:X}, crawl={state.IsCrawling},8E8={state.Field8E8},8E9={state.Field8E9},normal={state.CrawlSurfaceNormal}; {bounds}; rays with contacts={contacts.Count}. {string.Join(';', contacts.Take(20))}");

                    string DescribeRoof()
                    {
                        if (combat.World.Trace(state.Position, state.Position + new FixedVector3(0, 256 << 12, 0), true) is not { } floor ||
                            floor.Collider is not ImportedCollisionBody body) return "No original roof source contact";
                        ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.Equals(floor.SourceAsset, StringComparison.OrdinalIgnoreCase));
                        Transform3D transform = PsxCoordinates.InstanceTransform(source.Scene.Instances[floor.InstanceIndex]);
                        FixedVector3[] vertices = body.SourceTriangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
                            .Select(vertex => PsxCoordinates.OriginalWorldPosition(transform * PsxCoordinates.WorldPosition(vertex)))
                            .Where(vertex => Math.Abs((long)vertex.Y - floor.Position.Y) <= 32).ToArray();
                        return vertices.Length == 0 ? "Original roof plane has no vertices" :
                            $"roof={floor.SourceAsset}/instance{floor.InstanceIndex}/model{floor.ModelIndex}/face{floor.Triangle.FaceIndex}, roofY={floor.Position.Y}, X={vertices.Min(vertex => vertex.X)}..{vertices.Max(vertex => vertex.X)}, Z={vertices.Min(vertex => vertex.Z)}..{vertices.Max(vertex => vertex.Z)}";
                    }
                }
                void VerifyAttachment(WebSwingAnchor current)
                {
                    GodotPlayerMelee melee = player.Melee!;
                    FixedVector3 hand = melee.GetPlayerHookPosition(player.Model.ClipId == 0x118 ? 1 : 0) - current.Forward * 8;
                    FixedVector3 step = (hand - current.Anchor) / current.Strand.SegmentCount;
                    Assert(current.Strand.Knots[^1].RestPosition == current.Anchor + step * current.Strand.SegmentCount,
                        "Source final hand attachment must use actual posed hook0/1 minus eight forward units before shared bit wobble.");
                    Assert(player.Model.PlaybackSpeed == 0 && player.Model.Playback.Frame == current.AnimationFrame(player.Model.FrameCount - 1),
                        "Source400 must freeze normal animation advance and sample the real clip frame from its whole swing phase.");
                }
                void Step(PlayerInputSample sample = default, PlayerButtonInput buttons = default)
                {
                    clock += 2; player.AdvanceSourceUpdate(sample, 0, 2, buttons);
                    registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
                    combat._PhysicsProcess(1.0 / 60); combat._PhysicsProcess(1.0 / 60);
                    camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
                }
            }
            finally { viewport.Free(); }
            async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("PASS: genuine ordinary/fire L1A1 swing face selection, shared100/200/400/cost128, direct CSwinger clock, posed hook2 physics/hand attachment, source web bits, widescreen caps and release cleanup.");
    }

    private static int VerifyLineGeometry(GodotWebStrands renderer, Camera3D camera)
    {
        int surface = 0;
        foreach (KnottedWebStrand strand in renderer.SourceStrands)
        {
            if (!strand.Alive) continue;
            Line(strand.InnerStart, strand.InnerStartColour, strand.InnerEnds, strand.InnerColours);
            Line(strand.Start, strand.StartColour, strand.Ends, strand.Colours);
        }
        Assert(surface == renderer.LineMesh.GetSurfaceCount(), "Native web uploaded extra/missing source paired polyline segments.");
        return surface;

        void Line(FixedVector3 start, uint colour, IReadOnlyList<FixedVector3> ends, IReadOnlyList<uint> colours)
        {
            for (int i = 0; i < ends.Count; i++)
            {
                Vector3 from = PsxCoordinates.FixedWorldPosition(start), to = PsxCoordinates.FixedWorldPosition(ends[i]);
                float fromDepth = -camera.GlobalBasis.Z.Dot(from - camera.GlobalPosition), toDepth = -camera.GlobalBasis.Z.Dot(to - camera.GlobalPosition);
                if (fromDepth > 0 && toDepth > 0)
                {
                    Vector2 first = camera.UnprojectPosition(from), second = camera.UnprojectPosition(to);
                    PlayerImpactLineCorner[] corners = PlayerImpactLineGeometry.Build(first.X, first.Y, second.X, second.Y);
                    Vector3[] expected = new[] { 0, 1, 2, 0, 3, 2 }.Select(index => camera.ProjectPosition(
                        new(corners[index].X, corners[index].Y), corners[index].Head ? fromDepth : toDepth)).ToArray();
                    using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(renderer.LineMesh.GetRid(), surface);
                    Vector3[] actual = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    Material material = (colour & 0x02000000) != 0 ? renderer.SourceTransparentLineMaterial : renderer.SourceOpaqueLineMaterial;
                    Assert(actual.SequenceEqual(expected) && renderer.LineMesh.SurfaceGetMaterial(surface).GetInstanceId() == material.GetInstanceId(),
                        "Real native camera must upload the exact original world-to-screen line caps, paired endpoints and source code2 material without a dimension tolerance.");
                    surface++;
                }
                start = ends[i]; colour = colours[i];
            }
        }
    }
    private static void Assert(bool condition, string evidence)
    {
        if (!condition) throw new InvalidDataException(evidence);
    }
}
