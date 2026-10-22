using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

/// <summary>CRhino AI leaves, Hit and DetermineFightState replayed against tools/reference_rhino_ai_vectors.py captures.</summary>
internal static class RhinoAiTests
{
    private static readonly string[] Names =
    [
        "Hit", "DetermineFightState", "LineOfSightCheck", "TakeHit", "GetLaunched", "Laugh", "HitWall", "StuckInWall",
        "GetShocked", "StompGround", "GetTrapped", "AttackPlayer", "ChasePlayer", "FollowWaypoints", "DieRhino", "PlaySounds"
    ];

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(RhinoAiTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.rhino-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        var covered = new HashSet<int>();
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            int function = input.GetProperty("fn").GetInt32();
            covered.Add(function);
            var host = new Host(input);
            var brain = new RhinoBrain(host.State, new RhinoStateFlags(), host);
            int argument = input.GetProperty("arg").GetInt32();
            long result = function switch
            {
                0 => brain.Hit(host.HitInfo),
                1 => brain.DetermineFightState(argument) ? 1 : 0,
                2 => brain.LineOfSightCheck(host.PlayerPosition, argument) ? 1 : 0,
                _ => Call(brain, function, argument)
            };
            string label = $"CRhino::{Names[function]} case{index}";
            long[] actual = host.Snapshot();
            long[] wanted = Numbers(expected.GetProperty("fields"));
            for (int field = 0; field < wanted.Length; field++)
                assert(actual[field] == wanted[field],
                    $"{label}: {document.RootElement.GetProperty("fields")[field].GetString()} actual={actual[field]} expected={wanted[field]}.");
            assert(result == expected.GetProperty("ret").GetInt64(), $"{label}: result {result} vs {expected.GetProperty("ret").GetInt64()}.");
            assert(host.Unused == expected.GetProperty("unused").GetInt32() && host.Underflow == expected.GetProperty("underflow").GetInt32(),
                $"{label}: scripted callee results consumed differently (unused {host.Unused}, underflow {host.Underflow}).");
            JsonElement events = expected.GetProperty("events");
            string actualEvents = string.Join(' ', host.Events.Select(e => $"[{string.Join(',', e)}]"));
            string wantedEvents = string.Join(' ', events.EnumerateArray().Select(e => $"[{string.Join(',', Numbers(e))}]"));
            assert(actualEvents == wantedEvents, $"{label}: events actual={actualEvents} expected={wantedEvents}.");
            index++;
        }
        assert(index >= 150 && covered.Count == Names.Length,
            "The Rhino AI fixture keeps every original leaf, Hit and DetermineFightState corpus.");
    }

    private static long Call(RhinoBrain brain, int function, int argument)
    {
        switch (function)
        {
            case 3: brain.TakeHit(); break;
            case 4: brain.GetLaunched(); break;
            case 5: brain.Laugh(); break;
            case 6: brain.HitWall(); break;
            case 7: brain.StuckInWall(); break;
            case 8: brain.GetShocked(); break;
            case 9: brain.StompGround(); break;
            case 10: brain.GetTrapped(); break;
            case 11: brain.AttackPlayer(); break;
            case 12: brain.ChasePlayer(argument); break;
            case 13: brain.FollowWaypoints(); break;
            case 14: brain.DieRhino(); break;
            case 15: brain.PlaySounds(); break;
            default: throw new InvalidDataException($"Unknown Rhino fixture function {function}.");
        }
        return 0;
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private sealed class Host : IRhinoHost
    {
        private readonly Queue<int> script;
        private readonly int[] globals;
        private int seed;
        private int trapWeb;
        private uint routeFlags;
        private uint playerDistance;
        public RhinoState State { get; } = new(1, 0, 0);
        public ActorBodyMotion Body { get; } = new();
        public ActorProcedureState Actor { get; }
        public ActorProcedureList Procedures { get; } = new();
        public ThugMessageQueue Messages { get; } = new();
        public List<long[]> Events { get; } = [];
        public ActorHit HitInfo { get; }
        public int Unused => script.Count;
        public int Underflow { get; private set; }
        public int AnimationId { get; private set; }
        public int AnimationFrame { get; private set; }
        public bool AnimationFinished { get; private set; }
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks { get; }
        public int AttackClock => globals[0];
        public int Difficulty => globals[1];
        public FixedVector3 PlayerPosition => new(globals[2], globals[3], globals[4]);
        public bool PlayerIsCrawling => globals[5] != 0;
        public bool PlayerOnWallOrCeiling => globals[6] != 0 || globals[7] != 0;
        public int PlayerActionState => globals[8];
        public int PlayerInputSuppression => globals[9];
        public int WebDomeCount => 0;
        public uint RouteFlags => routeFlags;
        public uint PlayerDistance => playerDistance;
        public object? PlayerHeldObject => null;
        public bool VibrationEnabled => false;
        public bool CameraPresent => false;
        public bool TrapWebHandleSet => trapWeb != 0;
        public RhinoWallChunkLists WallChunks { get; } = new() { Chunk0 = [1581659904u] };
        public IReadOnlyList<RhinoEnvironmentBody> EnvironmentalBodies => [];
        public ReadOnlySpan<RhinoSpeechEntry> SpeechEntries => ReadOnlySpan<RhinoSpeechEntry>.Empty;

        public Host(JsonElement input)
        {
            Actor = new(Body);
            globals = input.GetProperty("globals").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            script = new(input.GetProperty("script").EnumerateArray().Select(v => v.GetInt32()));
            seed = input.GetProperty("seed").GetInt32();
            int[] hit = input.GetProperty("hit").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            HitInfo = new((byte)hit[0], 0, hit[1], unchecked((ushort)hit[2]), new(hit[3], hit[4], hit[5]),
                unchecked((short)hit[6]), unchecked((ushort)hit[7]));
            int[] f = input.GetProperty("fields").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            State.State = f[0]; State.Substate = f[1]; State.Field310 = f[2]; State.Field324 = f[3]; State.Field328 = f[4];
            State.Field330 = f[5]; State.Field334 = f[6]; State.Field338 = f[7]; State.Field344 = f[8]; State.Field348 = f[9];
            State.Field34C = f[10]; State.Field350 = f[11]; State.Field354 = f[12]; State.Field358 = f[13]; State.Field35C = f[14];
            State.Field360 = f[15]; State.Field388 = f[16]; State.Field3D0 = f[17]; State.Field420 = f[18]; State.Field1F0 = f[19];
            State.Field1F8 = f[20]; State.Field230 = f[21]; State.CombatFlags = unchecked((uint)f[22]);
            Actor.CompletionFlags = unchecked((uint)f[23]); Actor.BehaviorFlags = unchecked((uint)f[24]);
            State.BodyFlags = unchecked((ushort)f[25]); State.Health = unchecked((short)f[26]); State.LastHitKind = f[27];
            Body.Velocity = new(f[28], f[29], f[30]); Body.Angles = new(0, unchecked((short)f[31]), 0);
            Body.Position = new(f[32], f[33], f[34]); State.PathPoints[1] = new(f[35], f[36], f[37]);
            State.Field21D = (byte)f[38]; State.CachedPlayerDistance = f[39]; State.CachedPlayerDistanceClock = f[40];
            Body.MovementFlags = unchecked((uint)f[41]); State.MovementAnimations = unchecked((uint)f[42]);
            State.IdleAnimations = unchecked((uint)f[43]); routeFlags = unchecked((uint)f[44]); playerDistance = unchecked((uint)f[45]);
            AnimationId = f[46]; AnimationFrame = f[47]; AnimationFinished = f[48] != 0; ElapsedTicks = f[49];
            State.LastFootstepSound = f[50]; trapWeb = f[51];
        }

        public long[] Snapshot() =>
        [
            State.State, State.Substate, State.Field310, State.Field324, State.Field328, State.Field330, State.Field334,
            State.Field338, State.Field344, State.Field348, State.Field34C, State.Field350, State.Field354, State.Field358,
            State.Field35C, State.Field360, State.Field388, State.Field3D0, State.Field420, State.Field1F0, State.Field1F8,
            State.Field230, State.CombatFlags, Actor.CompletionFlags, Actor.BehaviorFlags, State.BodyFlags & 0xFFFF,
            State.Health, State.LastHitKind, Body.Velocity.X, Body.Velocity.Y, Body.Velocity.Z, Body.Angles.Y,
            Body.Position.X, Body.Position.Y, Body.Position.Z, State.PathPoints[1].X, State.PathPoints[1].Y,
            State.PathPoints[1].Z, State.Field21D, State.CachedPlayerDistance, State.CachedPlayerDistanceClock,
            Body.MovementFlags, State.MovementAnimations, State.IdleAnimations, routeFlags, playerDistance,
            (ushort)AnimationId, AnimationFrame, AnimationFinished ? 1 : 0, ElapsedTicks, State.LastFootstepSound, trapWeb
        ];

        private int Pop()
        {
            if (script.Count != 0) return script.Dequeue();
            Underflow++;
            return 0;
        }

        public int Random(int bound)
        {
            Events.Add([1, bound, seed]);
            int value = bound <= 0 ? 0 : seed % bound;
            seed++;
            return value;
        }
        public void Neutralize() => Events.Add([3]);
        public void AttachPlayerLookAt(uint completionMask, int rate, int tolerance) => Events.Add([4, completionMask, rate, tolerance, 1]);
        public void AttachLookAtYaw(int yaw, uint completionMask, int rate, int tolerance) =>
            Events.Add([24, (short)yaw, completionMask, rate, tolerance]);
        public void AttachMoveTo(FixedVector3 destination, int speed, int turnRate, int tolerance, uint completionMask) =>
            Events.Add([23, destination.X, destination.Y, destination.Z, speed, turnRate, tolerance, completionMask]);
        public void AttachAcceleration(int acceleration, int targetSpeed, uint completionMask) =>
            Events.Add([12, acceleration, targetSpeed, completionMask]);
        public void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask) =>
            Events.Add([11, startFrame, hookMask, frames, completionMask]);
        public void AttachStateSwitchMessage(RhinoState state, int message) => Events.Add([13, message]);
        public void RunAnimation(int animation) => RunAnimation(animation, 0);
        public void RunAnimation(int animation, int frame)
        {
            Events.Add([5, animation, frame]);
            AnimationId = animation;
            AnimationFrame = (short)frame;
            AnimationFinished = false;
        }
        public void CycleAnimation(int animation)
        {
            Events.Add([9, animation]);
            AnimationId = animation;
            AnimationFinished = false;
        }
        public void PlayPositionalSound(int id) => Events.Add([6, id]);
        public int PlayTrackedSound(int id)
        {
            Events.Add([6, id]);
            return 5;
        }
        public void StopSound(int handle) => Events.Add([22, handle]);
        public void DoPhysics(int mode) => Events.Add([7, mode]);
        public void PlayXAPlease(RhinoState state, int bankOffset, int slotCount, int rndAddend) =>
            Events.Add([10, bankOffset, slotCount, rndAddend]);
        public ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int clearance)
        {
            Events.Add([16, to.X, to.Y, to.Z, clearance, wantHitPoint ? 1 : 0]);
            int code = Pop();
            FixedVector3 hit = default;
            if (wantHitPoint) hit = new(Pop(), Pop(), Pop());
            return new(code, hit);
        }
        public bool TryLineToItem(FixedVector3 start, FixedVector3 end, out uint checksum, out FixedVector3 hitPosition)
        {
            Events.Add([17, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1]);
            checksum = 0;
            hitPosition = default;
            if (Pop() == 0) return false;
            hitPosition = new(Pop(), Pop(), Pop());
            checksum = unchecked((uint)Pop());
            return true;
        }
        public void SendSignal() => Events.Add([15]);
        public bool HitPlayer(ActorHit hit)
        {
            Events.Add([14, hit.Flags, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z]);
            return Pop() != 0;
        }
        public void ShakeCamera() => Events.Add([18]);
        public void EmitStompEffect() => Events.Add([19]);
        public void Electrify() => Events.Add([20]);
        public void KnockPlayerFromCrawl() => Events.Add([28]);
        public void SetPlayerIgnoreInputTimer(int ticks) => Events.Add([29, ticks]);
        public void StopMyXA() => Events.Add([30]);
        public void Die() => Events.Add([31]);
        public bool TryAdvanceWaypoint()
        {
            Events.Add([25]);
            return Pop() != 0;
        }
        public IThugTrapWeb? RecoverTrapWeb()
        {
            Events.Add([32]);
            return null;
        }
        public void ReleaseTrapWebHandle() => trapWeb = 0;
        public int ActuatorTime(int controller, int actuator)
        {
            Events.Add([33]);
            return 0;
        }
        public void ActuatorOn(int controller, int duration, int actuator, int strength) => Events.Add([34]);

        public int GroundHeight(FixedVector3 position, int above, int below, int flags) =>
            throw new InvalidOperationException("Rhino AI leaves never query the ground.");
        public void SetHeight(bool initial, int above, int below) => throw new InvalidOperationException("Not a leaf call.");
        public void BuildTransform() => throw new InvalidOperationException("Not a leaf call.");
        public void SetGlobalBoss(bool enabled) => throw new InvalidOperationException("Not a leaf call.");
        public void SetBossCameraMode() => throw new InvalidOperationException("Not a leaf call.");
        public ActorBodyMotion? RecoverPlayerBody() => throw new InvalidOperationException("Look-at stubs never recover the body.");
        public bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact) =>
            throw new InvalidOperationException("Stationary fixture GonnaHitWall never probes.");
        public void ChunkItemByChecksum(uint checksum) => throw new InvalidOperationException("Not reached.");
        public bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius) => throw new InvalidOperationException("Not reached.");
        public void HitPlayerFromCharge(FixedVector3 delta) => throw new InvalidOperationException("Not reached.");
        public bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority) => throw new InvalidOperationException("XA is stubbed.");
        public bool PlayerCanTalkNow() => throw new InvalidOperationException("XA is stubbed.");
        public void AttachRhinoSpeech(int bank, int slot) => throw new InvalidOperationException("XA is stubbed.");
        public void AttachPlayerSpeech(int bank, int slot) => throw new InvalidOperationException("XA is stubbed.");
    }
}
