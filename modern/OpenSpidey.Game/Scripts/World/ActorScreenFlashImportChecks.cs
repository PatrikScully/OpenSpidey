using System.Diagnostics;
using System.Buffers.Binary;
using Godot;
using OpenSpidey.Core.Missions;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.World;

/// <summary>Bounded authored343 construction, genuine live source wait/flash phases and global overlay ownership.</summary>
internal static class ActorScreenFlashImportChecks
{
    private static readonly byte[][] ExpectedColours = [[120, 20, 20], [120, 120, 120], [20, 20, 120], [20, 120, 20], [120, 120, 120]];

    public static async Task VerifyAsync(StoryLevelSession story)
    {
        GodotActorRegistry registry = story.Registry;
        GodotScreenFlash flash = Descendants(story).OfType<GodotScreenFlash>().Single();
        ScreenFlashState state = flash.State;
        IActorScreenFlashHost? previousHost = registry.ScreenFlash;
        long[] previousState = Snapshot(state);
        GodotControlActor? actor = null;
        int checks = 0;
        Assert(story.Failure is null && story.Startup.Phase == MissionStartupPhase.Playing &&
            story.Application.MissionName.Equals("L1A3_T", StringComparison.OrdinalIgnoreCase) && ReferenceEquals(previousHost, flash),
            "Actual L1A3 Playing uses the same global flash owner as inherited CBaddy429C and the source pre-player update phase.");
        TriggerNode node = registry.Triggers.Nodes[343];
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        ushort[] words = Enumerable.Range(0, definition.BehaviorData.Length / 2)
            .Select(index => BinaryPrimitives.ReadUInt16LittleEndian(definition.BehaviorData.Span[(index * 2)..])).ToArray();
        ushort[] expected = [0x4105, 0x428E, 300, 0x4104, 0, 0x4280, 18,
            0x429C, 120, 20, 20, 2, 1, 0x4280, 17, 0x429C, 120, 120, 120, 2, 1,
            0x4280, 17, 0x429C, 20, 20, 120, 2, 1, 0x4280, 17, 0x429C, 20, 120, 20, 2, 1,
            0x4280, 17, 0x428E, 300, 0x429C, 120, 120, 120, 2, 1, 0x4102, 0, 0x4100];
        Assert(definition.ActorType == 203 && !definition.InitializeOnLoad && !definition.StartsSuspended &&
            words.SequenceEqual(expected) && !registry.SourceActors(ActorUpdateGroup.Control).Any(body => body.SourceNodeId == 343),
            "Untouched343 retains original18/17 waits, two sound300 calls, five independent five-word429C operands and its label0 loop; this bounded proof explicitly constructs the authored control without claiming manual vault traversal.");
        var calls = new List<(int Update, byte Red, byte Green, byte Blue)>();
        int decayVisits = 0;
        bool complete = false;
        var recorder = new FlashRecorder((red, green, blue, frames, importance, sort) =>
        {
            GodotControlActor actual = actor ?? throw new InvalidDataException("Authored343 flashed during its initial wait constructor.");
            int index = calls.Count;
            Assert(index < ExpectedColours.Length && actual.SourceUpdates == (index + 1) * 10 &&
                red == ExpectedColours[index][0] && green == ExpectedColours[index][1] && blue == ExpectedColours[index][2] &&
                frames == 2 && importance == 0 && sort == 1 && state.Importance == 0 && state.FlashCountdown == 0,
                $"Original343 flash{index} follows exact wait-source update{(index + 1) * 10}, independent RGB, two logic visits and priority0/sort1; actual update{actual.SourceUpdates}, RGB{red}/{green}/{blue}, frames{frames}, priority{importance}/{state.Importance}, sort{sort}, countdown{state.FlashCountdown}.");
            flash.Screen(red, green, blue, frames, importance, sort);
            ColorRect overlay = flash.GetChildren().OfType<ColorRect>().Single();
            Assert(state.FlashCountdown == 2 && state.Sort == 1 && state.CurrentRed == (uint)red << 16 &&
                state.CurrentGreen == (uint)green << 16 && state.CurrentBlue == (uint)blue << 16 && overlay.Visible &&
                overlay.Color.IsEqualApprox(new Color(red / 255f, green / 255f, blue / 255f, ScreenFlashState.DisplayAlpha / 255f)),
                "Same source control visit immediately presents the existing Flash_Display overlay, its independent RGB and original packed alpha32; no new particle or duplicate overlay is allocated.");
            calls.Add((actual.SourceUpdates, red, green, blue));
            decayVisits = 0;
        });
        registry.ScreenFlash = recorder;
        flash.SourceUpdated += VerifyDecay;
        try
        {
            flash.Reset();
            registry.Spawn(node);
            actor = registry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single(body => body.SourceNodeId == 343);
            Assert(actor.SourceUpdates == 0 && actor.ScriptState.WaitTicks == 18 && actor.ScriptState.Active && calls.Count == 0 &&
                registry.SourceList(ActorUpdateGroup.Control).SourceOrder.Contains(actor),
                "Genuine343 constructor parses its unchanged discover-label/sound/wait18 program before any AI or flash and attaches the canonical source Control head.");
            var deadline = Stopwatch.StartNew();
            // Flash_Update runs before Control AI. Wait for the final logic visit to finish both phases.
            while (!complete || actor.SourceUpdates < 52)
            {
                if (story.Failure is { } failure) throw new InvalidOperationException("Actual L1A3 flash source phase failed.", failure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Original343 did not complete its first five source flashes.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            // Flash5/update50 executes4102: the original jumps to label0 but yields before parsing its wait.
            // Update51 installs wait18; only update52 and later visits subtract their two elapsed ticks.
            Assert(actor.SourceUpdates >= 52 && actor.SourceUpdates < 60 && actor.ScriptState.WaitTicks == 18 - (actor.SourceUpdates - 51) * 2 && calls.Count == 5 &&
                state.FlashCountdown == 0 && state.Importance == 0 && registry.Owns(actor),
                $"After original white flash5/update50,4102 jumps and yields; update51 installs wait18 and update52 first decrements to16. Two source pre-player updates retire only the global flash, retaining the live authored looping control. Updates={actor.SourceUpdates}, wait={actor.ScriptState.WaitTicks}, countdown={state.FlashCountdown}.");
            registry.DestroyImmediately(actor);
            Assert(!GodotObject.IsInstanceValid(actor) && !registry.SourceList(ActorUpdateGroup.Control).SourceOrder.Contains(actor),
                "Explicit fixture cleanup uses real source registry destruction and removes only authored343; the shared scene flash remains alive.");
            actor = null;
            GD.Print($"Passed {checks} actual source429C checks: untouched L1A3 control343,18/17-tick waits, five distinct RGB flashes, two-visit pre-player decay, shared alpha32 widescreen overlay and genuine control destruction. Vault activation was a bounded authored construction.");
        }
        finally
        {
            flash.SourceUpdated -= VerifyDecay;
            registry.ScreenFlash = previousHost;
            if (actor is not null && GodotObject.IsInstanceValid(actor)) registry.DestroyImmediately(actor);
            Restore(state, previousState); flash.Synchronize();
        }

        void Assert(bool condition, string message)
        { checks++; if (!condition) throw new InvalidDataException(message); }
        void VerifyDecay()
        {
            if (actor is null || calls.Count == 0 || decayVisits == 2) return;
            int emissionUpdate = calls[^1].Update;
            Assert(actor.SourceUpdates == emissionUpdate + decayVisits,
                $"The actual pre-player Flash_Update precedes control AI exactly once per logic visit: emitted{emissionUpdate}, visits{decayVisits}, actor{actor.SourceUpdates}.");
            decayVisits++;
            int wanted = 2 - decayVisits;
            Assert(state.FlashCountdown == wanted && state.Visible == (wanted != 0),
                $"Flash_Update retains original two-visit decay: wanted countdown{wanted}, actual{state.FlashCountdown}.");
            ColorRect overlay = flash.GetChildren().OfType<ColorRect>().Single();
            Assert(overlay.Visible == (wanted != 0) && overlay.GetGlobalRect().Size.IsEqualApprox(story.GetViewport().GetVisibleRect().Size),
                "The existing flash quad follows source countdown and fills the current viewport, including its widescreen edges.");
            if (calls.Count == ExpectedColours.Length && decayVisits == 2) complete = true;
        }
    }

    private sealed class FlashRecorder(Action<byte, byte, byte, int, byte, int> invoke) : IActorScreenFlashHost
    {
        public void Screen(byte red, byte green, byte blue, int frames, byte importance, int sort) => invoke(red, green, blue, frames, importance, sort);
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        { yield return child; foreach (Node descendant in Descendants(child)) yield return descendant; }
    }
    private static long[] Snapshot(ScreenFlashState state) => [state.FadeCountdown, state.FlashCountdown, state.Fading, state.Importance, state.Sort,
        state.CurrentRed, state.CurrentGreen, state.CurrentBlue, state.DeltaRed, state.DeltaGreen, state.DeltaBlue];
    private static void Restore(ScreenFlashState state, long[] v)
    {
        state.FadeCountdown = (int)v[0]; state.FlashCountdown = (int)v[1]; state.Fading = (int)v[2]; state.Importance = (byte)v[3]; state.Sort = (int)v[4];
        state.CurrentRed = (uint)v[5]; state.CurrentGreen = (uint)v[6]; state.CurrentBlue = (uint)v[7];
        state.DeltaRed = (uint)v[8]; state.DeltaGreen = (uint)v[9]; state.DeltaBlue = (uint)v[10];
    }
}
