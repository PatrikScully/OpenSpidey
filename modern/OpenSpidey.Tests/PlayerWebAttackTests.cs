using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWebAttackTests
{
    private sealed class Target(ushort type) : IPlayerMeleeTarget
    {
        public object Identity => this;
        public FixedVector3 Position => default;
        public ushort Type => type;
        public ushort ItemFlags => 0;
        public IPlayerMeleeTarget? Next => null;
    }
    private sealed class Host(int[] input, PlayerFireWebState webs) : IPlayerWebAttackHost, IWebStrandOwnerHost
    {
        public List<long> Events { get; } = [];
        public uint Clock => unchecked((uint)input[18]);
        public byte FireWebbing => unchecked((byte)input[28]);
        public bool AnimationFinished => input[3] != 0;
        public FixedVector3 ExplicitAimPosition => new(11, -12, 13);
        public ActorAngles ExplicitAimNormal => new(7, -8, 9);
        public ActorAngles TrajectoryAngles => new(7, -8, 9);
        public bool CheckJump() { Events.Add(1); return input[26] != 0; }
        public bool DecreaseWebbing(int amount) { Events.AddRange([4, amount]); return input[27] != 0; }
        public WebStrandOwner CreateStrand() => new(this);
        public void DeleteStrand(WebStrandOwner strand) => strand.Dispose();
        public void SwitchStrandToBlob(WebStrandOwner strand) => Events.Add(9);
        public bool StrandTargetsCurrentAim(WebStrandOwner strand) { Events.Add(12); return input[25] != 0; }
        public int FireWeb(PlayerFireWebRequest request)
        {
            WebStrandOwner strand = webs.CurrentStrand ?? throw new InvalidDataException("Source driver must create its strand before FireWeb.");
            Events.AddRange([8, request.UseHeldTarget ? 1 : 0, request.Cost, request.Target.X, request.Target.Y, request.Target.Z,
                request.HitSomething ? 1 : 0, request.Normal.X, request.Normal.Y, request.Normal.Z,
                strand.HookIndex, strand.FireVariant]);
            if (input[24] != 0) webs.CurrentStrand = null;
            return input[23];
        }
        public void StopSound(int handle) => Events.AddRange([11, handle]);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([5, sound, position.X, position.Y, position.Z, pitch]);
        public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([3, animation, firstFrame, lastFrame]);
        public void RunAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([6, animation, firstFrame, lastFrame]);
        public void SetAnimationFrame(int frame) { }
        public void SwitchToStandMode() => Events.Add(2);
        public PlayerKickHandle SelectZipDiveTarget() { Events.AddRange([13, 190, -4096, 4096, 0, 15]); return new(this, 1); }
        public PlayerKickHandle CreateDome(byte fireVariant) { Events.AddRange([14, fireVariant, 15]); return new(this, 1); }
        public object PlayerIdentity => this;
        public int PlayerWebShot => 1;
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => default;
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => null;
        public void AttachOwner(WebStrandOwner owner) => Events.Add(7);
        public void DetachOwner(WebStrandOwner owner) => Events.Add(10);
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) =>
            throw new InvalidOperationException("The driver fixture only observes its source FireWeb boundary.");
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) =>
            throw new InvalidOperationException("The driver fixture only observes its source FireWeb boundary.");
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerWebAttackTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-web-attack.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => unchecked((int)value.GetInt64())).ToArray();
            var combat = new PlayerCombatState(100, 100) { AnimationId = (ushort)input[1], AnimationFrame = input[2],
                ActionState = input[0] == 0 ? 0x4000u : 0x8000u, FieldEA6 = 77, ElapsedTicks = input[22],
                IsCrawling = input[17] != 0, Field5B4 = unchecked((uint)input[19]), LoopingSoundHandle = input[20],
                ActionProgress = input[29], Position = new(12345, -54321, 67890) };
            var melee = new PlayerMeleeState(combat) { WebAttackSubstate = (byte)input[4], WebAttackStarted = (byte)input[5],
                AimTarget = input[6] == 0 ? null : new Target((ushort)input[6]), WebFiringSide = (byte)input[8],
                WebAttackMode = (byte)input[9], AnimationFlags = (byte)input[31] };
            melee.Input.Sideways = (sbyte)input[11]; melee.Input.Forward = (sbyte)input[12];
            melee.Input.Pressed[17] = (byte)input[13]; melee.Input.Pressed[18] = (byte)input[14]; melee.Input.Pressed[19] = (byte)input[15];
            var kick = new PlayerKickState(melee) { SurfaceTransition = (byte)input[16] };
            var webs = new PlayerFireWebState(combat); var host = new Host(input, webs);
            if (input[7] != 0) webs.CurrentStrand = new(host);
            host.Events.Clear();
            var state = new PlayerWebAttackState { BlockedVariants = (byte)input[10], WebTimer = (short)input[21], RecoveryFlag = (byte)input[30] };
            if (input[0] == 0) PlayerWebAttackBehavior.UpdateWindup(kick, webs, state, host);
            else PlayerWebAttackBehavior.UpdateFiring(melee, webs, state, host);
            JsonElement expected = test.GetProperty("expected");
            long[] actual = [combat.FieldEA6, melee.WebAttackSubstate, melee.WebAttackStarted, melee.WebAttackMode,
                melee.WebFiringSide, combat.ActionState, combat.ActionProgress, state.RecoveryFlag, melee.AnimationFlags,
                melee.AimTarget?.Type ?? 0, webs.CurrentStrand is null ? 0 : 1, state.WebTimer, state.DomeCreationClock,
                state.DomeHandle.Target is null ? 0 : 1, melee.ZipDiveClock, melee.ZipDiveHandle.Target is null ? 0 : 1,
                webs.TrapSoundHandle, combat.AnimationFrame, webs.CurrentStrand is { } strand ? strand.HookIndex : -1,
                webs.CurrentStrand is { } held ? held.FireVariant : -1];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(20).Select(value => value.GetInt64())),
                $"Actual4000/8000 web driver case {index}: shared release/aim/target/sound/strand ownership and exact short timer.");
            assert(host.Events.SequenceEqual(expected[20].EnumerateArray().Select(value => value.GetInt64())),
                $"Actual4000/8000 web driver case {index}: exact stick variants, source costs, delete/blob/stop order and held-refire callbacks.");
            assert(combat.WebPresent == (webs.CurrentStrand is not null), "Every web driver shares actualE6C with damage and release state.");
            index++;
        }
    }
}
