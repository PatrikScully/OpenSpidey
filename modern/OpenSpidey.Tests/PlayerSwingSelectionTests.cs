using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSwingSelectionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSwingSelectionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-swing-selection.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            bool zip = row.GetProperty("kind").GetString() == "swing_zip";
            var combat = new PlayerCombatState(100, 100)
            {
                Position = Vector(0), ForwardDirection = Vector(3), UpDirection = Vector(6), ActionState = (uint)input[12],
                SynthesizedInputActive = input[15] != 0, LookaroundActive = input[18] != 0, HeldObjectPresent = input[19] != 0,
                Velocity = new(0, (int)input[25], 0), IsCrawling = input[26] != 0, HeadingTurnTicksRemaining = (int)input[28],
                Field54C = (byte)input[29], ActionProgress = 99
            };
            var melee = new PlayerMeleeState(combat) { RightDirection = Vector(9), WebAttackStarted = (byte)input[27], WebAttackMode = 3 };
            melee.Input.Pressed[7] = (byte)input[13]; melee.Input.Pressed[16] = (byte)input[17]; melee.Input.Triggered[4] = (byte)input[31];
            var availability = new PlayerSwingWebAvailabilityState { TargetPosition = new(11, -12, 13), PlaneNormal = new(21, -22, 23) };
            var target = new PlayerLookaroundTargetState(new(new(combat)), melee)
            { TargetPosition = availability.TargetPosition, SurfaceNormal = availability.PlaneNormal, SwingFacingTarget = new(31, -32, 33) };
            var selection = new PlayerSwingSelectionState(melee, availability, target) { WideFanCursor = (int)input[21], NarrowFanCursor = (int)input[22] };
            var fire = new PlayerFireWebState(combat);
            var inventory = new PlayerWebInventory { FireWebbing = (byte)input[32], EmptyUseOverride = (byte)input[33] };
            var zipState = new PlayerStraightZipState(melee, fire, inventory)
            { TargetPosition = target.TargetPosition, SurfaceNormal = target.SurfaceNormal, Origin = new(41, -42, 43) };
            var host = new Host(input, selection, zipState);
            bool accepted = zip ? PlayerSwingZipSelection.Check(zipState, host) : PlayerSwingSelection.Check(selection, host);
            FixedVector3 position = zip ? zipState.TargetPosition : target.TargetPosition;
            FixedVector3 normal = zip ? zipState.SurfaceNormal : target.SurfaceNormal;
            long[] actual = [accepted ? 1 : 0, combat.ActionState, melee.WebAttackStarted, combat.IsCrawling ? 1 : 0,
                combat.HeadingTurnTicksRemaining, combat.Field54C, combat.Velocity.Y, combat.ActionProgress, host.CameraLatch,
                melee.Input.Triggered[4], selection.WideFanCursor, selection.NarrowFanCursor, melee.WebAttackMode, inventory.EmptyUseOverride,
                fire.CurrentStrand is null ? 0 : 1, fire.CurrentStrand?.HookIndex ?? -1, fire.CurrentStrand?.FireVariant ?? -1,
                position.X, position.Y, position.Z, normal.X, normal.Y, normal.Z,
                target.SwingFacingTarget.X, target.SwingFacingTarget.Y, target.SwingFacingTarget.Z,
                zipState.Origin.X, zipState.Origin.Y, zipState.Origin.Z];
            JsonElement expected = row.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Original PC {row.GetProperty("kind").GetString()} {index}: all shared action/input/fan/target/normal/web fields; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Original PC swing selector {index++}: exact physical/practice gating, ordered original fan rays/partial rejection writes, actual random512 and Web allocation/FireWeb/animation callbacks.");
            fire.CurrentStrand?.Dispose();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private sealed class Host(long[] input, PlayerSwingSelectionState state, PlayerStraightZipState zip) :
        IPlayerSwingSelectionHost, IPlayerSwingZipSelectionHost, IWebStrandOwnerHost
    {
        private int probes, kind;
        public List<long> Events { get; } = [];
        public int CameraLatch { get; private set; } = (byte)input[30];
        public bool PracticeMode => input[14] != 0;
        public byte AirborneReleaseLatch => (byte)input[16];
        public byte SwingHeldLatch => (byte)input[20];
        public bool SwingPresent => false;
        public object PlayerIdentity => this;
        public int PlayerWebShot => state.Melee.WebAttackMode;
        public uint Clock => 0;
        public PlayerSwingProbeContact? TraceSwingWorld(FixedVector3 start, FixedVector3 end)
        {
            int index = probes++;
            kind = index < 8 ? (int)((input[23] >> (index * 4)) & 15) : 0;
            Events.AddRange([6, index, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1]);
            return kind == 0 ? null : new(end - new FixedVector3(111 + index, 222 + index, 333 + index),
                new(-123, kind == 3 ? -2600 : kind == 4 ? -2601 : -4096, -3456), kind is 3 or 4 ? 0x40000u : 0, 777 + index);
        }
        public PlayerStraightZipContact? TraceWorld(FixedVector3 start, FixedVector3 end)
        {
            PlayerSwingProbeContact? contact = TraceSwingWorld(start, end);
            return contact is { } hit ? new(this, hit.Position, hit.Normal, hit.FaceFlags) : null;
        }
        public bool CheckSwingAvailability(PlayerSwingProbeContact contact)
        {
            Events.AddRange([7, contact.Position.X, contact.Position.Y, contact.Position.Z]);
            state.Targets.TargetPosition = state.Availability.TargetPosition = contact.Position;
            state.Targets.SurfaceNormal = state.Availability.PlaneNormal = new(probes * 11, -probes * 12, probes * 13);
            return kind != 2;
        }
        public bool CheckZipAvailability(PlayerStraightZipContact contact, int range) { Events.AddRange([7, range]); return kind != 2; }
        public int Random(int bound) { Events.AddRange([11, bound]); return (int)input[24]; }
        public void PlayAnimation(ushort clip) => SelectAndRunAnimation(clip, 0);
        public void SelectAndRunAnimation(ushort clip, int firstFrame) => Events.AddRange([4, clip, firstFrame, -1]);
        public void SetSwingCameraResetLatch(byte value) => CameraLatch = value;
        public void SetCameraFocusHook(int hook) => throw new InvalidOperationException("Original R2 never resets held swing focus.");
        public void ReleaseSwing(bool createSwingBack) => throw new InvalidOperationException("Original R2 never deletes the existing swing in its selection leaf.");
        public WebStrandOwner CreateCurrentStrand() { Events.Add(8); return new(this); }
        public int FireWeb(PlayerFireWebRequest request)
        {
            Events.AddRange([10, request.UseHeldTarget ? 1 : 0, request.Cost, request.Target.X, request.Target.Y, request.Target.Z,
                request.HitSomething ? 1 : 0, request.Normal.X, request.Normal.Y, request.Normal.Z, state.Melee.WebAttackMode, zip.Inventory.EmptyUseOverride]);
            return 0;
        }
        public void BeginZip(PlayerStraightZipContact contact) { }
        public void AttachOwner(WebStrandOwner owner) => Events.Add(9);
        public void DetachOwner(WebStrandOwner owner) { }
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target) => throw new InvalidOperationException("Guard fixture does not execute independently proved FireWeb.");
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle) => throw new InvalidOperationException("Guard fixture does not update web actors.");
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) => throw new InvalidOperationException("Guard fixture does not render web particles.");
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) => throw new InvalidOperationException("R2 source selects world surfaces only.");
    }
}
