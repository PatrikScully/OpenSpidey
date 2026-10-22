using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

public class ThugHostageFixtureHost : DispatchProxy
{
    public ActorProcedureState Actor = null!;
    public ActorProcedureState Hostage = null!;
    public ActorProcedureState? CurrentHostage;
    public ActorProcedureList Procedures = null!;
    public long[] Input = null!;
    public List<int> Events { get; } = [];
    public int AnimationId;
    public int AnimationFrame;
    public byte AnimationFinished;
    public bool LaserPresent;
    private int recoveryIndex;
    private int randomIndex;
    public int Random(int range) { Events.AddRange([8, range]); return (int)Input[21 + Math.Min(randomIndex++, 3)] % range; }
    private static readonly FixedVector3[] Hooks = [new(81920, 16384, 40960), new(40960, 8192, 20480),
        new(-81920, 16384, -40960), new(-40960, 8192, -20480)];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        object?[] arguments = args ?? [];
        switch (targetMethod!.Name)
        {
            case "get_AnimationId": return AnimationId;
            case "get_AnimationFrame": return AnimationFrame;
            case "get_AnimationFinished": return AnimationFinished;
            case "set_AnimationFinished": AnimationFinished = (byte)arguments[0]!; return null;
            case "get_ItemFlags": return (ushort)Input[10];
            case "get_DifficultyLevel": return (int)Input[14];
            case "get_Player": return new ThugPlayerState(new(9000000, 7000000, -11000000), 100, 0, 0, false);
            case "RecoverHostage":
                Events.AddRange([30, recoveryIndex]);
                return ((int)Input[17] & (1 << recoveryIndex++)) != 0 ? CurrentHostage : null;
            case "RecoverHostageBody":
                if (!ReferenceEquals(arguments[0], Hostage)) throw new InvalidOperationException("LookAt changed the captured hostage identity.");
                return Hostage.Body;
            case "Neutralize": Events.Add(1); Actor.Neutralize(Procedures); return null;
            case "RunAnimation":
                AnimationId = (ushort)arguments[0]!; AnimationFrame = (int)arguments[1]!; AnimationFinished = (byte)Input[9];
                Events.AddRange([2, AnimationId, AnimationFrame, (int)arguments[2]!]); return null;
            case "CycleAnimation":
                AnimationId = (ushort)arguments[0]!; AnimationFinished = 0;
                Events.AddRange([3, AnimationId, (sbyte)arguments[1]!]); return null;
            case "LineOfSight": Events.Add(27); return Input[15] != 0;
            case "PlaySound": Events.AddRange([9, (ushort)arguments[0]!, 0]); return null;
            case "PlayRangedSound": Events.AddRange([9, (ushort)arguments[0]!, (int)arguments[1]!]); return null;
            case "SendHostageMessage":
                if (!ReferenceEquals(arguments[0], Hostage)) throw new InvalidOperationException("Message lost the hostage identity.");
                Events.AddRange([23, (int)arguments[1]!]); return null;
            case "SendSelfMessage": Events.AddRange([23, (int)arguments[0]!]); return null;
            case "SelectHostageCamera":
                FixedVector3 actor = (FixedVector3)arguments[0]!, hostage = (FixedVector3)arguments[1]!;
                Events.AddRange([31, 2, actor.X, actor.Y, actor.Z, hostage.X, hostage.Y, hostage.Z]); return null;
            case "ClearLaser": Events.Add(15); LaserPresent = false; return null;
            case "GetHookPosition": int index = (int)arguments[0]!; Events.AddRange([14, index]); return Hooks[index];
            case "LaserCollision":
                var line = (ThugShotLine)arguments[0]!;
                FixedVector3 direction = (FixedVector3)arguments[1]!;
                Events.AddRange([17, (bool)arguments[2]! ? 1 : 0, line.Start.X, line.Start.Y, line.Start.Z,
                    line.End.X, line.End.Y, line.End.Z, direction.X, direction.Y, direction.Z]);
                line.End += new FixedVector3(11, -23, 17);
                return (int)Input[16];
            case "DrawHostageTracer":
                if (!ReferenceEquals(arguments[1], Hostage)) throw new InvalidOperationException("Tracer lost the hostage identity.");
                AddLine(18, (ThugShotLine)arguments[0]!, [1, (byte)arguments[2]!, (byte)arguments[3]!, (byte)arguments[4]!]); return null;
            case "DrawTracer":
                AddLine(18, (ThugShotLine)arguments[0]!, [(bool)arguments[1]! ? 1 : 0, (byte)arguments[2]!, (byte)arguments[3]!, (byte)arguments[4]!]); return null;
            case "DrawBarrelFlash":
                AddLine(19, (ThugShotLine)arguments[0]!, [(byte)arguments[1]!, (byte)arguments[2]!, (byte)arguments[3]!]); return null;
            case "DrawGlowFlash":
                FixedVector3 start = (FixedVector3)arguments[0]!;
                Events.AddRange([20, start.X, start.Y, start.Z]); Events.AddRange((IReadOnlyList<int>)arguments[1]!); return null;
            case "SetLaser":
                AddLine(21, new() { Start = (FixedVector3)arguments[0]!, End = (FixedVector3)arguments[1]! }, []);
                LaserPresent = true; return null;
            default: throw new NotSupportedException($"Hostage fixture excludes callback {targetMethod.Name}.");
        }
    }

    private void AddLine(int id, ThugShotLine line, int[] suffix)
    {
        Events.AddRange([id, line.Start.X, line.Start.Y, line.Start.Z, line.End.X, line.End.Y, line.End.Z]);
        Events.AddRange(suffix);
    }
}

