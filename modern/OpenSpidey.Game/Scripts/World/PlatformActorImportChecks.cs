using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Import;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.Audio;
using BitOperations = System.Numerics.BitOperations;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class PlatformActorImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotActorRegistry sessionActors)
    {
        var deathPulses = new List<ushort>();
        var actors = new GodotActorRegistry { Name = "PlatformVerification", Triggers = sessionActors.Triggers,
            ObjectLibrary = sessionActors.ObjectLibrary, ObjectTextures = sessionActors.ObjectTextures,
            ClaimObjectModel = sessionActors.ClaimObjectModel,
            Sounds = sessionActors.Sounds, SoundListener = sessionActors.SoundListener,
            RandomSource = sessionActors.RandomSource,
            ShatterPlatform = sessionActors.ShatterPlatform,
            ReceivePulse = deathPulses.Add,
            Environment = sessionActors.Environment };
        owner.AddChild(actors);
        actors.SetPhysicsProcess(false);
        GodotPlatformActor? platform = null;
        void Capture(GodotScriptActor actor) { if (actor is GodotPlatformActor mechanism) platform = mechanism; }
        actors.Spawned += Capture;
        try
        {
            actors.Spawn(actors.Triggers.Nodes[55]);
            if (platform is null || platform.SourceUpdates != 0 || platform.ModelIndex != -1)
                throw new InvalidDataException("Platform constructor parsed its script before the first AI update.");
            VerifyDamageMetadata(platform);
            if (!ReferenceEquals(platform.SupportMotion, platform.Motion.Body))
                throw new InvalidDataException("Player support and recorded platform physics must share the actual source body.");
            ushort initialBodyFlags = platform.BodyFlags;
            var start = platform.Motion.Position;
            platform.Advance();
            if (platform.BodyFlags != (ushort)(initialBodyFlags & ~4))
                throw new InvalidDataException("The platform's first source update retained CBody's initial timing flag.");
            MeshInstance3D model = platform.GetNode<MeshInstance3D>("SourceModel");
            if (platform.ModelChecksum != 0x26179DC6 || platform.Motion.Angles.Y != 1024 ||
                platform.ScriptState.WaitTicks != 30 || platform.Motion.Position == start || !platform.Visible ||
                model.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() == 0)
                throw new InvalidDataException("Original platform failed to select its model, rotate, seek a linked target or begin its update-based wait.");
            ImportedCollisionBody[] bodies = platform.GetNode<Node3D>("WorldCollision").GetChildren().OfType<ImportedCollisionBody>().ToArray();
            if (bodies.Length != 0)
                throw new InvalidDataException("The original non-colliding spinning model acquired fabricated collision bodies.");
            for (int frame = 0; frame < 30; frame++) platform.Advance();
            if (platform.ScriptState.WaitTicks != 0 || platform.Motion.Angles.Y != 3072 || platform.SourceUpdates != 31)
                throw new InvalidDataException("Platform wait used elapsed ticks rather than source update count.");
            platform.Advance(); // The yielding jump consumes the next update.
            platform.Advance(); // The next leg starts on the following update.
            if (platform.ScriptState.WaitTicks != 30 || platform.SourceUpdates != 33 || platform.Motion.Angles.Y != 1024)
                throw new InvalidDataException("Original platform did not resume its linked route after the yielding jump.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            TriggerNode source = actors.Triggers.Nodes[54];
            TriggerActorDefinition definition = TriggerActorDefinition.Read(source);
            int behaviorStart = definition.ObjectDataOffset + 18;
            byte[] stoppedScript = source.Data[..(behaviorStart + 6)].ToArray().Concat(new byte[] { 0, 0x41 }).ToArray();
            actors.Spawn(source with { Data = stoppedScript });
            GodotPlatformActor stopped = platform;
            stopped.Advance();
            stopped.WriteVariable(0x2134, ActorScriptCursorForWords(0, 0, 5));
            stopped.WriteVariable(0x2127, ActorScriptCursorForWords(0, 512, 0));
            for (int update = 0; update < 5; update++) stopped.Advance();
            if (stopped.ScriptState.Active || stopped.ScriptState.Retired || !stopped.Visible ||
                stopped.Lifecycle.Phase != ActorLifePhase.Active || stopped.GetNode<MeshInstance3D>("SourceModel").Mesh is null)
                throw new InvalidDataException("A completed platform script discarded its visible model or actor lifetime.");
            bodies = stopped.GetNode<Node3D>("WorldCollision").GetChildren().OfType<ImportedCollisionBody>().ToArray();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (bodies.Length == 0 || bodies.Any(body => body.GlobalPosition.DistanceTo(stopped.GlobalPosition) > 0.001f ||
                body.ModelChecksum != 0x6E8DB978 || body.SourceTriangles.Count == 0 ||
                body.GetChildren().OfType<CollisionShape3D>().Any(shape => shape.Disabled)))
                throw new InvalidDataException("Solid platform collision did not follow its source translation/rotation.");
            bool contacted = false;
            foreach (ImportedCollisionBody body in bodies)
                foreach (CollisionTriangle triangle in body.SourceTriangles)
                {
                    Vector3 center = body.GlobalTransform * PsxCoordinates.WorldPosition((triangle.A + triangle.B + triangle.C) / 3);
                    var sourceNormal = triangle.FixedNormal;
                    Vector3 normal = body.GlobalBasis * new Vector3(sourceNormal.X, -sourceNormal.Y, -sourceNormal.Z).Normalized();
                    if (normal.LengthSquared() < 0.5f) continue;
                    using var query = PhysicsRayQueryParameters3D.Create(center + normal * 0.1f, center - normal * 0.1f, body.CollisionLayer);
                    using var hit = owner is Node3D spatial ? spatial.GetWorld3D().DirectSpaceState.IntersectRay(query)
                        : throw new InvalidDataException("Platform verification requires a 3D world.");
                    contacted |= hit.Count != 0 && hit["collider"].AsGodotObject() == body;
                }
            if (!contacted) throw new InvalidDataException("Native rays did not hit the translated and rotated source platform faces.");
            actors.Spawn(actors.Triggers.Nodes[54]);
            GodotPlatformActor helicopter = platform;
            helicopter.Advance();
            if (helicopter.SoundHandle is 0 or uint.MaxValue || helicopter.SoundUpdateInterval != 3)
                throw new InvalidDataException("Original platform 54 did not execute its level-specific positional loop command.");
            var sounds = owner.GetNode<GodotSoundChannels>("SoundChannels");
            int soundChannel = BitOperations.TrailingZeroCount(helicopter.SoundHandle);
            var voice = sounds.PlayerForChannel(soundChannel) ?? throw new InvalidDataException("Platform sound has no native voice.");
            if (voice.Stream is not AudioStreamSynchronized soundStream || soundStream.GetSyncStream(0) is not AudioStreamWav left ||
                soundStream.GetSyncStream(1) is not AudioStreamWav right || left.LoopMode != AudioStreamWav.LoopModeEnum.Forward ||
                right.LoopMode != AudioStreamWav.LoopModeEnum.Forward || left.GetLength() <= 0 || left.GetLength() != right.GetLength() ||
                voice.PitchScale != 1 || !sounds.Controller.ReadChannel(soundChannel).Looped)
                throw new InvalidDataException("Platform loop changed its source PCM clock, stereo synchronization or original pitch.");
            int mixUpdates = sounds.MixUpdates;
            actors.SetPhysicsProcess(true);
            while (actors.SourceUpdates < 3)
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            actors.SetPhysicsProcess(false);
            if (actors.SourceUpdates != 3 || sounds.MixUpdates != mixUpdates + 1)
                throw new InvalidDataException("Platform sound interval did not follow the original once-per-world-update TTime counter.");
            sounds.Controller.UpdatePosition(helicopter.SoundHandle, helicopter.Motion.Position, new(helicopter.Motion.Position >> 12));
            SoundMix nearMix = sounds.Controller.ReadChannel(soundChannel).Mix;
            if (Mathf.Abs(soundStream.GetSyncStreamVolume(0) - nearMix.VolumeDb - nearMix.LeftDb) > 0.0001f ||
                Mathf.Abs(soundStream.GetSyncStreamVolume(1) - nearMix.VolumeDb - nearMix.RightDb) > 0.0001f)
                throw new InvalidDataException("Native positional gains did not update both synchronized channels.");
            sounds.Controller.UpdatePosition(helicopter.SoundHandle, helicopter.Motion.Position, new(new(100000, 0, 0)));
            if (sounds.Controller.ReadChannel(soundChannel).Mix.Volume != 0)
                throw new InvalidDataException("A distant platform loop retained audible source attenuation.");
            ulong deadline = Time.GetTicksMsec() + (ulong)(left.GetLength() * 1000) + 150;
            while (Time.GetTicksMsec() < deadline)
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (!voice.Playing) throw new InvalidDataException("Original positional loop ended at its PCM boundary.");
            helicopter.ExecuteCommand(0x4509, ActorScriptCursorForWords(), actors.Environment);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (helicopter.SoundHandle != 0 || sounds.Controller.ReadChannel(soundChannel).Allocated || GodotObject.IsInstanceValid(voice))
                throw new InvalidDataException("Platform sound stop retained a source allocation or native playback node.");
            if (owner is not Node3D spatialOwner)
                throw new InvalidDataException("Original item shatter verification requires the native level world.");
            await ItemShatterImportChecks.VerifyAsync(spatialOwner, helicopter, actors.ObjectTextures(),
                new GodotActorWorldQuery(spatialOwner, actors.Triggers));
            GodotPlayerCombatRuntime runtime = owner.GetNode<GodotPlayerCombatRuntime>("PlayerCombatRuntime");
            helicopter.WriteVariable(0x2100, ActorScriptCursorForWords(1));
            helicopter.WriteVariable(0x2125, ActorScriptCursorForWords(1, 1));
            if (!helicopter.Hit(new ActorHit(0, 0, 0, 1, default)) || helicopter.Variables.Health != 0 ||
                helicopter.Lifecycle.Phase != ActorLifePhase.Dead || helicopter.Visible ||
                (helicopter.BodyFlags & 0x40) == 0 || !deathPulses.SequenceEqual(helicopter.Definition.Links) ||
                runtime.ItemShatter.InstanceState(helicopter)?.ItemFlags != 1 ||
                runtime.ItemShatter.ChunkCount + runtime.ItemShatter.GlassCount == 0)
                throw new InvalidDataException("Lethal platform contact did not compose actual death pulses, body flags and production shatter effects.");
            int pulseCount = deathPulses.Count;
            int pieceCount = runtime.ItemShatter.ChunkCount + runtime.ItemShatter.GlassCount;
            helicopter.Hit(new ActorHit(0, 0, 0, 1, default));
            if (deathPulses.Count != pulseCount || runtime.ItemShatter.ChunkCount + runtime.ItemShatter.GlassCount != pieceCount)
                throw new InvalidDataException("A second contact repeated the dead platform's effects or mission pulses.");
            runtime.ItemShatter.Forget(helicopter);
            for (int update = 0; update < 96; update++) runtime.ItemShatter.Advance();
            if (runtime.ItemShatter.ChunkCount + runtime.ItemShatter.GlassCount != 0)
                throw new InvalidDataException("Production platform shatter retained expired source fragments.");
            GD.Print("Verified native platform Hit: actual body death, ordered authored pulses, source break sound and production shatter lifetime.");
            GD.Print("Verified Godot mechanisms: original platform 55 route/rotation and 30-update wait, source model collision filtering, solid moving collision ray hits and persistent visible body after script completion.");
            GD.Print("Verified original platform 54 audio: level alias 1, source PCM stereo clock, synchronized positional gains, loop beyond sample end and explicit stop cleanup.");
        }
        finally
        {
            actors.Spawned -= Capture;
            actors.QueueFree();
        }
    }

    private static void VerifyDamageMetadata(GodotPlatformActor platform)
    {
        ushort expectedFlags = (ushort)(0x216 & (platform.Definition.ClearsBodyFlag2 ? ~2 : -1));
        if (platform.Lifecycle.Phase == ActorLifePhase.Suspended) expectedFlags |= 1;
        ActorCollisionBody snapshot = new GodotActorSessionPlatforms().Snapshot(platform);
        if (!ReferenceEquals(snapshot.Identity, platform) || snapshot.Type != 402 ||
            snapshot.Position != platform.Motion.Position || snapshot.MinorRadius != 128 ||
            snapshot.BodyFlags != expectedFlags || snapshot.Health != 0)
            throw new InvalidDataException("Platform contact metadata lost its real constructor, radius or source body identity.");

        // Explicit crafted boundaries on an actual source402 node; authored script state is restored below.
        platform.WriteVariable(0x2100, ActorScriptCursorForWords(50));
        if (!platform.Hit(new ActorHit(0, 0, 0, 7, default)) ||
            platform.Variables.Health != 50 || platform.Variables.ByteParameter != 1)
            throw new InvalidDataException("An unbreakable platform changed health or failed to count accepted contact.");
        platform.WriteVariable(0x2125, ActorScriptCursorForWords(1, 1));
        if (!new GodotActorSessionPlatforms().Hit(platform, new ActorHit(0, 0, 0, 7, default)) ||
            platform.Variables.Health != 43 || platform.Variables.ByteParameter != 2)
            throw new InvalidDataException("Native platform damage did not route through its actual Hit override.");
        platform.WriteVariable(0x2100, ActorScriptCursorForWords(1));
        platform.WriteVariable(0x212D, ActorScriptCursorForWords(255));
        platform.Hit(new ActorHit(0, 0, 0, ushort.MaxValue, default));
        if (platform.Variables.Health != 2 || platform.Variables.ByteParameter != 0 ||
            platform.Lifecycle.Phase is ActorLifePhase.Dead or ActorLifePhase.PendingDeletion)
            throw new InvalidDataException("Platform damage lost the original signed-short overflow and byte counter wrap.");
        platform.WriteVariable(0x2100, ActorScriptCursorForWords(0));
        platform.WriteVariable(0x212D, ActorScriptCursorForWords(0));
        platform.WriteVariable(0x2125, ActorScriptCursorForWords(1, 0));
    }

    private static ActorScriptCursor ActorScriptCursorForWords(params ushort[] words)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (ushort word in words) writer.Write(word);
        return new(stream.ToArray());
    }
}
