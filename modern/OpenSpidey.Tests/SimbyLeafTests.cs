using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

/// <summary>Replays tools/reference_simby_leaf_vectors.py: multi-frame original CSimby AI leaves.</summary>
internal static class SimbyLeafTests
{
    private static readonly string[] Names =
    [
        "WalkToTarget", "SlideToAttack", "AttackPlayer", "Shoot", "TakeHit", "GetLaunched", "Trapped",
        "DieWhileFalling", "DieSimby", "DieFireyDeath", "SlinkToTarget", "ClimbToTarget", "DropDown", "GettingGrabbed", "AI", "Hit"
    ];

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SimbyLeafTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.simby-leaves.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int[][] fields = document.RootElement.GetProperty("fields").EnumerateArray().Select(Words).ToArray();
        int index = 0;
        int[] perLeaf = new int[Names.Length];
        var failures = new List<string>();
        int[] failedLeaf = new int[Names.Length];
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int fn = test.GetProperty("fn").GetInt32();
            long[] expected = Longs(test.GetProperty("state"));
            long[][] events = test.GetProperty("events").EnumerateArray().Select(Longs).ToArray();
            (long[] state, List<long[]> actual) = Replay(fn, fields, Words(test.GetProperty("header")),
                Words(test.GetProperty("joints")), Words(test.GetProperty("extra")), Words(test.GetProperty("tail")));
            bool eventsMatch = actual.Count == events.Length && actual.Zip(events).All(pair => pair.First.SequenceEqual(pair.Second));
            if (!state.SequenceEqual(expected) || !eventsMatch)
            {
                int diff = Enumerable.Range(0, Math.Min(state.Length, expected.Length)).FirstOrDefault(i => state[i] != expected[i], -1);
                int eventDiff = Enumerable.Range(0, Math.Min(actual.Count, events.Length))
                    .FirstOrDefault(i => !actual[i].SequenceEqual(events[i]), Math.Min(actual.Count, events.Length));
                failedLeaf[fn]++;
                failures.Add($"Actual PC CSimby {Names[fn]} case {index}: state length {state.Length}/{expected.Length}, first state diff {diff} " +
                    $"({(diff >= 0 ? $"{state[diff]} vs {expected[diff]}" : "-")}); events {actual.Count}/{events.Length}, first event diff {eventDiff}: " +
                    $"[{(eventDiff < actual.Count ? string.Join(',', actual[eventDiff]) : "")}] vs [{(eventDiff < events.Length ? string.Join(',', events[eventDiff]) : "")}].");
            }
            perLeaf[fn]++;
            index++;
        }
        assert(failures.Count == 0, $"{failures.Count} Simby leaf cases differ ({string.Join(", ", Names.Zip(failedLeaf).Where(p => p.Second != 0).Select(p => $"{p.First} {p.Second}"))}): {string.Join(" | ", failures.Take(4))}");
        assert(perLeaf.All(count => count >= 100),
            "Simby leaf fixture covers every ported leaf with at least 100 multi-frame original cases.");
    }

    private static (long[] State, List<long[]> Events) Replay(int fn, int[][] fields, int[] header, int[] joints, int[] x, int[] tail)
    {
        int levelField = Array.FindIndex(fields, field => field[0] == 0x344);
        var state = new SimbyState(1, header[levelField], 0);
        var world = new SimbyWorldState
        {
            AttackData = x[8], TargetSlotMask = unchecked((uint)x[9]), CountResetFlag = x[10]
        };
        for (int i = 0; i < 8; i++) world.TargetSlots[i] = new(x[22] + i * 409600, 0, x[23]);
        int at = 0;
        int[] frames = Take(4);
        var host = new Host(Take(1), Take(2), Take(4), Take(2), Take(3), Take(1), Take(4), state, x);
        int[] Take(int stride) { int n = tail[at++] * stride; int[] r = tail[at..(at + n)]; at += n; return r; }
        state.BindCombatFlags(host.Actor);
        for (int i = 0; i < fields.Length; i++) Set(state, host, fields[i][0], header[i]);
        for (int i = 0; i < 16; i++)
            state.Joints[i] = new(S(joints[i * 6]), S(joints[i * 6 + 1]), S(joints[i * 6 + 2]), S(joints[i * 6 + 3]), S(joints[i * 6 + 4]), S(joints[i * 6 + 5]));
        if (x[17] != 0) state.SymBase = Host.InitialBase;
        if (x[18] != 0) state.ClimbSymBase = Host.InitialClimbBase;
        if (x[19] != 0) state.Ribbon = Host.InitialRibbon;
        if (x[20] != 0) state.SlimeHandle = 1;
        int hitResult = -7;
        var flags = new SimbyStateFlags();
        var brain = new SimbyBrain(state, world, flags, host);
        var messages = new List<ThugMessage>();
        for (int m = 0; m < x[38]; m++)
            messages.Add(new ThugMessage(x[39 + m * 6]) { Vector0 = new(x[40 + m * 6], x[41 + m * 6], x[42 + m * 6]), Age = x[43 + m * 6], Flags = x[44 + m * 6] });
        for (int m = messages.Count - 1; m >= 0; m--) host.Messages.Enqueue(messages[m]);
        Action leaf = fn switch
        {
            0 => brain.WalkToTarget, 1 => brain.SlideToAttack, 2 => brain.AttackPlayer, 3 => brain.Shoot,
            4 => brain.TakeHit, 5 => brain.GetLaunched, 6 => brain.Trapped, 7 => brain.DieWhileFalling, 8 => brain.DieSimby,
            9 => brain.DieFireyDeath, 10 => brain.SlinkToTarget, 11 => brain.ClimbToTarget, 12 => brain.DropDown,
            13 => brain.GettingGrabbed, 14 => brain.Advance,
            _ => () => hitResult = brain.Hit(new ActorHit(unchecked((byte)x[57]), unchecked((byte)x[58]), x[59], unchecked((ushort)x[60]),
                new(x[61], x[62], x[63]), unchecked((short)x[64]), unchecked((ushort)x[65])))
        };
        for (int frame = 0; frame < frames.Length / 4; frame++)
        {
            host.ElapsedTicks = frames[frame * 4];
            host.AnimationFrame = unchecked((short)frames[frame * 4 + 1]);
            host.AnimationFinished = frames[frame * 4 + 2] != 0;
            host.Actor.CompletionFlags |= unchecked((uint)frames[frame * 4 + 3]);
            host.Events.Add([90, frame]);
            leaf();
        }
        var s = new List<long>();
        foreach (int[] field in fields) s.Add(Get(state, host, field[0]));
        foreach (RawJointPose joint in state.Joints)
            s.AddRange([joint.RotationX, joint.RotationY, joint.RotationZ, joint.TranslationX, joint.TranslationY, joint.TranslationZ]);
        s.AddRange([host.AnimationId, world.AttackData, world.TargetSlotMask, world.CountResetFlag,
            Host.Index(state.SymBase), Host.Index(state.ClimbSymBase), Host.Index(state.Ribbon), state.SlimeHandle != 0 ? 1 : 0,
            x[20] != 0 && host.InitialSlimeReleased ? 1 : 0]);
        foreach (Host.Base b in host.Bases) b.Dump(s);
        host.InitialBaseObject.Dump(s);
        host.InitialClimbBaseObject.Dump(s);
        foreach (FixedVector3[] ribbon in host.Ribbons) foreach (FixedVector3 point in ribbon) s.AddRange([point.X, point.Y, point.Z]);
        foreach (FixedVector3 point in host.InitialRibbonPoints) s.AddRange([point.X, point.Y, point.Z]);
        foreach ((int flag, int scale) in host.Slimes) s.AddRange([flag, scale]);
        s.AddRange([host.PlayerAi, state.PlayerAiSaved ? host.SavedPlayerAi : 0, host.PlayerPosition.X, host.PlayerPosition.Y, host.PlayerPosition.Z,
            host.ThrownVelocity.X, host.ThrownVelocity.Y, host.ThrownVelocity.Z, host.PlayerActionState]);
        foreach (ThugMessage message in messages) s.AddRange([message.Flags, message.Age]);
        s.AddRange([unchecked((ushort)flags.CachedIndex), host.HasSupportBody ? 1 : 0, fn == 15 ? hitResult : 0, state.LastHitKind]);
        return (s.ToArray(), host.Events);
    }

    private static short S(int value) => unchecked((short)value);

    private static void Set(SimbyState state, Host host, int offset, int value)
    {
        ActorBodyMotion body = host.Body;
        switch (offset)
        {
            case 0x8: body.Position = body.Position with { X = value }; break;
            case 0xC: body.Position = body.Position with { Y = value }; break;
            case 0x10: body.Position = body.Position with { Z = value }; break;
            case 0x14: body.Angles = body.Angles with { X = S(value) }; break;
            case 0x16: body.Angles = body.Angles with { Y = S(value) }; break;
            case 0x18: body.Angles = body.Angles with { Z = S(value) }; break;
            case 0x60: body.Velocity = body.Velocity with { X = value }; break;
            case 0x64: body.Velocity = body.Velocity with { Y = value }; break;
            case 0x68: body.Velocity = body.Velocity with { Z = value }; break;
            case 0x4: host.ItemFlags = unchecked((ushort)value); break;
            case 0x28: state.Scale = S(value); break;
            case 0x46: state.BodyFlags = unchecked((ushort)value); break;
            case 0xDC: state.MinorRadius = unchecked((ushort)value); break;
            case 0xE2: state.Health = S(value); break;
            case 0x1F8: state.Field1F8 = value; break;
            case 0x218: state.BehaviorFlags = unchecked((uint)value); break;
            case 0x21E: state.SurfaceClearance = S(value); break;
            case 0x230: state.Field230 = value; break;
            case 0x288: host.Actor.CompletionFlags = unchecked((uint)value); break;
            case 0x2A8: state.CombatFlags = unchecked((uint)value); break;
            case 0x2AC: body.MovementFlags = unchecked((uint)value); break;
            case 0x294: state.SetupLow = value; break;
            case 0x298: state.SetupHigh = value; break;
            case 0x29C: state.ShadowPosition = state.ShadowPosition with { Y = value }; break;
            case 0x2A0: state.ShadowGroundY = value; break;
            case 0x31C: state.Stage = value; break;
            case 0x320: state.Substate = value; break;
            case 0x310: state.Field310 = value; break;
            case 0x324: state.Field324 = value; break;
            case 0x334: state.SlinkScaleTimer = value; break;
            case 0x348: state.AlertModeTimer = value; break;
            case 0x350: state.StretchStart = state.StretchStart with { X = value }; break;
            case 0x354: state.StretchStart = state.StretchStart with { Y = value }; break;
            case 0x358: state.StretchStart = state.StretchStart with { Z = value }; break;
            case 0x35C: state.TargetPosition = state.TargetPosition with { X = value }; break;
            case 0x360: state.TargetPosition = state.TargetPosition with { Y = value }; break;
            case 0x364: state.TargetPosition = state.TargetPosition with { Z = value }; break;
            case 0x368: state.UnitDirection = state.UnitDirection with { X = value }; break;
            case 0x36C: state.UnitDirection = state.UnitDirection with { Y = value }; break;
            case 0x370: state.UnitDirection = state.UnitDirection with { Z = value }; break;
            case 0x380: state.ClimbTarget = state.ClimbTarget with { X = value }; break;
            case 0x384: state.ClimbTarget = state.ClimbTarget with { Y = value }; break;
            case 0x388: state.ClimbTarget = state.ClimbTarget with { Z = value }; break;
            case 0x398: state.AimYaw = value; break;
            case 0x3A0: state.Field3A0 = unchecked((uint)value); break;
            case 0x3A4: state.CeilingY = value; break;
            case 0x3A8: state.JumpHeight = value; break;
            case 0x3AC: state.TargetSlot = value; break;
            case 0x3B0: state.StretchScale = value; break;
            case 0x3D4: state.ClipScaleRate = value; break;
            case 0x3DC: state.HandPosition = state.HandPosition with { X = value }; break;
            case 0x3E0: state.HandPosition = state.HandPosition with { Y = value }; break;
            case 0x3E4: state.HandPosition = state.HandPosition with { Z = value }; break;
            case 0x3EC: state.AttackRelatedSnapshot = value; break;
            case 0x3F0: state.AttackMask = value; break;
            case 0x3F8: state.SlinkDelta = state.SlinkDelta with { X = value }; break;
            case 0x3FC: state.SlinkDelta = state.SlinkDelta with { Y = value }; break;
            case 0x400: state.SlinkDelta = state.SlinkDelta with { Z = value }; break;
            case 0x410: state.SlinkLength = value; break;
            case 0x444: state.SlinkRate = value; break;
            case 0x448: state.SlinkProgress = value; break;
            case >= 0x418 and < 0x440: state.SlinkSegments[(offset - 0x418) / 4] = value; break;
            case 0x8A: body.AngularVelocity = body.AngularVelocity with { Y = S(value) }; break;
            case 0x90: body.AngularAcceleration = body.AngularAcceleration with { Y = S(value) }; break;
            case 0x1F0: state.Field1F0 = value; break;
            case 0x38C: state.GrabHandPosition = state.GrabHandPosition with { X = value }; break;
            case 0x390: state.GrabHandPosition = state.GrabHandPosition with { Y = value }; break;
            case 0x394: state.GrabHandPosition = state.GrabHandPosition with { Z = value }; break;
            case 0x344: break;
            case 0x3F4: state.SlimeRespawnTimer = value; break;
            case 0x34C: state.AlertNoiseTimer = value; break;
            case 0x3C4: state.FightProbeAccumulator = value; break;
            case 0x3C8: state.FightProbeThreshold = value; break;
            case 0x3B4: state.CeilingProbeLatch = value; break;
            case 0x24: state.Rgb = value; break;
            case 0x194: state.Field194 = unchecked((uint)value); break;
            case 0x3B8: state.Field3B8 = value; break;
            case 0x3BC: state.Field3BC = value; break;
            case 0x3C0: state.Field3C0 = value; break;
            case 0x30C: state.Field30C = value; break;
            default: throw new InvalidDataException($"Unmapped CSimby fixture field 0x{offset:X}.");
        }
    }

    private static long Get(SimbyState state, Host host, int offset)
    {
        ActorBodyMotion body = host.Body;
        return offset switch
        {
            0x8 => body.Position.X, 0xC => body.Position.Y, 0x10 => body.Position.Z,
            0x14 => body.Angles.X, 0x16 => body.Angles.Y, 0x18 => body.Angles.Z,
            0x60 => body.Velocity.X, 0x64 => body.Velocity.Y, 0x68 => body.Velocity.Z,
            0x4 => unchecked((short)host.ItemFlags), 0x28 => state.Scale, 0x46 => unchecked((short)state.BodyFlags),
            0xDC => unchecked((short)state.MinorRadius), 0xE2 => state.Health, 0x1F8 => state.Field1F8,
            0x218 => unchecked((int)state.BehaviorFlags), 0x21E => state.SurfaceClearance, 0x230 => state.Field230,
            0x288 => unchecked((int)host.Actor.CompletionFlags), 0x2A8 => unchecked((int)state.CombatFlags),
            0x2AC => unchecked((int)body.MovementFlags), 0x294 => state.SetupLow, 0x298 => state.SetupHigh,
            0x29C => state.ShadowPosition.Y, 0x2A0 => state.ShadowGroundY, 0x31C => state.Stage, 0x320 => state.Substate,
            0x310 => state.Field310, 0x324 => state.Field324, 0x334 => state.SlinkScaleTimer, 0x348 => state.AlertModeTimer,
            0x350 => state.StretchStart.X, 0x354 => state.StretchStart.Y, 0x358 => state.StretchStart.Z,
            0x35C => state.TargetPosition.X, 0x360 => state.TargetPosition.Y, 0x364 => state.TargetPosition.Z,
            0x368 => state.UnitDirection.X, 0x36C => state.UnitDirection.Y, 0x370 => state.UnitDirection.Z,
            0x380 => state.ClimbTarget.X, 0x384 => state.ClimbTarget.Y, 0x388 => state.ClimbTarget.Z,
            0x398 => state.AimYaw, 0x3A0 => unchecked((int)state.Field3A0), 0x3A4 => state.CeilingY, 0x3A8 => state.JumpHeight,
            0x3AC => state.TargetSlot, 0x3B0 => state.StretchScale, 0x3D4 => state.ClipScaleRate,
            0x3DC => state.HandPosition.X, 0x3E0 => state.HandPosition.Y, 0x3E4 => state.HandPosition.Z,
            0x3EC => state.AttackRelatedSnapshot, 0x3F0 => state.AttackMask,
            0x3F8 => state.SlinkDelta.X, 0x3FC => state.SlinkDelta.Y, 0x400 => state.SlinkDelta.Z, 0x410 => state.SlinkLength,
            0x444 => state.SlinkRate, 0x448 => state.SlinkProgress,
            >= 0x418 and < 0x440 => state.SlinkSegments[(offset - 0x418) / 4],
            0x8A => body.AngularVelocity.Y, 0x90 => body.AngularAcceleration.Y, 0x1F0 => state.Field1F0,
            0x38C => state.GrabHandPosition.X, 0x390 => state.GrabHandPosition.Y, 0x394 => state.GrabHandPosition.Z,
            0x344 => state.LevelId, 0x3F4 => state.SlimeRespawnTimer, 0x34C => state.AlertNoiseTimer,
            0x3C4 => state.FightProbeAccumulator, 0x3C8 => state.FightProbeThreshold, 0x3B4 => state.CeilingProbeLatch,
            0x24 => state.Rgb, 0x194 => unchecked((int)state.Field194), 0x3B8 => state.Field3B8, 0x3BC => state.Field3BC,
            0x3C0 => state.Field3C0, 0x30C => state.Field30C,
            _ => throw new InvalidDataException($"Unmapped CSimby fixture field 0x{offset:X}.")
        };
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static long[] Longs(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private sealed class Host(int[] rnds, int[] grounds, int[] lines, int[] items, int[] hooks, int[] xas, int[] paths, SimbyState state, int[] x)
        : ISimbyHost
    {
        public const int InitialBase = 1001, InitialClimbBase = 1002, InitialRibbon = 2001;
        private int rndIndex, groundIndex, lineIndex, itemIndex, hookIndex, xaIndex, pathIndex;
        private bool supportBody;
        private int currentSlime = x[20] != 0 ? -1 : -2;
        public sealed class Base
        {
            public FixedVector3 Position;
            public ActorAngles Angles;
            public int Following, TargetScale;
            public short Scale;
            public void Dump(List<long> s) => s.AddRange([Position.X, Position.Y, Position.Z, Angles.X, Angles.Y, Angles.Z,
                Following, TargetScale, Scale, Scale, Scale]);
        }
        public List<long[]> Events { get; } = [];
        public List<Base> Bases { get; } = [];
        public Base InitialBaseObject { get; } = new();
        public Base InitialClimbBaseObject { get; } = new();
        public List<FixedVector3[]> Ribbons { get; } = [];
        public FixedVector3[] InitialRibbonPoints { get; } = new FixedVector3[10];
        public List<(int Flag, int Scale)> Slimes { get; } = [];
        public bool InitialSlimeReleased { get; private set; }
        public ActorBodyMotion Body { get; } = new();
        public ActorProcedureState Actor => actor ??= new(Body);
        private ActorProcedureState? actor;
        public ActorProcedureList Procedures { get; } = new();
        public ushort ItemFlags { get; set; }
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public int ElapsedTicks { get; set; }
        public int AttackClock => x[7];
        public FixedVector3 PlayerPosition { get; set; } = new(x[0], x[1], x[2]);
        public int PlayerAnimationId => x[33];
        public int PlayerActionState { get; set; } = x[34];
        public short PlayerYaw => unchecked((short)x[35]);
        public FixedVector3 ThrownVelocity { get; private set; }
        public FixedVector3 PlayerVelocity { set => ThrownVelocity = value; }
        public int PlayerAi { get; private set; }
        public int SavedPlayerAi { get; private set; }
        public bool GrabUpdate(ref FixedVector3 position, ref short yaw)
        {
            int result = x[36];
            Events.Add([63, position.X, position.Y, position.Z, yaw, result]);
            if (result != 0) { position = position with { X = unchecked(position.X + 4096) }; yaw = unchecked((short)(yaw + 16)); }
            return result != 0;
        }
        public void SetPlayerAi(SimbyPlayerAi ai)
        {
            if (ai == SimbyPlayerAi.WaitForSimbyGrab)
            {
                SavedPlayerAi = PlayerAi switch { 0 => 1, 1 => 2, _ => 9 };
                PlayerAi = 1;
            }
            else PlayerAi = 2;
        }
        public void RestorePlayerAi() => PlayerAi = state.PlayerAiSaved ? SavedPlayerAi switch { 1 => 0, 2 => 1, _ => 9 } : 3;
        public void HitPlayer(ActorHit hit) => Events.Add([60, hit.Flags, hit.Damage]);
        public void PlayPlayerSingleAnimation(int animation) => Events.Add([62, animation, 0, -1]);
        public void CreatePlayerImpactEffect(FixedVector3 position, int flags) => Events.Add([61, position.X, position.Y, position.Z, flags]);
        public short PlayerHealth => unchecked((short)x[3]);
        public byte PlayerSpecialState => unchecked((byte)x[4]);
        public byte PlayerInputLock => 0;
        public bool HasProcedures => false;
        public bool HasSupportBody => supportBody;
        public int SupportVelocityY => x[11];
        public int IdleSetupLow => 513;
        public int IdleSetupHigh => 0;
        public int CeilingSetupLow => 555819296;
        public int CeilingSetupHigh => 543;
        public int CurrentWaypoint => 0;
        public ushort WaypointFlags { get; set; }
        public uint PlayerDistance => unchecked((uint)x[6]);
        public ushort PlayerMinorRadius => unchecked((ushort)x[5]);
        public int AnimationFrame { get; set; }
        public byte HeightQueryInterval { set { } }
        public FixedJointTransform TransformBasis => new(S(x[24]), S(x[25]), S(x[26]), S(x[27]), S(x[28]), S(x[29]), S(x[30]), S(x[31]), S(x[32]), 0, 0, 0);
        public FixedVector3 LastLineHitNormal => throw Unexpected();
        public ThugMessageQueue Messages { get; } = new();
        public ThugMessage? FirstMessage => Messages.Head;

        public static long Index(int handle) => handle switch
        {
            0 => 0, InitialBase => 9, InitialClimbBase => 8, InitialRibbon => 9, >= 3000 => handle - 3000 + 10, _ => handle - 1 + 10
        };

        public int Random(int bound)
        {
            int r = rnds[rndIndex++ % rnds.Length];
            r = bound > 0 ? unchecked((int)((uint)r % (uint)bound)) : 0;
            Events.Add([8, bound, r]);
            return r;
        }
        private int Ground(FixedVector3 p, int above, int below, bool wantSupport, out bool support)
        {
            int k = groundIndex++ % (grounds.Length / 2) * 2;
            Events.Add([2, p.X, p.Y, p.Z, above, below, wantSupport ? 1 : 0]);
            support = grounds[k + 1] != 0;
            return grounds[k];
        }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => Ground(position, above, below, false, out _);
        public int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport) =>
            Ground(position, above, below, true, out hasSupport);
        public int UpdateGroundSupport(FixedVector3 position, int above, int below)
        {
            int ground = Ground(position, above, below, true, out bool support);
            supportBody = support;
            return ground;
        }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, out FixedVector3 hit)
        {
            int k = lineIndex++ % (lines.Length / 4) * 4;
            Events.Add([3, from.X, from.Y, from.Z, to.X, to.Y, to.Z, 0]);
            hit = lines[k] == 0 ? new(lines[k + 1], lines[k + 2], lines[k + 3]) : default;
            return lines[k] != 0;
        }
        public bool LineToItem(FixedVector3 start, FixedVector3 end, out int hitY)
        {
            int k = itemIndex++ % (items.Length / 2) * 2;
            Events.Add([5, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1]);
            hitY = items[k + 1];
            return items[k] != 0;
        }
        public FixedVector3 DynamicHookPosition(ActorHook hook)
        {
            int k = hookIndex++ % (hooks.Length / 3) * 3;
            Events.Add([44, hook.X, hook.Y, hook.Z, hook.PartIndex]);
            return new(hooks[k], hooks[k + 1], hooks[k + 2]);
        }
        public void Neutralize() => Events.Add([6]);
        public void RunAnimation(int animation) { AnimationId = animation; AnimationFinished = false; Events.Add([7, animation, 0, -1]); }
        public void CycleAnimation(int animation) { AnimationId = animation; Events.Add([9, animation, 1]); }
        public void PlayPositionalSound(int id) => Events.Add([11, id & 0xFFFF, 1, 0]);
        public int SetHeight(bool initial, int above, int below) { Events.Add([14, initial ? 1 : 0, above, below]); return 1; }
        public void DoPhysics(int mode)
        {
            Events.Add([15, mode]);
            if (mode == 0) return;
            int used = state.Field1F8 > ElapsedTicks ? ElapsedTicks : state.Field1F8;
            if (used != 0) state.Field1F8 -= used;
        }
        public void RunTimer(ref int timer) { timer = unchecked(timer - ElapsedTicks); if (timer < 0) timer = 0; }
        public void AttachMoveTo(FixedVector3 target, int speed, int turnRate, int tolerance, uint mask) =>
            Events.Add([20, target.X, target.Y, target.Z, speed, turnRate, tolerance, mask]);
        public void AttachLookAtPlayer(uint mask, int rate, int tolerance) => Events.Add([21, 1, 0, 0, 0, mask, rate, tolerance]);
        public void AttachLookAtPoint(FixedVector3 point, uint mask, int rate, int tolerance) =>
            Events.Add([21, 0, point.X, point.Y, point.Z, mask, rate, tolerance]);
        public void AttachLookAtYaw(int yaw, uint mask, int rate, int tolerance) => Events.Add([22, yaw, mask, rate, tolerance]);
        public void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask) =>
            Events.Add([23, startFrame, hookMask, frames, completionMask]);
        public void AttachStateSwitchMessage(int message) => Events.Add([24, message]);
        public void CreateSlimeQuad(FixedVector3 position, int flags, bool scaleUp)
        {
            Events.Add([25, position.X, position.Y, position.Z, flags]);
            Slimes.Add((1, scaleUp ? 32 : 0));
            currentSlime = Slimes.Count - 1;
        }
        public void ReleaseSlimeQuad()
        {
            if (currentSlime == -1) InitialSlimeReleased = true;
            else if (currentSlime >= 0) Slimes[currentSlime] = (0, -32);
            else throw Unexpected();
            currentSlime = -2;
        }
        public void SpawnFireDeath() => Events.Add([29]);
        public int CreateRibbon(int faces, byte red, byte green, byte blue, int width)
        {
            Events.Add([30, faces]); Events.Add([31, 1]); Events.Add([32]); Events.Add([33, red, green, blue]); Events.Add([34, unchecked((ushort)width)]);
            Ribbons.Add(new FixedVector3[10]);
            return 3000 + Ribbons.Count - 1;
        }
        private FixedVector3[] Ribbon(int handle) => handle == InitialRibbon ? InitialRibbonPoints : Ribbons[handle - 3000];
        public void SetRibbonPoint(int handle, int index, FixedVector3 position) => Ribbon(handle)[index] = position;
        public void SetRibbonPointY(int handle, int index, int y) => Ribbon(handle)[index] = Ribbon(handle)[index] with { Y = y };
        public void SetRibbonWidth(int handle, int index, int width) =>
            Events.Add([35, handle == InitialRibbon ? -1 : handle - 3000, index, unchecked((ushort)width)]);
        public void DeleteRibbon(int handle) => Events.Add([37, handle == InitialRibbon ? -1 : handle - 3000]);
        public int SpawnSymBase(FixedVector3 position, ActorAngles angles, bool following)
        {
            Events.Add([40, 1]);
            Bases.Add(new Base { Position = position, Angles = angles, Following = following ? 1 : 0 });
            return Bases.Count;
        }
        private Base SymBase(int handle) => handle switch
        {
            InitialBase => InitialBaseObject, InitialClimbBase => InitialClimbBaseObject, _ => Bases[handle - 1]
        };
        public void StartSymBaseFollowing(int handle, int scale) { Base b = SymBase(handle); b.Following = 1; b.TargetScale = scale; }
        public void SetSymBaseTarget(int handle, int scale) => SymBase(handle).TargetScale = scale;
        public void ReleaseSymBase(int handle) { Base b = SymBase(handle); b.Following = 0; b.TargetScale = 0; }
        public void StopSymBaseFollowing(int handle) => SymBase(handle).Following = 0;
        public void DeleteSymBase(int handle) => Events.Add([42, handle switch { InitialBase => -1, InitialClimbBase => -2, _ => handle - 1 }]);
        public void SetSymBaseItemScale(int handle, short scale) => SymBase(handle).Scale = scale;
        public void SetSymBaseAngleX(int handle, short angle) => SymBase(handle).Angles = SymBase(handle).Angles with { X = angle };
        public void SpawnShot(FixedVector3 position) => Events.Add([43, position.X, position.Y, position.Z]);
        public void ApplyPose()
        {
            var e = new List<long> { 45 };
            foreach (RawJointPose j in state.Joints) e.AddRange([j.RotationX, j.RotationY, j.RotationZ, j.TranslationX, j.TranslationY, j.TranslationZ]);
            Events.Add(e.ToArray());
        }
        public void NotifyPlayerKill() => Events.Add([46, SimbyState.Type]);
        public void Die() => Events.Add([47, 0]);
        public void SplattyDeath() => Events.Add([48]);
        public bool PlayerCanTalk() { Events.Add([49, x[12]]); return x[12] != 0; }
        public bool PlayAndAttachPlayerXA(int bank, int slot, int volume)
        {
            int r = xas[xaIndex++ % xas.Length];
            Events.Add([50, bank, slot, 1, volume, r]);
            if (r != 0) Events.Add([51, 1, bank, slot]);
            return r != 0;
        }
        public bool TrapWebBurning()
        {
            Events.Add([52]);
            return x[14] != 0 && x[15] != 0 && x[16] > 40;
        }
        public int WebGroundY(FixedVector3 position) { Events.Add([53, position.X, position.Y, position.Z]); return x[13]; }
        public void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int flags) =>
            Events.Add([54, position.X, position.Y, position.Z, velocity == default ? 1 : 0, groundY, flags]);
        public int ClipFrameCount(int clip) => x[21] + clip;
        public FixedVector3 WaypointPosition(int node) => throw Unexpected();
        public void BuildTransform() => Events.Add([72]);
        public void ProcessProcedures() => throw Unexpected();
        public bool TryAdvanceWaypoint() { Events.Add([16]); return false; }
        public void SpawnEmergeEffect() => throw Unexpected();
        public void DestroySlimeQuad()
        {
            Events.Add([26]);
            currentSlime = -2;
        }
        public void UpdateSlimeQuad(FixedVector3 position, bool grounded, byte red, byte green, byte blue)
        {
            Events.Add([70, position.X, position.Y, position.Z, grounded ? 1 : 0]);
            for (int quad = 0; quad < 4; quad++) Events.Add([71, red, green, blue]);
        }
        public bool BumpedIntoSpidey(int radius) { Events.Add([29, radius, x[37]]); return x[37] != 0; }
        public bool CheckObjectCollision(FixedVector3 from, FixedVector3 to) { Events.Add([4]); return false; }
        public ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int groundRange, FixedVector3 initialHit)
        {
            int k = pathIndex++ % (paths.Length / 4) * 4;
            Events.Add([1, from.X, from.Y, from.Z, to.X, to.Y, to.Z, wantHitPoint ? 1 : 0, groundRange]);
            return new(paths[k], wantHitPoint ? new FixedVector3(paths[k + 1], paths[k + 2], paths[k + 3]) : default);
        }
        public void SendSelfMessage(int code) => Events.Add([80, code, 1, 1]);
        public void BurstTrapWeb()
        {
            Events.Add([52]);
            if (x[14] != 0) Events.Add([28]);
        }
        public void CleanUpMessages() => Events.Add([27, 0, 0]);
        private static InvalidOperationException Unexpected() => new("Simby leaf fixture reached an unrecorded host service.");
    }
}
