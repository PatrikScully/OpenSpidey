using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugRangedTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugRangedTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-ranged.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                Substate = (int)input[2], State = (int)input[3], ActionCounter = (int)input[4], PauseUpdates = (int)input[5],
                AlertTicks = (int)input[7], CombatFlags = (uint)input[8], GunSoundCooldownTicks = (int)input[10],
                GunSoundDelayTicks = (int)input[11], AimPlayerY = (int)input[12], GrenadeElapsedTicks = (int)input[13],
                RangedCooldownTicks = (int)input[14], BodyFlags = (uint)input[17], GroundReferenceY = (int)input[18]
            };
            state.SetThugType((int)input[1]);
            state.SetParamByIndex(5, (int)input[50]);
            state.SetParamByIndex(7, (int)input[51]);
            state.SetParamByIndex(8, (int)input[52]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(31), Velocity = new(11, 17, 13), Acceleration = new(-7, -23, 5), LocalVelocity = new(11, 17, 13),
                AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13)
            }) { BehaviorFlags = (uint)input[16], CompletionFlags = (uint)input[15] };
            var procedures = new ActorProcedureList();
            var existing = new[] { new Procedure(257), new Procedure(1794), new Procedure(0x20501) };
            foreach (Procedure procedure in existing) { procedure.Flags = (int)input[53]; procedures.Attach(procedure); }
            var host = new Host(actor, procedures)
            {
                AnimationId = (int)input[19], AnimationFrame = (int)input[20], AnimationFinished = (byte)input[21],
                RunFinished = (byte)input[22], Health = (short)input[23], HeldObject = input[24] != 0,
                PlayerBodyFlags = (uint)input[25], PlayerPosition = Vector(34), AttackClock = (int)input[26],
                Visible = input[27] != 0, PathResult = (int)input[28], LaserResult = (int)input[29], HitMutation = input[30] != 0,
                Distances = input.Skip(37).Take(6).Select(value => (int)value).ToArray(),
                RandomValues = input.Skip(43).Take(4).Select(value => (int)value).ToArray(),
                ItemFlags = (ushort)input[9], LaserPresent = input[48] != 0
            };
            ThugAnimationBehavior.SetMode(state, (int)input[47], false, host);
            host.Events.Clear();
            int result = 0;
            FixedVector3 aimTarget = default, aimDirection = default;
            switch (input[0])
            {
                case 0: ThugRangedBehavior.ShootPlayer(state, actor, procedures, host, host.Random, (int)input[6]); break;
                case 1: ThugRangedBehavior.TossGrenade(state, actor, procedures, host, host.Random); break;
                case 2: result = ThugRangedBehavior.DrawPlayerShot(state, actor, host, host.Random, input[49] != 0); break;
                case 3:
                    var line = new ThugShotLine { Start = actor.Body.Position };
                    aimDirection = ThugRangedBehavior.AutoAim(state, line, host.PlayerPosition, state.Field380, host.Random);
                    aimTarget = line.End;
                    break;
                case 4:
                    procedures.Attach(new ThugStateSwitchMessageProcedure(state, 12, host.SendSelfMessage));
                    procedures.Attach(new ThugStateSwitchMessageProcedure(state, 17, host.SendSelfMessage));
                    procedures.Execute(2);
                    state.State = unchecked(state.State + 1);
                    procedures.Execute(2);
                    procedures.Execute(2);
                    break;
            }
            ActorBodyMotion body = actor.Body;
            long[] actual = [result, state.Substate, state.State, state.ActionCounter, state.PauseUpdates, state.AlertTicks,
                state.CombatFlags, state.GunSoundCooldownTicks, state.GunSoundDelayTicks, state.AimPlayerY, state.GrenadeElapsedTicks,
                state.RangedCooldownTicks, actor.CompletionFlags, actor.BehaviorFlags, state.BodyFlags,
                host.AnimationId, host.AnimationFrame, host.AnimationFinished, state.TurnRate, host.Health,
                host.LaserPresent ? 1 : 0, state.AnimationWord294, state.AnimationWord298,
                state.LastSeenPlayerPosition.X, state.LastSeenPlayerPosition.Y, state.LastSeenPlayerPosition.Z,
                aimTarget.X, aimTarget.Y, aimTarget.Z, aimDirection.X, aimDirection.Y, aimDirection.Z,
                body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z, body.AngularVelocity.X, body.AngularVelocity.Y,
                body.AngularVelocity.Z, body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(47).Select(value => value.GetInt64())),
                $"Ranged case {caseIndex}, input [{string.Join(",", input)}], actual [{string.Join(",", actual)}], expected {expected}.");
            assert(host.Events.SequenceEqual(expected[48].EnumerateArray().Select(value => value.GetInt32())),
                "Shooting preserves exact hook/shot schedules, aim spread/smoothing, tracer/flash/collision/hit order and grenade spawn arguments.");
            JsonElement[] expectedProcedures = expected[47].EnumerateArray().ToArray();
            assert(procedures.Count == expectedProcedures.Length, "Ranged behavior schedules source persistent state messages and real player LookAt.");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == expectedProcedures[^(i + 1)][1].GetInt32(),
                    "Ranged neutralization preserves source ordinary/category cancellation.");
            caseIndex++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }

        bool rejected = false;
        try { ThugRangedBehavior.AutoAim(new ThugState(0, 0), new ThugShotLine(), default, 400, _ => 0); }
        catch (InvalidOperationException) { rejected = true; }
        assert(rejected, "Auto aim reports the original nonterminating coincident-horizontal-position case.");
    }

    private sealed class Procedure(int sourceType) : IActorProcedure
    {
        public int SourceType { get; } = sourceType;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => ExecuteNext?.Invoke();
    }

    private sealed class Host(ActorProcedureState actor, ActorProcedureList procedures) : IThugRangedHost
    {
        private int distanceIndex, randomIndex;
        private static readonly FixedVector3[] HookPositions =
            [new(81920, 16384, 40960), new(40960, 8192, 20480), new(-81920, 16384, -40960), new(-40960, 8192, -20480)];
        public short Health { get; set; }
        public short PlayerHealth => Health;
        public FixedVector3 PlayerPosition { get; init; }
        public uint PlayerBodyFlags { get; init; }
        public bool HeldObject { get; init; }
        public ThugPlayerState Player => new(PlayerPosition, Health, PlayerBodyFlags, 0, HeldObject);
        public int AttackClock { get; init; }
        public bool HasFightOwner => false;
        public ushort ItemFlags { get; init; }
        public bool LaserPresent { get; set; }
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public bool Visible { get; init; }
        public int PathResult { get; init; }
        public int LaserResult { get; init; }
        public bool HitMutation { get; init; }
        public required int[] Distances { get; init; }
        public required int[] RandomValues { get; init; }
        public List<int> Events { get; } = [];
        public int Random(int bound)
        {
            Events.AddRange([8, bound]);
            return RandomValues[Math.Min(randomIndex++, RandomValues.Length - 1)] % bound;
        }
        public ActorBodyMotion? RecoverPlayerBody() => new() { Position = PlayerPosition };
        public int DistanceToPlayer(int cacheTolerance)
        {
            int value = Distances[Math.Min(distanceIndex++, Distances.Length - 1)];
            Events.AddRange([7, cacheTolerance, value]);
            return value;
        }
        public void Neutralize() { Events.Add(1); actor.Neutralize(procedures); }
        public void RunAnimation(ushort clip, int from = 0, int to = -1)
        {
            Events.AddRange([2, clip, from, to]);
            AnimationId = clip;
            AnimationFrame = from;
            AnimationFinished = RunFinished;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            Events.AddRange([3, clip, direction]);
            AnimationId = clip;
            AnimationFinished = 0;
        }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags) { Events.Add(27); return Visible; }
        public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance)
        {
            Events.AddRange([4, from.X, from.Y, from.Z, to.X, to.Y, to.Z, clearance]);
            return PathResult;
        }
        public FixedVector3 GetHookPosition(int index) { Events.AddRange([14, index]); return HookPositions[index]; }
        public FixedVector3 GetDynamicHookPosition(short partX, short partY, short partZ, int offset)
        {
            Events.AddRange([26, partX, partY, partZ, offset]);
            return new(1234, 4567, 8901);
        }
        public void RotateTorsoToAimAtPlayer(FixedVector3 origin) => Events.AddRange([16, origin.X, origin.Y, origin.Z]);
        public void ClearLaser() { Events.Add(15); LaserPresent = false; }
        public void SetLaser(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([21, start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            LaserPresent = true;
        }
        public int LaserCollision(ThugShotLine line, ref FixedVector3 direction, bool checkPlayer)
        {
            Events.AddRange([17, checkPlayer ? 1 : 0, line.Start.X, line.Start.Y, line.Start.Z,
                line.End.X, line.End.Y, line.End.Z, direction.X, direction.Y, direction.Z]);
            line.End += new FixedVector3(11, -23, 17);
            return LaserResult;
        }
        public void DrawTracer(ThugShotLine line, bool attachToPlayer, byte red, byte green, byte blue) =>
            Events.AddRange([18, line.Start.X, line.Start.Y, line.Start.Z, line.End.X, line.End.Y, line.End.Z,
                attachToPlayer ? 1 : 0, red, green, blue]);
        public void DrawBarrelFlash(ThugShotLine line, byte red, byte green, byte blue) =>
            Events.AddRange([19, line.Start.X, line.Start.Y, line.Start.Z, line.End.X, line.End.Y, line.End.Z, red, green, blue]);
        public void DrawGlowFlash(FixedVector3 position, IReadOnlyList<int> constructorArguments)
        {
            Events.AddRange([20, position.X, position.Y, position.Z]);
            Events.AddRange(constructorArguments);
        }
        public void PlaySound(ushort sound) => PlayRangedSound(sound, 0);
        public void PlayRangedSound(ushort sound, int attenuation) => Events.AddRange([9, sound, attenuation]);
        public void ApplyPlayerHit(uint flags, int damage)
        {
            Events.AddRange([24, (int)flags, damage]);
            if (HitMutation) Health = unchecked((short)(Health - damage));
        }
        public void Victorious() => Events.Add(25);
        public void SpawnGrenade(ThugGrenadeRequest request) => Events.AddRange([22, request.Start.X, request.Start.Y, request.Start.Z,
            request.Target.X, request.Target.Y, request.Target.Z, request.Speed, request.Damage, request.Radius,
            unchecked((int)request.ModelChecksum), request.ExplodeOnContact, request.Flag, (int)request.TargetList]);
        public void SendSelfMessage(int code) => Events.AddRange([23, code]);
        public bool AddPointToPath(FixedVector3 point, int argument) => throw new NotSupportedException("Ranged behavior does not add paths.");
        public FixedVector3 GetPathPoint(int sourceIndex) => throw new NotSupportedException("Ranged behavior does not read paths.");
        public void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask) =>
            throw new NotSupportedException("Ranged behavior does not start melee monitors.");
        public bool MonitorSpitPlease() => throw new NotSupportedException("Ranged behavior does not request spit effects.");
        public void SetFightOwner() => throw new NotSupportedException("Ranged behavior starts with fight ownership assigned.");
        public void ClearAttackOwnership() => throw new NotSupportedException("Ranged behavior does not clear attack ownership.");
        public void SendSignal() => throw new NotSupportedException("Ranged behavior does not emit an actor signal directly.");
    }
}
