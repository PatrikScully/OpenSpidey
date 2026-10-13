using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player.Scripting;

/// <summary>Ordered two-phase player input VM, preserving concurrent commands and next-update resumption.</summary>
public sealed class PlayerScriptRuntime
{
    private PlayerScriptProgram program;
    private readonly IPlayerScriptHost host;
    private readonly LinkedList<Command> commands = [];
    private readonly PlayerButtonState[] buttons = new PlayerButtonState[20];
    private int cursor;
    private int elapsed;
    private bool faulted;
    private PlayerAxes axes;

    public bool Active { get; private set; } = true;
    public bool Decoding { get; private set; } = true;
    public int CheckpointNode { get; private set; }
    public int PendingCommands => commands.Count;
    public int InstructionIndex => cursor;
    public int ElapsedTicks => elapsed;

    public PlayerScriptRuntime(PlayerScriptProgram program, IPlayerScriptHost host, int initialElapsedTicks = 0)
    {
        this.program = program;
        this.host = host;
        elapsed = initialElapsedTicks;
    }

    public void ReplaceProgram(PlayerScriptProgram replacement)
    {
        if (faulted) throw new InvalidOperationException("A faulted player script requires a new session.");
        program = replacement;
        cursor = 0;
        CheckpointNode = 0;
        Active = true;
        Decoding = true;
        Array.Clear(buttons);
        // SwitchToSynthesizedInput replaces the cursor but retains elapsed time and concurrent commands.
    }

    public PlayerScriptInput Update(int ticks, PlayerScriptEnvironment environment,
        ReadOnlySpan<PlayerButtonState> physicalButtons = default)
    {
        if (faulted) throw new InvalidOperationException("Player script is faulted; partially executed input cannot be replayed.");
        if (!Active) throw new InvalidOperationException("Player script has completed.");
        if (physicalButtons.Length is not (0 or 20)) throw new ArgumentException("Expected twenty physical button states.", nameof(physicalButtons));
        try
        {
            elapsed = unchecked(elapsed + ticks);
            axes = default;
            host.ResetIdleTimer();
            if (environment.Abort) { Decoding = false; commands.Clear(); }
            ExecuteDue(ticks);
            uint held = UpdateCommands(ticks);
            PlayerButtonState[] output = physicalButtons.IsEmpty ? new PlayerButtonState[20] : physicalButtons.ToArray();
            for (int index = 0; index < buttons.Length; index++)
            {
                if (index is 14 or 15) continue;
                if ((held & (1u << index)) == 0) buttons[index] = buttons[index] with { Pressed = 0 };
                if (buttons[index].Pressed == 0) buttons[index] = default;
                output[index] = buttons[index];
            }
            FinishIfIdle(environment);
            return new(axes, Array.AsReadOnly(output));
        }
        catch { faulted = true; throw; }
    }

    private void ExecuteDue(int ticks)
    {
        while (Decoding)
        {
            if (cursor >= program.Instructions.Count) throw new InvalidDataException("Player script has no Stop instruction.");
            PlayerScriptInstruction instruction = program.Instructions[cursor];
            if (instruction.DueTime != -1 && instruction.DueTime > elapsed) return;
            elapsed = 0;
            cursor++;
            int duration = unchecked(instruction.Second + ticks);
            switch (instruction.Opcode)
            {
                case PlayerScriptOpcode.Teleport: host.TeleportToNode(instruction.First); break;
                case PlayerScriptOpcode.WalkToTarget:
                    commands.AddLast(new Walk(host.TriggerPosition(instruction.First)));
                    Decoding = false;
                    break;
                case PlayerScriptOpcode.HoldInput:
                    int index = instruction.First;
                    if (index is < 0 or > 19 or 12 or 13 or 14 or 15)
                        throw new InvalidDataException($"Player script at 0x{instruction.SourceOffset:X}: unsupported input register {index}.");
                    commands.AddLast(new HoldInput(index, duration));
                    break;
                case PlayerScriptOpcode.PlayVoice: host.PlayVoice(instruction.First, instruction.Second, instruction.Third); break;
                case PlayerScriptOpcode.MoveToTarget:
                    commands.AddLast(new Move(host.TriggerPosition(instruction.First), instruction.Second));
                    Decoding = false;
                    break;
                case PlayerScriptOpcode.HoldAnimation: commands.AddLast(new HoldAnimation(instruction.First, duration)); break;
                case PlayerScriptOpcode.StandAfterDelay: commands.AddLast(new DelayedStand(unchecked(instruction.First + ticks))); break;
                case PlayerScriptOpcode.HoldActionValue: commands.AddLast(new HoldActionValue(instruction.First, duration)); break;
                case PlayerScriptOpcode.Wait:
                    commands.AddLast(new Wait(unchecked(instruction.First + ticks)));
                    Decoding = false;
                    break;
                case PlayerScriptOpcode.WaitForAnimationSignal:
                    commands.AddLast(new AnimationSignal());
                    Decoding = false;
                    if (host.AnimationId != instruction.First || host.AnimationFinished)
                        host.PlayAnimation(instruction.First, ScriptAnimationPolicy.PreserveSoundScript);
                    break;
                case PlayerScriptOpcode.SetCheckpoint: CheckpointNode = instruction.First; break;
                case PlayerScriptOpcode.HoldInteraction:
                    if (host.IsCrawling) commands.AddLast(new HoldInput(16, ticks));
                    break;
                case PlayerScriptOpcode.SetScriptFlag: host.SetScriptFlag(instruction.First != 0); break;
                case PlayerScriptOpcode.Stop: Decoding = false; break;
                default: throw new InvalidDataException($"Unimplemented player opcode {instruction.Opcode}.");
            }
        }
    }