internal static class ThugHostageTests
{
    private sealed class Procedure : IActorProcedure
    {
        public int SourceType => 1794;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => ExecuteNext?.Invoke();
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugHostageTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-hostage.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(9, 0x1101)
            {
                State = 8, Substate = (int)input[0], PauseUpdates = (int)input[2], CombatFlags = (uint)input[5],
                TurnRate = 99, GunSoundCooldownTicks = (int)input[11], GunSoundDelayTicks = (int)input[12], AimPlayerY = (int)input[13]
            };
            state.SetThugType((int)input[1]);
            var body = new ActorBodyMotion
            {
                Position = new(100001, -200002, 300003), Velocity = new(11, 17, 13), Acceleration = new(-7, -23, 5),
                LocalVelocity = new(11, 17, 13), AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13)
            };
            var actor = new ActorProcedureState(body) { BehaviorFlags = (uint)input[4], CompletionFlags = (uint)input[3] };
            var hostage = new ActorProcedureState(new() { Position = new(4000000, -5000000, 6000000) }) { BehaviorFlags = (uint)input[18] };
            var procedures = new ActorProcedureList();
            var previous = new Procedure { Flags = (int)input[19] }; procedures.Attach(previous);
            IThugHostageHost host = DispatchProxy.Create<IThugHostageHost, ThugHostageFixtureHost>();
            var callbacks = (ThugHostageFixtureHost)host;
            callbacks.Input = input; callbacks.Actor = actor; callbacks.Hostage = hostage; callbacks.CurrentHostage = hostage; callbacks.Procedures = procedures;
            callbacks.AnimationId = (int)input[6]; callbacks.AnimationFrame = (int)input[7]; callbacks.AnimationFinished = (byte)input[8];
            callbacks.LaserPresent = input[20] != 0;
            ThugHostageBehavior.Step(state, actor, procedures, host, callbacks.Random);
            long[] actual = [state.State, state.Substate, state.PauseUpdates, actor.CompletionFlags, actor.BehaviorFlags, state.CombatFlags,
                callbacks.AnimationId, callbacks.AnimationFrame, callbacks.AnimationFinished, state.TurnRate, state.GunSoundDelayTicks,
                state.AimPlayerY, callbacks.LaserPresent ? 1 : 0, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z,
                body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, body.AngularAcceleration.X,
                body.AngularAcceleration.Y, body.AngularAcceleration.Z, previous.Flags];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(29).Select(value => value.GetInt64())),
                $"Hostage case {caseIndex} matches original timers, handle escape, completion, motion, shot scheduling and animation states.");
            assert(callbacks.Events.SequenceEqual(expected[30].EnumerateArray().Select(value => value.GetInt32())),
                $"Hostage case {caseIndex} preserves source recovery/message/camera/random/shot/flash order and explicit target identity.");
            assert(procedures.Count == expected[29].GetArrayLength(),
                "Hostage startup schedules the real body-facing and persistent source state-exit procedures.");
            if (procedures.Count == 3)
            {
                // Execute the source-created procedures after moving the stored handle to an unrelated target.
                // The separate body resolver must keep the original captured target, not retarget the procedure.
                callbacks.CurrentHostage = new(new() { Position = body.Position });
                actor.BehaviorFlags &= ~0x40000004u;
                state.State = 10;
                callbacks.Events.Clear();
                int targetYaw = FixedAim.Calculate(body.Position, hostage.Body.Position).Yaw;
                int Residual(int yaw) { int delta = (targetYaw - yaw) & 0xFFF; return Math.Abs(delta > 2048 ? delta - 4096 : delta); }
                int residualBefore = Residual(body.Angles.Y);
                procedures.Execute(2);
                assert(Residual(body.Angles.Y) < residualBefore &&
                    callbacks.Events.SequenceEqual(new[] { 23, 17 }),
                    "Hostage LookAt retains its original target after handle replacement, and the real state monitor sends message17 before retirement.");
            }
            caseIndex++;
        }
    }
}
