using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class LizManFollowWaypointsTests
{
    public static void Run(Action<bool, string> assert)
    {
        LizManState state = new(12, 0x504, 0, 0)
        {
            State = 2, Field1F4 = 12, Field338 = 0x1000, Field374 = 900
        };
        FollowHost host = new(state) { Waypoint = new(40960, -327680, 532480), RouteFlags = 2 };
        LizManAttackOwnership ownership = new();
        LizManFollowWaypoints.Advance(state, ownership, host, host);
        assert(state.State == 2 && state.Substate == 1 && host.Procedures.Count >= 1 && host.AnimationId == 2,
            "FollowWaypoints substate0 on 0x504 attaches MoveTo and selects walk anim2.");
        host.Actor.CompletionFlags |= 1;
        host.Ground = 160 << 12;
        LizManFollowWaypoints.Advance(state, ownership, host, host);
        assert(state.State == 2 && state.Substate == 2 && state.Field230 == 50 && host.AnimationId == 5,
            "Arrival with route flag2 enters pause substate before the next leg.");
        state.Substate = 0;
        state.Field1F0 = 0;
        state.Field1F4 = 0;
        LizManFollowWaypoints.Advance(state, ownership, host, host);
        assert(state.State == 1 && state.Substate == 0,
            "FollowWaypoints with no waypoint or path point returns to guard state1.");
    }

    private sealed class FollowHost(LizManState state) : ILizManFollowWaypointsHost
    {
        public object AttackIdentity { get; } = new();
        public ActorBodyMotion Body { get; } = new() { Position = new(40960, -327680, 532480) };
        public ActorProcedureState Actor { get; } = new(new ActorBodyMotion());
        public ActorProcedureList Procedures { get; } = new();
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks { get; set; } = 2;
        public int AttackClock { get; set; }
        public int TotalTime { get; set; } = 1000;
        public FixedVector3 PlayerPosition { get; set; }
        public bool HasMessages => false;
        public bool HasLinkedClimbNode => false;
        public uint RouteFlags { get; set; }
        public int ShadowGroundY { get; set; }
        public FixedVector3 Waypoint { get; set; }
        public int Ground { get; set; } = -1;
        public bool Advanced { get; set; }

        public bool TryAdvanceWaypoint() { Advanced = true; return true; }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => Ground;
        public void SetHeight(bool initial, int above, int below) { }
        public void RunAnimation(int animation) => AnimationId = animation;
        public void Neutralize() => Procedures.CleanUp(all: true);
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void UpdateFootstepBits() { }
        public void PlaySounds() { }
        public ActorBodyMotion? RecoverPlayerBody() => null;
        public void SetAnimationFrame(int frame) => AnimationFrame = frame;
        public short PlayerHealth => 100;
        public byte PlayerUnavailable => 0;
        public bool PlayerIsDead => false;
        public bool LineOfSight(FixedVector3 from, FixedVector3 to) => false;
        public int PathCheck(FixedVector3 from, FixedVector3 to) => 1;
        public bool AddPointToPath(FixedVector3 point, int distanceLimit) => false;
        public void SendSignal() { }
        public IEnumerable<LizManWarnCandidate> OtherLizMen() => [];
        public void SendMessage(object target, int code) { }
        public FixedVector3? RecoverSenderPosition(object? handle) => null;
        public ActorPathCheckResult PathCheckWithHit(FixedVector3 from, FixedVector3 to) => new(1, to);
        public int ScanNearbyNodesForJumpTarget() => 0;
        public FixedVector3 TriggerPosition(int node) => Waypoint;
        public void PlayPositionalSound(int id) { }
        public int Random(int bound) => 0;
        public FixedVector3 WaypointPosition(int node) => Waypoint;
        public FixedVector3 PathPointPosition(int index) => Waypoint;
        public void EnsureMatrixFacing() { }
        public void ClearClimbFacing() { }
        public void KillShadow() => state.Field394 |= 2;
        public bool DetermineFightState() => false;
        public bool WaypointHasLinks(int node) => false;
        public void RunTimer(ref int ticks)
        {
            if (ticks <= ElapsedTicks) ticks = 0;
            else ticks -= ElapsedTicks;
        }
    }
}