    private uint UpdateCommands(int ticks)
    {
        uint held = 0;
        LinkedListNode<Command>? node = commands.First;
        while (node is not null)
        {
            LinkedListNode<Command>? next = node.Next;
            bool completed = UpdateCommand(node.Value, ticks, ref held);
            if (completed) commands.Remove(node);
            node = next;
        }
        return held;
    }

    private bool UpdateCommand(Command command, int ticks, ref uint held)
    {
        if (command is Timed timed) timed.Remaining = unchecked(timed.Remaining - ticks);
        switch (command)
        {
            case Walk walk:
                FixedVector3 delta = (walk.Target - host.Position) >> 12;
                if (FixedVectorMath.OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Z * delta.Z)) < 64)
                { Decoding = true; return true; }
                int heading = 1024 - GameAngle.Atan2(delta.Z, delta.X) - host.CameraHeading;
                (short sine, short cosine) = GameAngle.Heading(heading);
                axes = new((sbyte)Math.Clamp(cosine / 32, -127, 127), (sbyte)Math.Clamp(sine / -32, -127, 127));
                return false;
            case HoldInput input:
                if (input.Remaining < 0) return true;
                held |= 1u << input.Index;
                if (input.Index is >= 8 and <= 11)
                {
                    buttons[input.Index] = buttons[input.Index] with { Pressed = 1 };
                    axes = input.Index switch
                    {
                        8 => axes with { Sideways = -127 }, 9 => axes with { Sideways = 127 },
                        10 => axes with { Forward = -127 }, _ => axes with { Forward = 127 }
                    };
                }
                else buttons[input.Index] = buttons[input.Index] with
                { Pressed = 1, Triggered = buttons[input.Index].Pressed == 0 ? (byte)1 : buttons[input.Index].Triggered };
                return false;
            case Move move:
                FixedVector3 movement = (move.Target - host.Position) >> 12;
                if (FixedVectorMath.OriginalSquareRoot(unchecked(movement.X * movement.X + movement.Y * movement.Y + movement.Z * movement.Z)) < 64)
                { Decoding = true; return true; }
                host.Velocity = FixedVectorMath.Normalize(movement);
                return false;
            case HoldAnimation animation:
                if (animation.Remaining < 0) return true;
                if (host.AnimationId != animation.AnimationId || host.AnimationFinished)
                    host.PlayAnimation(animation.AnimationId, ScriptAnimationPolicy.ResetSoundScript);
                return false;
            case DelayedStand stand:
                if (stand.Remaining < 0) { host.Stand(); return true; }
                host.EnterDelayedStandState();
                return false;
            case HoldActionValue value:
                if (value.Remaining < 0) { host.ScriptedActionValue = 0; return true; }
                host.ScriptedActionValue = value.Value;
                return false;
            case Wait wait:
                if (wait.Remaining >= 0) return false;
                Decoding = true;
                return true;
            case AnimationSignal:
                if (host.AnimationSignal) { host.AnimationSignal = false; Decoding = true; return true; }
                if (host.AnimationFinished) host.PlayAnimation(host.AnimationId, ScriptAnimationPolicy.PreserveSoundScript);
                return false;
            default: throw new InvalidOperationException("Unknown concurrent player command.");
        }
    }

    private void FinishIfIdle(PlayerScriptEnvironment environment)
    {
        if (Decoding || commands.Count != 0) return;
        FixedVector3? boss = environment.BossPosition;
        bool insideBossArea = environment.LevelId == 1795 && boss is { } position &&
            GameWord.Absolute(unchecked(31900664 + position.X)) < 4915200 &&
            GameWord.Absolute(unchecked(position.Z - 34009330)) < 4915200;
        if (!insideBossArea) Active = false;
        if (environment.ForceMenuExit) host.RequestMenuExit();
        if (environment.Abort && CheckpointNode != 0) host.Position = host.TriggerPosition(CheckpointNode);
    }

    private abstract record Command;
    private sealed record Walk(FixedVector3 Target) : Command;
    // The recovered move-to duration is stored but never decremented.
    private sealed record Move(FixedVector3 Target, int Duration) : Command;
    private abstract record Timed(int InitialRemaining) : Command { public int Remaining { get; set; } = InitialRemaining; }
    private sealed record HoldInput(int Index, int Duration) : Timed(Duration);
    private sealed record HoldAnimation(int AnimationId, int Duration) : Timed(Duration);
    private sealed record DelayedStand(int Duration) : Timed(Duration);
    private sealed record HoldActionValue(int Value, int Duration) : Timed(Duration);
    private sealed record Wait(int Duration) : Timed(Duration);
    private sealed record AnimationSignal : Command;
}
