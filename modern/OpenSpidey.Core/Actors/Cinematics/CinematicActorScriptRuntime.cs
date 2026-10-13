using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cinematics;

public enum CinematicActorSignalSource { BodyFlags, InputFlags }

public readonly record struct CinematicActorScriptProfile(int WalkSpeed, int WalkStartAnimation,
    int WalkLoopAnimation, int WalkEndAnimation, bool ScaleMoveVelocity,
    CinematicActorSignalSource SignalSource, bool HasCheckpoint)
{
    public static CinematicActorScriptProfile BlackCat => new(3, 2, 3, 4, true, CinematicActorSignalSource.InputFlags, true);
    public static CinematicActorScriptProfile SpiderClone => new(8, 1, 21, 13, false, CinematicActorSignalSource.InputFlags, false);
}

public interface ICinematicActorScriptHost
{
    ActorBodyMotion Body { get; }
    int SurfaceClearance { get; }
    int AnimationId { get; }
    bool AnimationFinished { get; }
    ushort ItemFlags { get; set; }
    FixedVector3 TriggerPosition(int node);
    IReadOnlyList<ushort> TriggerLinks(int node);
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    void RunAnimation(int animation);
    void CycleAnimation(int animation);
    void PlayVoice(short bank, short track, short priority);
    bool ConsumeAnimationSignal(CinematicActorSignalSource source);
}

/// <summary>Shared source actor cinematic streams and ordered concurrent command blocks, separate from player input and CBaddy4100 scripts.</summary>
public sealed class CinematicActorScriptRuntime
{
    private readonly ActorScriptCursor cursor;
    private readonly ICinematicActorScriptHost host;
    private readonly CinematicActorScriptProfile profile;
    private readonly LinkedList<Command> commands = new();
    public bool Running { get; private set; }
    public bool Decoding { get; private set; }
    public int ElapsedTicks { get; private set; }
    public int LookTargetNode { get; private set; }
    public int CheckpointNode { get; private set; }
    public int Cursor => cursor.Position;
    public int CommandCount => commands.Count;
    public IReadOnlyList<int> CommandTypes => commands.Select(command => command.Type).ToArray();

    public CinematicActorScriptRuntime(ActorScriptCursor cursor, ICinematicActorScriptHost host, CinematicActorScriptProfile profile)
    {
        this.cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.profile = profile;
    }

    public void Begin() { Running = true; Decoding = true; }
    public void ClearCommands() => commands.Clear();

    public void Update(int ticks, bool abort, int instructionBudget = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        ElapsedTicks = unchecked(ElapsedTicks + ticks);
        if (profile.HasCheckpoint && abort)
        {
            Decoding = false; commands.Clear(); host.Body.Velocity = default;
            if (CheckpointNode != 0)
            {
                host.Body.Position = host.TriggerPosition(CheckpointNode);
                IReadOnlyList<ushort> links = host.TriggerLinks(CheckpointNode);
                if (links.Count != 0)
                {
                    AimAngles aim = FixedAim.Calculate(host.Body.Position, host.TriggerPosition(links[0]) with { Y = host.Body.Position.Y });
                    host.Body.Angles = new(aim.Pitch, aim.Yaw, 0);
                }
            }
        }
        int decoded = 0;
        while (Decoding)
        {
            short due = unchecked((short)cursor.PeekWord());
            // MOVSX followed by JA in the PC interpreter compares unsigned words.
            if (due != -1 && unchecked((uint)due) > unchecked((uint)ElapsedTicks)) break;
            if (decoded++ == instructionBudget) throw new InvalidDataException("Cinematic actor bytecode exceeded its bounded instruction budget.");
            ElapsedTicks = 0; cursor.ReadWord();
            short opcode = ReadSigned();
            switch (opcode)
            {
                case 1:
                    FixedVector3 target = host.TriggerPosition(ReadSigned());
                    int ground = host.GroundHeight(target, 0, 0x800, 0);
                    host.Body.Position = target with { Y = unchecked(ground - (host.SurfaceClearance << 12)) };
                    break;
                case 2:
                    RemoveType(6);
                    FixedVector3 walk = host.TriggerPosition(ReadSigned());
                    commands.AddLast(new Walk(walk)); Decoding = false;
                    host.RunAnimation(profile.WalkStartAnimation);
                    break;
                case 4: host.PlayVoice(ReadSigned(), ReadSigned(), ReadSigned()); break;
                case 5:
                    FixedVector3 move = host.TriggerPosition(ReadSigned());
                    commands.AddLast(new Move(move, ReadSigned())); Decoding = false;
                    break;
                case 6: commands.AddLast(new HoldAnimation(ReadSigned(), unchecked(ReadSigned() + ticks))); break;
                case 8: commands.AddLast(new Look(ReadSigned(), unchecked(ReadSigned() + ticks))); break;
                case 15:
                    short animation = ReadSigned(); commands.AddLast(new AnimationSignal()); Decoding = false;
                    if (host.AnimationId != animation) host.RunAnimation(animation);
                    break;
                case 16:
                    if (profile.HasCheckpoint) CheckpointNode = ReadSigned();
                    break;
                case 255: Decoding = false; break;
                default: break;
            }
        }
        LinkedListNode<Command>? node = commands.First;
        while (node is not null)
        {
            LinkedListNode<Command>? next = node.Next;
            if (UpdateCommand(node.Value, ticks)) commands.Remove(node);
            node = next;
        }
        if (commands.Count == 0 && !Decoding)
        {
            Running = false;
            if (profile.HasCheckpoint && abort && CheckpointNode != 0) host.Body.Position = host.TriggerPosition(CheckpointNode);
        }
    }

