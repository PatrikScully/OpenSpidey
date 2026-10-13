using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Uses the existing genuine L1A3 NPC fixture; no replacement actors, message queues or bodies.</summary>
internal static class ActorSessionDirectoryImportChecks
{
    public static void Verify(PlayerInteractionFixture fixture, GodotActorSessionDirectory directory,
        GodotThugNativeServices effects, GodotThugActor thug, GodotHostageActor hostage)
    {
        var hostageHandle = directory.StoreHandle(hostage);
        var thugHandle = directory.StoreHandle(thug);
        Assert(ReferenceEquals(directory.RecoverActor(hostageHandle), hostage.Actor) &&
            ReferenceEquals(effects.RecoverActor(hostageHandle), hostage.Actor) &&
            ReferenceEquals(effects.RecoverBody(hostage.Actor), hostage.Actor.Body),
            "Original thug effects must recover real cross-family hostage procedure/body identity.");
        Assert(ReferenceEquals(directory.RecoverActor(thugHandle), thug.Actor) &&
            ReferenceEquals(directory.RecoverNativeActor(thug.Actor), thug),
            "Real source thug procedure identity must recover its native owner.");
        directory.SendMessage(hostage, thugHandle, 13);
        var incoming = thug.Messages.Head!;
        Assert(incoming.Code == 13 && incoming.SenderHandle is GodotActorSessionHandle sender &&
            ReferenceEquals(directory.RecoverHandle(sender), hostage),
            "Hostage message13 must enter the actual thug queue with typed sender generation.");
        effects.SendMessage(thug, hostage.Actor, 2);
        HostageMessage accepted = hostage.Messages.Head!;
        effects.SendMessage(thug, hostageHandle, 3);
        HostageMessage shot = hostage.Messages.Head!;
        Assert(shot.Code == 3 && ReferenceEquals(shot.Next, accepted) && accepted.Code == 2 &&
            accepted.SenderHandle is GodotActorSessionHandle shooter && ReferenceEquals(directory.RecoverHandle(shooter), thug),
            "Messages2/3 must retain original newest-first order and the real typed shooter handle.");
        short health = hostage.State.Health;
        Assert(directory.ApplyHit(directory.SourceBaddyBodies.Single(body => ReferenceEquals(body.Identity, hostage)),
            new ActorHit(6, 2, 25, 0, default, 0, 1)) && hostage.State.Health == health,
            "CHostage's actual inherited Hit accepts contact without fabricated damage behavior.");
        incoming.Flags |= 1; thug.Messages.Cleanup();
        // First prove CMessage ordering above, then deliver2 and3 on separate actual source updates.
        shot.Flags |= 1; hostage.Messages.Cleanup();
        hostage.Advance();
        Assert(hostage.State.State == HostageBehaviorState.Begging && hostage.State.Substate == 1 &&
            ReferenceEquals(directory.RecoverActor(hostage.State.ShooterHandle), thug.Actor) && hostage.Model.ClipId == 5 &&
            hostage.Procedures.Count == 1,
            "Actual message2 must enter source Begging, recover the real shooter body, attach LookAt and run male clip5.");
        effects.SendMessage(thug, hostageHandle, 3); hostage.Advance();
        Assert(hostage.State.State == HostageBehaviorState.Dying && hostage.State.Substate == 1 && hostage.Model.ClipId == 3 &&
            (hostage.BodyFlags & 8) == 0 && (hostage.ItemFlags & 0xC00) == 0xC00,
            "Actual message3 must enter source death clip3, retire its real shadow and set original death item flags.");
        GodotScriptActor[] signalRecipients = fixture.Directory.SourceBodies.Select(body => body.Native)
            .Concat(fixture.Directory.SourceControls).Concat(directory.Registry.SourceActors(ActorUpdateGroup.Control))
            .Concat(directory.Registry.SourceActors(ActorUpdateGroup.Environment)).Distinct().ToArray();
        var pending = signalRecipients.ToDictionary(actor => actor, actor => actor.ScriptState.PendingSignals);
        int pulseCount = fixture.Pulses.Count;
        directory.SendSignal(hostage);
        foreach (GodotScriptActor recipient in signalRecipients)
        {
            bool linkedActor = hostage.Definition.Links.Any(link => link == recipient.SourceNodeId &&
                directory.Registry.Triggers.Nodes[link].TypeCode is 1 or 7);
            Assert(recipient.ScriptState.PendingSignals == (ushort)(pending[recipient] | (linkedActor ? 1 : 0)),
                $"Signal must use actual type1/7 linked actor identity, node{recipient.SourceNodeId}.");
            recipient.ScriptState.PendingSignals = pending[recipient];
        }
        Assert(fixture.Pulses.Count == pulseCount, "Linked signals must not enter mission pulse processing.");
        directory.SendDeathPulse(hostage);
        Assert(hostage.Definition.Links.SequenceEqual(new ushort[] { 18, 62, 231 }) &&
            fixture.Pulses.Skip(pulseCount).SequenceEqual(hostage.Definition.Links),
            "Genuine node17 death pulse must preserve authored18/62/231 link order through the scene mission handler.");
        // Kill only after the existing melee/pose proof. Original handles survive dead and pending phases.
        thug.Lifecycle.Kill();
        Assert(ReferenceEquals(directory.RecoverHandle(thugHandle), thug), "Dead source body remains recoverable until actual list removal.");
        directory.Registry._PhysicsProcess(1.0 / 60); directory.Registry._PhysicsProcess(1.0 / 60);
        Assert(thug.Lifecycle.Phase == ActorLifePhase.PendingDeletion && ReferenceEquals(directory.RecoverHandle(thugHandle), thug),
            "First source cleanup phase must retain pending actor pointer validity.");
        directory.Registry._PhysicsProcess(1.0 / 60); directory.Registry._PhysicsProcess(1.0 / 60);
        uint generation = thugHandle.Validation;
        Assert(directory.RecoverHandle(thugHandle) is null && !thugHandle.HasPointer && thugHandle.Validation == generation &&
            directory.RecoverActor(thug.Actor) is null && !fixture.Directory.Contains(thug),
            "Actual registry removal must expire typed pointer and captured body view immediately, retaining validation.");
        GD.Print("Verified scene actor directory: genuine304/305 cross-family messages13/2/3, source head order, native body/Hit routing, ordered mission signals/pulses, dead/pending validity and immediate removal generation expiry.");
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Actor session directory: {evidence}"); }
}
