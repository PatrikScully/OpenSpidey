using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cops;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class CopTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CopTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.cop.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, frames = 0, stopped = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            JsonElement expected = test.GetProperty("expected");
            if (mode == "C") Construct(assert, index, Words(test.GetProperty("input")), expected);
            else if (mode == "S") Parameter(assert, index, Words(test.GetProperty("input")), expected);
            else if (mode == "M") AnimationMode(assert, index, Words(test.GetProperty("input")), expected);
            else frames += Ai(assert, index, test, expected, ref stopped);
            index++;
        }
        assert(index == 793 && frames > 2000 && stopped > 50,
            $"Actual PC CCop fixture keeps constructor, parameter, mode and AI cases ({index} cases, {frames} frames, {stopped} stops).");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, JsonElement expected)
    {
        CopState state = new(input[1], 0x301, input[2], 0);
        CopConstruction.Apply(state, () => { });
        long[] events = Longs(expected[4]);
        long[] actual = [0, state.ShadowScale, state.PauseUpdates, state.SourceNode, (ushort)state.SourceNode, state.Field216,
            state.PushValue, state.State, state.MinorRadius, CopState.InitialBehaviorFlags, state.Field370, state.AttackRelatedSnapshot,
            state.SightFar, state.SightVertical, state.SightCone, state.SightNear, state.Field374, state.Damage,
            state.FightClaimDistance, state.Field212, 0];
        bool threw = false;
        try { state.SetCopType(input[0]); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        long[] typed = Longs(expected[expected.GetArrayLength() - 1]);
        bool unknown = expected.GetArrayLength() == 7;
        bool ok = actual.SequenceEqual(events) && threw == unknown && typed[0] == state.Type;
        if (!threw)
            ok &= typed.SequenceEqual(new long[] { state.Type, state.SurfaceClearance, state.AnimationWord294, state.AnimationWord298,
                state.MinorRadius, state.Field370 }) && Longs(expected[5])[1] == (state.IsPolice ? 1 : 2) &&
                Longs(expected[6])[1] == (state.IsPolice ? 0x549328 : 0x549408);
        assert(ok, $"Actual PC CCop constructor case {index}: [{string.Join(',', actual)}] expected [{string.Join(',', events)}].");
    }

    private static void Parameter(Action<bool, string> assert, int index, int[] input, JsonElement expected)
    {
        CopState state = new(0, 0, 0, 0) { CombatFlags = (uint)input[2] };
        state.SetParamByIndexForTest(0x364, 0x360, 0x368, 0x36C, 0x370, 0x374, 7, 0x37C);
        bool threw = false;
        try { state.SetParamByIndex(input[0], input[1]); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        long[] reference = Longs(expected[expected.GetArrayLength() - 1]);
        long[] actual = [state.SightFar, state.SightVertical, state.SightCone, state.SightNear, state.Field370, state.Field374,
            0x378, state.FightClaimDistance, state.Damage, state.CombatFlags];
        assert(actual.SequenceEqual(reference) && threw == (expected.GetArrayLength() == 2),
            $"Actual PC CCop::SetParamByIndex case {index}: [{string.Join(',', actual)}] expected [{string.Join(',', reference)}].");
    }

    private static void AnimationMode(Action<bool, string> assert, int index, int[] input, JsonElement expected)
    {
        CopState state = new(0, 0, 0, 0);
        if (input[0] != 0) state.SetCopType(input[0]);
        state.AnimationWord294 = (uint)input[1];
        state.AnimationWord298 = (uint)input[2];
        List<long> events = [];
        bool threw = false, result = false;
        try { result = state.SetAnimMode(input[3], input[4] != 0, clip => events.AddRange([9, clip, 0, -1])); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        long[] reference = Longs(expected[expected.GetArrayLength() - 1]);
        bool ok = threw == (expected.GetArrayLength() > 1 && Longs(expected[0])[0] == 99);
        if (!threw)
        {
            List<long> flat = [];
            for (int i = 0; i < expected.GetArrayLength() - 1; i++) flat.AddRange(Longs(expected[i]));
            ok &= reference.SequenceEqual(new long[] { result ? 1 : 0, state.AnimationWord294, state.AnimationWord298 }) &&
                  flat.SequenceEqual(events);
        }
        assert(ok, $"Actual PC CCop::SetAnimMode case {index}.");
    }

    private static int Ai(Action<bool, string> assert, int index, JsonElement test, JsonElement expected, ref int stopped)
    {
        JsonElement h = test.GetProperty("header");
        int I(string name) => h.GetProperty(name).GetInt32();
        CopState state = new(7, 0x301, I("snapshot"), 0) { MinorRadius = 0 };
        state.SetCopType(I("type"));
        state.State = I("state"); state.Substate = I("substate"); state.CombatFlags = (uint)I("combat");
        state.BodyFlags = (ushort)I("bodyFlags"); state.AlertTicks = I("alert"); state.Timer328Ticks = I("timer328");
        state.ClosePlayerTicks = I("closeTicks"); state.FightSuppressionTicks = I("suppression"); state.HitKind = I("hitKind");
        state.FightPollInterval = I("poll"); state.ActionCounter = I("counter"); state.PauseUpdates = I("pause");
        state.PathPointIndex = I("pathIndex"); state.ActorPhase = (byte)I("phase"); state.ShadowScale = I("shadow");
        state.Health = (short)I("health"); state.AttackOwnershipFlags = (byte)I("ownership"); state.AttackSlotMask = (byte)I("slotMask");
        state.LastSeenPlayerPosition = new(I("seenX"), I("seenY"), I("seenZ")); state.ActionFlags = (uint)I("actionFlags");
        state.SetParamByIndexForTest(I("vertical"), I("far"), I("cone"), I("near"), state.Field370, state.Field374, state.Damage, I("claim"));
        state.DistanceSampleClock = I("distanceClock"); state.CachedPlayerDistance = I("distance");
        state.SurfaceClearance = (short)I("clearance");
        state.SetAnimMode(I("mode"), false, _ => { });
        if (I("mode") == 0) (state.AnimationWord294, state.AnimationWord298) = (0x02010E0D, 3);
        ActorBodyMotion body = new()
        {
            Position = new(I("x"), I("y"), I("z")), Velocity = new(I("vx"), I("vy"), I("vz")), Acceleration = new(301, -503, 701),
            Angles = new(0, (short)I("yaw"), 0), AngularVelocity = new(0, -5, 0), AngularAcceleration = new(0, 2, 0),
            LinearDamping = new(4, 4, 4), AngularDamping = new(4, 4, 4)
        };
        ActorProcedureState actor = new(body) { BehaviorFlags = (uint)I("behavior"), CompletionFlags = (uint)I("completion") };
        ActorProcedureList procedures = new();
        ThugCombatCoordinator shared = new() { OccupiedSlots = (uint)I("sharedSlots") };
        CopCombatCoordinator combat = new(shared);
        CopState other = new(0, 0, 0, 0);
        combat.PrimaryAttacker = I("copGlobal") switch { 1 => state, 2 => other, _ => null };
        combat.FightOwner = I("copList") switch { 1 => state, 2 => other, _ => null };
        Host host = new(state, actor, procedures)
        {
            AnimationId = I("anim"), AnimationFrame = I("frame"), AnimationFinished = (byte)I("finished"), ItemFlags = (ushort)I("itemFlags"),
            HasMessages = I("messages") != 0, RouteFlags = (uint)I("route"), Seed = (uint)I("seed"),
            PlayerSurfaceClearance = (short)I("playerClearance"),
            Player = new(new(I("playerX"), I("playerY"), I("playerZ")), (short)I("playerHealth"), (uint)I("playerFlags"),
                I("unavailable"), I("held") != 0)
        };
        host.PlayerBody = new ActorBodyMotion { Position = host.Player.Position };
        CopBrain brain = new(state, actor, procedures, combat, host, host.Random);
        JsonElement[] frameInputs = test.GetProperty("frames").EnumerateArray().ToArray();
        int frame = 0;
        foreach (JsonElement result in expected.EnumerateArray())
        {
            JsonElement f = frameInputs[frame];
            int F(string name) => f.GetProperty(name).GetInt32();
            host.Events.Clear();
            host.AttackClock = F("clock");
            if (F("finished") >= 0) host.AnimationFinished = (byte)F("finished");
            (host.Los, host.Path, host.Add, host.Next, host.RouteAfter, host.Ground, host.Recover) =
                (F("los"), F("path"), F("add"), F("next") != 0, (uint)F("routeAfter"), F("ground"), F("recover") != 0);
            (host.LosCalls, host.PathCalls, host.AddCalls) = (0, 0, 0);
            List<long[]> reference = result.GetProperty("e").EnumerateArray().Select(Longs).ToList();
            long[]? stop = reference.LastOrDefault(e => e[0] is 98 or 99);
            Exception? thrown = null;
            try { brain.Advance(F("ticks")); }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException) { thrown = exception; }
            string where = $"Actual PC CCop::AI case {index} frame {frame}";
            if (stop is not null)
            {
                stopped++;
                bool matches = thrown is not null && (stop[0] == 99 ? thrown is InvalidOperationException
                    : thrown is NotSupportedException && thrown.Message.Contains($"0x{stop[1]:X6}"));
                long[] snapshot = Snapshot(state, actor, procedures, combat, host, shared);
                long[] expectedSnapshot = stop[2..];
                List<long> events = host.Flat();
                List<long> expectedEvents = reference.TakeWhile(e => e[0] is not (98 or 99)).SelectMany(e => e).ToList();
                assert(matches && snapshot.SequenceEqual(expectedSnapshot) && events.SequenceEqual(expectedEvents),
                    $"{where} stop: thrown={thrown?.Message}; actual=[{string.Join(',', snapshot)}] expected=[{string.Join(',', expectedSnapshot)}]; events=[{string.Join(',', events)}] expected=[{string.Join(',', expectedEvents)}].");
                return frame + 1;
            }
            long[] actual = Snapshot(state, actor, procedures, combat, host, shared);
            long[] wanted = Longs(result.GetProperty("s"));
            List<long> actualEvents = host.Flat();
            List<long> wantedEvents = reference.SelectMany(e => e).ToList();
            assert(thrown is null && actual.SequenceEqual(wanted) && actualEvents.SequenceEqual(wantedEvents),
                $"{where}: thrown={thrown?.Message}; actual=[{string.Join(',', actual)}] expected=[{string.Join(',', wanted)}]; events=[{string.Join(',', actualEvents)}] expected=[{string.Join(',', wantedEvents)}].");
            frame++;
        }
        return frame;
    }

    private static long[] Snapshot(CopState s, ActorProcedureState actor, ActorProcedureList procedures, CopCombatCoordinator combat,
        Host host, ThugCombatCoordinator shared)
    {
        ActorBodyMotion b = actor.Body;
        List<long> v = [s.State, s.Substate, actor.BehaviorFlags, actor.CompletionFlags, s.CombatFlags, (ushort)s.BodyFlags, s.AlertTicks,
            s.Timer328Ticks, s.ClosePlayerTicks, s.FightSuppressionTicks, s.TurnRate, s.HitKind, s.FightPollInterval,
            s.DistanceSampleClock, s.CachedPlayerDistance, s.ActionCounter, s.PauseUpdates, s.PathPointIndex, s.ActorPhase,
            s.GroundReferenceY, s.GroundHeight, host.RouteFlags, s.AnimationWord294, s.AnimationWord298, (ushort)s.ShadowScale];
        foreach (FixedVector3 vector in new[] { s.ShadowPosition, b.Position, b.Velocity, b.Acceleration, b.LocalVelocity })
            v.AddRange([vector.X, vector.Y, vector.Z]);
        foreach (ActorAngles angles in new[] { b.Angles, b.AngularVelocity, b.AngularAcceleration }) v.AddRange([angles.X, angles.Y, angles.Z]);
        v.AddRange([(ushort)host.AnimationId, (short)host.AnimationFrame, host.AnimationFinished, b.MovementFlags,
            s.LastSeenPlayerPosition.X, s.LastSeenPlayerPosition.Y, s.LastSeenPlayerPosition.Z, (ushort)s.MinorRadius, s.ActionFlags,
            s.AttackOwnershipFlags, s.AttackSlotMask, Role(combat.PrimaryAttacker), Role(combat.FightOwner), (byte)shared.OccupiedSlots,
            host.Node, procedures.Count]);
        foreach (IActorProcedure procedure in procedures.SourceOrder)
            v.AddRange([procedure.SourceType, procedure.Flags, procedure switch
            {
                ActorMoveToProcedure move => move.CompletionMask,
                ActorLookAtProcedure look => look.CompletionMask,
                _ => -1
            }]);
        return v.ToArray();

        int Role(CopState? owner) => owner is null ? 0 : ReferenceEquals(owner, s) ? 1 : 2;
    }

    private sealed class Host(CopState state, ActorProcedureState actor, ActorProcedureList procedures) : ICopHost
    {
        private readonly List<long[]> events = [];
        private readonly FixedVector3[] path = Enumerable.Range(0, 6).Select(i => new FixedVector3(100000 + i * 12288, -200000, 300000 + i * 8192)).ToArray();
        public List<long[]> Events => events;
        public uint Seed { get; set; }
        public int Los, Path, Add, Ground, LosCalls, PathCalls, AddCalls;
        public bool Next, Recover;
        public uint RouteAfter;
        public int Node { get; private set; } = 7;
        public int AttackClock { get; set; }
        public ThugPlayerState Player { get; set; }
        public short PlayerSurfaceClearance { get; set; }
        public ActorBodyMotion? PlayerBody { get; set; }
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public bool HasMessages { get; set; }
        public uint RouteFlags { get; set; }

        public List<long> Flat() => events.SelectMany(e => e).ToList();
        private void Event(params long[] values) => events.Add(values);
        public int Random(int bound)
        {
            Seed = unchecked(Seed * 1103515245u + 12345u);
            int result = (int)((Seed >> 16) & 0x7FFF) % bound;
            Event(8, bound, result);
            return result;
        }
        public void Neutralize() => actor.Neutralize(procedures);
        public void CycleAnimation(ushort clip, sbyte direction = 1) { AnimationId = clip; AnimationFinished = 0; Event(10, clip, direction); }
        public void RunAnimation(ushort clip, int from = 0, int to = -1) { AnimationId = clip; AnimationFrame = from; AnimationFinished = 0; Event(9, clip, from, to); }
        public int SetHeight(bool initial, int above, int below) { Event(14, initial ? 1 : 0, above, below); return 0; }
        public void BuildTransform() => Event(1);
        public void ProcessMessages() => Event(2);
        public int GroundHeight(FixedVector3 p, int above, int below, int flags) { Event(3, p.X, p.Y, p.Z, above, below, flags); return Ground; }
        public bool GetNextWaypoint()
        {
            Event(15, Next ? 1 : 0);
            if (Next) { Node++; RouteFlags = RouteAfter; }
            return Next;
        }
        public FixedVector3 WaypointPosition() { Event(16, Node); return new(100000 + Node * 40960, -200000, 300000 - Node * 20480); }
        public FixedVector3 GetPathPoint(int index) => path[index];
        public bool LineOfSight(FixedVector3 a, FixedVector3 b)
        {
            int result = (Los >> LosCalls++) & 1;
            Event(13, a.X, a.Y, a.Z, b.X, b.Y, b.Z, 0, 0, result);
            return result != 0;
        }
        public int PathCheck(FixedVector3 a, FixedVector3 b, int clearance, bool flag)
        {
            int result = (Path >> PathCalls++) & 1;
            Event(12, a.X, a.Y, a.Z, b.X, b.Y, b.Z, 0, clearance, flag ? 1 : 0, result);
            return result;
        }
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            int result = (Add >> AddCalls++) & 1;
            Event(17, point.X, point.Y, point.Z, argument, result);
            if (result != 0 && state.PathPointIndex < 5) path[++state.PathPointIndex] = point;
            return result != 0;
        }
        public FixedVector3 GetWaypointNearTarget(FixedVector3 t, int radius, byte phase)
        {
            Event(21, t.X, t.Y, t.Z, radius, phase);
            return new(t.X + phase * 4096, t.Y, t.Z - phase * 8192);
        }
        public void SendSignal() => Event(18);
        public void ApplyHit(byte flags, short damage) => Event(19, flags, damage, 0, 0, 0);
        public void WarnOtherCops() => Event(20);
        public void PlayPositionSound(ushort sound, FixedVector3 p, int pitch) => Event(11, sound, p.X, p.Y, p.Z, pitch);
        public void MakeSpriteRing(FixedVector3 o) => Event(22, o.X, o.Y, o.Z);
        public ActorBodyMotion? RecoverAcknowledgeTarget() { Event(23, Recover ? 1 : 0); return Recover ? PlayerBody : null; }
    }

    private static int[] Words(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
    private static long[] Longs(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt64()).ToArray();
}