    private bool UpdateCommand(Command command, int ticks)
    {
        switch (command)
        {
            case Walk walk:
                FixedVector3 delta = (walk.Target - host.Body.Position) >> 12;
                if (FixedVectorMath.OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Z * delta.Z)) >= 64)
                {
                    AimAngles aim = FixedAim.Calculate(host.Body.Position with { Y = 0 }, walk.Target with { Y = 0 });
                    host.Body.Velocity = FixedAim.Velocity(profile.WalkSpeed, aim);
                    CinematicActorMotion.TurnTowards(host.Body, aim, 8);
                    if (host.AnimationId == profile.WalkStartAnimation && host.AnimationFinished) host.CycleAnimation(profile.WalkLoopAnimation);
                    return false;
                }
                host.Body.Velocity = default; host.Body.AngularVelocity = default; host.Body.AngularAcceleration = default;
                Decoding = true; host.RunAnimation(profile.WalkEndAnimation); return true;
            case Move move:
                if (FixedAim.Distance(move.Target, host.Body.Position) < 64) { Decoding = true; return true; }
                host.Body.Velocity = FixedVectorMath.Normalize((move.Target - host.Body.Position) >> 12);
                if (profile.ScaleMoveVelocity) host.Body.Velocity *= move.Speed;
                return false;
            case HoldAnimation animation:
                animation.Remaining = unchecked(animation.Remaining - ticks);
                if (animation.Remaining < 0) return true;
                if (host.AnimationFinished || host.AnimationId != animation.Animation) host.RunAnimation(animation.Animation);
                return false;
            case Look look:
                look.Remaining = unchecked(look.Remaining - ticks);
                if (look.Remaining < 0) { LookTargetNode = 0; return true; }
                host.ItemFlags |= 4; LookTargetNode = look.Node; return false;
            case AnimationSignal:
                if (host.ConsumeAnimationSignal(profile.SignalSource)) { Decoding = true; return true; }
                if (host.AnimationFinished) host.RunAnimation(host.AnimationId);
                return false;
            default: throw new InvalidOperationException("Unknown cinematic actor command block.");
        }
    }

    private short ReadSigned() => unchecked((short)cursor.ReadWord());
    private void RemoveType(int type)
    {
        LinkedListNode<Command>? node = commands.First;
        while (node is not null)
        {
            LinkedListNode<Command>? next = node.Next;
            if (node.Value.Type == type) commands.Remove(node);
            node = next;
        }
    }
    private abstract record Command(int Type);
    private sealed record Walk(FixedVector3 Target) : Command(2);
    private sealed record Move(FixedVector3 Target, int Speed) : Command(5);
    private sealed record HoldAnimation(int Animation, int Duration) : Command(6) { public int Remaining { get; set; } = Duration; }
    private sealed record Look(int Node, int Duration) : Command(8) { public int Remaining { get; set; } = Duration; }
    private sealed record AnimationSignal() : Command(15);
}
