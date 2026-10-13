using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Actual control/platform constructor scopes and native destruction, using untouched source trigger records.</summary>
internal static class BaddyConstructionImportChecks
{
    public static async Task VerifyAsync(Node3D owner, TriggerFile triggers)
    {
        TriggerNode controlNode = triggers.Nodes.First(node => (node.TypeCode is 1 or 7) &&
            TriggerActorDefinition.Read(node).ActorType == 203);
        TriggerNode platformNode = triggers.Nodes.Where(node => node.TypeCode is 1 or 7).First(node =>
        {
            TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
            return definition.ActorType == 402 && !definition.StartsSuspended;
        });
        var root = new Node3D { Name = "OriginalBaddyConstructionChecks" };
        owner.AddChild(root);
        var registry = new GodotActorRegistry { Triggers = triggers };
        root.AddChild(registry);
        registry.SetPhysicsProcess(false);
        GodotBaddyConstructionOwner construction = registry.BaddyConstruction;
        var allocated = new List<GodotScriptActor>();
        int assertions = 0;
        try
        {
            Assert(construction.State.Count == 0 && construction.OwnedCount == 0,
                "A fresh isolated source scene has no constructed CBaddy bodies.");
            Reject<InvalidOperationException>(() => construction.RequireCurrentOrdinal());
            GodotPlatformActor? nested = null;
            GodotControlActor outer = construction.Create(203, () =>
            {
                Assert(construction.RequireCurrentOrdinal() == 0 && construction.RequireCurrentPhase() == 0,
                    "The outer base constructor captures count zero before its derived constructor.");
                nested = construction.Create(402, () =>
                {
                    Assert(construction.RequireCurrentOrdinal() == 1 && construction.RequireCurrentPhase() == 1,
                        "A nested actual platform constructor sees the same count and its own scope.");
                    return CreatePlatform(registry);
                });
                Assert(construction.RequireCurrentOrdinal() == 0 && construction.State.Count == 2,
                    "Returning from the nested native factory restores the outer constructor scope.");
                return CreateControl(registry);
            });
            Assert(construction.State.Count == 2 && construction.OwnedCount == 2,
                "Both returned actual source actors own distinct base-destructor leases.");
            Reject<InvalidOperationException>(() => construction.RequireCurrentPhase());
            bool invoked = false;
            Reject<NotSupportedException>(() => construction.Create(401, () =>
            {
                invoked = true;
                return CreateControl(registry);
            }));
            Assert(!invoked && construction.State.Count == 2,
                "A CBody-only manipulation/debris request never calls a CBaddy factory or increments NumBaddies.");
            Reject<InvalidOperationException>(() => construction.Create<GodotControlActor>(203,
                () => throw new InvalidOperationException("Deliberate derived-constructor unwind.")));
            Assert(construction.State.Count == 2 && construction.OwnedCount == 2,
                "A failed derived construction immediately releases exactly its own base-constructor count.");
            root.AddChild(outer);
            outer.Lifecycle.Suspend();
            root.RemoveChild(outer);
            Assert(construction.State.Count == 2 && construction.OwnedCount == 2,
                "Suspending or leaving the native tree retains both real constructor leases.");
            outer.Free();
            Assert(construction.State.Count == 1 && construction.OwnedCount == 1,
                "Actual Free of an unparented control body releases through its native pre-delete observer.");
            nested!.Free();
            Assert(construction.State.Count == 0 && construction.OwnedCount == 0,
                "Native Free also releases an actor that never entered the scene tree.");

            // Spawn performs the canonical scope; callers must never wrap its registered factory again.
            GodotScriptActor? adopted = null;
            void Capture(GodotScriptActor actor) => adopted = actor;
            registry.Spawned += Capture;
            registry.Spawn(platformNode);
            registry.Spawned -= Capture;
            if (adopted is not null) allocated.Add(adopted);
            Assert(adopted is GodotPlatformActor && construction.State.Count == 1 && construction.OwnedCount == 1,
                "Central source Spawn wraps the genuine platform factory exactly once before registry adoption.");
            adopted!.Lifecycle.Kill();
            UpdateRegistry();
            Assert(adopted.Lifecycle.Phase == ActorLifePhase.PendingDeletion && construction.State.Count == 1,
                "Dead and pending source bodies retain their constructor count until actual logical deletion.");
            UpdateRegistry();
            Assert(registry.LiveCount == 0 && registry.DeletedCount == 1 && construction.State.Count == 0 &&
                construction.OwnedCount == 0 && GodotObject.IsInstanceValid(adopted),
                "The source removal phase releases immediately, before the later native queued destructor.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!GodotObject.IsInstanceValid(adopted) && construction.State.Count == 0,
                "The subsequent native destructor cannot decrement the already released source lease again.");

            var linkedRegistry = new GodotActorRegistry
            {
                Triggers = triggers,
                BaddyConstructionState = registry.BaddyConstructionState
            };
            root.AddChild(linkedRegistry);
            linkedRegistry.SetPhysicsProcess(false);
            GodotControlActor first = construction.Create(203, () => CreateControl(registry));
            GodotPlatformActor second = linkedRegistry.BaddyConstruction.Create(402, () =>
            {
                Assert(linkedRegistry.BaddyConstruction.RequireCurrentOrdinal() == 1,
                    "A second scheduler in the same source scene shares the actual count rather than a zero fallback.");
                return CreatePlatform(linkedRegistry);
            });
            Assert(construction.State.Count == 2 && ReferenceEquals(construction.State, linkedRegistry.BaddyConstruction.State),
                "Nested scene registries explicitly share one signed source construction count.");
            linkedRegistry.BaddyConstruction.Dispose();
            Assert(construction.State.Count == 2, "Disposing the factory owner does not destroy its still-live native actor.");
            second.Free();
            first.Free();
            Assert(construction.State.Count == 0 && construction.OwnedCount == 0 && linkedRegistry.BaddyConstruction.OwnedCount == 0,
                "Pre-delete observers release native actors even after their factory owner retires.");
            GD.Print($"Passed {assertions} native CBaddy construction checks: real control/platform factories, nested scopes, CBody exclusions, failed construction, tree exit, shared count and logical/native destructor ordering.");
        }
        finally
        {
            root.Free();
            foreach (GodotScriptActor actor in allocated.AsEnumerable().Reverse())
                if (GodotObject.IsInstanceValid(actor)) actor.Free();
        }

        GodotControlActor CreateControl(GodotActorRegistry actorRegistry)
        {
            GodotControlActor actor = GodotControlActor.Create(controlNode, actorRegistry);
            allocated.Add(actor);
            return actor;
        }
        GodotPlatformActor CreatePlatform(GodotActorRegistry actorRegistry)
        {
            GodotPlatformActor actor = GodotPlatformActor.Create(platformNode, actorRegistry);
            allocated.Add(actor);
            return actor;
        }
        void UpdateRegistry()
        {
            registry._PhysicsProcess(1.0 / 60);
            registry._PhysicsProcess(1.0 / 60);
        }
        void Assert(bool condition, string evidence)
        {
            if (!condition) throw new InvalidDataException($"Native CBaddy construction: {evidence}");
            assertions++;
        }
        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assertions++; return; }
            throw new InvalidDataException($"Native CBaddy construction expected {typeof(T).Name}.");
        }
    }
}
