using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Unchanged five-bank ENV references, static executable programs, real crate destruction and source contact queue ownership.</summary>
internal static class ContactCommandImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, GodotPlayer player)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string[] banks = ["l2a1", "l2a2", "l4a1", "lda2", "lda3"];
        int verified = 0, cratePulses = 0;
        foreach (string name in banks)
        {
            var viewport = new SubViewport { Size = new(320, 240), OwnWorld3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            GodotPlayerMeleeSession? session = null;
            try
            {
                var root = new Node3D { Name = "OriginalEnvironmentContacts", ProcessMode = Node.ProcessModeEnum.Disabled };
                viewport.AddChild(root);
                // Each private scene owns its decoded bank generation; proof imports must not consume the live campaign's region slots.
                var proofLibraries = new PsxLibraryRepository(archive);
                string geometryAsset = $"data/{name}_g.psx";
                TriggerFile triggers = TriggerFile.Read(archive.Read($"data/{name}_t.trg"));
                ImportedLevelWorld world = ImportedLevelWorld.Load(proofLibraries, geometryAsset); root.AddChild(world);
                var registry = new GodotActorRegistry { Triggers = triggers, Player = player, ProcessMode = Node.ProcessModeEnum.Disabled };
                root.AddChild(registry);
                var nativeSession = new GodotPlayerMeleeSession(registry, player, () => world);
                session = nativeSession;
                var mission = new MissionRuntime(triggers, registry, []);
                registry.ReceivePulse = mission.ReceivePulse;
                var commands = new List<MissionTrace>();
                mission.Trace += trace => { if (trace.Kind == MissionTraceKind.CommandCompleted) commands.Add(trace); };
                var camera = new Camera3D { Current = true, Near = .01f, Far = 10000 };
                root.AddChild(camera); camera.MakeCurrent();
                var query = new GodotActorWorldQuery(root, triggers);
                var random = new OpenSpidey.Core.Mathematics.GameRandom(0x5580C0);
                GodotItemShatter shatter = GodotItemShatter.Create((start, end) => query.Trace(start, end)?.Position.Y ?? int.MaxValue, random.Next);
                root.AddChild(shatter);
                GodotExplosionFragments fragments = GodotExplosionFragments.Create(archive.Read("data/bits.psx")); root.AddChild(fragments);
                var glows = new GodotGlowFlash(); root.AddChild(glows);
                var scheduler = new SourceParticleScheduler();
                shatter.ChunkEmitted += bit => scheduler.Attach(SourceParticleList.Chunk, bit);
                shatter.GlassEmitted += bit => scheduler.Attach(SourceParticleList.Glass, bit);
                fragments.SourceBitCreated += bit => scheduler.Attach(SourceParticleList.Flat, bit);
                fragments.SourceBitRemoved += identity => scheduler.Detach(identity);
                glows.SourceBitCreated += bit => scheduler.Attach(SourceParticleList.Glow, bit);
                glows.SourceBitRemoved += identity => scheduler.Detach(identity);
                string textureAsset = $"data/{name}_l.psx";
                IReadOnlyList<DecodedTexture> loadedTextures = (proofLibraries.Contains(textureAsset)
                    ? proofLibraries.Load(textureAsset).Textures : Array.Empty<DecodedTexture>())
                    .Concat(proofLibraries.RequireLoaded(geometryAsset).Textures).ToArray();
                var effects = new GodotCrateEffects(shatter, fragments, glows, _ => loadedTextures,
                    checksum => mission.TriggerCommandPoint(checksum), random.Next);
                var environment = new GodotGrenadeEnvironment(() => world, proofLibraries, path => archive.Read(path), nativeSession, effects);
                ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.Equals(geometryAsset, StringComparison.OrdinalIgnoreCase));
                GodotGrenadeEnvironmentItem[] items = environment.SourceItems.ToArray();
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);

                foreach (TriggerNode contact in triggers.Nodes.Where(node => node.TypeCode is 2 or 9))
                {
                    uint checksum = TriggerEnvironmentReference.ReadChecksum(contact);
                    GodotGrenadeEnvironmentItem[] matches = items.Where(item => item.ModelChecksum == checksum).ToArray();
                    Assert(matches.Length > 0 && matches.All(item => ReferenceEquals(item.Identity,
                        nativeSession.ResolveStaticItem(source, item.InstanceIndex)) &&
                        source.Scene.Models[item.ModelIndex].Checksum == checksum),
                        "Every genuine static ENV point must resolve to its actual imported model/instance and canonical session identity.");
                    Assert(!source.Scene.Models[matches[0].ModelIndex].Faces.Any(face => face.CollisionWord is uint word && (word & 0x20000) != 0),
                        "These authored ordinary ENV references must not be substituted with trigger-zone faces.");
                    CommandPointState point = mission.CommandPoints[contact.Id];
                    Assert(MissionRuntime.ReadLinks(contact).Count == 0 && point.Checksum == checksum && point.RemainingPulses == 0,
                        "The unchanged five-bank records have no source links and retain their actual contact-registry checksum/zero pulse state.");
                    mission.BeginContactFrame(); int before = commands.Count;
                    Assert(ReferenceEquals(mission.TriggerCommandPoint(matches[0].ModelChecksum), point) && mission.PendingCount == 1,
                        "A genuine native ENV model checksum must select and queue its actual source static-program owner.");
                    Assert(mission.ExecutePending() == 1 && commands.Count == before + 1 &&
                        commands[^1] is { Opcode: 3, SourceKind: TriggerInstructionSourceKind.OriginalExecutable } &&
                        commands[^1].SourceOffset == OriginalContactCommandProgram.SourceAddress &&
                        point.ExecutedForContact && point.ContactThisFrame && point.PulsesReceived == 0,
                        "The real contact queue executes its5580C0 static SendPulse once while preserving original point state and executable provenance.");
                    mission.EndContactFrame();
                    mission.BeginContactFrame(); Assert(mission.TriggerCommandPoint(checksum) is null && mission.PendingCount == 0,
                        "Continuous ordinary ENV contact must not replay the already executed source program."); mission.EndContactFrame();
                    mission.BeginContactFrame(); mission.EndContactFrame();
                    Assert(!point.ExecutedForContact && !point.ContactThisFrame,
                        "The actual no-contact phase must rearm its source registry point.");
                    verified++;
                }

                if (name == "l2a1")
                {
                    // Actual node173 references G-model130/instance130. Its real on-disk face is already seen/shatterable.
                    uint checksum = TriggerEnvironmentReference.ReadChecksum(triggers.Nodes[173]);
                    GodotGrenadeEnvironmentItem crate = items.Single(item => item.ModelChecksum == checksum);
                    Vector3 position = PsxCoordinates.FixedWorldPosition(crate.Position);
                    camera.Position = position + new Vector3(0, 0, 10); camera.LookAt(position);
                    Assert(crate.InstanceIndex == 130 && crate.ModelIndex == 130 && crate.ItemFlags == 0 && crate.ModelFlags == 10 &&
                        crate.Bank.Scene.Models[crate.ModelIndex].Faces.Any(face => face.CollisionWord is uint word && (word & 0x90000) == 0x80000),
                        "Positive destruction uses unchanged L2A1's actual crate/window flags and already-seen source face, without editing eligibility.");
                    int before = commands.Count;
                    crate.HitEnvironment(1);
                    Assert(mission.PendingCount == 0 && crate.ItemFlags == 0 && shatter.InstanceState(crate.Identity) is null,
                        "Ordinary null-face damage must preserve the exact crate-health gate and emit no mission event.");
                    crate.HitEnvironment(ushort.MaxValue);
                    Assert((crate.ItemFlags & 1) != 0 && mission.PendingCount == 1 &&
                        shatter.InstanceState(crate.Identity)?.ItemFlags == 1 && fragments.Count > 0 && glows.Count == 1 && scheduler.Count > 0,
                        "The real Exp_HitEnvItem/Crate_Destroy path shatters the unchanged item, hides its native owner and queues its model checksum before actual explosion effects.");
                    ImportedSceneMesh[] drawable = Descendants(world.Geometry).OfType<ImportedSceneMesh>()
                        .Where(mesh => mesh.SourceInstanceIndex == crate.InstanceIndex).ToArray();
                    ImportedCollisionBody[] colliders = Descendants(world.Collision).OfType<ImportedCollisionBody>()
                        .Where(body => body.SourceAsset == geometryAsset && body.InstanceIndex == crate.InstanceIndex).ToArray();
                    Assert(drawable.Length > 0 && colliders.Length > 0 && drawable.All(mesh => !mesh.Visible) &&
                        colliders.All(body => body.CollisionLayer == 0),
                        "Actual successful crate destruction must retire its drawable native instance and genuine collision faces.");
                    Assert(mission.ExecutePending() == 1 && commands.Count == before + 1 && commands[^1].Opcode == 3 &&
                        commands[^1].SourceKind == TriggerInstructionSourceKind.OriginalExecutable,
                        "A real source crate producer must execute the same static SendPulse program through the canonical event queue.");
                    crate.HitEnvironment(ushort.MaxValue);
                    Assert(mission.PendingCount == 0 && commands.Count == before + 1,
                        "A retired actual ENV item must not shatter again or duplicate its source command point.");
                    cratePulses++;
                }
                nativeSession.Dispose(); session = null;
            }
            finally
            {
                session?.Dispose();
                viewport.Free();
            }
        }
        Assert(verified == 101 && cratePulses == 1, "The bounded native proof must cover all101 original five-bank contacts and one genuine eligible crate producer.");
        GD.Print($"Verified native static ENV command points: {verified} unchanged five-bank model/instance identities, genuine5580C0 SendPulse provenance, queue/continuous-contact/exit lifecycle and actual L2A1 crate destruction producer.");
    }

    private static IEnumerable<Node> Descendants(Node owner)
    {
        foreach (Node child in owner.GetChildren())
        { yield return child; foreach (Node nested in Descendants(child)) yield return nested; }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException("Native static ENV commands: " + evidence); }
}
