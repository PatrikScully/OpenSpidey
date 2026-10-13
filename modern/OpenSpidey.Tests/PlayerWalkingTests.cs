using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWalkingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerWalkingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-walking.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var combat = new PlayerCombatState(100, 100) { ActionState = 16, AnimationId = 21 };
            List<long> events;
            long[] actual;
            bool wall = test.GetProperty("kind").GetString() == "wall";
            if (wall)
            {
                combat.HeldObjectPresent = input[0] != 0; combat.CollisionFlags = unchecked((ushort)input[1]);
                combat.ForwardDirection = new((int)input[7], (int)input[8], (int)input[9]);
                combat.FieldAD7 = unchecked((byte)input[10]); combat.ElapsedTicks = (int)input[11];
                var host = new WallHost(combat);
                PlayerRunIntoWallContact? contact = input[2] != 0
                    ? new(new(unchecked((short)input[3]), unchecked((short)input[4]), unchecked((short)input[5])), (uint)input[6]) : null;
                bool accepted = PlayerRunIntoWall.Check(combat, contact, host);
                actual = [accepted ? 1 : 0, combat.FieldAD7, combat.ActionState, combat.AnimationId, host.SoundEntryPresent ? 1 : 0];
                events = host.Events;
            }
            else
            {
                combat.AnimationId = (ushort)input[0]; combat.HeldObjectPresent = input[2] != 0;
                combat.CollisionFlags = (ushort)input[3]; combat.IsCrawling = input[4] != 0;
                combat.LookaroundActive = input[5] != 0; combat.Field8E8 = (byte)input[9];
                combat.FieldEA6 = 456; combat.HeadingTurnTicksRemaining = 31; combat.Velocity = new(12345, -23456, 34567);
                var melee = new PlayerMeleeState(combat);
                melee.Input.Sideways = (sbyte)input[6]; melee.Input.Forward = (sbyte)input[7];
                var host = new WalkingHost(combat, input);
                PlayerWalkingBehavior.Step(melee, (ushort)input[1], host);
                actual = [combat.FieldEA6, combat.HeldObjectPresent ? 1 : 0, combat.ActionState, combat.AnimationId,
                    combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z, combat.HeadingTurnTicksRemaining];
                events = host.Events;
            }
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC {(wall ? "RunIntoWall" : "action16")}[{string.Join(',', input)}] exact shared source fields and original action/clip selection; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(events.SequenceEqual(wantedEvents), $"Actual PC {(wall ? "RunIntoWall" : "action16")}[{string.Join(',', input)}] entire source guard/held-object/heading/animation sequence; actual[{string.Join(',', events)}], expected[{string.Join(',', wantedEvents)}].");
        }
    }

    private sealed class WalkingHost(PlayerCombatState combat, long[] input) : IPlayerWalkingHost
    {
        private int webCalls;
        public List<long> Events { get; } = [];
        public bool HardSurface => input[8] != 0;
        public short InputHeading => (short)input[10];
        public short CameraHeading => (short)input[11];
        public short GetEffectiveHeading() { Events.Add(4); return (short)input[12]; }
        public void ClearAndSmashHeldObject()
        {
            if (combat.HeldObjectPresent) throw new InvalidDataException("Original action16 clears the actual held pointer before its virtual Smash callback.");
            Events.Add(6);
        }
        private bool Guard(int index) { Events.AddRange([1, index]); return input[13] == index; }
        public bool CheckGroundGone() => Guard(1);
        public bool CheckWebShot() => Guard(++webCalls == 1 ? 2 : 6);
        public bool CheckJump() => Guard(3);
        public bool CheckCeilingJumpingSmashPunch() => Guard(4);
        public bool CheckKick() => Guard(5);
        public bool CheckRunIntoWall() => Guard(7);
        public bool CheckJumpingSwingWeb() => Guard(8);
        public bool CheckJumpingR1ZipWeb() => Guard(9);
        public bool CheckJumpingR2ZipWeb() => Guard(10);
        public bool CheckInteriorSurfaceTransition() => Guard(11);
        public bool CheckExteriorSurfaceTransition() => Guard(12);
        public bool CheckFenceSurfaceTransition() => Guard(13);
        public void PlayAnimation(ushort animation) { Events.AddRange([2, animation, 0, -1]); combat.AnimationId = animation; }
        public void SwitchToStandMode() => Events.Add(3);
        public void LockTargetTorsoAngle() { Events.Add(5); combat.HeadingTurnTicksRemaining = 0; }
    }

    private sealed class WallHost(PlayerCombatState combat) : IPlayerRunIntoWallHost
    {
        public List<long> Events { get; } = [];
        public bool SoundEntryPresent { get; private set; } = true;
        public void SelectAndRunAnimation(ushort animation)
        {
            SoundEntryPresent = false; Events.AddRange([2, animation, 0, -1]); combat.AnimationId = animation;
        }
    }
}
