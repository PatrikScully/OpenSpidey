using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Real original roof, player input, runtime banks and mixed Misc/bit scheduling; no substituted dome callbacks.</summary>
internal static class WebDomeImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory)
    {
        var viewport = new SubViewport { OwnWorld3D = true, Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        GodotActorRegistry? registry = null; GodotWebDomes? domes = null;
        try
        {
            var root = new Node3D { Name = "GenuineNativeWebDomeProof" }; viewport.AddChild(root);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A1_G.psx"); root.AddChild(world);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
            GodotPlayer player = playerFactory(); root.AddChild(player); player.SetPhysicsProcess(false);
            uint clock = 1000;
            var camera = new PlayerFollowCamera { Target = player, Current = true, SourceTotalTime = () => clock }; root.AddChild(camera);
            camera.SetPhysicsProcess(false); camera.MakeCurrent();
            var generator = new GameRandom(0x4FA830);
            var randomEvents = new List<(int Bound, int Value)>();
            int Random(int bound) { int value = generator.Next(bound); randomEvents.Add((bound, value)); return value; }
            registry = new() { Triggers = triggers, Player = player, Difficulty = 2, RandomSource = Random };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            var combat = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry,
                () => world, camera, owner.GetNode<GodotSpeechPlayer>("Speech"), () => clock);
            root.AddChild(combat); combat.SetPhysicsProcess(false);
            var channels = owner.GetNode<GodotSoundChannels>("SoundChannels");
            var webs = GodotPlayerWebRuntime.Create(archive, repository, player, combat, camera, () => clock, Random,
                (sound, position, _) => unchecked((int)channels.Controller.PlayAt(sound, position, new(camera.SourcePosition >> 12))));
            root.AddChild(webs); webs.ConfigureDomes(registry, combat, repository, camera, () => clock, Random);
            domes = webs.Domes!;
            GodotItemBurstFragments fragments = webs.GetChildren().OfType<GodotItemBurstFragments>().Single();
            player.Controller.Web = webs;
            await Frame(); await Frame();
            for (int update = 0; update < 30 && !player.Controller.IsGrounded; update++) Step();
            Assert(player.Controller.IsGrounded && player.Controller.Action == PlayerAction.Idle,
                "Dome input proof must start on the genuine first-level restart roof.");
            foreach (bool fire in new[] { false, true })
            {
                player.Inventory!.State.Webbing = 4096;
                player.Inventory.State.FireWebbing = 0;
                if (fire) Assert(player.Inventory.SetFireWebbing(), "Native fire dome must enter the actual inventory fire variant.");
                int cartridgeCount = player.Inventory.State.Cartridges;
                Step(new(true, new(0, 127), false, false, false, false, false, false), new(PlayerButton.Web, PlayerButton.Web));
                Assert(player.Controller.Action == PlayerAction.WebDome && player.Model.ClipId == 0x11B &&
                    player.Inventory.State.Webbing == 1024 && player.Inventory.State.Cartridges == cartridgeCount &&
                    domes.DomeCount == 1 && domes.FireDomeCount == (fire ? 1 : 0),
                    "Actual Web17 and physical right E2E must construct one source dome and charge3072 before its original clip.");
                WebDome dome = domes.Domes.Single();
                PlayerKickHandle handle = webs.Attack!.State.DomeHandle;
                Assert(ReferenceEquals(domes.Recover(ref handle), dome) && dome.Mode == 0 && dome.Pieces.All(piece => piece is null),
                    "Native dome AB8 must recover its actual allocated identity; birth-frame AI must retain the source frame4 gate.");
                Assert(domes.Timing(dome) == (clock, 2, 2) && (dome.BodyFlags & 4) == 0,
                    "Player-born Misc dome must receive its genuine EveryFrame seed and AI later in the same source frame.");
                await Frame(); await Frame();
                Assert(dome.Mode == 0 && domes.Timing(dome) == (clock, 2, 2), "Rendering must not advance source dome bodies.");
                for (int update = 0; update < 20 && dome.Mode == 0; update++) Step();
                Assert(dome.Mode == 1 && dome.Pieces.Count(piece => piece is not null) == (fire ? 3 : 5),
                    "Source frame4 must create the exact ordinary/fire model pieces.");
                int[] models = fire ? [0, 2, 3] : [3, 0, 4, 1, 2];
                for (int slot = 0; slot < models.Length; slot++)
                {
                    WebDomePiece piece = dome.Pieces[slot]!;
                    Assert(piece.ModelIndex == models[slot] && piece.Delay == slot * 2 && piece.Tint == 0 && (piece.BodyFlags & 4) != 0,
                        "Misc-born pieces must preserve source model/delay/flag32 and wait for the next captured Misc pass.");
                    VerifyGeometry(domes.ModelFor(piece), null);
                }
                VerifyGeometry(domes.ModelFor(dome), null);
                if (fire)
                {
                    randomEvents.Clear(); Step();
                    int[] wanted = [domes.FireModelVertexCount, 10, 4096, 4096, 200, 5, 5, 256];
                    Assert(randomEvents.Select(value => value.Bound).SequenceEqual(wanted),
                        $"Fire dome must use source vertex→six ember ctor calls→same-pass Flat flicker RNG; actual={string.Join(',', randomEvents.Select(value => value.Bound))}.");
                    FireEmber ember = webs.Embers.Embers.Single();
                    FixedVector3 pulse = domes.FireModelVertexWorldPosition(dome, randomEvents[0].Value);
                    Assert(dome.Scale.Y == 600 && ember.Origin.X == pulse.X && ember.Origin.Z == pulse.Z &&
                        ember.Position.Y == unchecked(pulse.Y - ((randomEvents[6].Value + 6) << 12)) &&
                        ember.Scale == randomEvents[4].Value + 350 && ember.FadeDelay == randomEvents[5].Value + 4,
                        "Actual firedome model1 must emit its genuine unscaled vertex ember and move it in the later same-frame Flat list.");
                    for (int update = 0; update < 8; update++) Step();
                    Assert(dome.Scale.Y == 4096 && (dome.ItemFlags & 1) == 0 && webs.Embers.Count > 0,
                        "Native firedome must complete source short600 growth/clamp and genuine continuing ember births.");
                }
                else
                {
                    for (int update = 0; update < 25 && dome.Mode != 2; update++) Step();
                    Assert(dome.Mode == 2 && (dome.ItemFlags & 1) == 0 && dome.Pieces.All(piece => piece!.TintStep == -10),
                        "Ordinary dome must reveal original webdome2 only when piece3 reachesRGB32.");
                }
                GodotWebDomeModel surface = domes.ModelFor(dome);
                var first = ProjectedBounds(camera, surface);
                viewport.Size = new(1280, 720); await Frame(); await Frame();
                var wide = ProjectedBounds(camera, surface);
                VerifyProportions(first, wide);
                viewport.Size = new(640, 480);
                Step(default, new(PlayerButton.Jump, PlayerButton.None));
                Assert(dome.Mode == 3 && (dome.BodyFlags & 0xC0) == 0xC0 && domes.DomeCount == 1 &&
                    webs.Attack.State.DomeHandle.Target is null && player.Model.ClipId == 0x11C && domes.ShockCount == 1 &&
                    domes.Shocks.Single().Radius == 140,
                    "Actual heldJump16 dome release must burst once, clear AB8 and create a same-pass NonRendered shock while Misc dome waits for deletion.");
                WebDomeRing ring = domes.CurrentRing ?? throw new InvalidDataException("Source dome burst omitted its real ring singleton.");
                Assert(ring.Tint == 0x7D7D7D && (ring.BodyFlags & 4) == 0,
                    "Player-born ring must execute its source vertex/RGB update in this same Misc pass.");
                VerifyGeometry(domes.ModelFor(ring), ring.Vertices);
                if (!fire)
                {
                    Assert(fragments.Count > 0 && fragments.Fragments.All(fragment => fragment.Age == 1 &&
                        repository.Load("webdome3").Scene.Models[4].Faces.Contains(fragment.Face)),
                        "Ordinary dome must burst the genuine trapped model4 quads through CItemFrag's later same-frame Quad phase.");
                    VerifyFragmentGeometry(fragments);
                }
                Step();
                Assert(domes.DomeCount == 0 && dome.IsDisposed && registry.SourceList(ActorUpdateGroup.Miscellaneous).Count == 1,
                    "Next Misc pass must directly destroy source pending dome and all five/three owned pieces, retaining only its ring.");
                for (int update = 0; update < 180; update++) Step();
                Assert(domes.CurrentRing is null && domes.ShockCount == 0 && webs.Embers.Count == 0 && fragments.Count == 0 &&
                    combat.Particles.Count == 0 && player.Controller.Action == PlayerAction.Idle && player.ActorState.MinorRadius == 100 &&
                    registry.SourceList(ActorUpdateGroup.Miscellaneous).Count == 0 && registry.SuspendedSourceList.Count == 0,
                    "Native dome release must restore radius100/idle and retire every genuine body, ember, fragment and shock through source phases.");
            }
            VerifySuspension();
            GD.Print("PASS: real ordinary/fire dome Web17 input/cost/AB8, source banks/piece geometry, mixed Misc birth/death, shared ember/fragment/shock phases, ring deformation/suspension and widescreen.");

            void Step(PlayerInputSample sample = default, PlayerButtonInput buttons = default)
            {
                clock += 2; player.AdvanceSourceUpdate(sample, 0, 2, buttons);
                registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
                combat._PhysicsProcess(1.0 / 60); combat._PhysicsProcess(1.0 / 60);
                camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
            }
            void VerifySuspension()
            {
                FixedVector3 originalPosition = player.Controller.Position;
                domes.EmitRing(originalPosition, 0); WebDomeRing ring = domes.CurrentRing!;
                registry.SourceList(ActorUpdateGroup.Miscellaneous).Update();
                var timing = domes.Timing(ring); uint tint = ring.Tint;
                player.Controller.SetScriptPosition(originalPosition + new FixedVector3(20000 << 12, 0, 0)); clock += 100;
                registry.SourceList(ActorUpdateGroup.Miscellaneous).Update();
                Assert((ring.BodyFlags & 1) != 0 && registry.SuspendedSourceList.Count == 1 && ring.Tint == tint && domes.Timing(ring) == timing,
                    "Distant ring must join the actual mixed suspended head before clock/AI.");
                player.Controller.SetScriptPosition(originalPosition); registry.SuspendedSourceList.Update();
                Assert((ring.BodyFlags & 1) == 0 && registry.SuspendedSourceList.Count == 0 && domes.Timing(ring) == timing,
                    "Source suspended pass must reattach nearby ring for next Misc AI without a fabricated update.");
                registry.SourceList(ActorUpdateGroup.Miscellaneous).Update();
                Assert(domes.Timing(ring).ElapsedTicks == 6 && domes.Timing(ring).LastClock == clock,
                    "Resumed ring must use its actual retained-clock signed difference and clamp6.");
                for (int update = 0; update < 50; update++) { clock += 2; registry.SourceList(ActorUpdateGroup.Miscellaneous).Update(); }
                Assert(domes.CurrentRing is null, "Real resumed ring must still finish its source RGB3 and40→80 destruction path.");
            }
        }
        finally
        {
            viewport.Free();
            if (registry is not null && (registry.SourceList(ActorUpdateGroup.Miscellaneous).Count != 0 || registry.SuspendedSourceList.Count != 0))
                throw new InvalidDataException("Dome viewport disposal retained source active/suspended registrations.");
        }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void VerifyGeometry(GodotWebDomeModel native, IReadOnlyList<FixedVector3>? deformation)
    {
        int surface = 0;
        foreach (var group in native.SourceModel.Faces.GroupBy(face => (face.TextureChecksum, Mode: (face.Flags & 0x40) == 0 ? 0 : (face.Flags & 0x80) != 0 ? 2 : 1)))
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(native.RenderMesh.GetRid(), surface++);
            Vector3[] actual = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var expected = new List<Vector3>();
            foreach (PsxFace face in group)
                foreach (int corner in face.IsTriangle ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 2, 1, 3 })
                {
                    int index = face.VertexIndices[corner]; PsxVertex vertex = native.SourceModel.Vertices[index];
                    System.Numerics.Vector3 point = deformation is null ? vertex.Position : new(deformation[index].X, deformation[index].Y, deformation[index].Z);
                    expected.Add(PsxCoordinates.WorldPosition(point / ((vertex.Flags & 0x10) != 0 ? 8 : 1)));
                }
            Assert(actual.SequenceEqual(expected), "Native dome/piece/ring must upload its exact genuine source corners, flags and current signed-short deformation.");
        }
        Assert(surface == native.RenderMesh.GetSurfaceCount() && surface > 0, "Dome renderer must own the real source textured model surfaces.");
    }
    private static void VerifyFragmentGeometry(GodotItemBurstFragments effects)
    {
        effects.Rebuild(); int surface = 0;
        foreach (ItemBurstFragment fragment in effects.Fragments.Where(fragment => fragment.Alive))
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(effects.RenderMesh.GetRid(), surface++);
            Vector3[] actual = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Assert(actual.SequenceEqual(new[] { 0, 1, 2, 1, 3, 2 }.Select(corner => PsxCoordinates.FixedWorldPosition(fragment.Corners[corner]))),
                "Native CItemFrag must render its actual four independently jittered source quad corners.");
        }
    }
    private static (Vector2 Minimum, Vector2 Maximum, Vector2 Dimensions) ProjectedBounds(Camera3D camera, GodotWebDomeModel model)
    {
        using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(model.RenderMesh.GetRid(), 0);
        Vector2[] points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(vertex => camera.UnprojectPosition(model.GlobalTransform * vertex)).ToArray();
        Vector2 minimum = new(points.Min(point => point.X), points.Min(point => point.Y));
        Vector2 maximum = new(points.Max(point => point.X), points.Max(point => point.Y));
        return (minimum, maximum, maximum - minimum);
    }
    private static void VerifyProportions((Vector2 Minimum, Vector2 Maximum, Vector2 Dimensions) original,
        (Vector2 Minimum, Vector2 Maximum, Vector2 Dimensions) wide)
    {
        // World source geometry is unchanged. At fixed verticalFOV, the viewport-height ratio alone scales both axes.
        Vector2 expected = original.Dimensions * 1.5f;
        static float Spacing(float value) => Math.Abs(MathF.BitIncrement(value) - value);
        static float Bound(float firstMin, float firstMax, float firstSize, float secondMin, float secondMax,
            float secondSize, float expectedSize) => Spacing(secondMin) + Spacing(secondMax) +
            1.5f * (Spacing(firstMin) + Spacing(firstMax)) + .5f *
            (Spacing(secondSize) + 1.5f * Spacing(firstSize) + Spacing(expectedSize));
        Vector2 tolerance = new(Bound(original.Minimum.X, original.Maximum.X, original.Dimensions.X,
            wide.Minimum.X, wide.Maximum.X, wide.Dimensions.X, expected.X),
            Bound(original.Minimum.Y, original.Maximum.Y, original.Dimensions.Y,
            wide.Minimum.Y, wide.Maximum.Y, wide.Dimensions.Y, expected.Y));
        Assert(Math.Abs(wide.Dimensions.X - expected.X) <= tolerance.X && Math.Abs(wide.Dimensions.Y - expected.Y) <= tolerance.Y,
            "Wide viewport must preserve the actual source dome model's two projected dimensions.");
    }
    private static void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException(evidence); }
}
