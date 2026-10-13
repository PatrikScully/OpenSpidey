using System.Text.Json;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Source152 over a real current player, live protected swing/web effects and the genuine status2 retry owner.</summary>
internal static class PlayerScriptDeathImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotPlayer player, GodotActorRegistry registry,
        GodotPlayerWebRuntime webs, GodotPlayerCombatRuntime combat)
    {
        Action<int>? previousExit = player.Combat.LevelStatus;
        ActorScriptEnvironment previousEnvironment = registry.Environment;
        Input.MouseModeEnum previousMouseMode = Input.MouseMode;
        var lifecycle = new GodotLevelLifecycle { Name = "Source152RetryOwner", ProcessMode = Node.ProcessModeEnum.Always };
        owner.AddChild(lifecycle);
        int checks = 0;
        try
        {
            Assert(ReferenceEquals(registry.Player, player) && webs.Swing?.State.Current is { } &&
                webs.Actions.State.CurrentStrand is { } && combat.Particles.Count > 0,
                "Positive152 requires the actual current CPlayer and real protected swing/current-web source particle identities before requesting death.");
            var host = new GodotPlayerScriptDeath(player, registry);
            var handler = new PlayerScriptDeathCommandHandler(() => host,
                () => GodotObject.IsInstanceValid(player) && ReferenceEquals(registry.Player, player));
            // A parsed kind6 program retains exactly the no-operand source command shape.
            var node = new TriggerNode(0, 6, 0, new byte[] { 6, 0, 0, 0, 0, 0, 0, 0, 152, 0, 255, 255 });
            TriggerInstruction instruction = TriggerCommandReader.Read(node).Single();
            string state = JsonSerializer.Serialize(player.Combat.State);
            var position = player.Controller.Position; var velocity = player.Controller.Velocity;
            var action = player.Controller.Action; int clip = player.Model.ClipId;
            var pose = Enumerable.Range(0, player.Model.SourcePartCount).Select(player.Model.GetSourcePartPose).ToArray();
            object[] particles = Enum.GetValues<SourceParticleList>().SelectMany(combat.Particles.Identities).ToArray();
            object[] bodies = Enum.GetValues<ActorUpdateGroup>().SelectMany(group => registry.SourceList(group).SourceOrder)
                .Concat(registry.SuspendedSourceList.SourceOrder).ToArray();
            var swing = webs.Swing!.State.Current; var currentWeb = webs.Actions.State.CurrentStrand;
            int leases = registry.BaddyConstructionState.Count;
            ulong[] resources = Descendants(player).Select(child => child.GetInstanceId()).ToArray();

            // This is the same concrete callback as production; its private real lifecycle keeps the outer proof scene running.
            player.Combat.LevelStatus = lifecycle.RequestExit;
            Assert(host.LevelId == registry.Environment.LevelId && lifecycle.ExitReason == LevelExitReason.Running,
                "Native scripted death queries the canonical authored registry level and the genuine player-bound lifecycle callback.");
            handler.Execute(new(node, 0), instruction);
            Assert(lifecycle.ExitReason == LevelExitReason.PlayerDied && lifecycle.Initialized &&
                JsonSerializer.Serialize(player.Combat.State) == state && player.Controller.Position == position &&
                player.Controller.Velocity == velocity && player.Controller.Action == action && player.Model.ClipId == clip &&
                Enumerable.Range(0, pose.Length).All(part => player.Model.GetSourcePartPose(part) == pose[part]),
                "Source152 requests real status2 without zeroing health, switching action/animation, resetting initialization or altering any current physical/posed body field.");
            Assert(ReferenceEquals(swing, webs.Swing.State.Current) && !swing!.IsDisposed &&
                ReferenceEquals(currentWeb, webs.Actions.State.CurrentStrand) && !currentWeb!.Strand!.IsDisposed &&
                Enum.GetValues<SourceParticleList>().SelectMany(combat.Particles.Identities).SequenceEqual(particles) &&
                Enum.GetValues<ActorUpdateGroup>().SelectMany(group => registry.SourceList(group).SourceOrder)
                    .Concat(registry.SuspendedSourceList.SourceOrder).SequenceEqual(bodies) && registry.BaddyConstructionState.Count == leases &&
                Descendants(player).Select(child => child.GetInstanceId()).SequenceEqual(resources),
                "Unlike SwitchToDeathMode(false) and Prior cleanup, scripted152 retains every protected swing/web bit, native player attachment, source body and CBaddy lease.");
            registry.Environment = previousEnvironment with { LevelId = 0x901 };
            try { handler.Execute(new(node, 0), instruction); throw new Exception("Missing genuine training1 dependency was swallowed."); }
            catch (NotSupportedException error)
            {
                Assert(error.Message.Contains("training module user function1", StringComparison.Ordinal) &&
                    lifecycle.ExitReason == LevelExitReason.PlayerDied && JsonSerializer.Serialize(player.Combat.State) == state &&
                    ReferenceEquals(swing, webs.Swing.State.Current) && ReferenceEquals(currentWeb, webs.Actions.State.CurrentStrand),
                    "The native training-group path requires the separate actual module1 boundary and never falls through to ordinary death or mutates player resources.");
            }
            registry.Environment = previousEnvironment;
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(Descendants(lifecycle).OfType<Button>().Single().Text == "Restart checkpoint" && lifecycle.NavigationTask is null,
                "The genuine status2 owner presents its actual retry UI and waits for player input rather than eagerly deleting bodies or navigating.");
            GD.Print($"Passed {checks} actual source152 checks: current native player/level, genuine status2 retry callback/UI, unchanged health/action/pose and protected swing/web/attachments, plus strict separate training1 dependency.");
        }
        finally
        {
            registry.Environment = previousEnvironment;
            player.Combat.LevelStatus = previousExit;
            lifecycle.Free(); Input.MouseMode = previousMouseMode;
        }
        void Assert(bool condition, string message)
        { checks++; if (!condition) throw new InvalidDataException(message); }
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        { yield return child; foreach (Node descendant in Descendants(child)) yield return descendant; }
    }
}
