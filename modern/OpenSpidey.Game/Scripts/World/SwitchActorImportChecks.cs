using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Original L1A3 vault controls and LFA1 reward/penalty ENV objects in separate native worlds.</summary>
public static class SwitchActorImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository)
    {
        int assertions = 0;
        await CheckLevel("L1A3", [176, 177, 178]);
        await CheckLevel("LFA1", [3, 8]);
        GD.Print($"Native original switch checks passed {assertions} assertions.");
        async Task CheckLevel(string level, ushort[] nodes)
        {
            string geometry = $"data/{level}_G.psx";
            TriggerFile triggers = TriggerFile.Read(archive.Read($"data/{level}_T.trg"));
            PsxAssetLibrary library = repository.Load(geometry);
            var viewport = new SubViewport { Name = $"Original{level}SwitchWorld", OwnWorld3D = true, Size = new(640, 480) };
            owner.AddChild(viewport);
            var fixture = new Node3D(); viewport.AddChild(fixture);
            var controls = new List<GodotSwitchActor>();
            var pulses = new List<ushort>(); int counter = 0;
            try
            {
                ImportedLevelWorld world = ImportedLevelWorld.Load(repository, geometry); fixture.AddChild(world);
                var textures = repository.Load($"data/{level}_L.psx").Textures.Concat(library.Textures).ToArray();
                GodotSwitchEnvironment environment = GodotSwitchEnvironment.Create(world, library, textures, archive.Read(geometry));
                fixture.AddChild(environment);
                var registry = new GodotActorRegistry { Triggers = triggers, ReceivePulse = pulses.Add }; registry.SetPhysicsProcess(false); fixture.AddChild(registry);
                var configuration = new GodotSwitchConfiguration { Environment = environment, AttachToControlList = actor => controls.Insert(0, actor),
                    DetachFromControlList = actor => controls.Remove(actor), HasPlayer = () => true,
                    ReadPlayerCounter = () => counter, WritePlayerCounter = value => counter = value };
                foreach (ushort node in nodes)
                {
                    GodotSwitchActor actor = GodotSwitchActor.Create(triggers.Nodes[node], registry, configuration); registry.AddChild(actor);
                    Assert(actor.UpdateGroup == ActorUpdateGroup.Control && actor.Variables.CollisionRadius == 0 && actor.State.BodyFlags == 6,
                        "Source407 attaches actual Control body with zero radius, unchanged source suspension flags and no proxy model.");
                    Assert(actor.AutoAimPoint?.Position == actor.State.OffItem?.Position || actor.AutoAimPoint?.Position == actor.State.OnItem?.Position,
                        "Switch aim comes from resolved ENV position rather than constructor placement.");
                }
                await Physics(); await Physics();
                foreach (GodotSwitchEnvironmentItem item in environment.Items)
                {
                    Assert(item.Mesh.Mesh!.GetSurfaceCount() > 0 && item.ModelIndex == item.SourceInstanceIndex && item.SourceAsset == library.AssetPath &&
                        item.State.Identity == item && item.Colliders.All(body => body.ModelIndex == item.ModelIndex && body.InstanceIndex == item.SourceInstanceIndex &&
                            body.SourceTriangles.All(triangle => (triangle.PackedMetadata & 0x02000000) != 0)),
                        "Genuine original ENV mesh/triangle ownership retains instance, checksum, face flags and actor identity.");
                }
                if (level == "L1A3")
                {
                    (ushort Node, uint Off, uint On, FixedVector3 Position)[] expected =
                    [ (176, 0x2570E809, 0xF2A5820D, new(-17874944, -573440, 13238272)),
                      (177, 0xCB7E8925, 0x1CABE321, new(-13811712, -569344, 6238208)),
                      (178, 0x5277D89F, 0x85A2B29B, new(-19189760, -569344, 8658944)) ];
                    var query = new GodotActorWorldQuery(fixture, triggers);
                    foreach (var record in expected)
                    {
                        GodotSwitchActor actor = controls.Single(actor => actor.Definition.NodeId == record.Node);
                        Assert(actor.State.Mode == SourceSwitchMode.Off && actor.State.Timeout == 0 && actor.State.OffItem?.ModelChecksum == record.Off &&
                            actor.State.OnItem?.ModelChecksum == record.On && actor.AutoAimPoint?.Position == record.Position,
                            "Independent original vault checksum/position evidence binds source constructor and actual ENV auto-aim.");
                        var off = (GodotSwitchEnvironmentItem)actor.State.OffItem!.Identity;
                        var on = (GodotSwitchEnvironmentItem)actor.State.OnItem!.Identity;
                        Assert(off.Mesh.Visible && !on.Mesh.Visible && off.Colliders.All(body => body.CollisionLayer != 0) && on.Colliders.All(body => body.CollisionLayer == 0),
                            "Original off-state model owns active collision while its alternate is hidden and excluded.");
                        GodotActorWorldHit? hit = Contact(off, query);
                        Assert(hit is not null && ReferenceEquals(environment.ResolveCollider(hit.Collider), off) &&
                            (hit.Triangle.PackedMetadata & 0x02000000) != 0 && hit.ModelChecksum == record.Off,
                            "Native nearest valid real switch face resolves the ENV identity and original target-selectable face word.");
                        pulses.Clear(); actor.Flick(); await Physics();
                        Assert(actor.State.Mode == SourceSwitchMode.On && actor.AutoAimPoint?.Position == record.Position && !off.Mesh.Visible && on.Mesh.Visible &&
                            pulses.SequenceEqual(actor.Definition.Links), "Source Flick turns on the genuine alternate ENV model and pulses exact attached trigger links.");
                        pulses.Clear(); actor.Flick(); await Physics();
                        Assert(actor.State.Mode == SourceSwitchMode.Off && off.Mesh.Visible && !on.Mesh.Visible && pulses.SequenceEqual(actor.Definition.Links),
                            "Untimed vault switch Flick restores the original off model and sends its original links again.");
                    }
                }
                else
                {
                    GodotSwitchActor reward = controls.Single(actor => actor.Definition.NodeId == 3), penalty = controls.Single(actor => actor.Definition.NodeId == 8);
                    Assert(reward.State.OffItem is null && reward.State.OnItem?.ModelChecksum == 0x76366E74 && reward.AutoAimPoint?.Position == new FixedVector3(663552, -552960, 16842752) &&
                        penalty.State.OffItem is null && penalty.State.OnItem?.ModelChecksum == 0xEBE80E0C && penalty.AutoAimPoint?.Position == new FixedVector3(794624, -540672, 21835776),
                        "Independent original LFA1 ENV pickup positions differ from the trigger body and zero off checksum is genuinely absent.");
                    pulses.Clear(); reward.Flick();
                    Assert(counter == 1 && pulses.SequenceEqual(new ushort[] { 80 }) && reward.State.Mode == SourceSwitchMode.Fading,
                        "Actual reward Flick increments player field568 and pulses original success marker80.");
                    penalty.Flick();
                    Assert(counter == 0 && pulses.SequenceEqual(new ushort[] { 80, 81 }) && penalty.State.Mode == SourceSwitchMode.Fading,
                        "Actual penalty Flick decrements player field568 and pulses original penalty marker81.");
                    // Separate timeout fixture: destroy these controls and reconstruct the original deferred nodes,
                    // retaining the same actual static ENV items, as the original constructor does.
                    reward.Free(); penalty.Free(); pulses.Clear();
                    reward = GodotSwitchActor.Create(triggers.Nodes[3], registry, configuration); registry.AddChild(reward);
                    penalty = GodotSwitchActor.Create(triggers.Nodes[8], registry, configuration); registry.AddChild(penalty);
                    var rewardItem = (GodotSwitchEnvironmentItem)reward.State.OnItem!.Identity;
                    Assert(rewardItem.Mesh.Visible && reward.State.OnItem.Color == 0 && ColorSum(rewardItem) == 0 && reward.State.Timeout == 120 && penalty.State.Timeout == 120,
                        "Original pickup constructor shows the real ENV with zero RGB and source120tick timeout.");
                    for (int update = 0; update < 60; update++) { reward.AdvanceSource(2); penalty.AdvanceSource(2); }
                    Assert(reward.State.Mode == SourceSwitchMode.Reward && penalty.State.Mode == SourceSwitchMode.Penalty && reward.State.OnItem.Color == 0x787878 && ColorSum(rewardItem) > 0 && pulses.Count == 0,
                        "Source timer exact endpoint remains active through sixty two-tick updates while real ENV RGB reaches120.");
                    reward.AdvanceSource(2); penalty.AdvanceSource(2);
                    Assert(reward.State.Mode == SourceSwitchMode.Fading && penalty.State.Mode == SourceSwitchMode.Fading && reward.State.OnItem.Color == 0x707070 && pulses.SequenceEqual(new ushort[] { 83, 82 }),
                        "The next original update expires strictly after120, pulses independent LFA1 timeout markers83/82 and fades ENV RGB in that same call.");
                    for (int update = 0; update < 14; update++) { reward.AdvanceSource(2); penalty.AdvanceSource(2); }
                    Assert((reward.State.BodyFlags & 0x40) != 0 && (penalty.State.BodyFlags & 0x40) != 0 && reward.State.OnItem.Color == 0 &&
                        !rewardItem.Mesh.Visible && ColorSum(rewardItem) == 0 && rewardItem.Colliders.All(body => body.CollisionLayer == 0), "Completed pickup fade kills each Control actor and hides its persistent original ENV item.");
                    Assert(reward.Lifecycle.PrepareUpdate(0, 13000) == ActorUpdateAction.Skip && reward.Lifecycle.PrepareUpdate(0, 13000) == ActorUpdateAction.Delete,
                        "Source switch zombie cleanup waits one actor update before deletion.");
                    // Actual mode5 Flick still changes the source player counter; it sends no additional pulse.
                    int pulseCount = pulses.Count; reward.Flick(); penalty.Flick();
                    Assert(counter == 0 && pulses.Count == pulseCount, "Reward/penalty fading-state Flick applies balanced actual source counter effects without repeating pulses.");
                }
            }
            finally { owner.RemoveChild(viewport); viewport.Free(); }
        }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        void Assert(bool condition, string evidence)
        {
            if (!condition) throw new InvalidDataException(evidence);
            assertions++;
        }
    }
    private static float ColorSum(GodotSwitchEnvironmentItem item)
    {
        float sum = 0;
        for (int surface = 0; surface < item.Mesh.Mesh!.GetSurfaceCount(); surface++)
        {
            using var arrays = item.Mesh.Mesh.SurfaceGetArrays(surface);
            foreach (Color color in arrays[(int)Godot.Mesh.ArrayType.Color].AsColorArray()) sum += color.R + color.G + color.B;
        }
        return sum;
    }
    private static GodotActorWorldHit? Contact(GodotSwitchEnvironmentItem item, GodotActorWorldQuery query)
    {
        foreach (ImportedCollisionBody body in item.Colliders)
            foreach (var triangle in body.SourceTriangles)
            {
                Vector3 center = item.Mesh.Transform * PsxCoordinates.WorldPosition((triangle.A + triangle.B + triangle.C) / 3);
                Vector3 normal = (item.Mesh.Transform.Basis * PsxCoordinates.WorldPosition(triangle.Normal)).Normalized();
                // An explicit sixteen-source-unit segment on either side of an actual imported face.
                GodotActorWorldHit? hit = query.Trace(PsxCoordinates.OriginalWorldPosition(center + normal * .16f), PsxCoordinates.OriginalWorldPosition(center - normal * .16f));
                if (hit is not null && item.Colliders.Any(candidate => ReferenceEquals(candidate, hit.Collider))) return hit;
            }
        return null;
    }
}
