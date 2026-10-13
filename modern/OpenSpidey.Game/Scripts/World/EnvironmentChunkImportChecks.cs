using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Every unchanged authored Chnk record, actual native ENV identities, static collision replacement, flying geometry and canonical control retirement.</summary>
internal static class EnvironmentChunkImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, GodotPlayer player)
    {
        string[] banks = ["l2a2", "l3a1", "l3a3", "l3a5", "l4a1", "l7a2", "lda3"];
        int records = 0, stationary = 0, moving = 0, contacts = 0, nativeRays = 0, playerBodies = 0, attemptedRays = 0;
        foreach (string name in banks)
        {
            var viewport = new SubViewport { Size = new(320, 240), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            GodotPlayerMeleeSession? session = null;
            GodotActorRegistry? registry = null;
            try
            {
                // Imported collision must remain in simulation: inherited Disabled removes CollisionObject3D bodies with their default DisableMode.Remove.
                // Only the registry's source scheduling is disabled; this private world has no autonomous gameplay owners.
                var root = new Node3D { Name = "OriginalEnvironmentChunks" }; viewport.AddChild(root);
                var repository = new PsxLibraryRepository(archive);
                string asset = $"data/{name}_g.psx";
                byte[] raw = archive.Read(asset);
                TriggerFile triggers = TriggerFile.Read(archive.Read($"data/{name}_t.trg"));
                ImportedLevelWorld world = ImportedLevelWorld.Load(repository, asset); root.AddChild(world);
                ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.Equals(asset, StringComparison.OrdinalIgnoreCase));
                ushort[] archivedModelFlags = source.Scene.Models.Select(model => model.Flags).ToArray();
                uint[][] archivedFaces = source.Scene.Models.Select(model => model.Faces.Select(face => face.Flags).ToArray()).ToArray();
                PsxChunkTable table = PsxChunkTable.Read(raw) ?? throw new InvalidDataException("Authored chunk proof bank lost its genuine Chnk packet.");
                var nativeRegistry = new GodotActorRegistry { Triggers = triggers, Player = player, ProcessMode = Node.ProcessModeEnum.Disabled };
                registry = nativeRegistry; root.AddChild(nativeRegistry);
                session = new(nativeRegistry, player, () => world);
                var visibility = new GodotEnvironmentVisibility(() => world, repository);
                visibility.PrepareChunkSource(raw);
                var blast = new GodotGrenadeEnvironment(() => world, repository, path => archive.Read(path), session, new UnreachableCrateHost(), visibility);
                GodotGrenadeEnvironmentItem[] blastItems = blast.SourceItems.ToArray();
                var mission = new MissionRuntime(triggers, nativeRegistry, []);
                nativeRegistry.ReceivePulse = mission.ReceivePulse;
                uint clock = 100;
                var chunks = new GodotEnvironmentChunks(nativeRegistry, visibility, path => archive.Read(path),
                    checksum => mission.TriggerCommandPoint(checksum), () => clock);
                nativeRegistry.EnvironmentChunks = chunks;
                var camera = new Camera3D { Current = true, Near = .01f, Far = 10000 }; root.AddChild(camera); camera.MakeCurrent();
                var controls = new List<GodotEnvironmentChunkControl>();
                GodotControlActor? producer = null;
                foreach (PsxChunkRecord record in table.Records)
                {
                    GodotEnvironmentVisibilityItem original = visibility.SourceItems.First(item => item.ModelChecksum == record.Checksum);
                    camera.Position = PsxCoordinates.FixedWorldPosition(original.Position) + new Vector3(0, 10, 30);
                    camera.LookAt(PsxCoordinates.FixedWorldPosition(original.Position));
                    int oldPending = mission.PendingCount;
                    EnvironmentChunkControl? state;
                    if (name == "l3a1" && record.Checksum == 0xBC64AF62)
                    {
                        // Unchanged L3A1 node154 starts with42B5(BC64AF62), followed directly by4100. Run its genuine factory/interpreter producer.
                        TriggerNode authored = triggers.Nodes[154];
                        TriggerActorDefinition definition = TriggerActorDefinition.Read(authored);
                        var actualOperands = new OpenSpidey.Core.Actors.Scripting.ActorScriptCursor(definition.BehaviorData,
                            authored.SourceOffset + definition.ObjectDataOffset + 18);
                        Assert(definition.ActorType == 203 && actualOperands.ReadWord() == 0x42B5 && actualOperands.ReadAlignedDword() == record.Checksum &&
                            actualOperands.PeekWord() == 0x4100 && definition.Links.Count == 0,
                            "Positive42B5 producer uses the actual authored L3A1 control154 checksum/stop program and no synthetic script bytes.");
                        nativeRegistry.Spawn(authored);
                        producer = nativeRegistry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>().Single(actor => actor.SourceNodeId == 154);
                        state = nativeRegistry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotEnvironmentChunkControl>().First().State;
                        Assert(!producer.ScriptState.Active && producer.SourceUpdates == 0 && producer.Runtime.SourcePosition == actualOperands.AbsolutePosition,
                            $"Genuine control154 factory must execute source42B5 through the required chunk service and retain field24C on the actual4100 terminator. Active={producer.ScriptState.Active}, Updates={producer.SourceUpdates}, ActualCursor={producer.Runtime.SourcePosition}, ExpectedStopCursor={actualOperands.AbsolutePosition}.");
                    }
                    else state = chunks.ChunkByChecksum(record.Checksum);
                    Assert((original.ItemFlags & 0x21) == 0x21 && blastItems[original.InstanceIndex].ItemFlags == original.ItemFlags &&
                        !Meshes(world, original).Any(mesh => mesh.Visible) && Bodies(world, original).All(body => body.CollisionLayer == 0),
                        "Original Chunk must hide the genuine ENV render/collision owner before revealing authored rubble, sharing flags with blast traversal.");
                    if (mission.PendingCount != oldPending)
                    {
                        Assert(mission.PendingCount == oldPending + 1 && mission.CommandPoints.Values.Any(point => point.Checksum == record.Checksum &&
                            point.ContactThisFrame && point.ExecutedForContact), "Authored chunk destruction must queue the actual source checksum contact before piece construction.");
                        contacts++;
                    }
                    foreach (uint checksum in record.StaticRuinModels)
                    {
                        GodotEnvironmentVisibilityItem piece = visibility.SourceItems.First(item => item.ModelChecksum == checksum);
                        Assert((piece.ItemFlags & 0x1021) == 0 && (piece.Model.Flags & 0x30) == 0 && (piece.Model.RendererFlags & 0x4100) == 0x4000 &&
                            piece.Model.Faces.All(face => (face.Flags & 0x80) != 0 && (face.RendererFlags & 0xC0) != 0 && (face.CollisionWord & 0x10000) == 0),
                            "Static authored ruin must retain original EFDE/FFCF masks, actual source/DC draw bits and newly enabled collision metadata.");
                        VerifyMesh(world, piece);
                        Assert(blastItems[piece.InstanceIndex].ModelFlags == piece.Model.Flags && blastItems[piece.InstanceIndex].Position == piece.Position,
                            "Visibility, blast and chunk consumers must share one actual mutable source model/position owner.");
                        stationary++;
                    }
                    if (record.MovingRuinChunks.Count == 0)
                        Assert(state is null, "Purely stationary source chunk records do not allocate a control body.");
                    else
                    {
                        GodotEnvironmentChunkControl native = nativeRegistry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotEnvironmentChunkControl>().First();
                        controls.Add(native);
                        Assert(ReferenceEquals(native.State, state) && nativeRegistry.Owns(native) && native.SourceNodeId == ushort.MaxValue &&
                            native.SourceActorType == 0 && native.BodyFlags == 0x16 && native.SourceUpdates == 0 && native.State.Position == original.Position &&
                            native.State.Capacity == record.MovingRuinChunks.Count && native.State.Entries.Count == record.MovingRuinChunks.Count,
                            "Actual CChunkControl must attach at the canonical Control head with original CBody nodeFFFF/type0/flags16 before source updates.");
                        for (int index = 0; index < record.MovingRuinChunks.Count; index++)
                        {
                            GodotEnvironmentVisibilityItem piece = visibility.SourceItems.First(item => item.ModelChecksum == record.MovingRuinChunks[index]);
                            Assert(ReferenceEquals(native.State.Entries[index].Item, piece) && (piece.ItemFlags & 0x1001) == 0 && (piece.Model.Flags & 0x20) == 0 &&
                                (piece.Model.RendererFlags & 0x4100) == 0x4000 && piece.Model.Faces.All(face => (face.Flags & 0x80) != 0 && (face.RendererFlags & 0xC0) != 0),
                                "Each moving entry must retain its genuine source item identity and original EFFE/FFDF reveal policy in authored order.");
                            VerifyMesh(world, piece); moving++;
                        }
                    }
                    records++;
                }
                await Physics(); await Physics();
                // A real revealed static triangle must participate in native player collision; aim a tiny ray through its actual transformed centroid.
                foreach (GodotEnvironmentVisibilityItem piece in table.Records.SelectMany(record => record.StaticRuinModels).Distinct()
                    .Select(checksum => visibility.SourceItems.First(item => item.ModelChecksum == checksum)))
                {
                    foreach (ImportedCollisionBody body in Bodies(world, piece).OfType<ImportedCollisionBody>().Where(body => (body.CollisionLayer & CollisionLayers.PlayerTerrain) != 0))
                    {
                        playerBodies++;
                        Assert(body.CanProcess(), "Positive ruin collision proof requires the actual imported body to remain enabled in the native physics world.");
                        for (int face = 0; face < body.SourceTriangles.Count; face++)
                        {
                            CollisionTriangle triangle = body.SourceTriangles[face];
                            Vector3 center = body.GlobalTransform * PsxCoordinates.WorldPosition((triangle.A + triangle.B + triangle.C) / 3);
                            Vector3 normal = (body.GlobalBasis * PsxCoordinates.WorldPosition(triangle.Normal)).Normalized();
                            if (normal.LengthSquared() == 0) continue;
                            var query = new GodotPlayerWorldQuery(root);
                            attemptedRays++;
                            var hit = query.Trace(PsxCoordinates.OriginalWorldPosition(center + normal * .01f), PsxCoordinates.OriginalWorldPosition(center - normal * .01f));
                            if (hit is not { } actual || actual.InstanceIndex != piece.InstanceIndex || !string.Equals(actual.SourceAsset, asset, StringComparison.OrdinalIgnoreCase)) continue;
                            Assert((actual.Contact.SurfaceFlags & 0x10000) == 0, "Actual revealed ruin ray must carry the source collision-word edit through the native physics result.");
                            nativeRays++; break;
                        }
                    }
                }
                if (controls.Count > 0)
                {
                    GodotEnvironmentChunkControl native = controls[0];
                    EnvironmentChunkEntry entry = native.State.Entries[0];
                    var piece = (GodotEnvironmentVisibilityItem)entry.Item!;
                    FixedVector3 before = piece.Position; ActorAngles angles = piece.Angles;
                    FixedVector3 velocity = entry.Velocity; ActorAngles spin = entry.AngularVelocity; uint lifetime = entry.Lifetime;
                    native.AdvanceSourceClock(clock);
                    Assert(piece.Position == before + velocity && piece.Angles == new ActorAngles(unchecked((short)(angles.X + spin.X)),
                        unchecked((short)(angles.Y + spin.Y)), unchecked((short)(angles.Z + spin.Z))) && entry.Lifetime == lifetime - 2 &&
                        (native.BodyFlags & 4) == 0 && blastItems[piece.InstanceIndex].Position == piece.Position,
                        "The real first EveryFrame forces2 elapsed ticks while CChunkControl adds each velocity/spin once and shares exact fixed positions with blast traversal.");
                    VerifyMesh(world, piece);
                }
                for (int update = 0; update < 45; update++)
                {
                    clock += 6;
                    foreach (GodotEnvironmentChunkControl native in controls.Where(native => native.Lifecycle.Phase == ActorLifePhase.Active)) native.AdvanceSourceClock(clock);
                }
                Assert(controls.All(native => (native.BodyFlags & 0x40) != 0 && native.Lifecycle.Phase == ActorLifePhase.Dead && native.State.Entries.All(entry => entry.Item is null)),
                    "Every genuine flying source ruin must expire by its original unsigned lifetime and mark the canonical CChunkControl dead.");
                producer?.Advance(); // The stopped original control retires on its next source AI, before the ordinary two-pass body destructor walk.
                nativeRegistry._PhysicsProcess(1.0 / 60); nativeRegistry._PhysicsProcess(1.0 / 60);
                Assert(controls.All(native => native.Lifecycle.Phase == ActorLifePhase.PendingDeletion), "Actual generic source actor cleanup preserves the first dead pass.");
                nativeRegistry._PhysicsProcess(1.0 / 60); nativeRegistry._PhysicsProcess(1.0 / 60);
                await Frame();
                Assert(nativeRegistry.SourceList(ActorUpdateGroup.Control).SourceOrder.Count == 0 && controls.All(native => !GodotObject.IsInstanceValid(native) && native.State.EntriesDisposed),
                    "Second source dead pass must run actual native control destruction and detach the canonical Control list without retiring bank-owned static ruins.");
                Assert(source.Scene.Models.Select(model => model.Flags).SequenceEqual(archivedModelFlags) && source.Scene.Models.Select((model, index) =>
                    model.Faces.Select(face => face.Flags).SequenceEqual(archivedFaces[index])).All(value => value),
                    "Live chunk/source model edits must leave every immutable original archive record unchanged.");
                GD.Print($"Verified actual{name.ToUpperInvariant()} Chnk records={table.Records.Count}, controls={controls.Count}, source contacts={contacts}, player bodies={playerBodies}, attempted rays={attemptedRays}, positive static native rays={nativeRays}.");
            }
            finally
            {
                session?.Dispose();
                if (registry is not null && GodotObject.IsInstanceValid(registry))
                    foreach (GodotEnvironmentChunkControl native in registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotEnvironmentChunkControl>().ToArray())
                        if (registry.Owns(native)) registry.DestroyImmediately(native);
                if (GodotObject.IsInstanceValid(viewport)) viewport.Free();
                await Frame();
            }
        }
        Assert(records == 51 && stationary > 0 && moving > 0 && contacts > 0 && nativeRays > 0,
            $"All51 original Chnk programs must close static/flying geometry, actual source contacts and positive native ruin collision. Actual records={records}, static={stationary}, moving={moving}, contacts={contacts}, playerBodies={playerBodies}, attemptedRays={attemptedRays}, positiveRays={nativeRays}.");
        GD.Print($"Verified source Chunk_ChunkItemByChecksum:51 authored records, static pieces={stationary}, flying pieces={moving}, checksum contacts={contacts}, genuine ruin rays={nativeRays}.");
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private static IEnumerable<ImportedSceneMesh> Meshes(ImportedLevelWorld world, GodotEnvironmentVisibilityItem item) =>
        world.Geometry.GetChildren().OfType<Node3D>().SelectMany(group => group.GetChildren().OfType<ImportedSceneMesh>()).Where(mesh =>
            mesh.SourceInstanceIndex == item.InstanceIndex && mesh.GetParent().Name.ToString().Equals(System.IO.Path.GetFileNameWithoutExtension(item.Source.AssetPath), StringComparison.OrdinalIgnoreCase));
    private static IEnumerable<CollisionObject3D> Bodies(ImportedLevelWorld world, GodotEnvironmentVisibilityItem item) =>
        world.Collision.GetChildren().OfType<Node3D>().SelectMany(group => group.GetChildren().OfType<CollisionObject3D>()).Where(body => body switch
        {
            ImportedCollisionBody terrain => terrain.SourceAsset == item.Source.AssetPath && terrain.InstanceIndex == item.InstanceIndex,
            ImportedTriggerSurface trigger => trigger.SourceAsset == item.Source.AssetPath && trigger.InstanceIndex == item.InstanceIndex,
            _ => false
        });
    private static void VerifyMesh(ImportedLevelWorld world, GodotEnvironmentVisibilityItem item)
    {
        ImportedSceneMesh[] meshes = Meshes(world, item).ToArray();
        Assert(meshes.Length == 1 && meshes[0].Visible && meshes[0].Mesh is ArrayMesh mesh && mesh.GetSurfaceCount() > 0 &&
            meshes[0].Transform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(item.Position)),
            $"Genuine ENV piece{item.InstanceIndex}/{item.ModelChecksum:X8} must draw real source faces at its exact current fixed position.");
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private sealed class UnreachableCrateHost : IEnvironmentCrateHost
    {
        public bool LowMemory => throw new InvalidOperationException("This authored Chunk producer proof never calls Crate_Destroy.");
        public bool Shatter(IEnvironmentCrate crate, bool checkSeen, bool markProcessed) => throw new InvalidOperationException("This authored Chunk producer proof never calls Crate_Destroy.");
        public void TriggerCommandPoint(uint checksum) => throw new InvalidOperationException("This authored Chunk producer proof never calls Crate_Destroy.");
        public int Random(int range) => throw new InvalidOperationException("This authored Chunk producer proof never calls Crate_Destroy.");
        public void CreateCrateExplosion(FixedVector3 position, bool small) => throw new InvalidOperationException("This authored Chunk producer proof never calls Crate_Destroy.");
    }
}
