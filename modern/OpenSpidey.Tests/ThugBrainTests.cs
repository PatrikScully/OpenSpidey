using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

// DispatchProxy keeps this deliberately narrow fixture independent of the growing native host API.
// Any callback outside the source-selected branches throws rather than supplying a dummy result.
public class ThugBrainFixtureHost : DispatchProxy
{
    public ThugState State = null!;
    public ActorProcedureState Actor = null!;
    public ActorProcedureList Procedures = null!;
    public long[] Input = null!;
    public List<long> Events { get; } = [];
    public int AnimationId;
    public int AnimationFrame;
    public byte AnimationFinished;
    public ThugPlayerState Player => new(new(9000000, -17000, -7000000), 0, (uint)Input[25], 1, false);

    public void Stage(int id)
    {
        FixedVector3 position = Actor.Body.Position, velocity = Actor.Body.Velocity, shadow = State.ShadowPosition;
        Events.AddRange([id, State.State, State.Substate, State.ActionFlags, Actor.BehaviorFlags,
            position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z, State.ShadowScale,
            shadow.X, shadow.Y, shadow.Z]);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        object?[] arguments = args ?? [];
        switch (targetMethod!.Name)
        {
            case "get_Player": return Player;
            case "get_AttackClock": return (int)Input[10];
            case "get_ItemFlags": return (ushort)Input[7];
            case "get_HasMessages": return Input[13] != 0;
            case "get_AnimationId": return AnimationId;
            case "get_AnimationFrame": return AnimationFrame;
            case "get_AnimationFinished": return AnimationFinished;
            case "set_AnimationFinished": AnimationFinished = (byte)arguments[0]!; return null;
            case "BuildTransform": Stage(1); return null;
            case "PlayAnimationSounds":
                Stage(2);
                if (Input[20] >= 0) State.ActionFlags = (uint)Input[20];
                return null;
            case "ProcessMessages":
                Stage(3);
                State.State = (int)Input[14]; State.Substate = (int)Input[15]; State.ActionFlags = 7;
                Actor.Body.Position += new FixedVector3(17, 0, 0);
                return null;
            case "WarnOtherThugs": Stage(5); return null;
            case "ShootHostage": Stage(6); return null;
            case "RunAnimation":
                AnimationId = (ushort)arguments[0]!; AnimationFrame = (int)arguments[1]!; AnimationFinished = 0;
                Events.AddRange([9, AnimationId, AnimationFrame, (int)arguments[2]!]);
                return null;
            case "CycleAnimation":
                AnimationId = (ushort)arguments[0]!; AnimationFinished = 0;
                Events.AddRange([10, AnimationId, (sbyte)arguments[1]!]);
                return null;
            case "PlaySound": Events.AddRange([11, (ushort)arguments[0]!]); return null;
            case "LineOfSight":
                AddQuery(13, arguments, (uint)arguments[2]!);
                return Input[22] != 0;
            case "PathCheck":
                AddQuery(12, arguments, (int)arguments[2]!);
                return (int)Input[23];
            case "Neutralize": Events.Add(14); Actor.Neutralize(Procedures); return null;
            default: throw new NotSupportedException($"Brain fixture excludes host callback {targetMethod.Name}.");
        }
    }

    private void AddQuery(int id, object?[] arguments, long parameter)
    {
        FixedVector3 start = (FixedVector3)arguments[0]!, end = (FixedVector3)arguments[1]!;
        Events.AddRange([id, start.X, start.Y, start.Z, end.X, end.Y, end.Z, parameter]);
    }
}

internal static class ThugBrainTests
{
    private sealed class Procedure(ThugBrainFixtureHost host) : IActorProcedure
    {
        public int SourceType => 1794;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks)
        {
            host.Stage(4);
            host.State.State = (int)host.Input[17]; host.State.Substate = (int)host.Input[18];
            host.State.ActionFlags = 4;
            host.Actor.Body.Position += new FixedVector3(0, 0, -23);
            if (host.Input[19] != 0) Flags |= 1;
            ExecuteNext?.Invoke();
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugBrainTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-brain.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(9, (int)input[2])
            {
                State = (int)input[0], AlertTicks = 1000, TurnRate = 99, BodyFlags = 0x206,
                ActionCounter = 8, ActionFlags = (uint)input[5], GrenadeElapsedTicks = (int)input[4],
                ShadowScale = (int)input[6], ShadowPosition = new(7, 11, 13), Health = 90,
                ActorPhase = (byte)input[11], FightPollInterval = (int)input[12]
            };
            state.SetThugType((int)input[1]);
            var body = new ActorBodyMotion
            {
                Position = new(100001, -200002, 300003), Velocity = new(9000, -11000, 13000),
                Acceleration = new(301, -503, 701), LocalVelocity = new(10000, -20000, 30000),
                Angles = new(100, 200, 300), AngularVelocity = new(3, -5, 7), AngularAcceleration = new(1, 2, 3),
                LinearDamping = new(4, 4, 4), AngularDamping = new(4, 4, 4),
                CollisionFlags = 7, MovementFlags = (uint)input[9]
            };
            var actor = new ActorProcedureState(body) { BehaviorFlags = (uint)input[8] };
            var procedures = new ActorProcedureList();
            IThugBrainHost host = DispatchProxy.Create<IThugBrainHost, ThugBrainFixtureHost>();
            var callbacks = (ThugBrainFixtureHost)host;
            callbacks.State = state; callbacks.Actor = actor; callbacks.Procedures = procedures; callbacks.Input = input;
            callbacks.AnimationFinished = (byte)input[21];
            var procedure = new Procedure(callbacks);
            if (input[16] != 0) procedures.Attach(procedure);
            var route = new ActorWaypointRoute(null!, 0, _ => throw new NotSupportedException("Brain fixture excludes waypoint script dispatch."));
            int Random(int range) { callbacks.Events.AddRange([8, range]); return (int)input[24] % range; }
            new ThugBrain(state, actor, procedures, route, new(), host, Random).Advance((int)input[3]);
            long[] actual = [state.State, state.Substate, state.GrenadeElapsedTicks, state.ActionFlags, actor.BehaviorFlags,
                state.ShadowScale, state.ShadowPosition.X, state.ShadowPosition.Y, state.ShadowPosition.Z,
                state.AlertTicks, state.TurnRate, state.BodyFlags, state.DistanceSampleClock, state.CachedPlayerDistance,
                state.ActionCounter, callbacks.AnimationId, callbacks.AnimationFrame, callbacks.AnimationFinished,
                body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
                body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, body.MovementFlags, body.CollisionFlags,
                procedures.Count, procedures.Count == 0 ? -1 : procedure.Flags,
                state.LastSeenPlayerPosition.X, state.LastSeenPlayerPosition.Y, state.LastSeenPlayerPosition.Z];
            JsonElement expected = test.GetProperty("expected");
            long[] reference = expected.EnumerateArray().Take(40).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(reference), $"Brain case {caseIndex} matches original stage state, shadow, poll cache, motion and selected leaf; " +
                string.Join("; ", actual.Select((value, i) => value != reference[i] ? $"field {i}: {value} != {reference[i]}" : null).Where(value => value != null)));
            assert(callbacks.Events.SequenceEqual(expected[40].EnumerateArray().Select(value => value.GetInt64())),
                $"Brain case {caseIndex} preserves physics-before-transform, sounds-before-shadow/messages, cleared actions before procedures, and current-state dispatch.");
            caseIndex++;
        }
    }
}
