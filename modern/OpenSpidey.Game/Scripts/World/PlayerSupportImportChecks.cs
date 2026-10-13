using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class PlayerSupportImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotActorRegistry sessionActors, GodotPlayer player)
    {
        var position = player.Controller.Position;
        var input = player.SampleInput;
        var buttons = player.SampleButtons;
        var voices = new List<(int Bank, int Slot, int Priority)>();
        var actors = new GodotActorRegistry { Name = "PlayerSupportVerification", Triggers = sessionActors.Triggers,
            ObjectLibrary = sessionActors.ObjectLibrary, ObjectTextures = sessionActors.ObjectTextures,
            ClaimObjectModel = sessionActors.ClaimObjectModel,
            Sounds = sessionActors.Sounds, SoundListener = sessionActors.SoundListener, Environment = sessionActors.Environment,
            VoicePlayback = (bank, slot, priority) => { voices.Add((bank, slot, priority)); sessionActors.VoicePlayback(bank, slot, priority); } };
        owner.AddChild(actors);
        actors.SetPhysicsProcess(false);
        GodotPlatformActor pressure = null!;
        actors.Spawned += actor => pressure = (GodotPlatformActor)actor;
        try
        {
            actors.Spawn(actors.Triggers.Nodes[170]);
            pressure.Advance();
            if (!pressure.ScriptState.Active || pressure.ModelChecksum != 0x749C9220 || voices.Count != 0)
                throw new InvalidDataException("Original pressure platform did not select its mesh and wait for player contact.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            player.SampleInput = () => default;
            player.SampleButtons = () => default;
            player.Controller.SetScriptPosition(pressure.Motion.Position + new FixedVector3(0, -(114 << 12), 0));
            player.Controller.SetScriptVelocity(default);
            player.Controller.Stand();
            await StepAsync(owner, player);
            if (player.Controller.SupportBody?.Id != pressure.GetInstanceId() || pressure.UpdatesSinceTrodden != 0)
            {
                var hit = player.Controller.LastTerrainStep.Fall?.Hit;
                throw new InvalidDataException($"Original pressure contact did not reach its actor: model={hit?.ModelIndex}, source={hit?.SourceAsset}, support={hit?.SupportId}, expected={pressure.GetInstanceId()}.");
            }
            pressure.Advance();
            if (pressure.ScriptState.Active || !pressure.Visible || !voices.SequenceEqual(new[] { (1, 9, 50) }))
                throw new InvalidDataException("Player contact did not resume the original pressure-platform voice script while retaining its visible model.");
            pressure.Motion.Velocity = new(4096, 0, 0);
            for (int step = 0; step < 3; step++)
            {
                FixedVector3 before = player.Controller.Position;
                FixedVector3 previousPlatform = pressure.Motion.Position;
                await StepAsync(owner, player);
                if (player.Controller.Position.X != before.X + 4096 || player.Controller.SupportBody?.Id != pressure.GetInstanceId())
                    throw new InvalidDataException("Player finalizer did not ride the specific native supporting actor at source velocity.");
                pressure.Advance();
                if (pressure.Motion.Position.X != previousPlatform.X + 8192)
                    throw new InvalidDataException("Platform body did not integrate its two elapsed ticks after its rider's update.");
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            pressure.Motion.Velocity = default;
            FixedVector3 gravityStart = pressure.Motion.Position;
            pressure.WriteVariable(0x2123, new ActorScriptCursor(new byte[] { 1, 0 }));
            pressure.Advance();
            if (!pressure.GravityEnabled || pressure.Motion.Position.Y != gravityStart.Y + 0x18000 || pressure.Motion.Velocity.Y != 0x10000)
                throw new InvalidDataException("Platform gravity failed to reset acceleration and integrate the original default vector twice.");
            pressure.WriteVariable(0x2123, new ActorScriptCursor(new byte[] { 0, 0 }));
            pressure.Motion.Velocity = default;
            pressure.Advance();
            if (pressure.GravityEnabled || pressure.Motion.Acceleration != default)
                throw new InvalidDataException("Disabling platform gravity retained acceleration from the preceding update.");
            pressure.Motion.Position = gravityStart;
            pressure.Advance();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            pressure.Lifecycle.Kill();
            await StepAsync(owner, player);
            if (player.Controller.SupportBody is not null || player.Controller.LastTerrainStep.Fall?.Hit?.SupportId != pressure.GetInstanceId())
                throw new InvalidDataException("The player retained a dead platform's support identity.");
            GD.Print("Verified native player support: original platform 170 contact, trodden wait, voice 1/9/50, persistent visibility, repeated source riding, gravity integration and dead-support release.");
        }
        finally
        {
            player.SetPhysicsProcess(false);
            player.Controller.TeleportFromScript(position);
            player.SampleInput = input;
            player.SampleButtons = buttons;
            actors.QueueFree();
        }
    }
    private static async Task StepAsync(Node owner, GodotPlayer player)
    {
        int updates = player.SourceUpdates;
        player.SetPhysicsProcess(true);
        while (player.SourceUpdates == updates)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.SetPhysicsProcess(false);
    }
}
