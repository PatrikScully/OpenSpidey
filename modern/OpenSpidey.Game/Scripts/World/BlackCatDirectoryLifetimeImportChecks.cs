using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual source319 lifetime, list order and weak identities across native reparenting, suspension and destruction.</summary>
internal static class BlackCatDirectoryLifetimeImportChecks
{
    public static void Verify(Node3D parent, GodotActorRegistry registry, GodotActorCodeModules modules,
        GodotBlackCatConfiguration configuration, GodotBlackCatActor actor, int initialCount)
    {
        using var melee = new GodotPlayerMeleeDirectory();
        melee.Bind(registry); melee.Bind(registry);
        using var directory = new GodotActorSessionDirectory(registry, melee, new ActorSessionCheckPlatforms());
        GodotActorSessionHandle handle = directory.StoreHandle(actor);
        PlayerKickHandle kick = melee.MakeHandle(actor);
        ActorProcedureState captured = directory.RecoverActor(handle)!;
        GodotPlayerMeleeDirectory.Body body = melee.RequireBody(actor);
        object[] sourceOrder = registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.ToArray();
        actor.Advance();
        GodotCinematicFootprintShadow shadow = actor.Shadow ?? throw new InvalidDataException("Real Black Cat AI did not allocate its protected footprint.");
        var corners = shadow.SourceCorners.ToArray();
        registry.RemoveChild(actor);
        Assert(registry.Owns(actor) && melee.Contains(actor) && ReferenceEquals(directory.RecoverHandle(handle), actor) &&
            ReferenceEquals(melee.Recover(ref kick), actor) && ReferenceEquals(directory.RecoverNativeActor(captured), actor) &&
            registry.BaddyConstruction.State.Count == initialCount + 1 && shadow.IsAlive && shadow.SourceCorners.SequenceEqual(corners),
            "RemoveChild retains the actual allocated source body, both weak handles, captured body identity, base count and protected footprint.");
        parent.AddChild(actor);
        Assert(registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.SequenceEqual(sourceOrder) &&
            ReferenceEquals(melee.RequireBody(actor), body) && ReferenceEquals(directory.RecoverHandle(handle), actor) &&
            ReferenceEquals(melee.Recover(ref kick), actor) && shadow.IsAlive,
            "Reparenting preserves source head order, the same collision-directory body and both validation words.");
        melee.Suspend(actor);
        Assert(!melee.SourceBodies.Contains(body) && registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.SequenceEqual(sourceOrder) &&
            ReferenceEquals(directory.RecoverHandle(handle), actor) && ReferenceEquals(melee.Recover(ref kick), actor) &&
            ReferenceEquals(directory.RecoverNativeActor(captured), actor) && shadow.IsAlive &&
            registry.BaddyConstruction.State.Count == initialCount + 1,
            "The directory's explicit source-suspension notification removes selection membership while retaining allocation, captured state and protected effects.");
        melee.Resume(actor);
        Assert(registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.SequenceEqual(sourceOrder) &&
            ReferenceEquals(melee.BaddyHead, body) && ReferenceEquals(directory.RecoverHandle(handle), actor) &&
            ReferenceEquals(melee.Recover(ref kick), actor),
            "The directory's explicit source-resume notification restores the same body and original directory validation.");
        modules.Modules.Unload("blackcat");
        uint validation = kick.Validation;
        Assert(!GodotObject.IsInstanceValid(actor) && !shadow.IsAlive && registry.BaddyConstruction.State.Count == initialCount &&
            directory.RecoverHandle(handle) is null && !handle.HasPointer && melee.Recover(ref kick) is null &&
            kick.Target is null && kick.Validation == validation && directory.RecoverNativeActor(captured) is null &&
            !melee.Contains(actor),
            "Actual immediate module destruction expires pointers once, retains their validation words and destroys the footprint/base lease.");
        modules.LoadCodeModule("blackcat");
        registry.Spawn(registry.Triggers.Nodes[100]);
        var replacement = (GodotBlackCatActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
        Assert(directory.StoreHandle(replacement).Validation != handle.Validation &&
            directory.RecoverNativeActor(captured) is null && directory.RecoverHandle(handle) is null,
            "A fresh real Black Cat at the same authored node never acquires the destroyed body's captured procedure-state tombstone.");
        modules.Modules.Unload("blackcat");

        GodotBlackCatActor? detached = null;
        GodotBlackCatActor? neverEntered = null;
        try
        {
            detached = Construct(); parent.AddChild(detached); detached.Begin(); detached.Advance();
            directory.AttachSourceBody(detached);
            GodotActorSessionHandle detachedHandle = directory.StoreHandle(detached);
            PlayerKickHandle detachedKick = melee.MakeHandle(detached);
            ActorProcedureState detachedState = directory.RecoverActor(detachedHandle)!;
            GodotCinematicFootprintShadow detachedShadow = detached.Shadow!;
            parent.RemoveChild(detached);
            Assert(ReferenceEquals(directory.RecoverHandle(detachedHandle), detached) && ReferenceEquals(melee.Recover(ref detachedKick), detached),
                "A genuine constructing body outside the scene tree keeps both native identities until actual destruction.");
            detached.Free();
            Assert(directory.RecoverHandle(detachedHandle) is null && melee.Recover(ref detachedKick) is null &&
                directory.RecoverNativeActor(detachedState) is null && !detachedShadow.IsAlive &&
                registry.BaddyConstruction.State.Count == initialCount && melee.SourceBodies.Count == 0,
                "Native Predelete without a registry-removal event retires both directory identities and the protected shadow exactly once.");

            neverEntered = Construct(); directory.AttachSourceBody(neverEntered);
            Assert(!neverEntered.IsInsideTree() && melee.Contains(neverEntered) && registry.BaddyConstruction.State.Count == initialCount + 1,
                "Source constructor identities can be registered before the actual native body ever enters a scene tree.");
            directory.Dispose(); directory.Dispose(); melee.Dispose(); melee.Dispose();
            Assert(!neverEntered.GetChildren().OfType<GodotNativeDestructionObserver>().Any(observer =>
                    observer.Name.ToString() is "SourceActorDirectoryLifetime" or "SourceMeleeDirectoryLifetime") &&
                melee.SourceBodies.Count == 0 && melee.SourceControls.Count == 0 &&
                registry.BaddyConstruction.State.Count == initialCount + 1,
                "Directory disposal cancels and frees only its own native observers and subscriptions, leaving the source actor/count alive.");
            neverEntered.Free();
            directory.Remove(neverEntered); melee.Remove(neverEntered); melee.Unbind(registry);
            Assert(registry.BaddyConstruction.State.Count == initialCount && !GodotObject.IsInstanceValid(neverEntered),
                "Actual destruction after repeated directory disposal is idempotent and releases the still-owned source constructor lease.");
        }
        finally
        {
            if (detached is not null && GodotObject.IsInstanceValid(detached)) detached.Free();
            if (neverEntered is not null && GodotObject.IsInstanceValid(neverEntered)) neverEntered.Free();
        }
        GD.Print("Verified native actor lifetime: genuine Black Cat reparent/source suspension, canonical list order, both weak handles, captured-state tombstones, protected footprint/count and disposal before Predelete.");

        GodotBlackCatActor Construct() => registry.BaddyConstruction.Create(319,
            () => GodotBlackCatActor.Create(registry.Triggers.Nodes[100], registry, configuration));
    }

    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native actor lifetime: {evidence}"); }
}
